using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class FinancialOperationsForm : Form
{
    readonly ComboBox operation = new();
    readonly ComboBox party = new();
    readonly ComboBox method = new();
    readonly TextBox amount = new();
    readonly TextBox description = new();
    readonly DataGridView grid = new();
    readonly Label balance = new();

    public FinancialOperationsForm()
    {
        Text = "BATIR | دریافت، پرداخت و هزینه";
        Width = 1100; Height = 680;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Build();
        LoadParties();
        LoadHistory();
    }

    void Build()
    {
        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 145, ColumnCount = 4, RowCount = 3, Padding = new Padding(8) };
        for (int i=0;i<4;i++) top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        AddCombo(top,"عملیات",operation,0,0);
        AddCombo(top,"طرف حساب",party,1,0);
        AddText(top,"مبلغ",amount,2,0);
        AddCombo(top,"روش پرداخت",method,3,0);
        AddText(top,"شرح",description,0,1);
        var save = new Button { Text="ثبت عملیات", Dock=DockStyle.Fill };
        save.Click += (_,_) => Save();
        top.Controls.Add(save,1,1);
        var refresh = new Button { Text="تازه‌سازی", Dock=DockStyle.Fill };
        refresh.Click += (_,_) => { LoadParties(); LoadHistory(); };
        top.Controls.Add(refresh,2,1);
        balance.Dock=DockStyle.Fill; balance.TextAlign=ContentAlignment.MiddleCenter;
        top.Controls.Add(balance,3,1);

        operation.Items.AddRange(new object[]{"دریافت از مشتری","پرداخت به تأمین‌کننده","هزینه"});
        method.Items.AddRange(new object[]{"نقدی","کارتخوان","انتقال بانکی","چک"});
        operation.SelectedIndex=0; method.SelectedIndex=0;
        operation.SelectedIndexChanged += (_,_) => LoadParties();

        grid.Dock=DockStyle.Fill; grid.ReadOnly=true; grid.AllowUserToAddRows=false;
        grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        Controls.Add(grid); Controls.Add(top);
    }

    void AddText(TableLayoutPanel t,string label,Control box,int c,int r)
    {
        box.Dock=DockStyle.Fill;
        var p=new Panel{Dock=DockStyle.Fill};
        p.Controls.Add(box);
        p.Controls.Add(new Label{Text=label,Dock=DockStyle.Right,Width=105,TextAlign=ContentAlignment.MiddleRight});
        t.Controls.Add(p,c,r);
    }

    void AddCombo(TableLayoutPanel t,string label,ComboBox box,int c,int r)
    {
        box.Dock=DockStyle.Fill;
        var p=new Panel{Dock=DockStyle.Fill};
        p.Controls.Add(box);
        p.Controls.Add(new Label{Text=label,Dock=DockStyle.Right,Width=105,TextAlign=ContentAlignment.MiddleRight});
        t.Controls.Add(p,c,r);
    }

    void LoadParties()
    {
        var selected=operation.SelectedItem?.ToString()??"دریافت از مشتری";
        party.Items.Clear();
        party.Items.Add("— بدون طرف حساب —");
        if(selected=="دریافت از مشتری")
        {
            var dt=Database.Query("SELECT Id,Name FROM Customers ORDER BY Name");
            foreach(DataRow r in dt.Rows) party.Items.Add(new PartyItem(Convert.ToInt64(r["Id"]),Convert.ToString(r["Name"])??"",true));
        }
        else if(selected=="پرداخت به تأمین‌کننده")
        {
            var dt=Database.Query("SELECT Id,Name FROM Suppliers ORDER BY Name");
            foreach(DataRow r in dt.Rows) party.Items.Add(new PartyItem(Convert.ToInt64(r["Id"]),Convert.ToString(r["Name"])??"",false));
        }
        party.SelectedIndex=0;
        balance.Text="";
    }

    void Save()
    {
        if(!long.TryParse(amount.Text.Replace(",","").Replace("٬","").Trim(),out var value)||value<=0)
        { MessageBox.Show("مبلغ معتبر وارد کنید."); return; }

        var selected=operation.SelectedItem?.ToString()??"";
        var item=party.SelectedItem as PartyItem;
        if(selected!="هزینه" && item==null)
        { MessageBox.Show("طرف حساب را انتخاب کنید."); return; }
        if(method.Text=="چک")
        { MessageBox.Show("ثبت چک در بخش «مدیریت چک‌ها» انجام می‌شود؛ این عملیات برای دریافت/پرداخت قطعی نقدی است."); return; }

        try
        {
            using var cn=Database.Open();
            using var tx=cn.BeginTransaction();
            long? customer=null, supplier=null;
            string type, reference, desc=description.Text.Trim();
            if(selected=="دریافت از مشتری")
            {
                customer=item!.Id; type="CustomerReceipt"; reference="CustomerReceipt";
                using var check=cn.CreateCommand(); check.Transaction=tx; check.CommandText="SELECT Balance FROM Customers WHERE Id=@id";
                check.Parameters.AddWithValue("@id",customer.Value);
                var current=Convert.ToInt64(check.ExecuteScalar()??0);
                if(value>current) throw new InvalidOperationException("مبلغ دریافت بیشتر از مانده بدهکار مشتری است.");
                using var upd=cn.CreateCommand(); upd.Transaction=tx; upd.CommandText="UPDATE Customers SET Balance=Balance-@x WHERE Id=@id";
                upd.Parameters.AddWithValue("@x",value); upd.Parameters.AddWithValue("@id",customer.Value); upd.ExecuteNonQuery();
                if(string.IsNullOrWhiteSpace(desc)) desc="دریافت از مشتری";
            }
            else if(selected=="پرداخت به تأمین‌کننده")
            {
                supplier=item!.Id; type="SupplierPayment"; reference="SupplierPayment";
                using var check=cn.CreateCommand(); check.Transaction=tx; check.CommandText="SELECT Balance FROM Suppliers WHERE Id=@id";
                check.Parameters.AddWithValue("@id",supplier.Value);
                var current=Convert.ToInt64(check.ExecuteScalar()??0);
                if(value>current) throw new InvalidOperationException("مبلغ پرداخت بیشتر از بدهی تأمین‌کننده است.");
                using var upd=cn.CreateCommand(); upd.Transaction=tx; upd.CommandText="UPDATE Suppliers SET Balance=Balance-@x WHERE Id=@id";
                upd.Parameters.AddWithValue("@x",value); upd.Parameters.AddWithValue("@id",supplier.Value); upd.ExecuteNonQuery();
                if(string.IsNullOrWhiteSpace(desc)) desc="پرداخت به تأمین‌کننده";
            }
            else
            {
                type="Expense"; reference="Expense";
                if(string.IsNullOrWhiteSpace(desc)) throw new InvalidOperationException("شرح هزینه را وارد کنید.");
            }

            using var cash=cn.CreateCommand(); cash.Transaction=tx;
            cash.CommandText=@"INSERT INTO CashTransactions(DateText,Type,Amount,Description,UserName,CustomerId,SupplierId,PaymentMethod,ReferenceType)
VALUES(@d,@t,@a,@desc,'کاربر',@c,@s,@m,@r)";
            cash.Parameters.AddWithValue("@d",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cash.Parameters.AddWithValue("@t",type); cash.Parameters.AddWithValue("@a",value);
            cash.Parameters.AddWithValue("@desc",desc); cash.Parameters.AddWithValue("@c",customer.HasValue?(object)customer.Value:DBNull.Value);
            cash.Parameters.AddWithValue("@s",supplier.HasValue?(object)supplier.Value:DBNull.Value);
            cash.Parameters.AddWithValue("@m",method.Text); cash.Parameters.AddWithValue("@r",reference);
            cash.ExecuteNonQuery();

            using var audit=cn.CreateCommand(); audit.Transaction=tx;
            audit.CommandText=@"INSERT INTO AuditLog(DateText,UserName,Action,Entity,Details)
VALUES(@d,'کاربر',@a,'CashTransaction',@x)";
            audit.Parameters.AddWithValue("@d",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            audit.Parameters.AddWithValue("@a",selected); audit.Parameters.AddWithValue("@x",desc+" | مبلغ: "+value);
            audit.ExecuteNonQuery();
            tx.Commit();
            MessageBox.Show("عملیات با موفقیت ثبت شد.");
            amount.Clear(); description.Clear(); LoadHistory(); LoadParties();
        }
        catch(Exception ex){ MessageBox.Show("عملیات ثبت نشد و تغییری ذخیره نشد.\n"+ex.Message,"خطا",MessageBoxButtons.OK,MessageBoxIcon.Error); }
    }

    void LoadHistory()
    {
        grid.DataSource=Database.Query(@"SELECT Id,DateText AS [تاریخ],
CASE Type WHEN 'CustomerReceipt' THEN 'دریافت از مشتری'
WHEN 'SupplierPayment' THEN 'پرداخت به تأمین‌کننده'
WHEN 'Expense' THEN 'هزینه' ELSE Type END AS [نوع],
Amount AS [مبلغ],PaymentMethod AS [روش],Description AS [شرح]
FROM CashTransactions ORDER BY Id DESC LIMIT 300");
    }

    sealed class PartyItem
    {
        public long Id{get;} public string Name{get;} public bool Customer{get;}
        public PartyItem(long id,string name,bool customer){Id=id;Name=name;Customer=customer;}
        public override string ToString()=>Name;
    }
}
