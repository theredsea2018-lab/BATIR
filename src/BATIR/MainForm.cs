using Microsoft.Data.Sqlite;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace BATIR;

public class MainForm : Form
{
    readonly DataGridView grid = new();
    readonly DataGridView invoiceGrid = new();
    readonly TextBox name = new();
    readonly TextBox barcode = new();
    readonly TextBox brand = new();
    readonly TextBox category = new();
    readonly NumericUpDown purchase = new();
    readonly NumericUpDown sale = new();
    readonly NumericUpDown stock = new();
    readonly TextBox unitName = new();
    readonly TextBox secondaryUnitName = new();
    readonly NumericUpDown unitFactor = new() { Minimum = 1, Maximum = 1000000, Value = 1 };
    readonly TextBox search = new();
    readonly TextBox invoiceSearch = new();
    readonly NumericUpDown invoiceQty = new() { Minimum = 1, Maximum = 999999 };
    readonly NumericUpDown invoiceDiscount = new() { Minimum = 0, Maximum = 999999999999 };
    readonly TextBox customerName = new();
    readonly TextBox invoiceNote = new();
    readonly Label invoiceTotal = new();
    readonly NumericUpDown invoicePaid = new() { Minimum = 0, Maximum = 999999999999 };
    readonly ComboBox paymentMethod = new();
    readonly Label status = new();
    readonly System.Windows.Forms.Timer autoLockTimer = new() { Interval = 1000 };
    DateTime lastActivity = DateTime.Now;
    bool lockDialogOpen = false;
    readonly DataTable invoiceItems = new();
    readonly DataGridView customerGrid = new();
    readonly TextBox customerSearch = new();
    readonly TextBox customerPhone = new();
    readonly TextBox customerAddress = new();
    readonly TextBox customerNotes = new();
    readonly NumericUpDown customerCreditLimit = new() { Minimum = 0, Maximum = 999999999999 };
    readonly ComboBox customerPriceTier = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly NumericUpDown customerPayment = new() { Minimum = 0, Maximum = 999999999999 };
    long editingCustomerId = 0;

    public MainForm()
    {
        Text = "BATIR | باتیر - حسابداری و انبارداری";
        Width = 1250; Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Build();
        LoadProducts();
        LoadDraftInvoice();
        FormClosing += (_, _) => { if (invoiceItems.Rows.Count > 0) SaveDraftInvoice(); };
        HookActivityTracking();
        autoLockTimer.Tick += (_, _) => CheckAutoLock();
        autoLockTimer.Start();
    }

    void HookActivityTracking()
    {
        void attach(Control control)
        {
            control.MouseMove += (_, _) => lastActivity = DateTime.Now;
            control.KeyDown += (_, _) => lastActivity = DateTime.Now;
            control.MouseClick += (_, _) => lastActivity = DateTime.Now;
            foreach (Control child in control.Controls) attach(child);
        }
        attach(this);
    }

    void CheckAutoLock()
    {
        if (lockDialogOpen || !Visible) return;
        var setting = Database.Query("SELECT Value FROM Settings WHERE Key='AutoLockMinutes' LIMIT 1");
        if (setting.Rows.Count == 0 || !int.TryParse(Convert.ToString(setting.Rows[0]["Value"]), out var minutes) || minutes <= 0) return;
        if ((DateTime.Now - lastActivity).TotalMinutes < minutes) return;
        var hashTable = Database.Query("SELECT Value FROM Settings WHERE Key='AutoLockPasswordHash' LIMIT 1");
        var expectedHash = hashTable.Rows.Count == 0 ? "" : Convert.ToString(hashTable.Rows[0]["Value"]) ?? "";
        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            lastActivity = DateTime.Now;
            return;
        }

