using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class InventoryTransferForm : Form
{
    readonly TextBox search=new();
    readonly ComboBox product=new();
    readonly TextBox quantity=new();
    readonly TextBox note=new();
    readonly DataGridView grid=new();

    public InventoryTransferForm()
    {
        Text="BATIR | انتقال/اصلاح موجودی";
        Width=1050; Height=650; RightToLeft=RightToLeft.Yes; RightToLeftLayout=true;
        Build(); LoadProducts(); LoadHistory();
    }

    void Build()
    {
        var top=new TableLayoutPanel{Dock=DockStyle.Top,Height=125,ColumnCount=4,RowCount=2,Padding=new Padding(8)};
        for(int i=0;i<4;i++) top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        AddText(top,"جستجوی کالا",search,0,0);
        AddCombo(top,"کالا",product,1,0);
        AddText(top,"تعداد (+/-)",quantity,2,0);
        AddText(top,"شرح",note,3,0);
        var save=new Button{Text="ثبت اصلاح موجودی",Dock=DockStyle.Fill}; save.Click+=(_,_)=>Save();
        top.Controls.Add(save,0,1);
        var refresh=new Button{Text="تازه‌سازی",Dock=DockStyle.Fill}; refresh.Click+=(_,_)=>{LoadProducts();LoadHistory();};
        top.Controls.Add(refresh,1,1);
        search.TextChanged+=(_,_)=>LoadProducts();

        grid.Dock=DockStyle.Fill; grid.ReadOnly=true; grid.AllowUserToAddRows=false; grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        Controls.Add(grid);Controls.Add(top);
    }
    void AddText(TableLayoutPanel t,string label,Control box,int c,int r)
    {
        box.Dock=DockStyle.Fill; var p=new Panel{Dock=DockStyle.Fill};p.Controls.Add(box);
        p.Controls.Add(new Label{Text=label,Dock=DockStyle.Right,Width=95,TextAlign=ContentAlignment.MiddleRight});t.Controls.Add(p,c,r);
    }
    void AddCombo(TableLayoutPanel t,string label,ComboBox box,int c,int r)
    {
        box.Dock=DockStyle.Fill;var p=new Panel{Dock=DockStyle.Fill};p.Controls.Add(box);
        p.Controls.Add(new Label{Text=label,Dock=DockStyle.Right,Width=95,TextAlign=ContentAlignment.MiddleRight});t.Controls.Add(p,c,r);
    }
    void LoadProducts()
    {
        var q=search.Text.Trim(); product.Items.Clear();
        var dt=string.IsNullOrWhiteSpace(q)?Database.Query("SELECT Id,Name,Stock FROM Products WHERE Active=1 ORDER BY Name"):
            Database.Query("SELECT Id,Name,Stock FROM Products WHERE Active=1 AND (Name LIKE @q OR Barcode LIKE @q OR Code LIKE @q) ORDER BY Name",new SqliteParameter("@q","%"+q+"%"));
        foreach(DataRow r in dt.Rows) product.Items.Add(new ProductItem(Convert.ToInt64(r["Id"]),Convert.ToString(r["Name"])??"",Convert.ToInt64(r["Stock"])));
        if(product.Items.Count>0) product.SelectedIndex=0;
    }
    void Save()
    {
        if(!(product.SelectedItem is ProductItem p)){MessageBox.Show("کالا را انتخاب کنید.");return;}
        if(!long.TryParse(quantity.Text.Replace(",","").Replace("٬","").Trim(),out var diff)||diff==0){MessageBox.Show("مقدار مثبت یا منفی وارد کنید.");return;}
        try
        {
            using var cn=Database.Open();using var tx=cn.BeginTransaction();
            using var q=cn.CreateCommand();q.Transaction=tx;q.CommandText="SELECT Stock FROM Products WHERE Id=@id";q.Parameters.AddWithValue("@id",p.Id);
            var current=Convert.ToInt64(q.ExecuteScalar()??0); var target=current+diff;
            if(target<0) throw new InvalidOperationException("موجودی نهایی نمی‌تواند منفی شود.");
            using var u=cn.CreateCommand();u.Transaction=tx;u.CommandText="UPDATE Products SET Stock=@s WHERE Id=@id";
            u.Parameters.AddWithValue("@s",target);u.Parameters.AddWithValue("@id",p.Id);u.ExecuteNonQuery();
            using var a=cn.CreateCommand();a.Transaction=tx;
            a.CommandText=@"INSERT INTO InventoryAdjustments(DateText,ProductId,BeforeStock,CountedStock,Difference,Note,UserName)
VALUES(@d,@p,@b,@c,@x,@n,'کاربر')";
            a.Parameters.AddWithValue("@d",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));a.Parameters.AddWithValue("@p",p.Id);
            a.Parameters.AddWithValue("@b",current);a.Parameters.AddWithValue("@c",target);a.Parameters.AddWithValue("@x",diff);a.Parameters.AddWithValue("@n",note.Text.Trim());
            a.ExecuteNonQuery();
            using var l=cn.CreateCommand();l.Transaction=tx;l.CommandText=@"INSERT INTO AuditLog(DateText,UserName,Action,Entity,EntityId,Details)
VALUES(@d,'کاربر','اصلاح/انتقال موجودی','Product',@p,@x)";
            l.Parameters.AddWithValue("@d",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));l.Parameters.AddWithValue("@p",p.Id);
            l.Parameters.AddWithValue("@x","قبل: "+current+" | بعد: "+target+" | تغییر: "+diff+" | "+note.Text.Trim());l.ExecuteNonQuery();
            tx.Commit();MessageBox.Show("موجودی اصلاح شد.");quantity.Clear();note.Clear();LoadProducts();LoadHistory();
        }catch(Exception ex){MessageBox.Show("ثبت اصلاح موجودی انجام نشد و تغییری ذخیره نشد.\n"+ex.Message,"خطا",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
    void LoadHistory(){grid.DataSource=Database.Query(@"SELECT ia.Id,ia.DateText AS [تاریخ],p.Name AS [کالا],ia.BeforeStock AS [قبل],ia.CountedStock AS [بعد],ia.Difference AS [تغییر],ia.Note AS [شرح]
FROM InventoryAdjustments ia JOIN Products p ON p.Id=ia.ProductId ORDER BY ia.Id DESC LIMIT 300");}
    sealed class ProductItem{public long Id{get;}public string Name{get;}public long Stock{get;}public ProductItem(long id,string name,long stock){Id=id;Name=name;Stock=stock;}public override string ToString()=>Name+" | موجودی: "+Stock;}
}
