using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class SalesReturnsForm : Form
{
    readonly TextBox invoiceNo=new();
    readonly DataGridView grid=new();
    readonly Button load=new(){Text="بارگذاری فاکتور"};
    readonly Button ret=new(){Text="برگشت کامل فاکتور",Dock=DockStyle.Fill};
    DataTable items=new();

    public SalesReturnsForm()
    {
        Text="BATIR | برگشت از فروش"; Width=1000; Height=650;
        RightToLeft=RightToLeft.Yes; RightToLeftLayout=true;
        var top=new Panel{Dock=DockStyle.Top,Height=70,Padding=new Padding(8)};
        invoiceNo.Dock=DockStyle.Fill; top.Controls.Add(invoiceNo);
        load.Dock=DockStyle.Right; load.Width=150; top.Controls.Add(load); load.Click+=(_,_)=>LoadInvoice();
        ret.Dock=DockStyle.Bottom; ret.Height=55; ret.Enabled=false; ret.Click+=(_,_)=>ReturnInvoice();
        grid.Dock=DockStyle.Fill; grid.ReadOnly=true; grid.AllowUserToAddRows=false; grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        Controls.Add(grid); Controls.Add(ret); Controls.Add(top);
    }
    void LoadInvoice()
    {
        var q=invoiceNo.Text.Trim(); if(q==""){MessageBox.Show("شماره فاکتور را وارد کنید.");return;}
        items=Database.Query(@"SELECT i.Id, i.ProductId, p.Name AS [کالا], i.Quantity AS [تعداد],
i.UnitPrice AS [فی], i.Discount AS [تخفیف], (i.Quantity*i.UnitPrice-i.Discount) AS [جمع]
FROM InvoiceItems i JOIN Invoices v ON v.Id=i.InvoiceId JOIN Products p ON p.Id=i.ProductId
WHERE v.InvoiceNo=@no AND v.Type='Sale'",new SqliteParameter("@no",q));
        grid.DataSource=items; ret.Enabled=items.Rows.Count>0;
        if(items.Rows.Count==0)MessageBox.Show("فاکتور فروش پیدا نشد.");
    }
    void ReturnInvoice()
    {
        if(items.Rows.Count==0)return;
        if(MessageBox.Show("کل اقلام این فاکتور به انبار برگردانده و بدهی مشتری اصلاح می‌شود. ادامه؟","برگشت از فروش",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
        try{
            using var cn=Database.Open(); using var tx=cn.BeginTransaction();
            long invoiceId=Convert.ToInt64(items.Rows[0]["Id"]); 
            using var q=cn.CreateCommand(); q.Transaction=tx;
            q.CommandText="SELECT Id,CustomerId,Total,Paid FROM Invoices WHERE Id=(SELECT InvoiceId FROM InvoiceItems WHERE Id=@item)";
            q.Parameters.AddWithValue("@item",invoiceId);
            long originalId=0,customerId=0,total=0,paid=0;
            using(var r=q.ExecuteReader()){if(!r.Read()){tx.Rollback();return;} originalId=r.GetInt64(0); customerId=r.IsDBNull(1)?0:r.GetInt64(1); total=r.GetInt64(2); paid=r.GetInt64(3);}
            var already=0L;
            using(var a=cn.CreateCommand()){a.Transaction=tx;a.CommandText="SELECT COALESCE(SUM(Total),0) FROM SalesReturns WHERE OriginalInvoiceId=@id";a.Parameters.AddWithValue("@id",originalId);already=Convert.ToInt64(a.ExecuteScalar());}
            if(already>=total){tx.Rollback();MessageBox.Show("این فاکتور قبلاً به‌طور کامل برگشت خورده است.");return;}
            string no=DateTime.Now.ToString("yyyyMMddHHmmssfff");
            using var h=cn.CreateCommand();h.Transaction=tx;
            h.CommandText=@"INSERT INTO SalesReturns(ReturnNo,OriginalInvoiceId,CustomerId,DateText,Total,Notes)
VALUES(@no,@inv,@customer,@date,@total,'برگشت کامل فاکتور'); SELECT last_insert_rowid();";
            h.Parameters.AddWithValue("@no",no);h.Parameters.AddWithValue("@inv",originalId);h.Parameters.AddWithValue("@customer",customerId==0?DBNull.Value:(object)customerId);
            h.Parameters.AddWithValue("@date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));h.Parameters.AddWithValue("@total",total);
            long rid=Convert.ToInt64(h.ExecuteScalar());
            foreach(DataRow row in items.Rows){
                long pid=Convert.ToInt64(row["ProductId"]),qty=Convert.ToInt64(row["تعداد"]),price=Convert.ToInt64(row["فی"]),disc=Convert.ToInt64(row["تخفیف"]);
                using var ri=cn.CreateCommand();ri.Transaction=tx;ri.CommandText="INSERT INTO SalesReturnItems(ReturnId,ProductId,Quantity,UnitPrice,Discount) VALUES(@r,@p,@q,@u,@d)";
                ri.Parameters.AddWithValue("@r",rid);ri.Parameters.AddWithValue("@p",pid);ri.Parameters.AddWithValue("@q",qty);ri.Parameters.AddWithValue("@u",price);ri.Parameters.AddWithValue("@d",disc);ri.ExecuteNonQuery();
                using var st=cn.CreateCommand();st.Transaction=tx;st.CommandText="UPDATE Products SET Stock=Stock+@q WHERE Id=@p";st.Parameters.AddWithValue("@q",qty);st.Parameters.AddWithValue("@p",pid);st.ExecuteNonQuery();
            }
            if(customerId>0){long outstanding=total-paid;using var b=cn.CreateCommand();b.Transaction=tx;b.CommandText="UPDATE Customers SET Balance=CASE WHEN Balance>=@x THEN Balance-@x ELSE 0 END WHERE Id=@id";b.Parameters.AddWithValue("@x",outstanding);b.Parameters.AddWithValue("@id",customerId);b.ExecuteNonQuery();}
            using var au=cn.CreateCommand();au.Transaction=tx;au.CommandText="INSERT INTO AuditLog(DateText,UserName,Action,Entity,EntityId,Details) VALUES(@d,'کاربر','برگشت از فروش','SalesReturn',@id,@details)";
            au.Parameters.AddWithValue("@d",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));au.Parameters.AddWithValue("@id",rid);au.Parameters.AddWithValue("@details","فاکتور: "+originalId+" | مبلغ: "+total);au.ExecuteNonQuery();
            tx.Commit();MessageBox.Show("برگشت از فروش ثبت شد. شماره: "+no);LoadInvoice();
        }catch(Exception ex){MessageBox.Show("ثبت برگشت انجام نشد و تغییری ذخیره نشد.\n"+ex.Message);}
    }
}