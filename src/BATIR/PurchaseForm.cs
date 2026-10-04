using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class PurchaseForm : Form
{
    readonly TextBox supplier = new();
    readonly TextBox product = new();
    readonly NumericUpDown qty = new() { Minimum = 1, Maximum = 999999 };
    readonly NumericUpDown unitPrice = new() { Minimum = 0, Maximum = 999999999999 };
    readonly NumericUpDown discount = new() { Minimum = 0, Maximum = 999999999999 };
    readonly NumericUpDown paid = new() { Minimum = 0, Maximum = 999999999999 };
    readonly DataGridView grid = new();
    readonly Label total = new();
    readonly DataTable items = new();

    public PurchaseForm()
    {
        Text = "BATIR | فاکتور خرید"; Width = 1100; Height = 700;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 150, ColumnCount = 4, RowCount = 3, Padding = new Padding(8) };
        for (int i=0;i<4;i++) header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        AddText(header,"تأمین‌کننده",supplier,0,0);
        AddText(header,"کالا / بارکد",product,1,0);
        AddNum(header,"تعداد",qty,2,0);
        AddNum(header,"قیمت خرید",unitPrice,3,0);
        AddNum(header,"تخفیف",discount,0,1);
        AddNum(header,"پرداختی",paid,1,1);
        var add = new Button { Text="افزودن به خرید", Dock=DockStyle.Fill };
        add.Click += (_,_) => AddItem();
        header.Controls.Add(add,2,1);
        var save = new Button { Text="ثبت فاکتور خرید", Dock=DockStyle.Fill };
        save.Click += (_,_) => SavePurchase();
        header.Controls.Add(save,3,1);

        items.Columns.Add("ProductId",typeof(long));
        items.Columns.Add("کالا",typeof(string));
        items.Columns.Add("تعداد",typeof(long));
        items.Columns.Add("فی",typeof(long));
        items.Columns.Add("تخفیف",typeof(long));
        items.Columns.Add("جمع",typeof(long));
        grid.Dock=DockStyle.Fill; grid.ReadOnly=true; grid.AllowUserToAddRows=false;
        grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill; grid.DataSource=items;
        total.Dock=DockStyle.Bottom; total.Height=42; total.Font=new Font("Tahoma",12,FontStyle.Bold);
        Controls.Add(grid); Controls.Add(total); Controls.Add(header);
        UpdateTotal();
    }

    void AddText(TableLayoutPanel t,string label,TextBox box,int c,int r)
    {
        var p=new Panel{Dock=DockStyle.Fill}; box.Dock=DockStyle.Fill; p.Controls.Add(box);
        p.Controls.Add(new Label{Text=label,Dock=DockStyle.Right,Width=100,TextAlign=ContentAlignment.MiddleRight}); t.Controls.Add(p,c,r);
    }
    void AddNum(TableLayoutPanel t,string label,NumericUpDown box,int c,int r)
    {
        box.ThousandsSeparator=true; box.Dock=DockStyle.Fill;
        var p=new Panel{Dock=DockStyle.Fill}; p.Controls.Add(box);
        p.Controls.Add(new Label{Text=label,Dock=DockStyle.Right,Width=100,TextAlign=ContentAlignment.MiddleRight}); t.Controls.Add(p,c,r);
    }

    void AddItem()
    {
        var q=product.Text.Trim(); if(q==""){MessageBox.Show("کالا را وارد کنید.");return;}
        var dt=Database.Query("SELECT Id,Name,PurchasePrice FROM Products WHERE Active=1 AND (Barcode=@q OR Name LIKE @like) ORDER BY CASE WHEN Barcode=@q THEN 0 ELSE 1 END,Id DESC LIMIT 1",
            new SqliteParameter("@q",q),new SqliteParameter("@like","%"+q+"%"));
        if(dt.Rows.Count==0){MessageBox.Show("کالا پیدا نشد.");return;}
        long id=Convert.ToInt64(dt.Rows[0]["Id"]);
        string n=Convert.ToString(dt.Rows[0]["Name"])??"";
        long price=unitPrice.Value>0?(long)unitPrice.Value:Convert.ToInt64(dt.Rows[0]["PurchasePrice"]);
        long qnty=(long)qty.Value, disc=(long)discount.Value;
        foreach(DataRow r in items.Rows) if(Convert.ToInt64(r["ProductId"])==id)
        { r["تعداد"]=Convert.ToInt64(r["تعداد"])+qnty; r["فی"]=price; r["تخفیف"]=Convert.ToInt64(r["تخفیف"])+disc; r["جمع"]=Convert.ToInt64(r["تعداد"])*price-Convert.ToInt64(r["تخفیف"]); UpdateTotal(); return; }
        var row=items.NewRow(); row["ProductId"]=id;row["کالا"]=n;row["تعداد"]=qnty;row["فی"]=price;row["تخفیف"]=disc;row["جمع"]=qnty*price-disc;items.Rows.Add(row);
        product.Clear();qty.Value=1;unitPrice.Value=0;discount.Value=0;UpdateTotal();
    }

    long Total(){long x=0;foreach(DataRow r in items.Rows)x+=Convert.ToInt64(r["جمع"]);return x;}
    void UpdateTotal(){total.Text="جمع خرید: "+Total().ToString("N0")+" ریال | پرداختی: "+paid.Value.ToString("N0")+" | مانده: "+Math.Max(0,Total()-(long)paid.Value).ToString("N0")+" ریال";}

    void SavePurchase()
    {
        if(items.Rows.Count==0){MessageBox.Show("خرید خالی است.");return;}
        long t=Total(), p=(long)paid.Value;if(p>t){MessageBox.Show("پرداختی بیشتر از مبلغ خرید است.");return;}
        try{
            using var cn=Database.Open();using var tx=cn.BeginTransaction();
            long? sid=null;string sn=supplier.Text.Trim();
            if(sn!=""){
                using var f=cn.CreateCommand();f.Transaction=tx;f.CommandText="SELECT Id FROM Suppliers WHERE Name=@n LIMIT 1";f.Parameters.AddWithValue("@n",sn);
                var x=f.ExecuteScalar();
                if(x==null||x==DBNull.Value){using var a=cn.CreateCommand();a.Transaction=tx;a.CommandText="INSERT INTO Suppliers(Name) VALUES(@n);SELECT last_insert_rowid();";a.Parameters.AddWithValue("@n",sn);sid=Convert.ToInt64(a.ExecuteScalar());}
                else sid=Convert.ToInt64(x);
            }
            string no=DateTime.Now.ToString("yyyyMMddHHmmssfff");
            using var inv=cn.CreateCommand();inv.Transaction=tx;
            inv.CommandText="INSERT INTO Invoices(InvoiceNo,Type,SupplierId,UserName,DateText,Total,Paid,Notes) VALUES(@no,'Purchase',@sid,'کاربر',@d,@t,@p,'') ;SELECT last_insert_rowid();";
            inv.Parameters.AddWithValue("@no",no);inv.Parameters.AddWithValue("@sid",(object?)sid??DBNull.Value);inv.Parameters.AddWithValue("@d",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));inv.Parameters.AddWithValue("@t",t);inv.Parameters.AddWithValue("@p",p);
            long iid=Convert.ToInt64(inv.ExecuteScalar());
            long outstanding=t-p;
            foreach(DataRow r in items.Rows){
                long pid=Convert.ToInt64(r["ProductId"]),q=Convert.ToInt64(r["تعداد"]),price=Convert.ToInt64(r["فی"]),disc=Convert.ToInt64(r["تخفیف"]);
                long effectiveUnitCost = q > 0 ? Math.Max(0, (q * price - disc) / q) : price;
                using var current=cn.CreateCommand(); current.Transaction=tx;
                current.CommandText="SELECT Stock,PurchasePrice FROM Products WHERE Id=@p";
                current.Parameters.AddWithValue("@p",pid);
                using var rd=current.ExecuteReader();
                if(!rd.Read()) throw new InvalidOperationException("کالا در زمان ثبت خرید پیدا نشد.");
                long oldStock=Convert.ToInt64(rd["Stock"]), oldCost=Convert.ToInt64(rd["PurchasePrice"]);
                rd.Close();
                long baseStock=Math.Max(0,oldStock);
                long newStock=baseStock+q;
                long weightedCost=newStock>0 ? ((baseStock*oldCost)+(q*effectiveUnitCost))/newStock : effectiveUnitCost;
                using var it=cn.CreateCommand();it.Transaction=tx;it.CommandText="INSERT INTO InvoiceItems(InvoiceId,ProductId,Quantity,UnitPrice,CostPrice,Discount) VALUES(@i,@p,@q,@u,@cost,@d)";
                it.Parameters.AddWithValue("@i",iid);it.Parameters.AddWithValue("@p",pid);it.Parameters.AddWithValue("@q",q);it.Parameters.AddWithValue("@u",price);it.Parameters.AddWithValue("@cost",effectiveUnitCost);it.Parameters.AddWithValue("@d",disc);it.ExecuteNonQuery();
                using var st=cn.CreateCommand();st.Transaction=tx;st.CommandText="UPDATE Products SET Stock=Stock+@q,PurchasePrice=@cost WHERE Id=@p";st.Parameters.AddWithValue("@q",q);st.Parameters.AddWithValue("@cost",weightedCost);st.Parameters.AddWithValue("@p",pid);st.ExecuteNonQuery();
            }
            if(sid.HasValue&&outstanding>0){using var b=cn.CreateCommand();b.Transaction=tx;b.CommandText="UPDATE Suppliers SET Balance=Balance+@x WHERE Id=@id";b.Parameters.AddWithValue("@x",outstanding);b.Parameters.AddWithValue("@id",sid.Value);b.ExecuteNonQuery();}
            if(sid.HasValue&&p>0){
                using var cash=cn.CreateCommand(); cash.Transaction=tx;
                cash.CommandText="INSERT INTO CashTransactions(DateText,Type,Amount,Description,UserName,SupplierId,PaymentMethod,ReferenceType,ReferenceId) VALUES(@d,'SupplierPayment',@a,@desc,'کاربر',@sid,'نقدی','Purchase',@rid)";
                cash.Parameters.AddWithValue("@d",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                cash.Parameters.AddWithValue("@a",p); cash.Parameters.AddWithValue("@desc","پرداخت فاکتور خرید "+no);
                cash.Parameters.AddWithValue("@sid",sid.Value); cash.Parameters.AddWithValue("@rid",iid); cash.ExecuteNonQuery();
            }
            using var au=cn.CreateCommand();au.Transaction=tx;au.CommandText="INSERT INTO AuditLog(DateText,UserName,Action,Entity,EntityId,Details) VALUES(@d,'کاربر','ثبت فاکتور خرید','Invoice',@id,@x)";au.Parameters.AddWithValue("@d",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));au.Parameters.AddWithValue("@id",iid);au.Parameters.AddWithValue("@x",no+" | مبلغ: "+t+" | مانده: "+outstanding);au.ExecuteNonQuery();
            tx.Commit();MessageBox.Show("فاکتور خرید ثبت شد. شماره: "+no);items.Clear();supplier.Clear();paid.Value=0;UpdateTotal();
        }catch(Exception ex){MessageBox.Show("ثبت خرید انجام نشد و تغییرات ذخیره نشد.\n"+ex.Message);}
    }
}