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
        Width = 1100; Height = 650;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Build(); Search();
    }

    void Build()
    {
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
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        Controls.Add(grid); Controls.Add(top);
    }

    void AddText(TableLayoutPanel t, string label, TextBox box, int c, int r)
    {
        var p = new Panel { Dock = DockStyle.Fill };
        p.Controls.Add(box); box.Dock = DockStyle.Fill;
        p.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 120, TextAlign = ContentAlignment.MiddleRight });
        t.Controls.Add(p, c, r);
    }

    void AddDate(TableLayoutPanel t, string label, DateTimePicker box, int c, int r)
    {
        var p = new Panel { Dock = DockStyle.Fill };
        p.Controls.Add(box); box.Dock = DockStyle.Fill;
        p.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 75, TextAlign = ContentAlignment.MiddleRight });
        t.Controls.Add(p, c, r);
    }

    void AddCombo(TableLayoutPanel t, string label, ComboBox box, int c, int r)
    {
        var p = new Panel { Dock = DockStyle.Fill };
        p.Controls.Add(box); box.Dock = DockStyle.Fill;
        p.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 75, TextAlign = ContentAlignment.MiddleRight });
        t.Controls.Add(p, c, r);
    }

    void Search()
    {
        var start = from.Value.Date.ToString("yyyy-MM-dd 00:00:00");
        var end = to.Value.Date.AddDays(1).ToString("yyyy-MM-dd 00:00:00");
        var q = number.Text.Trim();
        var selected = type.SelectedItem?.ToString() ?? "همه";
        var parts = new List<string>();
        if (selected is "همه" or "فروش" or "خرید")
        {
            var invoiceType = selected switch { "فروش" => "Sale", "خرید" => "Purchase", _ => null };
            var sql = @"SELECT i.Id AS [شناسه], i.InvoiceNo AS [شماره سند],
CASE i.Type WHEN 'Sale' THEN 'فروش' WHEN 'Purchase' THEN 'خرید' ELSE i.Type END AS [نوع],
i.DateText AS [تاریخ], COALESCE(c.Name,s.Name,'') AS [طرف حساب], i.Total AS [مبلغ], i.Paid AS [پرداختی],
(i.Total-i.Paid) AS [مانده], i.Notes AS [توضیحات]
FROM Invoices i LEFT JOIN Customers c ON c.Id=i.CustomerId LEFT JOIN Suppliers s ON s.Id=i.SupplierId
WHERE i.DateText>=@from AND i.DateText<@to";
            if (invoiceType != null) sql += " AND i.Type=@type";
            if (!string.IsNullOrWhiteSpace(q)) sql += " AND i.InvoiceNo LIKE @q";
            sql += " ORDER BY i.DateText DESC";
            var pars = new List<SqliteParameter> { new("@from", start), new("@to", end) };
            if (invoiceType != null) pars.Add(new("@type", invoiceType));
            if (!string.IsNullOrWhiteSpace(q)) pars.Add(new("@q", "%" + q + "%"));
            var dt = Database.Query(sql, pars.ToArray());
            if (selected != "همه" || dt.Rows.Count > 0) { grid.DataSource = dt; return; }
        }

        if (selected is "چک")
        {
            var sql = @"SELECT Id AS [شناسه], CheckNo AS [شماره سند], CASE Type WHEN 'دریافتی' THEN 'چک دریافتی' ELSE 'چک پرداختی' END AS [نوع],
DueDate AS [تاریخ], PartyName AS [طرف حساب], Amount AS [مبلغ], Status AS [وضعیت], Bank AS [بانک], Notes AS [توضیحات]
FROM Checks WHERE COALESCE(DueDate,'') BETWEEN @fromDate AND @toDate";
            if (!string.IsNullOrWhiteSpace(q)) sql += " AND CheckNo LIKE @q";
            sql += " ORDER BY DueDate DESC";
            var pars = new List<SqliteParameter> { new("@fromDate", from[..10]), new("@toDate", to[..10]) };
            if (!string.IsNullOrWhiteSpace(q)) pars.Add(new("@q", "%" + q + "%"));
            grid.DataSource = Database.Query(sql, pars.ToArray()); return;
        }

        var returnSql = selected == "برگشت فروش"
            ? @"SELECT r.Id AS [شناسه], r.ReturnNo AS [شماره سند], 'برگشت فروش' AS [نوع], r.DateText AS [تاریخ], c.Name AS [طرف حساب], r.Total AS [مبلغ], r.Notes AS [توضیحات]
FROM SalesReturns r LEFT JOIN Customers c ON c.Id=r.CustomerId WHERE r.DateText>=@from AND r.DateText<@to"
            : @"SELECT r.Id AS [شناسه], r.ReturnNo AS [شماره سند], 'برگشت خرید' AS [نوع], r.DateText AS [تاریخ], s.Name AS [طرف حساب], r.Total AS [مبلغ], r.Notes AS [توضیحات]
FROM PurchaseReturns r LEFT JOIN Suppliers s ON s.Id=r.SupplierId WHERE r.DateText>=@from AND r.DateText<@to";
        if (selected is "برگشت فروش" or "برگشت خرید")
        {
            if (!string.IsNullOrWhiteSpace(q)) returnSql += " AND r.ReturnNo LIKE @q";
            returnSql += " ORDER BY r.DateText DESC";
            var pars = new List<SqliteParameter> { new("@from", start), new("@to", end) };
            if (!string.IsNullOrWhiteSpace(q)) pars.Add(new("@q", "%" + q + "%"));
            grid.DataSource = Database.Query(returnSql, pars.ToArray()); return;
        }

        if (selected == "دریافت/پرداخت" || selected == "همه")
        {
            var sql = @"SELECT Id AS [شناسه], ReferenceType AS [شماره/مرجع], Type AS [نوع], DateText AS [تاریخ],
Amount AS [مبلغ], PaymentMethod AS [روش پرداخت], Description AS [توضیحات]
FROM CashTransactions WHERE DateText>=@from AND DateText<@to";
            if (!string.IsNullOrWhiteSpace(q)) sql += " AND (CAST(Id AS TEXT) LIKE @q OR COALESCE(ReferenceId,'') LIKE @q)";
            sql += " ORDER BY DateText DESC";
            var pars = new List<SqliteParameter> { new("@from", start), new("@to", end) };
            if (!string.IsNullOrWhiteSpace(q)) pars.Add(new("@q", "%" + q + "%"));
            grid.DataSource = Database.Query(sql, pars.ToArray());
            return;
        }
        grid.DataSource = null;
    }
}
