using Microsoft.Data.Sqlite;
namespace BATIR;

public class InventoryReportForm : Form
{
 readonly DataGridView grid=new(); readonly TextBox search=new();
 public InventoryReportForm(){Text="BATIR | گزارش موجودی و سود";Width=1100;Height=650;RightToLeft=RightToLeft.Yes;RightToLeftLayout=true;
  var top=new Panel{Dock=DockStyle.Top,Height=45,Padding=new Padding(6)};search.Dock=DockStyle.Fill;top.Controls.Add(search);search.TextChanged+=(_,_)=>LoadData();
  grid.Dock=DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
  Controls.Add(grid);Controls.Add(top);LoadData();}
 void LoadData(){var q=search.Text.Trim();var sql=@"SELECT Name AS [کالا],Brand AS [برند],Category AS [دسته],Stock AS [موجودی],
PurchasePrice AS [قیمت خرید],SalePrice AS [قیمت فروش],
(SalePrice-PurchasePrice) AS [سود واحد],
((SalePrice-PurchasePrice)*Stock) AS [سود بالقوه موجودی]
FROM Products WHERE Active=1";if(q!="")sql+=" AND (Name LIKE @q OR Barcode LIKE @q OR Brand LIKE @q OR Category LIKE @q)";sql+=" ORDER BY Name";
 grid.DataSource=q==""?Database.Query(sql):Database.Query(sql,new SqliteParameter("@q","%"+q+"%"));}
}