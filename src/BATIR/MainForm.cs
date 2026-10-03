using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class MainForm : Form
{
    readonly DataGridView grid = new();
    readonly TextBox name = new();
    readonly TextBox barcode = new();
    readonly TextBox brand = new();
    readonly TextBox category = new();
    readonly NumericUpDown purchase = new();
    readonly NumericUpDown sale = new();
    readonly NumericUpDown stock = new();
    readonly TextBox search = new();
    readonly Label status = new();

    public MainForm()
    {
        Text = "BATIR | باتیر - حسابداری و انبارداری";
        Width = 1200; Height = 720;
        StartPosition = FormStartPosition.CenterScreen;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Build();
        LoadProducts();
    }

    void Build()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildProducts());
        tabs.TabPages.Add(BuildInvoices());
        tabs.TabPages.Add(BuildCustomers());
        tabs.TabPages.Add(SimplePage("چک‌ها", "چک دریافتی، پرداختی، وصول و برگشتی"));
        tabs.TabPages.Add(SimplePage("تنظیمات", "ریال | فارسی | پایگاه داده SQLite"));

        var top = new Panel { Dock = DockStyle.Top, Height = 55 };
        var title = new Label { Text = "  باتیر | حسابداری و انبارداری", Dock = DockStyle.Left, Width = 350,
            Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        var backup = new Button { Text = "پشتیبان‌گیری", Dock = DockStyle.Right, Width = 120 };
        backup.Click += (_, _) => Backup();
        top.Controls.Add(backup); top.Controls.Add(title);

        status.Dock = DockStyle.Bottom; status.Height = 28; status.Text = "آماده | ریال";
        Controls.Add(tabs); Controls.Add(status); Controls.Add(top);
    }

    TabPage BuildProducts()
    {
        var page = new TabPage("کالا و انبار");
        var form = new TableLayoutPanel { Dock = DockStyle.Top, Height = 125, ColumnCount = 4, RowCount = 2, Padding = new Padding(8) };
        for (int i = 0; i < 4; i++) form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        AddText(form, "نام کالا", name, 0, 0); AddText(form, "بارکد", barcode, 1, 0);
        AddText(form, "برند", brand, 2, 0); AddText(form, "دسته‌بندی", category, 3, 0);
        AddNum(form, "قیمت خرید", purchase, 0, 1); AddNum(form, "قیمت فروش", sale, 1, 1); AddNum(form, "موجودی", stock, 2, 1);
        var add = new Button { Text = "ثبت کالا", Dock = DockStyle.Fill }; add.Click += AddProduct; form.Controls.Add(add, 3, 1);

        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8) };
        search.Dock = DockStyle.Fill; search.TextChanged += (_, _) => LoadProducts(search.Text.Trim());
        searchPanel.Controls.Add(search);

        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        page.Controls.Add(grid); page.Controls.Add(searchPanel); page.Controls.Add(form);
        return page;
    }

    TabPage BuildCustomers()
    {
        var p = new TabPage("مشتریان");
        p.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "مدیریت مشتری، سقف اعتبار و مانده بدهکار/بستانکار\n\nساختار پایگاه داده این بخش آماده است و فرم عملیاتی آن در مرحله بعد تکمیل می‌شود.", Padding = new Padding(30), TextAlign = ContentAlignment.TopRight });
        return p;
    }

    TabPage BuildInvoices()
    {
        var p = new TabPage("فاکتورها");
        var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
        var title = new Label { Text = "فاکتور فروش / خرید", Dock = DockStyle.Top, Height = 40, Font = new Font("Tahoma", 13, FontStyle.Bold) };
        var info = new Label { Text = "این نسخه ساختار اولیه فاکتور را دارد. ثبت نهایی باید در تراکنش واحد انجام شود تا اطلاعات ناقص در اثر قطع برق باقی نماند.", Dock = DockStyle.Top, Height = 70 };
        panel.Controls.Add(info); panel.Controls.Add(title); p.Controls.Add(panel);
        return p;
    }

    TabPage SimplePage(string title, string text)
    {
        var p = new TabPage(title);
        p.Controls.Add(new Label { Dock = DockStyle.Fill, Text = text + "\n\nساختار پایگاه داده این بخش آماده است و فرم عملیاتی آن در مرحله بعد تکمیل می‌شود.", Padding = new Padding(30), TextAlign = ContentAlignment.TopRight });
        return p;
    }

    void AddText(TableLayoutPanel t, string label, TextBox box, int c, int r)
    {
        var p = new Panel { Dock = DockStyle.Fill }; p.Controls.Add(box); box.Dock = DockStyle.Fill;
        var l = new Label { Text = label, Dock = DockStyle.Right, Width = 85, TextAlign = ContentAlignment.MiddleRight };
        p.Controls.Add(l); t.Controls.Add(p, c, r);
    }

    void AddNum(TableLayoutPanel t, string label, NumericUpDown box, int c, int r)
    {
        box.Maximum = 999999999999; box.ThousandsSeparator = true; box.Dock = DockStyle.Fill;
        var p = new Panel { Dock = DockStyle.Fill }; p.Controls.Add(box);
        p.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 85, TextAlign = ContentAlignment.MiddleRight });
        t.Controls.Add(p, c, r);
    }

    void LoadProducts(string q = "")
    {
        var sql = "SELECT Id,Code AS [کد],Barcode AS [بارکد],Name AS [نام کالا],Brand AS [برند],Category AS [دسته],PurchasePrice AS [خرید],SalePrice AS [فروش],Stock AS [موجودی] FROM Products WHERE Active=1";
        if (!string.IsNullOrWhiteSpace(q)) sql += " AND (Name LIKE @q OR Barcode LIKE @q OR Brand LIKE @q OR Category LIKE @q)";
        sql += " ORDER BY Id DESC";
        var dt = string.IsNullOrWhiteSpace(q) ? Database.Query(sql) : Database.Query(sql, new SqliteParameter("@q", "%" + q + "%"));
        grid.DataSource = dt; status.Text = $"تعداد کالا: {dt.Rows.Count:N0} | ریال";
    }

    void AddProduct(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(name.Text)) { MessageBox.Show("نام کالا را وارد کنید."); return; }
        if (sale.Value > 0 && sale.Value < purchase.Value && MessageBox.Show("قیمت فروش کمتر از قیمت خرید است. ثبت شود؟", "هشدار", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        Database.Execute(@"INSERT INTO Products(Code,Barcode,Name,Brand,Category,PurchasePrice,SalePrice,Stock,CreatedAt)
VALUES(@code,@barcode,@name,@brand,@category,@purchase,@sale,@stock,@date)",
        new SqliteParameter("@code", ""), new SqliteParameter("@barcode", barcode.Text.Trim()),
        new SqliteParameter("@name", name.Text.Trim()), new SqliteParameter("@brand", brand.Text.Trim()),
        new SqliteParameter("@category", category.Text.Trim()), new SqliteParameter("@purchase", (long)purchase.Value),
        new SqliteParameter("@sale", (long)sale.Value), new SqliteParameter("@stock", (long)stock.Value),
        new SqliteParameter("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        name.Clear(); barcode.Clear(); brand.Clear(); category.Clear(); purchase.Value = 0; sale.Value = 0; stock.Value = 0;
        LoadProducts();
    }

    void Backup()
    {
        try
        {
            using var s = new SaveFileDialog { Filter = "BATIR Database (*.db)|*.db", FileName = "BATIR-Backup.db" };
            if (s.ShowDialog() != DialogResult.OK) return;
            File.Copy(Database.FilePath, s.FileName, true);
            MessageBox.Show("پشتیبان‌گیری با موفقیت انجام شد.");
        }
        catch (Exception ex) { MessageBox.Show("خطا در پشتیبان‌گیری: " + ex.Message); }
    }
}