        lockDialogOpen = true;
        try
        {
            using var lockForm = new Form { Text = "BATIR | قفل خودکار", Width = 430, Height = 205, StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ControlBox = false,
                RightToLeft = RightToLeft.Yes, RightToLeftLayout = true };
            var label = new Label { Text = "برنامه قفل شده است. رمز را وارد کنید:", Dock = DockStyle.Top, Height = 48, TextAlign = ContentAlignment.MiddleCenter };
            var password = new TextBox { UseSystemPasswordChar = true, Dock = DockStyle.Top, Height = 32 };
            var button = new Button { Text = "باز کردن قفل", Dock = DockStyle.Bottom, Height = 48 };
            var error = new Label { Text = "", Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleCenter };
            button.Click += (_, _) =>
            {
                if (PasswordMatches(password.Text, expectedHash))
                    lockForm.DialogResult = DialogResult.OK;
                else
                {
                    error.Text = "رمز واردشده صحیح نیست.";
                    password.Clear();
                    password.Focus();
                }
            };
            password.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter) { button.PerformClick(); e.SuppressKeyPress = true; }
            };
            lockForm.Controls.Add(password);
            lockForm.Controls.Add(label);
            lockForm.Controls.Add(error);
            lockForm.Controls.Add(button);
            lockForm.Shown += (_, _) => password.Focus();
            lockForm.ShowDialog(this);
        }
        finally
        {
            lastActivity = DateTime.Now;
            lockDialogOpen = false;
        }
    }

    static bool PasswordMatches(string password, string expectedHash)
    {
        using var sha = SHA256.Create();
        var actual = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(password))).Replace("-", "").ToLowerInvariant();
        return string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    void Build()
    {
        BackColor = Color.FromArgb(232, 238, 245);
        MinimumSize = new Size(1100, 680);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Appearance = TabAppearance.Buttons,
            SizeMode = TabSizeMode.Fixed,
            ItemSize = new Size(1, 1),
            Padding = new Point(0, 0)
        };

        TabPage AddPage(TabPage page)
        {
            tabs.TabPages.Add(page);
            return page;
        }

        TabPage SimplePage(string title, string actionText, Action action)
        {
            var page = new TabPage(title);
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(242, 242, 242), Padding = new Padding(10) };
            var button = new Button
            {
                Text = actionText,
                Dock = DockStyle.Top,
                Height = 44,
                Font = new Font("Tahoma", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(223, 233, 246),
                FlatStyle = FlatStyle.Flat
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(145, 166, 194);
            button.Click += (_, _) => action();
            host.Controls.Add(button);
            page.Controls.Add(host);
            return page;
        }

        var home = new TabPage("خانه");
        var homeRoot = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(232, 232, 232), Padding = new Padding(8) };
        var homeTitle = new Label
        {
            Text = "باتیر | سیستم حسابداری و انبارداری",
            Dock = DockStyle.Top,
            Height = 38,
            Font = new Font("Tahoma", 13, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 62, 78),
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(6, 0, 6, 0)
        };
        var homeInfo = new Label
        {
            Text = "عملیات سریع  |  فروش، خرید، اشخاص، انبار، چک و گزارش‌ها",
            Dock = DockStyle.Top,
            Height = 28,
            Font = new Font("Tahoma", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(78, 91, 105),
            TextAlign = ContentAlignment.MiddleRight,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(246, 246, 246),
            Padding = new Padding(6, 0, 6, 0)
        };
        var quick = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 4, Padding = new Padding(4), CellBorderStyle = TableLayoutPanelCellBorderStyle.Single };
        for (int i = 0; i < 5; i++) quick.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        for (int i = 0; i < 4; i++) quick.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        void QuickButton(string text, int col, int row, Action action)
        {
            var b = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
                Font = new Font("Tahoma", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(235, 239, 244),
                FlatStyle = FlatStyle.Flat
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(145, 154, 163);
            b.Click += (_, _) => action();
            quick.Controls.Add(b, col, row);
        }

        homeRoot.Controls.Add(quick);
        homeRoot.Controls.Add(homeInfo);
        homeRoot.Controls.Add(homeTitle);
        home.Controls.Add(homeRoot);
        AddPage(home);

        var products = AddPage(BuildProducts());
        var invoices = AddPage(BuildInvoices());
        var customers = AddPage(BuildCustomers());
        var checks = AddPage(BuildChecksPage());

        var purchasePage = AddPage(SimplePage("فاکتور خرید", "باز کردن فاکتور خرید", () => new PurchaseForm().ShowDialog(this)));
        var purchaseReturnsPage = AddPage(SimplePage("برگشت از خرید", "باز کردن برگشت از خرید", () => new PurchaseReturnsForm().ShowDialog(this)));
        var returnsPage = AddPage(SimplePage("برگشت از فروش", "باز کردن برگشت از فروش", () => new SalesReturnsForm().ShowDialog(this)));
        var suppliersPage = AddPage(SimplePage("تأمین‌کنندگان", "مدیریت تأمین‌کنندگان", () => new SuppliersForm().ShowDialog(this)));
        var statementPage = AddPage(SimplePage("دفتر حساب مشتریان", "باز کردن دفتر حساب مشتریان", () => new CustomerStatementForm().ShowDialog(this)));
        var supplierStatementPage = AddPage(SimplePage("دفتر حساب تأمین‌کنندگان", "باز کردن دفتر حساب تأمین‌کنندگان", () => new SupplierStatementForm().ShowDialog(this)));
        var stocktakingPage = AddPage(SimplePage("انبارگردانی", "باز کردن انبارگردانی", () => new StocktakingForm().ShowDialog(this)));
        var reportPage = AddPage(SimplePage("گزارش موجودی و سود", "گزارش موجودی و سود", () => new InventoryReportForm().ShowDialog(this)));
        var documentSearchPage = AddPage(SimplePage("جستجوی اسناد", "جستجوی اسناد", () => new DocumentSearchForm().ShowDialog(this)));
        var debtorsPage = AddPage(SimplePage("بدهکاران و بستانکاران", "گزارش بدهکاران و بستانکاران", () => new DebtorsCreditorsForm().ShowDialog(this)));
        var stage2Page = AddPage(SimplePage("کنترل و گزارش", "سود، بهای میانگین، هشدار، قیمت‌گذاری و بستن شیفت", () => new Stage2ReportsForm().ShowDialog(this)));
        var settingsPage = AddPage(SimplePage("تنظیمات", "باز کردن تنظیمات BATIR", () => new SettingsForm().ShowDialog(this)));
        var financePage = AddPage(SimplePage("مالی روزانه", "دریافت، پرداخت، تأمین‌کننده و هزینه‌ها", () => new FinancialOperationsForm().ShowDialog(this)));
        var inventoryOpsPage = AddPage(SimplePage("اصلاح موجودی", "اصلاح و ثبت گردش موجودی", () => new InventoryTransferForm().ShowDialog(this)));
        var advancedPage = AddPage(SimplePage("امکانات پیشرفته", "بانک، بودجه، نقدینگی، شبکه، OCR و کاربران", () => new AdvancedFeaturesForm().ShowDialog(this)));
        TabPage? remindersPage = null;
        if (FeatureSettings.IsEnabled("DueReminders", true))
            remindersPage = AddPage(SimplePage("یادآوری سررسیدها", "مدیریت بدهی، چک و سررسیدها", () => new RemindersForm().ShowDialog(this)));

        void SelectPage(TabPage page) => tabs.SelectedTab = page;

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 58,
            BackColor = Color.FromArgb(214, 220, 226),
            Padding = new Padding(10, 8, 10, 8)
        };

        var title = new Label
        {
            Text = "باتیر",
            Dock = DockStyle.Left,
            Width = 145,
            Font = new Font("Tahoma", 15, FontStyle.Bold),
            ForeColor = Color.FromArgb(28, 61, 102),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var subtitle = new Label
        {
            Text = "حسابداری | انبارداری | فروش",
            Dock = DockStyle.Left,
            Width = 215,
            Font = new Font("Tahoma", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(63, 83, 106),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 545,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(2)
        };

        Button Tool(string text, Action action)
        {
            var b = new Button
            {
                Text = text,
                Width = 100,
                Height = 38,
                Margin = new Padding(3),
                Font = new Font("Tahoma", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(238, 244, 251),
                FlatStyle = FlatStyle.Flat
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(128, 151, 181);
            b.Click += (_, _) => action();
            return b;
        }

        actions.Controls.Add(Tool("پشتیبان‌گیری", Backup));
        actions.Controls.Add(Tool("بازیابی", RestoreBackup));
        actions.Controls.Add(Tool("فاکتور فروش", () => SelectPage(invoices)));
        actions.Controls.Add(Tool("فاکتور خرید", () => SelectPage(purchasePage)));
        actions.Controls.Add(Tool("جستجوی سند", () => SelectPage(documentSearchPage)));

        header.Controls.Add(actions);
        header.Controls.Add(subtitle);
        header.Controls.Add(title);

        var nav = new Panel
        {
            Dock = DockStyle.Right,
            Width = 205,
            BackColor = Color.FromArgb(224, 227, 230),
            Padding = new Padding(7)
        };

        var navTitle = new Label
        {
            Text = "منوی اصلی",
            Dock = DockStyle.Top,
            Height = 44,
            Font = new Font("Tahoma", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(33, 67, 108),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var menu = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(2)
        };

        void Section(string text)
        {
            menu.Controls.Add(new Label
            {
                Text = text,
                Width = 181,
                Height = 24,
                Margin = new Padding(2, 7, 2, 2),
                Font = new Font("Tahoma", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(78, 103, 132),
                TextAlign = ContentAlignment.MiddleRight
            });
        }

        void NavButton(string text, Action action, bool primary = false)
        {
            var b = new Button
            {
                Text = text,
                Width = 181,
                Height = primary ? 38 : 34,
                Margin = new Padding(2),
                Font = new Font("Tahoma", 9, primary ? FontStyle.Bold : FontStyle.Regular),
                BackColor = primary ? Color.FromArgb(189, 210, 235) : Color.FromArgb(231, 238, 247),
                FlatStyle = FlatStyle.Flat
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(143, 164, 190);
            b.Click += (_, _) => action();
            menu.Controls.Add(b);
        }

        Section("اسناد");
        NavButton("ثبت فروش", () => SelectPage(invoices), true);
        NavButton("خرید", () => SelectPage(purchasePage));
        NavButton("برگشت از فروش", () => SelectPage(returnsPage));
        NavButton("برگشت از خرید", () => SelectPage(purchaseReturnsPage));
        NavButton("دریافت / پرداخت", () => SelectPage(financePage));

        Section("حساب‌ها");
        NavButton("مشتریان و اشخاص", () => SelectPage(customers));
        NavButton("تأمین‌کنندگان", () => SelectPage(suppliersPage));
        NavButton("دفتر حساب مشتریان", () => SelectPage(statementPage));
        NavButton("دفتر حساب تأمین‌کنندگان", () => SelectPage(supplierStatementPage));
        NavButton("چک‌ها", () => SelectPage(checks));

        Section("کالا و انبار");
        NavButton("کالاها و موجودی", () => SelectPage(products));
        NavButton("انبارگردانی", () => SelectPage(stocktakingPage));
        NavButton("اصلاح / انتقال موجودی", () => SelectPage(inventoryOpsPage));

        Section("گزارش‌ها");
        NavButton("گزارش موجودی و سود", () => SelectPage(reportPage));
        NavButton("بدهکاران / بستانکاران", () => SelectPage(debtorsPage));
        NavButton("کنترل و گزارش‌های تکمیلی", () => SelectPage(stage2Page));
        NavButton("جستجوی اسناد", () => SelectPage(documentSearchPage));

        Section("سیستم");
        NavButton("تنظیمات", () => SelectPage(settingsPage));
        NavButton("امکانات پیشرفته", () => SelectPage(advancedPage));
        if (remindersPage != null)
            NavButton("یادآوری سررسیدها", () => SelectPage(remindersPage));

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            BackColor = Color.FromArgb(198, 211, 228)
        };

        status.Dock = DockStyle.Fill;
        status.Height = 30;
        status.Text = "آماده | " + CurrencyName();
        status.TextAlign = ContentAlignment.MiddleRight;
        status.Font = new Font("Tahoma", 8.5f, FontStyle.Regular);
        footer.Controls.Add(status);

        nav.Controls.Add(menu);
        nav.Controls.Add(navTitle);

        QuickButton("ثبت فروش", 0, 0, () => SelectPage(invoices));
        QuickButton("ثبت خرید", 1, 0, () => SelectPage(purchasePage));
        QuickButton("برگشت فروش", 2, 0, () => SelectPage(returnsPage));
        QuickButton("برگشت خرید", 3, 0, () => SelectPage(purchaseReturnsPage));
        QuickButton("دریافت / پرداخت", 4, 0, () => SelectPage(financePage));
        QuickButton("مشتریان", 0, 1, () => SelectPage(customers));
        QuickButton("تأمین‌کنندگان", 1, 1, () => SelectPage(suppliersPage));
        QuickButton("چک‌ها", 2, 1, () => SelectPage(checks));
        QuickButton("کالاها", 3, 1, () => SelectPage(products));
        QuickButton("موجودی / انتقال", 4, 1, () => SelectPage(inventoryOpsPage));
        QuickButton("انبارگردانی", 0, 2, () => SelectPage(stocktakingPage));
        QuickButton("جستجوی اسناد", 1, 2, () => SelectPage(documentSearchPage));
        QuickButton("گزارش موجودی و سود", 2, 2, () => SelectPage(reportPage));
        QuickButton("بدهکاران / بستانکاران", 3, 2, () => SelectPage(debtorsPage));
        QuickButton("کنترل و گزارش", 4, 2, () => SelectPage(stage2Page));
        QuickButton("تنظیمات", 0, 3, () => SelectPage(settingsPage));
        QuickButton("امکانات پیشرفته", 1, 3, () => SelectPage(advancedPage));
        QuickButton("پشتیبان‌گیری", 2, 3, Backup);
        QuickButton("بازیابی", 3, 3, RestoreBackup);
        if (remindersPage != null)
            QuickButton("یادآوری سررسید", 4, 3, () => SelectPage(remindersPage));

        homeRoot.BackColor = Color.FromArgb(229, 234, 241);
        tabs.SelectedTab = home;

        Controls.Add(tabs);
        Controls.Add(nav);
        Controls.Add(footer);
        Controls.Add(header);
    }

    TabPage BuildProducts()
    {
        var page = new TabPage("کالا و انبار");
        var form = new TableLayoutPanel { Dock = DockStyle.Top, Height = 170, ColumnCount = 4, RowCount = 3, Padding = new Padding(8) };
        for (int i = 0; i < 4; i++) form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        AddText(form, "نام کالا", name, 0, 0); AddText(form, "بارکد", barcode, 1, 0);
        AddText(form, "برند", brand, 2, 0); AddText(form, "دسته‌بندی", category, 3, 0);
        AddNum(form, "قیمت خرید", purchase, 0, 1); AddNum(form, "قیمت فروش", sale, 1, 1); AddNum(form, "موجودی", stock, 2, 1);
        AddText(form, "واحد اصلی", unitName, 0, 2); AddText(form, "واحد فرعی", secondaryUnitName, 1, 2); AddNum(form, "ضریب واحد", unitFactor, 2, 2);
        var add = new Button { Text = "ثبت کالا", Dock = DockStyle.Fill }; add.Click += AddProduct; form.Controls.Add(add, 3, 1);
        var finance = new Button { Text = "دریافت / پرداخت / هزینه", Dock = DockStyle.Fill }; finance.Click += (_,_) => new FinancialOperationsForm().ShowDialog(this); form.Controls.Add(finance, 3, 2);

        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(8) };
        search.Dock = DockStyle.Fill; search.TextChanged += (_, _) => LoadProducts(search.Text.Trim());
        searchPanel.Controls.Add(search);

        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        page.Controls.Add(grid); page.Controls.Add(searchPanel); page.Controls.Add(form);
        return page;
    }

    TabPage BuildInvoices()
    {
        var p = new TabPage("فاکتور فروش");
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

        var header = new TableLayoutPanel { Dock = DockStyle.Top, Height = 155, ColumnCount = 4, RowCount = 3, Padding = new Padding(5) };
        for (int i = 0; i < 4; i++) header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        AddText(header, "جستجوی کالا / بارکد", invoiceSearch, 0, 0);
        AddText(header, "مشتری", customerName, 1, 0);
        AddNum(header, "تعداد", invoiceQty, 2, 0);
        AddNum(header, "تخفیف", invoiceDiscount, 3, 0);
        AddNum(header, "پرداختی", invoicePaid, 0, 1);
        AddCombo(header, "روش پرداخت", paymentMethod, 1, 1);
        AddText(header, "توضیحات", invoiceNote, 2, 1);

        var add = new Button { Text = "افزودن به فاکتور", Dock = DockStyle.Fill };
        add.Click += AddInvoiceItem;
        header.Controls.Add(add, 0, 2);

        var save = new Button { Text = "ثبت نهایی فاکتور (F9)", Dock = DockStyle.Fill };
        save.Click += (_, _) => SaveInvoice();
        header.Controls.Add(save, 1, 2);

        var clear = new Button { Text = "فاکتور جدید", Dock = DockStyle.Fill };
        clear.Click += (_, _) => ClearInvoice();
        header.Controls.Add(clear, 2, 2);

        paymentMethod.Items.AddRange(new object[] { "نقدی", "کارتخوان", "انتقال بانکی", "اعتباری" });
        paymentMethod.SelectedIndex = 0;
        invoicePaid.ValueChanged += (_, _) =>
        {
            UpdateInvoiceTotal();
            if (invoiceItems.Rows.Count > 0) SaveDraftInvoice();
        };
        paymentMethod.SelectedIndexChanged += (_, _) =>
        {
            if (invoiceItems.Rows.Count > 0) SaveDraftInvoice();
        };
        customerName.TextChanged += (_, _) => { if (invoiceItems.Rows.Count > 0) SaveDraftInvoice(); };
        invoiceNote.TextChanged += (_, _) => { if (invoiceItems.Rows.Count > 0) SaveDraftInvoice(); };

        invoiceSearch.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { AddInvoiceItem(null, EventArgs.Empty); e.SuppressKeyPress = true; } };
        invoiceGrid.Dock = DockStyle.Fill; invoiceGrid.ReadOnly = true; invoiceGrid.AllowUserToAddRows = false;
        invoiceGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        invoiceTotal.Dock = DockStyle.Bottom; invoiceTotal.Height = 42;
        invoiceTotal.Font = new Font("Tahoma", 12, FontStyle.Bold);
        invoiceTotal.Text = "جمع فاکتور: ۰ " + CurrencyName();
        invoiceTotal.TextAlign = ContentAlignment.MiddleLeft;

        invoiceItems.Columns.Add("ProductId", typeof(long));
        invoiceItems.Columns.Add("نام کالا", typeof(string));
        invoiceItems.Columns.Add("تعداد", typeof(long));
        invoiceItems.Columns.Add("فی", typeof(long));
        invoiceItems.Columns.Add("تخفیف", typeof(long));
        invoiceItems.Columns.Add("جمع", typeof(long));
        invoiceGrid.DataSource = invoiceItems;

        root.Controls.Add(invoiceGrid);
        root.Controls.Add(invoiceTotal);
        root.Controls.Add(header);
        p.Controls.Add(root);
        return p;
    }

    TabPage BuildCustomers()
    {
        var p = new TabPage("مشتریان");
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 40 };
        customerSearch.Dock = DockStyle.Fill;
        customerSearch.TextChanged += (_, _) => LoadCustomers(customerSearch.Text.Trim());
        searchPanel.Controls.Add(customerSearch);

        var form = new TableLayoutPanel { Dock = DockStyle.Top, Height = 165, ColumnCount = 4, RowCount = 3, Padding = new Padding(4) };
        for (int i = 0; i < 4; i++) form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        AddText(form, "نام مشتری", customerName, 0, 0);
        AddText(form, "تلفن", customerPhone, 1, 0);
        AddText(form, "آدرس", customerAddress, 2, 0);
        AddNum(form, "سقف اعتبار", customerCreditLimit, 3, 0);
        customerPriceTier.Items.AddRange(new object[] { "عادی", "عمده‌فروشی", "همکار", "نماینده", "ویژه" }); customerPriceTier.SelectedIndex = 0;
        AddCombo(form, "نوع قیمت", customerPriceTier, 0, 1);
        AddText(form, "توضیحات", customerNotes, 1, 1);

        var save = new Button { Text = "ثبت / ویرایش مشتری", Dock = DockStyle.Fill };
        save.Click += (_, _) => SaveCustomer();
        form.Controls.Add(save, 1, 2);

        var payment = new Button { Text = "ثبت دریافت از مشتری", Dock = DockStyle.Fill };
        payment.Click += (_, _) => RecordCustomerPayment();
        form.Controls.Add(payment, 2, 2);

        var cancel = new Button { Text = "پاک کردن فرم", Dock = DockStyle.Fill };
        cancel.Click += (_, _) => ClearCustomerForm();
        form.Controls.Add(cancel, 3, 2);

        var paymentPanel = new Panel { Dock = DockStyle.Top, Height = 42, Padding = new Padding(4) };
        AddNumToPanel(paymentPanel, "مبلغ دریافت", customerPayment);

        customerGrid.Dock = DockStyle.Fill;
        customerGrid.ReadOnly = true;
        customerGrid.AllowUserToAddRows = false;
        customerGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        customerGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        customerGrid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) LoadCustomerForEdit(e.RowIndex); };

        root.Controls.Add(customerGrid);
        root.Controls.Add(paymentPanel);
        root.Controls.Add(form);
        root.Controls.Add(searchPanel);
        p.Controls.Add(root);
        p.Enter += (_, _) => LoadCustomers(customerSearch.Text.Trim());
        return p;
    }

    void AddNumToPanel(Panel panel, string label, NumericUpDown box)
    {
        var l = new Label { Text = label, Dock = DockStyle.Right, Width = 100, TextAlign = ContentAlignment.MiddleRight };
        box.Dock = DockStyle.Fill;
        panel.Controls.Add(box);
        panel.Controls.Add(l);
    }

    void LoadCustomers(string q = "")
    {
        var sql = @"SELECT Id, Name AS [نام مشتری], Phone AS [تلفن], Address AS [آدرس],
CreditLimit AS [سقف اعتبار], Balance AS [مانده بدهکار] FROM Customers";
        if (!string.IsNullOrWhiteSpace(q))
            sql += " WHERE Name LIKE @q OR Phone LIKE @q";
        sql += " ORDER BY Id DESC";
        var dt = string.IsNullOrWhiteSpace(q)
            ? Database.Query(sql)
            : Database.Query(sql, new SqliteParameter("@q", "%" + q + "%"));
        customerGrid.DataSource = dt;
    }

    void LoadCustomerForEdit(int rowIndex)
    {
        if (customerGrid.Rows[rowIndex].Cells["Id"].Value == null) return;
        editingCustomerId = Convert.ToInt64(customerGrid.Rows[rowIndex].Cells["Id"].Value);
        var dt = Database.Query("SELECT Name,Phone,Address,CreditLimit,Balance,Notes,PriceTier FROM Customers WHERE Id=@id",
            new SqliteParameter("@id", editingCustomerId));
        if (dt.Rows.Count == 0) return;
        customerName.Text = Convert.ToString(dt.Rows[0]["Name"]) ?? "";
        customerPhone.Text = Convert.ToString(dt.Rows[0]["Phone"]) ?? "";
        customerAddress.Text = Convert.ToString(dt.Rows[0]["Address"]) ?? "";
        customerCreditLimit.Value = Math.Max(0, Convert.ToDecimal(dt.Rows[0]["CreditLimit"]));
        customerNotes.Text = Convert.ToString(dt.Rows[0]["Notes"]) ?? "";
        var tier = Convert.ToString(dt.Rows[0]["PriceTier"]) ?? "Normal";
        var tierDisplay = tier switch
        {
            "Wholesale" => "عمده‌فروشی",
            "Partner" => "همکار",
            "Representative" => "نماینده",
            "Special" => "ویژه",
            _ => "عادی"
        };
        customerPriceTier.SelectedItem = tierDisplay; if (customerPriceTier.SelectedIndex < 0) customerPriceTier.SelectedIndex = 0;
        customerPayment.Value = 0;
    }

    string GetCustomerPriceTierValue()
    {
        return customerPriceTier.SelectedItem?.ToString() switch
        {
            "عمده‌فروشی" => "Wholesale",
            "همکار" => "Partner",
            "نماینده" => "Representative",
            "ویژه" => "Special",
            _ => "Normal"
        };
    }

    void SaveCustomer()
    {
        if (!PermissionService.Require("Customers.Edit", this)) return;
        if (string.IsNullOrWhiteSpace(customerName.Text))
        {
            MessageBox.Show("نام مشتری را وارد کنید.");
            return;
        }
        if (editingCustomerId == 0)
        {
            Database.Execute(@"INSERT INTO Customers(Name,Phone,Address,CreditLimit,Balance,Notes,PriceTier)
VALUES(@name,@phone,@address,@limit,0,@notes,@tier)",
                new SqliteParameter("@name", customerName.Text.Trim()),
                new SqliteParameter("@phone", customerPhone.Text.Trim()),
                new SqliteParameter("@address", customerAddress.Text.Trim()),
                new SqliteParameter("@limit", (long)customerCreditLimit.Value),
                new SqliteParameter("@notes", customerNotes.Text.Trim()),
                new SqliteParameter("@tier", GetCustomerPriceTierValue()));
            status.Text = "مشتری جدید ثبت شد";
        }
        else
        {
            Database.Execute(@"UPDATE Customers SET Name=@name,Phone=@phone,Address=@address,
CreditLimit=@limit,Notes=@notes,PriceTier=@tier WHERE Id=@id",
                new SqliteParameter("@name", customerName.Text.Trim()),
                new SqliteParameter("@phone", customerPhone.Text.Trim()),
                new SqliteParameter("@address", customerAddress.Text.Trim()),
                new SqliteParameter("@limit", (long)customerCreditLimit.Value),
                new SqliteParameter("@notes", customerNotes.Text.Trim()),
                new SqliteParameter("@tier", GetCustomerPriceTierValue()),
                new SqliteParameter("@id", editingCustomerId));
            status.Text = "اطلاعات مشتری ویرایش شد";
        }
        ClearCustomerForm();
        LoadCustomers(customerSearch.Text.Trim());
    }

    void RecordCustomerPayment()
    {
        if (!PermissionService.Require("Finance.Edit", this)) return;
        if (editingCustomerId == 0)
        {
            MessageBox.Show("ابتدا مشتری را از جدول انتخاب کنید.");
            return;
        }
        long amount = (long)customerPayment.Value;
        if (amount <= 0) { MessageBox.Show("مبلغ دریافت را وارد کنید."); return; }

        using var cn = Database.Open();
        using var tx = cn.BeginTransaction();
        using var get = cn.CreateCommand();
        get.Transaction = tx;
        get.CommandText = "SELECT Balance FROM Customers WHERE Id=@id";
        get.Parameters.AddWithValue("@id", editingCustomerId);
        var value = get.ExecuteScalar();
        long balance = value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value);
        if (amount > balance)
        {
            tx.Rollback();
            MessageBox.Show("مبلغ دریافت نمی‌تواند بیشتر از مانده بدهکار مشتری باشد.");
            return;
        }

        using var update = cn.CreateCommand();
        update.Transaction = tx;
        update.CommandText = "UPDATE Customers SET Balance=Balance-@amount WHERE Id=@id";
        update.Parameters.AddWithValue("@amount", amount);
        update.Parameters.AddWithValue("@id", editingCustomerId);
        update.ExecuteNonQuery();

        using var cash = cn.CreateCommand();
        cash.Transaction = tx;
        cash.CommandText = @"INSERT INTO CashTransactions(DateText,Type,Amount,Description,UserName,CustomerId,PaymentMethod,ReferenceType)
