using Microsoft.Data.Sqlite;
namespace BATIR;

public class InventoryReportForm : Form
{
 readonly DataGridView grid=new(); readonly TextBox search=new();
 public InventoryReportForm(){Text="BATIR | گزارش موجودی و سود";Width=1100;Height=650;RightToLeft=RightToLeft.Yes;RightToLeftLayout=true;
  var top=new Panel{Dock=DockStyle.Top,Height=45,Padding=new Padding(6)};search.Dock=DockStyle.Fill;top.Controls.Add(search);search.TextChanged+=(_,_)=>LoadData();
  grid.Dock=DockStyle.Fill;grid.ReadOnly=true;grid.AllowUserToAddRows=false;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
  Controls.Add(grid);Controls.Add(top);LoadData();}
 void LoadData(){var q=search.Text.Trim();var sql=@"SELECT p.Name AS [کالا],p.Brand AS [برند],p.Category AS [دسته],p.Stock AS [موجودی],
p.PurchasePrice AS [قیمت خرید],p.SalePrice AS [قیمت فروش],
(p.SalePrice-p.PurchasePrice) AS [سود واحد],
((p.SalePrice-p.PurchasePrice)*p.Stock) AS [سود بالقوه موجودی],
COALESCE(s.TotalIn,0) AS [ورودی ثبت‌شده],
COALESCE(s.TotalOut,0) AS [خروجی ثبت‌شده],
COALESCE(s.NetQuantity,0) AS [خالص گردش],
CASE WHEN p.Stock=COALESCE(s.NetQuantity,0) THEN 'تطبیق' ELSE 'نیازمند بررسی' END AS [وضعیت انبار]
FROM Products p
LEFT JOIN v_InventoryMovementSummary s ON s.ProductId=p.Id
WHERE p.Active=1";if(q!="")sql+=" AND (Name LIKE @q OR Barcode LIKE @q OR Brand LIKE @q OR Category LIKE @q)";sql+=" ORDER BY Name";
 grid.DataSource=q==""?Database.Query(sql):Database.Query(sql,new SqliteParameter("@q","%"+q+"%"));}
}