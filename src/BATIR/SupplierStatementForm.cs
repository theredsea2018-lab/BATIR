using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class SupplierStatementForm : Form
{
    readonly TextBox search = new();
    readonly DataGridView grid = new();
    readonly Label total = new();

    public SupplierStatementForm()
    {
        Text = "BATIR | دفتر حساب تأمین‌کنندگان"; Width = 1100; Height = 700;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var top = new Panel { Dock = DockStyle.Top, Height = 45, Padding = new Padding(6) };
        search.Dock = DockStyle.Fill;
        search.TextChanged += (_,_) => LoadData();
        top.Controls.Add(search);
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        total.Dock = DockStyle.Bottom; total.Height = 38;
        Controls.Add(grid); Controls.Add(total); Controls.Add(top);
        LoadData();
    }

    void LoadData()
    {
        var q = search.Text.Trim();
        var dt = Database.Query(@"
SELECT s.Name AS [تأمین‌کننده], s.Phone AS [تلفن],
       s.Balance AS [مانده بدهی],
       COALESCE((SELECT SUM(i.Total) FROM Invoices i WHERE i.SupplierId=s.Id AND i.Type='Purchase'),0) AS [جمع خرید],
       COALESCE((SELECT SUM(cr.Amount) FROM CashTransactions cr
                 WHERE cr.Type='SupplierPayment' AND cr.SupplierId=s.Id),0) AS [جمع پرداخت],
       COALESCE((SELECT SUM(pr.Total) FROM PurchaseReturns pr WHERE pr.SupplierId=s.Id),0) AS [جمع برگشت]
FROM Suppliers s
WHERE (@q='' OR s.Name LIKE @like OR s.Phone LIKE @like)
ORDER BY s.Name",
            new SqliteParameter("@q", q),
            new SqliteParameter("@like", "%" + q + "%"));
        grid.DataSource = dt;
        long balance = 0;
        foreach (DataRow row in dt.Rows) balance += Convert.ToInt64(row["مانده بدهی"]);
        total.Text = "مجموع بدهی به تأمین‌کنندگان: " + balance.ToString("N0") + " ریال";
    }
}
