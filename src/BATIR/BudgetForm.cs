using Microsoft.Data.Sqlite;

namespace BATIR;

public class BudgetForm : Form
{
    readonly DataGridView grid = new();
    readonly TextBox name = new();
    readonly ComboBox period = new();
    readonly TextBox from = new();
    readonly TextBox to = new();
    readonly NumericUpDown amount = new() { Minimum = 0, Maximum = 999999999999 };
    readonly TextBox category = new();

    public BudgetForm()
    {
        Text = "BATIR | بودجه‌بندی"; Width = 1000; Height = 620;
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        period.Items.AddRange(new object[] { "ماهانه", "سالانه", "سفارشی" }); period.SelectedIndex = 0;
        from.Text = DateTime.Now.ToString("yyyy-MM-dd"); to.Text = DateTime.Now.AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        var form = new TableLayoutPanel { Dock = DockStyle.Top, Height = 120, ColumnCount = 4, RowCount = 2 };
        for (var i = 0; i < 4; i++) form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        Add(form, "نام بودجه", name, 0, 0); Add(form, "دوره", period, 1, 0); Add(form, "از تاریخ", from, 2, 0); Add(form, "تا تاریخ", to, 3, 0);
        Add(form, "مبلغ", amount, 0, 1); Add(form, "گروه هزینه", category, 1, 1);
        var save = new Button { Text = "ثبت بودجه", Dock = DockStyle.Fill }; save.Click += Save; form.Controls.Add(save, 2, 1);
        var refresh = new Button { Text = "بازخوانی", Dock = DockStyle.Fill }; refresh.Click += (_, _) => LoadData(); form.Controls.Add(refresh, 3, 1);
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        root.Controls.Add(grid); root.Controls.Add(form); Controls.Add(root); LoadData();
    }

    void Add(TableLayoutPanel t, string label, Control c, int col, int row)
    {
        var p = new Panel { Dock = DockStyle.Fill }; p.Controls.Add(c); c.Dock = DockStyle.Fill; p.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 80, TextAlign = ContentAlignment.MiddleRight }); t.Controls.Add(p, col, row);
    }

    void LoadData() => grid.DataSource = Database.Query("SELECT Id,Name AS [بودجه],PeriodType AS [دوره],FromDate AS [از],ToDate AS [تا],Amount AS [مبلغ],Category AS [گروه] FROM Budgets ORDER BY Id DESC");

    void Save(object? sender, EventArgs e)
    {
        if (!PermissionService.Require("Finance.Edit", this)) return;
        if (string.IsNullOrWhiteSpace(name.Text) || !DateTime.TryParse(from.Text, out _) || !DateTime.TryParse(to.Text, out _)) { MessageBox.Show(this, "نام و تاریخ‌های معتبر را وارد کنید."); return; }
        Database.Execute("INSERT INTO Budgets(Name,PeriodType,FromDate,ToDate,Amount,Category) VALUES(@name,@period,@from,@to,@amount,@category)", new SqliteParameter("@name", name.Text.Trim()), new SqliteParameter("@period", period.Text), new SqliteParameter("@from", from.Text.Trim()), new SqliteParameter("@to", to.Text.Trim()), new SqliteParameter("@amount", (long)amount.Value), new SqliteParameter("@category", category.Text.Trim()));
        LoadData();
    }
}
