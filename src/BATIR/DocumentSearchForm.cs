using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class DocumentSearchForm : Form
{
    readonly TextBox number = new();
    readonly DateTimePicker from = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddMonths(-1) };
    readonly DateTimePicker to = new() { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
    readonly ComboBox type = new();
    readonly DataGridView grid = new();

    public DocumentSearchForm()
    {
        Text = "جستجوی اسناد | BATIR";
        Width = 1150; Height = 680;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Build();
        number.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Search(); e.SuppressKeyPress = true; } };
        Search();
    }

    void Build()
    {
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.F)
            {
                number.Focus();
                number.SelectAll();
                e.SuppressKeyPress = true;
            }
        };
        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 105, ColumnCount = 5, RowCount = 2, Padding = new Padding(8) };
        for (int i = 0; i < 5; i++) top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        AddText(top, "شماره سند / فاکتور", number, 0, 0);
        AddDate(top, "از تاریخ", from, 1, 0);
        AddDate(top, "تا تاریخ", to, 2, 0);
        AddCombo(top, "نوع سند", type, 3, 0);
        type.Items.AddRange(new object[] { "همه", "فروش", "خرید", "برگشت فروش", "برگشت خرید", "چک", "دریافت/پرداخت" });
        type.SelectedIndex = 0;

        var search = new Button { Text = "جستجو", Dock = DockStyle.Fill };
        search.Click += (_, _) => Search();
        top.Controls.Add(search, 4, 0);

        var clear = new Button { Text = "پاک کردن فیلتر شماره", Dock = DockStyle.Fill };
        clear.Click += (_, _) => { number.Clear(); Search(); };
        top.Controls.Add(clear, 0, 1);

        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        Controls.Add(grid);
        Controls.Add(top);
    }

    void AddText(TableLayoutPanel t, string label, TextBox box, int c, int r)
    {
        var p = new Panel { Dock = DockStyle.Fill };
        p.Controls.Add(box);
        box.Dock = DockStyle.Fill;
        p.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 120, TextAlign = ContentAlignment.MiddleRight });
        t.Controls.Add(p, c, r);
    }

    void AddDate(TableLayoutPanel t, string label, DateTimePicker box, int c, int r)
    {
        var p = new Panel { Dock = DockStyle.Fill };
        p.Controls.Add(box);
        box.Dock = DockStyle.Fill;
        p.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 75, TextAlign = ContentAlignment.MiddleRight });
        t.Controls.Add(p, c, r);
    }

    void AddCombo(TableLayoutPanel t, string label, ComboBox box, int c, int r)
    {
        var p = new Panel { Dock = DockStyle.Fill };
        p.Controls.Add(box);
        box.Dock = DockStyle.Fill;
        p.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 75, TextAlign = ContentAlignment.MiddleRight });
        t.Controls.Add(p, c, r);
    }

    void Search()
    {
        var start = from.Value.Date.ToString("yyyy-MM-dd 00:00:00");
        var end = to.Value.Date.AddDays(1).ToString("yyyy-MM-dd 00:00:00");
        var startDate = from.Value.Date.ToString("yyyy-MM-dd");
        var endDate = to.Value.Date.AddDays(1).ToString("yyyy-MM-dd");
        var q = number.Text.Trim();
        var selected = type.SelectedItem?.ToString() ?? "همه";

        try
        {
            var sources = new List<string>();

            if (selected is "همه" or "فروش" or "خرید")
            {
                var invoiceFilter = selected switch
                {
                    "فروش" => " AND i.Type='Sale'",
                    "خرید" => " AND i.Type='Purchase'",
                    _ => ""
                };
                sources.Add($@"
SELECT i.Id AS [شناسه], i.InvoiceNo AS [شماره سند],
CASE i.Type WHEN 'Sale' THEN 'فروش' WHEN 'Purchase' THEN 'خرید' ELSE i.Type END AS [نوع],
i.DateText AS [تاریخ], COALESCE(c.Name,s.Name,'') AS [طرف حساب],
i.Total AS [مبلغ], i.Paid AS [پرداختی], (i.Total-i.Paid) AS [مانده],
'' AS [وضعیت], '' AS [روش پرداخت], i.Notes AS [توضیحات]
FROM Invoices i
LEFT JOIN Customers c ON c.Id=i.CustomerId
LEFT JOIN Suppliers s ON s.Id=i.SupplierId
WHERE i.DateText>=@from AND i.DateText<@to{invoiceFilter}
AND (@q='' OR i.InvoiceNo LIKE @like)");
            }

            if (selected is "همه" or "برگشت فروش")
            {
                sources.Add(@"
SELECT r.Id, r.ReturnNo, 'برگشت فروش', r.DateText, COALESCE(c.Name,''),
r.Total, 0, 0, '', '', r.Notes
FROM SalesReturns r
LEFT JOIN Customers c ON c.Id=r.CustomerId
WHERE r.DateText>=@from AND r.DateText<@to
AND (@q='' OR r.ReturnNo LIKE @like)");
            }

            if (selected is "همه" or "برگشت خرید")
            {
                sources.Add(@"
SELECT r.Id, r.ReturnNo, 'برگشت خرید', r.DateText, COALESCE(s.Name,''),
r.Total, 0, 0, '', '', r.Notes
FROM PurchaseReturns r
LEFT JOIN Suppliers s ON s.Id=r.SupplierId
WHERE r.DateText>=@from AND r.DateText<@to
AND (@q='' OR r.ReturnNo LIKE @like)");
            }

            if (selected is "همه" or "چک")
            {
                sources.Add(@"
SELECT Id, CheckNo, CASE Type WHEN 'دریافتی' THEN 'چک دریافتی' ELSE 'چک پرداختی' END,
COALESCE(DueDate,''), COALESCE(PartyName,''), Amount, 0, 0, Status, 'چک', Notes
FROM Checks
WHERE COALESCE(DueDate,'')>=@startDate AND COALESCE(DueDate,'')<@endDate
AND (@q='' OR CheckNo LIKE @like OR PartyName LIKE @like)");
            }

            if (selected is "همه" or "دریافت/پرداخت")
            {
                sources.Add(@"
SELECT Id, COALESCE(ReferenceType || ':' || CAST(ReferenceId AS TEXT), CAST(Id AS TEXT)),
Type, DateText, '', Amount, Amount, 0, '', PaymentMethod, Description
FROM CashTransactions
WHERE DateText>=@from AND DateText<@to
AND (@q='' OR CAST(Id AS TEXT) LIKE @like
     OR CAST(COALESCE(ReferenceId,0) AS TEXT) LIKE @like
     OR COALESCE(ReferenceType,'') LIKE @like)");
            }

            if (sources.Count == 0)
            {
                grid.DataSource = null;
                return;
            }

            var sql = $@"
SELECT [شناسه], [شماره سند], [نوع], [تاریخ], [طرف حساب], [مبلغ], [پرداختی],
[مانده], [وضعیت], [روش پرداخت], [توضیحات]
FROM (
{string.Join("\nUNION ALL\n", sources)}
)
ORDER BY [تاریخ] DESC, [شناسه] DESC;";

            var parameters = new[]
            {
                new SqliteParameter("@from", start),
                new SqliteParameter("@to", end),
                new SqliteParameter("@startDate", startDate),
                new SqliteParameter("@endDate", endDate),
                new SqliteParameter("@q", q),
                new SqliteParameter("@like", "%" + q + "%")
            };
            grid.DataSource = Database.Query(sql, parameters);
        }
        catch (Exception ex)
        {
            grid.DataSource = null;
            MessageBox.Show("جستجوی اسناد انجام نشد.\n" + ex.Message, "خطا",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
