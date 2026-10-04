using Microsoft.Data.Sqlite;

namespace BATIR;

public class CustomerStatementForm : Form
{
    readonly TextBox search = new();
    readonly DataGridView grid = new();
    readonly Label total = new();

    public CustomerStatementForm()
    {
        Text = "BATIR | دفتر حساب مشتریان"; Width = 1100; Height = 700;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var top = new Panel { Dock = DockStyle.Top, Height = 45, Padding = new Padding(6) };
        search.Dock = DockStyle.Fill; search.PlaceholderText = "نام یا تلفن مشتری";
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
SELECT c.Name AS [مشتری], c.Phone AS [تلفن],
       c.Balance AS [مانده بدهکار], c.CreditLimit AS [سقف اعتبار],
       COALESCE((SELECT SUM(i.Total) FROM Invoices i WHERE i.CustomerId=c.Id AND i.Type='Sale'),0) AS [جمع فروش],
       COALESCE((SELECT SUM(i.Paid) FROM Invoices i WHERE i.CustomerId=c.Id AND i.Type='Sale'),0) AS [جمع پرداخت فاکتورها],
       COALESCE((SELECT SUM(cr.Amount) FROM CashTransactions cr
                 WHERE cr.Type='CustomerReceipt'
                 AND cr.Description LIKE '%' || c.Name || '%'),0) AS [جمع دریافت],
       COALESCE((SELECT SUM(sr.Total) FROM SalesReturns sr WHERE sr.CustomerId=c.Id),0) AS [جمع برگشت]
FROM Customers c
WHERE (@q='' OR c.Name LIKE @like OR c.Phone LIKE @like)
ORDER BY c.Name",
            new SqliteParameter("@q", q),
            new SqliteParameter("@like", "%" + q + "%"));
        grid.DataSource = dt;
        long balance = 0;
        foreach (System.Data.DataRow row in dt.Rows) balance += Convert.ToInt64(row["مانده بدهکار"]);
        total.Text = "مجموع مانده بدهکار مشتریان: " + balance.ToString("N0") + " ریال";
    }
}