VALUES(@date,'CustomerReceipt',@amount,@desc,'کاربر',@customerId,'نقدی','CustomerReceipt')";
        cash.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cash.Parameters.AddWithValue("@amount", amount);
        cash.Parameters.AddWithValue("@customerId", editingCustomerId);
        cash.Parameters.AddWithValue("@desc", "دریافت از مشتری: " + customerName.Text.Trim());
        cash.ExecuteNonQuery();

        using var audit = cn.CreateCommand();
        audit.Transaction = tx;
        audit.CommandText = @"INSERT INTO AuditLog(DateText,UserName,Action,Entity,EntityId,Details)
VALUES(@date,'کاربر','دریافت از مشتری','Customer',@id,@details)";
        audit.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        audit.Parameters.AddWithValue("@id", editingCustomerId);
        audit.Parameters.AddWithValue("@details", "مبلغ: " + amount);
        audit.ExecuteNonQuery();

        tx.Commit();
        customerPayment.Value = 0;
        LoadCustomers(customerSearch.Text.Trim());
        status.Text = "دریافت مشتری ثبت شد";
    }

    void ClearCustomerForm()
    {
        editingCustomerId = 0;
        customerName.Clear();
        customerPhone.Clear();
        customerAddress.Clear();
        customerNotes.Clear();
        if (customerPriceTier.Items.Count > 0) customerPriceTier.SelectedIndex = 0;
        customerCreditLimit.Value = 0;
        customerPayment.Value = 0;
    }

    TabPage BuildChecksPage()
    {
        var p = new TabPage("چک‌ها");
        var b = new Button { Text = "باز کردن مدیریت چک‌ها", Dock = DockStyle.Top, Height = 44 };
        b.Click += (_, _) => new ChecksForm().ShowDialog(this);
        p.Controls.Add(b);
        return p;
    }

    void AddText(TableLayoutPanel t, string label, TextBox box, int c, int r)
    {
        var p = new Panel { Dock = DockStyle.Fill };
        p.Controls.Add(box); box.Dock = DockStyle.Fill;
        p.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 110, TextAlign = ContentAlignment.MiddleRight });
        t.Controls.Add(p, c, r);
    }

    void AddCombo(TableLayoutPanel t, string label, ComboBox box, int c, int r)
    {
        box.Dock = DockStyle.Fill;
        var p = new Panel { Dock = DockStyle.Fill }; p.Controls.Add(box);
        p.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 90, TextAlign = ContentAlignment.MiddleRight });
        t.Controls.Add(p, c, r);
    }

    void AddNum(TableLayoutPanel t, string label, NumericUpDown box, int c, int r)
    {
        box.Maximum = 999999999999; box.ThousandsSeparator = true; box.Dock = DockStyle.Fill;
        var p = new Panel { Dock = DockStyle.Fill }; p.Controls.Add(box);
        p.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 90, TextAlign = ContentAlignment.MiddleRight });
        t.Controls.Add(p, c, r);
    }

    void LoadProducts(string q = "")
    {
        var sql = "SELECT Id,Code AS [کد],Barcode AS [بارکد],Name AS [نام کالا],Brand AS [برند],Category AS [دسته],UnitName AS [واحد],SecondaryUnitName AS [واحد فرعی],UnitConversionFactor AS [ضریب],PurchasePrice AS [خرید],SalePrice AS [فروش],Stock AS [موجودی] FROM Products WHERE Active=1";
        if (!string.IsNullOrWhiteSpace(q)) sql += " AND (Name LIKE @q OR Barcode LIKE @q OR Brand LIKE @q OR Category LIKE @q)";
        sql += " ORDER BY Id DESC";
        var dt = string.IsNullOrWhiteSpace(q) ? Database.Query(sql) : Database.Query(sql, new SqliteParameter("@q", "%" + q + "%"));
        grid.DataSource = dt; status.Text = "تعداد کالا: " + dt.Rows.Count.ToString("N0") + " | " + CurrencyName();
    }

    void AddProduct(object? sender, EventArgs e)
    {
        if (!PermissionService.Require("Products.Edit", this)) return;
        if (string.IsNullOrWhiteSpace(name.Text)) { MessageBox.Show("نام کالا را وارد کنید."); return; }

        var newBarcode = barcode.Text.Trim();
        if (!string.IsNullOrWhiteSpace(newBarcode))
        {
            var duplicate = Database.Query("SELECT Id,Name FROM Products WHERE Active=1 AND Barcode=@barcode LIMIT 1",
                new SqliteParameter("@barcode", newBarcode));
            if (duplicate.Rows.Count > 0)
            {
                var oldName = Convert.ToString(duplicate.Rows[0]["Name"]) ?? "";
                MessageBox.Show("این بارکد قبلاً برای کالا «" + oldName + "» ثبت شده است. بارکد تکراری ثبت نشد.",
                    "بارکد تکراری", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        if (sale.Value > 0 && sale.Value < purchase.Value &&
            MessageBox.Show("قیمت فروش کمتر از قیمت خرید است. ثبت شود؟", "هشدار", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        Database.Execute(@"INSERT INTO Products(Code,Barcode,Name,Brand,Category,PurchasePrice,SalePrice,Stock,CreatedAt,UnitName,SecondaryUnitName,UnitConversionFactor)
VALUES(@code,@barcode,@name,@brand,@category,@purchase,@sale,@stock,@date,@unit,@secondary,@factor)",
            new SqliteParameter("@code", ""), new SqliteParameter("@barcode", barcode.Text.Trim()),
            new SqliteParameter("@name", name.Text.Trim()), new SqliteParameter("@brand", brand.Text.Trim()),
            new SqliteParameter("@category", category.Text.Trim()), new SqliteParameter("@purchase", (long)purchase.Value),
            new SqliteParameter("@sale", (long)sale.Value), new SqliteParameter("@stock", (long)stock.Value),
            new SqliteParameter("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
            new SqliteParameter("@unit", string.IsNullOrWhiteSpace(unitName.Text) ? "عدد" : unitName.Text.Trim()),
            new SqliteParameter("@secondary", string.IsNullOrWhiteSpace(secondaryUnitName.Text) ? (object)DBNull.Value : secondaryUnitName.Text.Trim()),
            new SqliteParameter("@factor", (long)unitFactor.Value));
        name.Clear(); barcode.Clear(); brand.Clear(); category.Clear(); unitName.Clear(); secondaryUnitName.Clear(); unitFactor.Value = 1; purchase.Value = 0; sale.Value = 0; stock.Value = 0;
        LoadProducts();
    }

    static string CurrencyName()
    {
        try
        {
            var dt = Database.Query("SELECT Value FROM Settings WHERE Key='CurrencyName' LIMIT 1");
            if (dt.Rows.Count > 0 && !string.IsNullOrWhiteSpace(Convert.ToString(dt.Rows[0]["Value"])))
                return Convert.ToString(dt.Rows[0]["Value"])!;
        }
        catch { }
        return "ریال";
    }

    static decimal GetMinimumSaleMarginPercent()
    {
        var dt = Database.Query("SELECT Value FROM Settings WHERE Key='MinimumSaleMarginPercent' LIMIT 1");
        return dt.Rows.Count > 0 && decimal.TryParse(Convert.ToString(dt.Rows[0]["Value"]), out var value) ? Math.Max(0, value) : 0;
    }

    bool NegativeStockAllowed()
    {
        var dt = Database.Query("SELECT Value FROM Settings WHERE Key='NegativeStockAllowed' LIMIT 1");
        return dt.Rows.Count > 0 && string.Equals(Convert.ToString(dt.Rows[0]["Value"]), "true", StringComparison.OrdinalIgnoreCase);
    }


    long ResolveCustomerTierPrice(long productId, long baseSalePrice)
    {
        var customer = customerName.Text.Trim();
        if (string.IsNullOrWhiteSpace(customer)) return baseSalePrice;
        var tierTable = Database.Query("SELECT PriceTier FROM Customers WHERE Name=@name LIMIT 1",
            new SqliteParameter("@name", customer));
        var tier = tierTable.Rows.Count > 0
            ? (Convert.ToString(tierTable.Rows[0]["PriceTier"]) ?? "Normal")
            : "Normal";
        var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var price = Database.Query(@"SELECT Price FROM ProductTierPrices
WHERE ProductId=@product AND PriceTier=@tier AND Active=1
AND (FromDate IS NULL OR FromDate='' OR FromDate<=@now)
AND (ToDate IS NULL OR ToDate='' OR ToDate>=@now)
ORDER BY CASE WHEN FromDate IS NULL OR FromDate='' THEN 1 ELSE 0 END,
         FromDate DESC, Id DESC LIMIT 1",
            new SqliteParameter("@product", productId),
            new SqliteParameter("@tier", tier),
            new SqliteParameter("@now", now));
        return price.Rows.Count > 0 ? Convert.ToInt64(price.Rows[0]["Price"]) : baseSalePrice;
    }

    void AddInvoiceItem(object? sender, EventArgs e)
    {
        var q = invoiceSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(q)) return;

        var dt = Database.Query(@"SELECT Id,Name,SalePrice,Stock FROM Products
WHERE Active=1 AND (Barcode=@q OR Name LIKE @like) ORDER BY CASE WHEN Barcode=@q THEN 0 ELSE 1 END, Id DESC LIMIT 1",
            new SqliteParameter("@q", q), new SqliteParameter("@like", "%" + q + "%"));

        if (dt.Rows.Count == 0) { MessageBox.Show("کالا پیدا نشد."); return; }

        long id = Convert.ToInt64(dt.Rows[0]["Id"]);
        string productName = Convert.ToString(dt.Rows[0]["Name"]) ?? "";
        long price = ResolveCustomerTierPrice(id, Convert.ToInt64(dt.Rows[0]["SalePrice"]));
        long available = Convert.ToInt64(dt.Rows[0]["Stock"]);
        long qty = (long)invoiceQty.Value;
        if (qty > available) {
            if (!NegativeStockAllowed())
            {
                MessageBox.Show("موجودی کالا کافی نیست و فروش با موجودی منفی در تنظیمات غیرفعال است.", "هشدار موجودی", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show("موجودی کالا کمتر از تعداد درخواستی است. ادامه داده شود؟", "هشدار موجودی",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        }

        foreach (DataRow row in invoiceItems.Rows)
        {
            if (Convert.ToInt64(row["ProductId"]) == id) {
                var newQty = Convert.ToInt64(row["تعداد"]) + qty;
                var newDiscount = Convert.ToInt64(row["تخفیف"]) + (long)invoiceDiscount.Value;
                var lineTotal = newQty * Convert.ToInt64(row["فی"]);
                if (newDiscount > lineTotal)
                {
                    MessageBox.Show("تخفیف تجمعی این کالا نمی‌تواند بیشتر از مبلغ ردیف باشد.");
                    return;
                }
                row["تعداد"] = newQty;
                row["تخفیف"] = newDiscount;
                row["جمع"] = lineTotal - newDiscount;
                UpdateInvoiceTotal();
                SaveDraftInvoice();
                invoiceSearch.Clear();
                invoiceQty.Value = 1;
                invoiceDiscount.Value = 0;
                return;
            }
        }

        long discount = (long)invoiceDiscount.Value;
        if (discount > qty * price)
        {
            MessageBox.Show("تخفیف نمی‌تواند بیشتر از مبلغ ردیف کالا باشد.");
            return;
        }
        var newRow = invoiceItems.NewRow();
        newRow["ProductId"] = id; newRow["نام کالا"] = productName; newRow["تعداد"] = qty;
        newRow["فی"] = price; newRow["تخفیف"] = discount; newRow["جمع"] = qty * price - discount;
        invoiceItems.Rows.Add(newRow);
        UpdateInvoiceTotal();
        SaveDraftInvoice();
        invoiceSearch.Clear(); invoiceQty.Value = 1; invoiceDiscount.Value = 0;
    }

    void LoadDraftInvoice()
    {
        try
        {
            var header = Database.Query("SELECT CustomerName, Notes FROM DraftInvoice WHERE Id=1 LIMIT 1");
            if (header.Rows.Count == 0) return;

            customerName.Text = Convert.ToString(header.Rows[0]["CustomerName"]) ?? "";
            invoiceNote.Text = Convert.ToString(header.Rows[0]["Notes"]) ?? "";

            var items = Database.Query(@"SELECT ProductId,ProductName,Quantity,UnitPrice,Discount
FROM DraftInvoiceItems ORDER BY Id");
            foreach (DataRow source in items.Rows)
            {
                var row = invoiceItems.NewRow();
                long qty = Convert.ToInt64(source["Quantity"]);
                long price = Convert.ToInt64(source["UnitPrice"]);
                long discount = Convert.ToInt64(source["Discount"]);
                row["ProductId"] = Convert.ToInt64(source["ProductId"]);
                row["نام کالا"] = Convert.ToString(source["ProductName"]) ?? "";
                row["تعداد"] = qty;
                row["فی"] = price;
                row["تخفیف"] = discount;
                row["جمع"] = qty * price - discount;
                invoiceItems.Rows.Add(row);
            }
            UpdateInvoiceTotal();
            if (invoiceItems.Rows.Count > 0)
                status.Text = "پیش‌نویس فاکتور از آخرین جلسه بازیابی شد | " + CurrencyName();
        }
        catch
        {
            status.Text = "هشدار: بازیابی پیش‌نویس انجام نشد";
        }
    }

    void SaveDraftInvoice()
    {
        using var cn = Database.Open();
        using var tx = cn.BeginTransaction();
        using (var clear = cn.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM DraftInvoiceItems; DELETE FROM DraftInvoice;";
            clear.ExecuteNonQuery();
        }

        if (invoiceItems.Rows.Count > 0)
        {
            using var header = cn.CreateCommand();
            header.Transaction = tx;
            header.CommandText = @"INSERT INTO DraftInvoice(Id,CustomerName,Notes,UpdatedAt)
VALUES(1,@customer,@notes,@date)";
            header.Parameters.AddWithValue("@customer", customerName.Text.Trim());
            header.Parameters.AddWithValue("@notes", invoiceNote.Text.Trim());
            header.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            header.ExecuteNonQuery();

            foreach (DataRow row in invoiceItems.Rows)
            {
                using var item = cn.CreateCommand();
                item.Transaction = tx;
                item.CommandText = @"INSERT INTO DraftInvoiceItems(ProductId,ProductName,Quantity,UnitPrice,Discount)
VALUES(@product,@name,@qty,@price,@discount)";
                item.Parameters.AddWithValue("@product", Convert.ToInt64(row["ProductId"]));
                item.Parameters.AddWithValue("@name", Convert.ToString(row["نام کالا"]) ?? "");
                item.Parameters.AddWithValue("@qty", Convert.ToInt64(row["تعداد"]));
                item.Parameters.AddWithValue("@price", Convert.ToInt64(row["فی"]));
                item.Parameters.AddWithValue("@discount", Convert.ToInt64(row["تخفیف"]));
                item.ExecuteNonQuery();
            }
        }
        tx.Commit();
    }

    long InvoiceTotal()
    {
        long total = 0;
        foreach (DataRow row in invoiceItems.Rows) total += Convert.ToInt64(row["جمع"]);
        return total;
    }

    void UpdateInvoiceTotal()
    {
        long paid = Math.Min((long)invoicePaid.Value, InvoiceTotal());
        long remaining = InvoiceTotal() - paid;
        var unit = CurrencyName();
        invoiceTotal.Text = "جمع: " + InvoiceTotal().ToString("N0") + " " + unit +
            " | پرداختی: " + paid.ToString("N0") + " | مانده: " + remaining.ToString("N0") + " " + unit;
    }

    void SaveInvoice()
    {
        if (invoiceItems.Rows.Count == 0) { MessageBox.Show("فاکتور خالی است."); return; }

        long total = InvoiceTotal();
        if (total < 0) { MessageBox.Show("مبلغ نهایی فاکتور نمی‌تواند منفی باشد."); return; }
        long paid = (long)invoicePaid.Value;
        if (paid > total) { MessageBox.Show("مبلغ پرداختی نمی‌تواند بیشتر از مبلغ فاکتور باشد."); return; }
        long outstanding = total - paid;
        if (paymentMethod.SelectedItem?.ToString() == "اعتباری" && outstanding == 0)
            paymentMethod.SelectedIndex = 0;
        if (paymentMethod.SelectedItem?.ToString() == "اعتباری" && string.IsNullOrWhiteSpace(customerName.Text))
        {
            MessageBox.Show("برای فروش اعتباری باید مشتری مشخص شود.");
            return;
        }
        var minMargin = GetMinimumSaleMarginPercent();
        if (minMargin > 0 && !PermissionService.Has("Sales.OverrideMinimumPrice"))
        {
            foreach (DataRow row in invoiceItems.Rows)
            {
                var productId = Convert.ToInt64(row["ProductId"]);
                var unitPrice = Convert.ToInt64(row["فی"]);
                var qty = Math.Max(1, Convert.ToInt64(row["تعداد"]));
                var discount = Convert.ToInt64(row["تخفیف"]);
                var effective = unitPrice - (decimal)discount / qty;
                var purchase = Convert.ToInt64(Database.Query("SELECT PurchasePrice FROM Products WHERE Id=@id", new SqliteParameter("@id", productId)).Rows[0]["PurchasePrice"]);
                var minimum = (long)Math.Ceiling(purchase * (1m + minMargin / 100m));
                if (effective < minimum)
                {
                    MessageBox.Show("این فاکتور شامل فروش زیر حداقل قیمت مجاز است. برای ادامه، مدیر باید مجوز Sales.OverrideMinimumPrice داشته باشد.", "کنترل حداقل قیمت", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
        }

        if (!PermissionService.Require("Sales.Create", this)) return;
        try
        {
            using var cn = Database.Open();
            using var tx = cn.BeginTransaction();

            long? customerId = null;
            var customer = customerName.Text.Trim();
            if (!string.IsNullOrWhiteSpace(customer))
            {
                using var find = cn.CreateCommand();
                find.Transaction = tx;
                find.CommandText = "SELECT Id FROM Customers WHERE Name=@name LIMIT 1";
                find.Parameters.AddWithValue("@name", customer);
                var existing = find.ExecuteScalar();
                if (existing == null || existing == DBNull.Value)
                {
                    using var addCustomer = cn.CreateCommand();
                    addCustomer.Transaction = tx;
                    addCustomer.CommandText = "INSERT INTO Customers(Name) VALUES(@name); SELECT last_insert_rowid();";
                    addCustomer.Parameters.AddWithValue("@name", customer);
                    customerId = Convert.ToInt64(addCustomer.ExecuteScalar());
                }
                else customerId = Convert.ToInt64(existing);
            }

            if (customerId.HasValue && outstanding > 0)
            {
                using var credit = cn.CreateCommand();
                credit.Transaction = tx;
                credit.CommandText = "SELECT CreditLimit,Balance FROM Customers WHERE Id=@id";
                credit.Parameters.AddWithValue("@id", customerId.Value);
                using var reader = credit.ExecuteReader();
                if (reader.Read())
                {
                    long limit = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    long balance = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                    if (limit > 0 && balance + outstanding > limit)
                    {
                        reader.Close();
                        MessageBox.Show("سقف اعتبار مشتری کافی نیست. مانده فعلی: " + balance.ToString("N0") + " ریال | سقف اعتبار: " + limit.ToString("N0") + " ریال", "هشدار اعتبار", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        tx.Rollback();
                        return;
                    }
                }
                reader.Close();
            }

            string invoiceNo = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            using var inv = cn.CreateCommand();
            inv.Transaction = tx;
            inv.CommandText = @"INSERT INTO Invoices(InvoiceNo,Type,CustomerId,UserName,DateText,Total,Paid,Notes)
VALUES(@no,'Sale',@customer,'کاربر',@date,@total,@paid,@notes); SELECT last_insert_rowid();";
            inv.Parameters.AddWithValue("@no", invoiceNo);
            inv.Parameters.AddWithValue("@customer", (object?)customerId ?? DBNull.Value);
            inv.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            inv.Parameters.AddWithValue("@total", total);
            inv.Parameters.AddWithValue("@paid", paid);
            inv.Parameters.AddWithValue("@notes", invoiceNote.Text.Trim());
            long invoiceId = Convert.ToInt64(inv.ExecuteScalar());

            foreach (DataRow row in invoiceItems.Rows)
            {
                long productId = Convert.ToInt64(row["ProductId"]);
                long qty = Convert.ToInt64(row["تعداد"]);
                long unitPrice = Convert.ToInt64(row["فی"]);
                long discount = Convert.ToInt64(row["تخفیف"]);

                if (!NegativeStockAllowed())
                {
                    using var stockCheck = cn.CreateCommand();
                    stockCheck.Transaction = tx;
                    stockCheck.CommandText = "SELECT Stock FROM Products WHERE Id=@product";
                    stockCheck.Parameters.AddWithValue("@product", productId);
                    var currentStock = Convert.ToInt64(stockCheck.ExecuteScalar() ?? 0);
                    if (qty > currentStock)
                        throw new InvalidOperationException("موجودی یکی از کالاها کافی نیست و فروش با موجودی منفی غیرفعال است.");
                }

                using var item = cn.CreateCommand();
                item.Transaction = tx;
                item.CommandText = @"INSERT INTO InvoiceItems(InvoiceId,ProductId,Quantity,UnitPrice,CostPrice,Discount)
SELECT @invoice,@product,@qty,@price,PurchasePrice,@discount FROM Products WHERE Id=@product;";
                item.Parameters.AddWithValue("@invoice", invoiceId);
                item.Parameters.AddWithValue("@product", productId);
                item.Parameters.AddWithValue("@qty", qty);
                item.Parameters.AddWithValue("@price", unitPrice);
                item.Parameters.AddWithValue("@discount", discount);
                item.ExecuteNonQuery();

                using var stockCmd = cn.CreateCommand();
                stockCmd.Transaction = tx;
                stockCmd.CommandText = "UPDATE Products SET Stock=Stock-@qty WHERE Id=@product";
                stockCmd.Parameters.AddWithValue("@qty", qty);
                stockCmd.Parameters.AddWithValue("@product", productId);
                stockCmd.ExecuteNonQuery();
            }

            if (customerId.HasValue)
            {
                using var balance = cn.CreateCommand();
                balance.Transaction = tx;
                balance.CommandText = "UPDATE Customers SET Balance=Balance+@amount WHERE Id=@id";
                balance.Parameters.AddWithValue("@amount", outstanding);
                balance.Parameters.AddWithValue("@id", customerId.Value);
                balance.ExecuteNonQuery();
            }

            if (paid > 0)
            {
                using var payment = cn.CreateCommand();
                payment.Transaction = tx;
                payment.CommandText = @"INSERT INTO CashTransactions
(DateText,Type,Amount,Description,UserName,CustomerId,PaymentMethod,ReferenceType,ReferenceId)
VALUES(@date,'SalePayment',@amount,@description,'کاربر',@customer,@method,'SaleInvoice',@reference)";
                payment.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                payment.Parameters.AddWithValue("@amount", paid);
                payment.Parameters.AddWithValue("@description", "دریافت بابت فاکتور فروش " + invoiceNo);
                payment.Parameters.AddWithValue("@customer", (object?)customerId ?? DBNull.Value);
                payment.Parameters.AddWithValue("@method", paymentMethod.SelectedItem?.ToString() ?? "نقدی");
                payment.Parameters.AddWithValue("@reference", invoiceId);
                payment.ExecuteNonQuery();
            }

            using var audit = cn.CreateCommand();
            audit.Transaction = tx;
            audit.CommandText = "INSERT INTO AuditLog(DateText,UserName,Action,Entity,EntityId,Details) VALUES(@date,'کاربر','ثبت فاکتور','Invoice',@id,@details)";
            audit.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            audit.Parameters.AddWithValue("@id", invoiceId);
            audit.Parameters.AddWithValue("@details", invoiceNo + " | پرداختی: " + paid + " | مانده: " + outstanding + " | روش: " + (paymentMethod.SelectedItem?.ToString() ?? ""));
            audit.ExecuteNonQuery();

            NetworkLedgerSyncService.RecordInvoice(cn, tx, invoiceId);
            tx.Commit();
            MessageBox.Show("فاکتور با موفقیت ثبت شد. شماره: " + invoiceNo, "باتیر");
            ClearInvoice();
            LoadProducts();
        }
        catch (Exception ex)
        {
            MessageBox.Show("ثبت فاکتور انجام نشد و تغییرات ذخیره نشد.\n" + ex.Message, "خطا",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void ClearInvoice()
    {
        invoiceItems.Clear();
        invoicePaid.Value = 0;
        paymentMethod.SelectedIndex = 0;
        Database.Execute("DELETE FROM DraftInvoiceItems; DELETE FROM DraftInvoice;");
        customerName.Clear();
        invoiceNote.Clear();
        invoiceSearch.Clear();
        invoiceQty.Value = 1;
        invoiceDiscount.Value = 0;
        UpdateInvoiceTotal();
    }

    void VerifyBackup()
    {
        if (!PermissionService.Require("Backup.Create", this)) return;
        try
        {
            using var s = new OpenFileDialog { Filter = "BATIR Database (*.db)|*.db", Title = "انتخاب فایل پشتیبان برای بررسی" };
            if (s.ShowDialog() != DialogResult.OK) return;
            var valid = Database.VerifyBackup(s.FileName);
            MessageBox.Show(valid ? "پشتیبان سالم و قابل استفاده است." : "پشتیبان معتبر نیست.",
                "بررسی پشتیبان", MessageBoxButtons.OK,
                valid ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show("بررسی پشتیبان انجام نشد.\n" + ex.Message, "خطا",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void RestoreBackup()
    {
        if (!PermissionService.Require("Backup.Restore", this)) return;
        try
        {
            using var s = new OpenFileDialog { Filter = "BATIR Database (*.db)|*.db", Title = "انتخاب فایل پشتیبان برای بازیابی" };
            if (s.ShowDialog() != DialogResult.OK) return;
            if (!Database.VerifyBackup(s.FileName))
            {
                MessageBox.Show("این فایل پشتیبان معتبر نیست و بازیابی انجام نشد.", "خطا",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (MessageBox.Show("قبل از بازیابی، از اطلاعات فعلی یک نسخه ایمن گرفته می‌شود. ادامه می‌دهید؟",
                "تأیید بازیابی", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            var safetyPath = Database.RestoreFrom(s.FileName);
            MessageBox.Show("بازیابی با موفقیت انجام شد.\nنسخه ایمن اطلاعات قبلی نیز در این مسیر ذخیره شد:\n" + safetyPath +
                "\nبرای بارگذاری کامل اطلاعات بازیابی‌شده، برنامه را ببندید و دوباره اجرا کنید.",
                "BATIR", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Application.Restart();
        }
        catch (Exception ex)
        {
            MessageBox.Show("بازیابی انجام نشد و نسخه فعلی حفظ شد.\n" + ex.Message, "خطا",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void Backup()
    {
        if (!PermissionService.Require("Backup.Create", this)) return;
        try
        {
            using var s = new SaveFileDialog { Filter = "BATIR Database (*.db)|*.db", FileName = "BATIR-Backup.db" };
            if (s.ShowDialog() != DialogResult.OK) return;
            Database.BackupTo(s.FileName);
            MessageBox.Show("پشتیبان‌گیری با موفقیت انجام شد.");
        }
        catch (Exception ex) { MessageBox.Show("خطا در پشتیبان‌گیری: " + ex.Message); }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F9)
        {
            SaveInvoice();
            return true;
        }
        if (keyData == Keys.F4)
        {
            invoiceSearch.Focus();
            invoiceSearch.SelectAll();
            return true;
        }
        if (keyData == Keys.F5)
        {
            using var purchaseForm = new PurchaseForm();
            purchaseForm.ShowDialog(this);
            return true;
        }
        if (keyData == Keys.F6)
        {
            customerSearch.Focus();
            customerSearch.SelectAll();
            return true;
        }
        if (keyData == Keys.F7)
        {
            search.Focus();
            search.SelectAll();
            return true;
        }
        if (keyData == Keys.Control | Keys.F)
        {
            invoiceSearch.Focus();
            invoiceSearch.SelectAll();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
