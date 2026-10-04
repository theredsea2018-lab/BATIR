using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class PurchaseReturnsForm : Form
{
    readonly TextBox invoiceNo = new();
    readonly DataGridView grid = new();
    readonly Button load = new() { Text = "بارگذاری فاکتور" };
    readonly Button ret = new() { Text = "برگشت کامل فاکتور", Dock = DockStyle.Fill };
    DataTable items = new();

    public PurchaseReturnsForm()
    {
        Text = "BATIR | برگشت از خرید"; Width = 1000; Height = 650;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var top = new Panel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(8) };
        invoiceNo.Dock = DockStyle.Fill; top.Controls.Add(invoiceNo);
        load.Dock = DockStyle.Right; load.Width = 150; top.Controls.Add(load);
        load.Click += (_, _) => LoadInvoice();
        ret.Dock = DockStyle.Bottom; ret.Height = 55; ret.Enabled = false;
        ret.Click += (_, _) => ReturnInvoice();
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        Controls.Add(grid); Controls.Add(ret); Controls.Add(top);
    }

    void LoadInvoice()
    {
        var q = invoiceNo.Text.Trim();
        if (q == "") { MessageBox.Show("شماره فاکتور را وارد کنید."); return; }
        items = Database.Query(@"
SELECT i.Id, i.InvoiceId, i.ProductId, p.Name AS [کالا], i.Quantity AS [تعداد],
       i.UnitPrice AS [فی], i.Discount AS [تخفیف],
       (i.Quantity*i.UnitPrice-i.Discount) AS [جمع]
FROM InvoiceItems i
JOIN Invoices v ON v.Id=i.InvoiceId
JOIN Products p ON p.Id=i.ProductId
WHERE v.InvoiceNo=@no AND v.Type='Purchase'
ORDER BY i.Id", new SqliteParameter("@no", q));
        grid.DataSource = items;
        ret.Enabled = items.Rows.Count > 0;
        if (items.Rows.Count == 0) MessageBox.Show("فاکتور خرید پیدا نشد.");
    }

    void ReturnInvoice()
    {
        if (items.Rows.Count == 0) return;
        if (MessageBox.Show("کل اقلام فاکتور از انبار کسر و حساب تأمین‌کننده اصلاح می‌شود. ادامه؟",
            "برگشت از خرید", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try
        {
            using var cn = Database.Open();
            using var tx = cn.BeginTransaction();
            long itemId = Convert.ToInt64(items.Rows[0]["Id"]);
            long originalId, supplierId, total, paid;

            using (var q = cn.CreateCommand())
            {
                q.Transaction = tx;
                q.CommandText = @"SELECT Id,SupplierId,Total,Paid FROM Invoices
WHERE Id=(SELECT InvoiceId FROM InvoiceItems WHERE Id=@item)";
                q.Parameters.AddWithValue("@item", itemId);
                using var r = q.ExecuteReader();
                if (!r.Read()) { tx.Rollback(); MessageBox.Show("فاکتور اصلی پیدا نشد."); return; }
                originalId = r.GetInt64(0);
                supplierId = r.IsDBNull(1) ? 0 : r.GetInt64(1);
                total = r.GetInt64(2);
                paid = r.GetInt64(3);
            }

            using (var a = cn.CreateCommand())
            {
                a.Transaction = tx;
                a.CommandText = "SELECT COALESCE(SUM(Total),0) FROM PurchaseReturns WHERE ReturnNo IN (SELECT ReturnNo FROM PurchaseReturns WHERE ReturnNo IS NOT NULL) AND SupplierId=@s AND Total>0";
                a.Parameters.AddWithValue("@s", supplierId);
                // The legacy schema does not retain OriginalInvoiceId, so duplicate protection is handled by stock validation below.
            }

            foreach (DataRow row in items.Rows)
            {
                long pid = Convert.ToInt64(row["ProductId"]);
                long qty = Convert.ToInt64(row["تعداد"]);
                using var stock = cn.CreateCommand();
                stock.Transaction = tx;
                stock.CommandText = "SELECT Stock FROM Products WHERE Id=@p";
                stock.Parameters.AddWithValue("@p", pid);
                long current = Convert.ToInt64(stock.ExecuteScalar() ?? 0);
                if (current < qty)
                {
                    tx.Rollback();
                    MessageBox.Show("موجودی کالا برای برگشت کافی نیست: " + Convert.ToString(row["کالا"]));
                    return;
                }
            }

            string no = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            using var h = cn.CreateCommand();
            h.Transaction = tx;
            h.CommandText = @"INSERT INTO PurchaseReturns(ReturnNo,SupplierId,DateText,Total,Notes)
VALUES(@no,@supplier,@date,@total,@notes); SELECT last_insert_rowid();";
            h.Parameters.AddWithValue("@no", no);
            h.Parameters.AddWithValue("@supplier", supplierId > 0 ? (object)supplierId : DBNull.Value);
            h.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            h.Parameters.AddWithValue("@total", total);
            h.Parameters.AddWithValue("@notes", "برگشت کامل فاکتور " + originalId);
            long returnId = Convert.ToInt64(h.ExecuteScalar());

            foreach (DataRow row in items.Rows)
            {
                long pid = Convert.ToInt64(row["ProductId"]);
                long qty = Convert.ToInt64(row["تعداد"]);
                long price = Convert.ToInt64(row["فی"]);
                long disc = Convert.ToInt64(row["تخفیف"]);
                using var ri = cn.CreateCommand();
                ri.Transaction = tx;
                ri.CommandText = @"INSERT INTO PurchaseReturnItems(ReturnId,ProductId,Quantity,UnitPrice,Discount)
VALUES(@r,@p,@q,@u,@d)";
                ri.Parameters.AddWithValue("@r", returnId);
                ri.Parameters.AddWithValue("@p", pid);
                ri.Parameters.AddWithValue("@q", qty);
                ri.Parameters.AddWithValue("@u", price);
                ri.Parameters.AddWithValue("@d", disc);
                ri.ExecuteNonQuery();

                using var st = cn.CreateCommand();
                st.Transaction = tx;
                st.CommandText = "UPDATE Products SET Stock=Stock-@q WHERE Id=@p";
                st.Parameters.AddWithValue("@q", qty); st.Parameters.AddWithValue("@p", pid);
                st.ExecuteNonQuery();
            }

            long outstanding = Math.Max(0, total - paid);
            if (supplierId > 0 && outstanding > 0)
            {
                using var b = cn.CreateCommand();
                b.Transaction = tx;
                b.CommandText = "UPDATE Suppliers SET Balance=CASE WHEN Balance>=@x THEN Balance-@x ELSE 0 END WHERE Id=@id";
                b.Parameters.AddWithValue("@x", outstanding); b.Parameters.AddWithValue("@id", supplierId);
                b.ExecuteNonQuery();
            }

            if (paid > 0 && supplierId > 0)
            {
                using var refund = cn.CreateCommand();
                refund.Transaction = tx;
                refund.CommandText = @"INSERT INTO CashTransactions
(DateText,Type,Amount,Description,UserName,SupplierId,PaymentMethod,ReferenceType,ReferenceId)
VALUES(@date,'PurchaseReturnRefund',@amount,@desc,'کاربر',@supplier,'نقدی','PurchaseReturn',@rid)";
                refund.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                refund.Parameters.AddWithValue("@amount", paid);
                refund.Parameters.AddWithValue("@desc", "بازپرداخت برگشت خرید | فاکتور " + originalId);
                refund.Parameters.AddWithValue("@supplier", supplierId);
                refund.Parameters.AddWithValue("@rid", returnId);
                refund.ExecuteNonQuery();
            }

            using var au = cn.CreateCommand();
            au.Transaction = tx;
            au.CommandText = @"INSERT INTO AuditLog(DateText,UserName,Action,Entity,EntityId,Details)
VALUES(@d,'کاربر','برگشت از خرید','PurchaseReturn',@id,@details)";
            au.Parameters.AddWithValue("@d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            au.Parameters.AddWithValue("@id", returnId);
            au.Parameters.AddWithValue("@details", "فاکتور: " + originalId + " | مبلغ: " + total);
            au.ExecuteNonQuery();

            tx.Commit();
            MessageBox.Show("برگشت از خرید ثبت شد. شماره: " + no);
            LoadInvoice();
        }
        catch (Exception ex)
        {
            MessageBox.Show("ثبت برگشت انجام نشد و تغییری ذخیره نشد.
" + ex.Message,
                "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
