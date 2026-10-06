using Microsoft.Data.Sqlite;

namespace BATIR;

public class CashFlowForm : Form
{
    readonly DataGridView grid = new();
    readonly Label summary = new();

    public CashFlowForm()
    {
        Text = "BATIR | گردش و پیش‌بینی نقدینگی"; Width = 1050; Height = 650;
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        var refresh = new Button { Text = "محاسبه مجدد", Dock = DockStyle.Top, Height = 44 }; refresh.Click += (_, _) => LoadData();
        summary.Dock = DockStyle.Top; summary.Height = 55; summary.TextAlign = ContentAlignment.MiddleCenter;
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        root.Controls.Add(grid); root.Controls.Add(summary); root.Controls.Add(refresh); Controls.Add(root); LoadData();
    }

    void LoadData()
    {
        if (!PermissionService.Require("Reports.View", this)) { BeginInvoke(new Action(Close)); return; }
        var dt = Database.Query(@"SELECT substr(DateText,1,10) AS [تاریخ],
SUM(CASE WHEN Type IN ('SalePayment','CustomerReceipt','Income','Deposit','Credit') THEN Amount ELSE 0 END) AS [ورودی],
SUM(CASE WHEN Type IN ('SupplierPayment','Expense','Payment','Withdrawal','Debit') THEN Amount ELSE 0 END) AS [خروجی]
FROM CashTransactions GROUP BY substr(DateText,1,10) ORDER BY [تاریخ] DESC LIMIT 365");
        grid.DataSource = dt;
        long income = 0, expense = 0; foreach (System.Data.DataRow row in dt.Rows) { income += Convert.ToInt64(row["ورودی"]); expense += Convert.ToInt64(row["خروجی"]); }
        summary.Text = "ورودی ۳۶۵ روز اخیر: " + income.ToString("N0") + " | خروجی: " + expense.ToString("N0") + " | خالص: " + (income - expense).ToString("N0");
    }
}
