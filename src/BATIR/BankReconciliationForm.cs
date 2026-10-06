using Microsoft.Data.Sqlite;

namespace BATIR;

public class BankReconciliationForm : Form
{
    readonly DataGridView grid = new();
    readonly TextBox accountName = new();
    readonly TextBox bankName = new();
    readonly TextBox accountNo = new();
    readonly NumericUpDown statement = new() { Minimum = -999999999999, Maximum = 999999999999 };
    readonly Label result = new();

    public BankReconciliationForm()
    {
        Text = "BATIR | مغایرت‌گیری بانکی"; Width = 1050; Height = 650;
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        var form = new TableLayoutPanel { Dock = DockStyle.Top, Height = 125, ColumnCount = 4, RowCount = 2 };
        for (var i = 0; i < 4; i++) form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        Add(form, "نام حساب", accountName, 0, 0); Add(form, "بانک", bankName, 1, 0); Add(form, "شماره حساب", accountNo, 2, 0); Add(form, "مانده صورت‌حساب", statement, 3, 0);
        var save = new Button { Text = "ثبت حساب و مغایرت‌گیری", Dock = DockStyle.Fill }; save.Click += Reconcile; form.Controls.Add(save, 0, 1);
        var refresh = new Button { Text = "بازخوانی حساب‌ها", Dock = DockStyle.Fill }; refresh.Click += (_, _) => LoadData(); form.Controls.Add(refresh, 1, 1);
        result.Text = ""; result.Dock = DockStyle.Fill; result.TextAlign = ContentAlignment.MiddleCenter; form.Controls.Add(result, 2, 1); form.SetColumnSpan(result, 2);
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        root.Controls.Add(grid); root.Controls.Add(form); Controls.Add(root); LoadData();
    }

    void Add(TableLayoutPanel t, string label, Control c, int col, int row)
    {
        var p = new Panel { Dock = DockStyle.Fill }; p.Controls.Add(c); c.Dock = DockStyle.Fill; p.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 90, TextAlign = ContentAlignment.MiddleRight }); t.Controls.Add(p, col, row);
    }

    void LoadData()
    {
        grid.DataSource = Database.Query(@"SELECT b.Id,b.Name AS [حساب],b.BankName AS [بانک],b.AccountNo AS [شماره حساب],b.OpeningBalance AS [مانده افتتاحیه],
COALESCE((SELECT SUM(CASE WHEN Type IN ('Deposit','Credit') THEN Amount ELSE -Amount END) FROM BankTransactions t WHERE t.BankAccountId=b.Id),0)+b.OpeningBalance AS [مانده دفتری]
FROM BankAccounts b WHERE b.Active=1 ORDER BY b.Id DESC");
    }

    void Reconcile(object? sender, EventArgs e)
    {
        if (!PermissionService.Require("Finance.Edit", this)) return;
        if (string.IsNullOrWhiteSpace(accountName.Text)) { MessageBox.Show(this, "نام حساب را وارد کنید."); return; }
        Database.Execute("INSERT INTO BankAccounts(Name,BankName,AccountNo) VALUES(@name,@bank,@no)", new SqliteParameter("@name", accountName.Text.Trim()), new SqliteParameter("@bank", bankName.Text.Trim()), new SqliteParameter("@no", accountNo.Text.Trim()));
        var dt = Database.Query("SELECT Id,COALESCE(OpeningBalance,0)+COALESCE((SELECT SUM(CASE WHEN Type IN ('Deposit','Credit') THEN Amount ELSE -Amount END) FROM BankTransactions t WHERE t.BankAccountId=BankAccounts.Id),0) AS BookBalance FROM BankAccounts WHERE Name=@name ORDER BY Id DESC LIMIT 1", new SqliteParameter("@name", accountName.Text.Trim()));
        var book = dt.Rows.Count == 0 ? 0 : Convert.ToInt64(dt.Rows[0]["BookBalance"]); var difference = (long)statement.Value - book;
        Database.Execute(@"INSERT INTO BankReconciliations(BankAccountId,FromDate,ToDate,StatementBalance,BookBalance,Difference,Status,CreatedAt)
VALUES(@id,@date,@date,@statement,@book,@difference,@status,@created)",
            new SqliteParameter("@id", Convert.ToInt64(dt.Rows[0]["Id"])), new SqliteParameter("@date", DateTime.Now.ToString("yyyy-MM-dd")), new SqliteParameter("@statement", (long)statement.Value), new SqliteParameter("@book", book), new SqliteParameter("@difference", difference), new SqliteParameter("@status", difference == 0 ? "Matched" : "Open"), new SqliteParameter("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        result.Text = difference == 0 ? "مطابقت کامل است." : "مغایرت: " + difference.ToString("N0"); LoadData();
    }
}
