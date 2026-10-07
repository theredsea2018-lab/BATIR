using Microsoft.Data.Sqlite;
namespace BATIR;

public class SuppliersForm : Form
{
    readonly DataGridView grid=new(); readonly TextBox name=new(), phone=new(), address=new(), notes=new();
    readonly NumericUpDown balance=new(){Minimum=-999999999999,Maximum=999999999999};
    long id;
    public SuppliersForm(){Text="BATIR | تأمین‌کنندگان";Width=1050;Height=650;RightToLeft=RightToLeft.Yes;RightToLeftLayout=true;Build();LoadData();}
    void Build(){
        var f=new TableLayoutPanel{Dock=DockStyle.Top,Height=125,ColumnCount=4,RowCount=2,Padding=new Padding(8)};
        for(int i=0;i<4;i++)f.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        Add(f,"نام",name,0,0);Add(f,"تلفن",phone,1,0);Add(f,"آدرس",address,2,0);AddNum(f,"مانده",balance,3,0);Add(f,"توضیحات",notes,0,1);
        var s=new Button{Text="ثبت / ویرایش",Dock=DockStyle.Fill};s.Click+=(_,_)=>Save();f.Controls.Add(s,1,1);
        var c=new Button{Text="پاک کردن",Dock=DockStyle.Fill};c.Click+=(_,_)=>Clear();f.Controls.Add(c,2,1);
        grid.Dock=DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)Edit(e.RowIndex);};
        Controls.Add(grid);Controls.Add(f);
    }
    void Add(TableLayoutPanel t,string l,TextBox b,int c,int r){var p=new Panel{Dock=DockStyle.Fill};b.Dock=DockStyle.Fill;p.Controls.Add(b);p.Controls.Add(new Label{Text=l,Dock=DockStyle.Right,Width=80,TextAlign=ContentAlignment.MiddleRight});t.Controls.Add(p,c,r);}
    void AddNum(TableLayoutPanel t,string l,NumericUpDown b,int c,int r){b.Dock=DockStyle.Fill;var p=new Panel{Dock=DockStyle.Fill};p.Controls.Add(b);p.Controls.Add(new Label{Text=l,Dock=DockStyle.Right,Width=80,TextAlign=ContentAlignment.MiddleRight});t.Controls.Add(p,c,r);}
    void LoadData(){grid.DataSource=Database.Query("SELECT Id,Name AS [نام],Phone AS [تلفن],Address AS [آدرس],Balance AS [مانده],Notes AS [توضیحات] FROM Suppliers ORDER BY Id DESC");}
    void Edit(int r){id=Convert.ToInt64(grid.Rows[r].Cells["Id"].Value);var d=Database.Query("SELECT Name,Phone,Address,Balance,Notes FROM Suppliers WHERE Id=@id",new SqliteParameter("@id",id));if(d.Rows.Count==0)return;name.Text=d.Rows[0]["Name"].ToString();phone.Text=d.Rows[0]["Phone"].ToString();address.Text=d.Rows[0]["Address"].ToString();balance.Value=Convert.ToDecimal(d.Rows[0]["Balance"]);notes.Text=d.Rows[0]["Notes"].ToString();}
    void Save(){if(!PermissionService.Require("Suppliers.Edit", this)) return;if(string.IsNullOrWhiteSpace(name.Text)){MessageBox.Show("نام تأمین‌کننده را وارد کنید.");return;}if(id==0)Database.Execute("INSERT INTO Suppliers(Name,Phone,Address,Balance,Notes) VALUES(@n,@p,@a,@b,@x)",P());else Database.Execute("UPDATE Suppliers SET Name=@n,Phone=@p,Address=@a,Balance=@b,Notes=@x WHERE Id=@id",P(new SqliteParameter("@id",id)));Clear();LoadData();}
    SqliteParameter[] P(params SqliteParameter[] x){var a=new List<SqliteParameter>{new("@n",name.Text.Trim()),new("@p",phone.Text.Trim()),new("@a",address.Text.Trim()),new("@b",(long)balance.Value),new("@x",notes.Text.Trim())};a.AddRange(x);return a.ToArray();}
    void Clear(){id=0;name.Clear();phone.Clear();address.Clear();notes.Clear();balance.Value=0;}
}