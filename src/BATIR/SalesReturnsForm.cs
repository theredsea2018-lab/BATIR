using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class SalesReturnsForm : Form
{
    readonly TextBox invoiceNo = new();
    readonly DataGridView grid = new();
    readonly Button load = new() { Text = "بارگذاری فاکتور" };
    readonly Button ret = new() { Text = "ثبت برگشت اقلام انتخابی", Dock = DockStyle.Fill };
    readonly ComboBox refundMethod = new();
    DataTable items = new();

    public SalesReturnsForm()
    {
        Text = "BATIR | برگشت از فروش"; Width = 1000; Height = 650;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var top = new Panel { Dock = DockStyle.Top, Height = 70, Padding = new Padding(8) };
        invoiceNo.Dock = DockStyle.Fill; top.Controls.Add(invoiceNo);
        load.Dock = DockStyle.Right; load.Width = 150; top.Controls.Add(load);
        load.Click += (_, _) => LoadInvoice();
        refundMethod.DropDownStyle = ComboBoxStyle.DropDownList;
        refundMethod.Items.AddRange(new object[] { "نقدی", "کارتخوان", "انتقال بانکی", "چک" });
        refundMethod.SelectedIndex = 0;
        refundMethod.Dock = DockStyle.Right; refundMethod.Width = 150;
        top.Controls.Add(refundMethod);
        ret.Dock = DockStyle.Bottom; ret.Height = 55; ret.Enabled = false;
        ret.Click += (_, _) => ReturnInvoice();
        grid.Dock = DockStyle.Fill; grid.AllowUserToAddRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        Controls.Add(grid); Controls.Add(ret); Controls.Add(top);
    }

    void LoadInvoice()
    {
        var q = invoiceNo.Text.Trim();
        if (q == "") { MessageBox.Show("شماره فاکتور را وارد کنید."); return; }

        items = Database.Query(@"
SELECT i.Id, i.InvoiceId, i.ProductId, p.Name AS [کالا],
       i.Quantity AS [تعداد], i.UnitPrice AS [فی], i.Discount AS [تخفیف],
       COALESCE((SELECT SUM(r.Quantity) FROM SalesReturnItems r
                 WHERE r.OriginalInvoiceItemId=i.Id),0) AS [قبلاً برگشت],
       i.Quantity-COALESCE((SELECT SUM(r.Quantity) FROM SalesReturnItems r
                 WHERE r.OriginalInvoiceItemId=i.Id),0) AS [قابل برگشت],
       (i.Quantity*i.UnitPrice-i.Discount) AS [جمع]
FROM InvoiceItems i
JOIN Invoices v ON v.Id=i.InvoiceId
JOIN Products p ON p.Id=i.ProductId
WHERE v.InvoiceNo=@no AND v.Type='Sale'
ORDER BY i.Id",
            new SqliteParameter("@no", q));

        if (!items.Columns.Contains("تعداد برگشت"))
            items.Columns.Add("تعداد برگشت", typeof(long));
        foreach (DataRow row in items.Rows)
            row["تعداد برگشت"] = 0L;

        grid.DataSource = items;
        if (grid.Columns["Id"] != null) grid.Columns["Id"].Visible = false;
        if (grid.Columns["InvoiceId"] != null) grid.Columns["InvoiceId"].Visible = false;
        if (grid.Columns["ProductId"] != null) grid.Columns["ProductId"].Visible = false;
        if (grid.Columns["تعداد"] != null) grid.Columns["تعداد"].ReadOnly = true;
        if (grid.Columns["قبلاً برگشت"] != null) grid.Columns["قبلاً برگشت"].ReadOnly = true;
        if (grid.Columns["قابل برگشت"] != null) grid.Columns["قابل برگشت"].ReadOnly = true;
        if (grid.Columns["جمع"] != null) grid.Columns["جمع"].ReadOnly = true;
        if (grid.Columns["تعداد برگشت"] != null) grid.Columns["تعداد برگشت"].ReadOnly = false;
        ret.Enabled = items.Rows.Count > 0;
        if (items.Rows.Count == 0) MessageBox.Show("فاکتور فروش پیدا نشد.");
    }

    void ReturnInvoice()
    {
        if (!PermissionService.Require("Sales.Return", this)) return;
        if (items.Rows.Count == 0) return;

        try
        {
            using var cn = Database.Open();
            using var tx = cn.BeginTransaction();

            long itemId = Convert.ToInt64(items.Rows[0]["Id"]);
            long originalId, customerId, total, paid;

            using (var q = cn.CreateCommand())
            {
                q.Transaction = tx;
                q.CommandText = @"SELECT Id,CustomerId,Total,Paid FROM Invoices
WHERE Id=(SELECT InvoiceId FROM InvoiceItems WHERE Id=@item)";
                q.Parameters.AddWithValue("@item", itemId);
                using var r = q.ExecuteReader();
                if (!r.Read()) { tx.Rollback(); MessageBox.Show("فاکتور اصلی پیدا نشد."); return; }
                originalId = r.GetInt64(0);
                customerId = r.IsDBNull(1) ? 0 : r.GetInt64(1);
                total = r.GetInt64(2);
                paid = r.GetInt64(3);
            }

            var selected = new List<(long itemId, long productId, long qty, long price, long discount, long soldQty)>();
            long returnTotal = 0;

            foreach (DataRow row in items.Rows)
            {
                long returnQty;
                try { returnQty = Convert.ToInt64(row["تعداد برگشت"]); }
                catch { throw new InvalidOperationException("تعداد برگشت باید عدد صحیح باشد."); }

                long soldQty = Convert.ToInt64(row["تعداد"]);
                long returnable = Convert.ToInt64(row["قابل برگشت"]);
                if (returnQty < 0 || returnQty > returnable)
                    throw new InvalidOperationException($"تعداد برگشت برای «{row["کالا"]}» باید بین صفر و {returnable} باشد.");
                if (returnQty == 0) continue;

                long price = Convert.ToInt64(row["فی"]);
                long discount = Convert.ToInt64(row["تخفیف"]);
                long lineTotal = returnQty * price;
                long lineDiscount = soldQty == 0 ? 0 : (long)Math.Round(discount * (double)returnQty / soldQty);
                lineTotal -= Math.Min(lineTotal, lineDiscount);
                returnTotal += lineTotal;
                selected.Add((Convert.ToInt64(row["Id"]), Convert.ToInt64(row["ProductId"]),
                    returnQty, price, lineDiscount, soldQty));
            }

            if (selected.Count == 0 || returnTotal <= 0)
            {
                tx.Rollback();
                MessageBox.Show("حداقل یک قلم با تعداد برگشت بیشتر از صفر انتخاب کنید.");
                return;
            }

            string no = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            using var h = cn.CreateCommand();
            h.Transaction = tx;
            h.CommandText = @"
INSERT INTO SalesReturns(ReturnNo,OriginalInvoiceId,CustomerId,DateText,Total,Notes)
VALUES(@no,@inv,@customer,@date,@total,'برگشت جزئی/کامل اقلام'); SELECT last_insert_rowid();";
            h.Parameters.AddWithValue("@no", no);
            h.Parameters.AddWithValue("@inv", originalId);
            h.Parameters.AddWithValue("@customer", customerId == 0 ? DBNull.Value : (object)customerId);
            h.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            h.Parameters.AddWithValue("@total", returnTotal);
            long returnId = Convert.ToInt64(h.ExecuteScalar());

            foreach (var item in selected)
            {
                using var ri = cn.CreateCommand();
                ri.Transaction = tx;
                ri.CommandText = @"INSERT INTO SalesReturnItems
(ReturnId,ProductId,Quantity,UnitPrice,Discount,OriginalInvoiceItemId)
VALUES(@r,@p,@q,@u,@d,@oi)";
                ri.Parameters.AddWithValue("@r", returnId);
                ri.Parameters.AddWithValue("@p", item.productId);
                ri.Parameters.AddWithValue("@q", item.qty);
                ri.Parameters.AddWithValue("@u", item.price);
                ri.Parameters.AddWithValue("@d", item.discount);
                ri.Parameters.AddWithValue("@oi", item.itemId);
                ri.ExecuteNonQuery();

                using var st = cn.CreateCommand();
                st.Transaction = tx;
                st.CommandText = "UPDATE Products SET Stock=Stock+@q WHERE Id=@p";
                st.Parameters.AddWithValue("@q", item.qty);
                st.Parameters.AddWithValue("@p", item.productId);
                st.ExecuteNonQuery();
            }

            // Return value is applied to remaining customer debt first.
            // Previous partial returns are reconstructed from return and refund history.
            long previousRefunds;
            using (var pr = cn.CreateCommand())
            {
                pr.Transaction = tx;
                pr.CommandText = @"SELECT COALESCE(SUM(Amount),0) FROM CashTransactions
WHERE ReferenceType='SalesReturn' AND ReferenceId IN
(SELECT Id FROM SalesReturns WHERE OriginalInvoiceId=@id)";
                pr.Parameters.AddWithValue("@id", originalId);
                previousRefunds = Convert.ToInt64(pr.ExecuteScalar());
            }

            long previousReturns;
            using (var rr = cn.CreateCommand())
            {
                rr.Transaction = tx;
                rr.CommandText = "SELECT COALESCE(SUM(Total),0) FROM SalesReturns WHERE OriginalInvoiceId=@id";
                rr.Parameters.AddWithValue("@id", originalId);
                previousReturns = Convert.ToInt64(rr.ExecuteScalar()) - returnTotal;
            }

            long previousDebtReduction = Math.Max(0, previousReturns - previousRefunds);
            long remainingOutstanding = Math.Max(0, total - paid - previousDebtReduction);
            long debtReduction = Math.Min(returnTotal, remainingOutstanding);
            long refundAmount = returnTotal - debtReduction;

            if (customerId > 0 && debtReduction > 0)
            {
                using var b = cn.CreateCommand();
                b.Transaction = tx;
                b.CommandText = @"UPDATE Customers
SET Balance=CASE WHEN Balance>=@x THEN Balance-@x ELSE 0 END
WHERE Id=@id";
                b.Parameters.AddWithValue("@x", debtReduction);
                b.Parameters.AddWithValue("@id", customerId);
                b.ExecuteNonQuery();
            }

            if (refundAmount > 0)
            {
                using var refund = cn.CreateCommand();
                refund.Transaction = tx;
                refund.CommandText = @"
INSERT INTO CashTransactions(DateText,Type,Amount,Description,UserName,CustomerId,PaymentMethod,ReferenceType,ReferenceId)
VALUES(@date,'SalesReturnRefund',@amount,@desc,'کاربر',@customer,@method,'SalesReturn',@rid)";
                refund.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                refund.Parameters.AddWithValue("@amount", refundAmount);
                refund.Parameters.AddWithValue("@desc", "بازپرداخت برگشت فروش | فاکتور " + originalId);
                refund.Parameters.AddWithValue("@customer", customerId > 0 ? (object)customerId : DBNull.Value);
                refund.Parameters.AddWithValue("@method", refundMethod.SelectedItem?.ToString() ?? "نقدی");
                refund.Parameters.AddWithValue("@rid", returnId);
                refund.ExecuteNonQuery();
            }

            using var au = cn.CreateCommand();
            au.Transaction = tx;
            au.CommandText = @"INSERT INTO AuditLog
(DateText,UserName,Action,Entity,EntityId,Details)
VALUES(@d,'کاربر','برگشت از فروش','SalesReturn',@id,@details)";
            au.Parameters.AddWithValue("@d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            au.Parameters.AddWithValue("@id", returnId);
            au.Parameters.AddWithValue("@details",
                "فاکتور: " + originalId + " | مبلغ برگشت: " + returnTotal +
                " | پرداخت‌شده اولیه: " + paid +
                " | کاهش بدهی: " + debtReduction +
                " | بازپرداخت: " + refundAmount);
            au.ExecuteNonQuery();

            tx.Commit();
            MessageBox.Show("برگشت ثبت شد. مبلغ: " + returnTotal + " | شماره: " + no);
            LoadInvoice();
        }
        catch (Exception ex)
        {
            MessageBox.Show("ثبت برگشت انجام نشد و تغییری ذخیره نشد.\n" + ex.Message,
                "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
