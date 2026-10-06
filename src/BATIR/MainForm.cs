using Microsoft.Data.Sqlite;
using System.Data;

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
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildProducts());
        tabs.TabPages.Add(BuildInvoices());
        tabs.TabPages.Add(BuildCustomers());
        tabs.TabPages.Add(BuildChecksPage());
        var purchasePage = new TabPage("فاکتور خرید");
        var purchaseButton = new Button { Text = "باز کردن فاکتور خرید", Dock = DockStyle.Top, Height = 70 };
        purchaseButton.Click += (_, _) => new PurchaseForm().ShowDialog(this);
        purchasePage.Controls.Add(purchaseButton);
        tabs.TabPages.Add(purchasePage);
        var purchaseReturnsPage = new TabPage("برگشت از خرید");
        var purchaseReturnsButton = new Button { Text = "باز کردن برگشت از خرید", Dock = DockStyle.Top, Height = 70 };
        purchaseReturnsButton.Click += (_, _) => new PurchaseReturnsForm().ShowDialog(this);
        purchaseReturnsPage.Controls.Add(purchaseReturnsButton);
        tabs.TabPages.Add(purchaseReturnsPage);
        var returnsPage = new TabPage("برگشت از فروش");
        var returnsButton = new Button { Text = "باز کردن", Dock = DockStyle.Top, Height = 70 };
        returnsButton.Click += (_, _) => { var form = new SalesReturnsForm(); form.ShowDialog(this); };
        returnsPage.Controls.Add(returnsButton);
        tabs.TabPages.Add(returnsPage);
        var suppliersPage = new TabPage("تأمین‌کنندگان");
        var suppliersButton = new Button { Text = "باز کردن مدیریت تأمین‌کنندگان", Dock = DockStyle.Top, Height = 70 };
        suppliersButton.Click += (_, _) => new SuppliersForm().ShowDialog(this);
        suppliersPage.Controls.Add(suppliersButton);
        tabs.TabPages.Add(suppliersPage);
        var statementPage = new TabPage("دفتر حساب مشتریان");
        var statementButton = new Button { Text = "باز کردن دفتر حساب مشتریان", Dock = DockStyle.Top, Height = 70 };
        statementButton.Click += (_, _) => new CustomerStatementForm().ShowDialog(this);
        statementPage.Controls.Add(statementButton);
        tabs.TabPages.Add(statementPage);
        var supplierStatementPage = new TabPage("دفتر حساب تأمین‌کنندگان");
        var supplierStatementButton = new Button { Text = "باز کردن دفتر حساب تأمین‌کنندگان", Dock = DockStyle.Top, Height = 70 };
        supplierStatementButton.Click += (_, _) => new SupplierStatementForm().ShowDialog(this);
        supplierStatementPage.Controls.Add(supplierStatementButton);
        tabs.TabPages.Add(supplierStatementPage);
        var stocktakingPage = new TabPage("انبارگردانی");
        var stocktakingButton = new Button { Text = "باز کردن انبارگردانی", Dock = DockStyle.Top, Height = 70 };
        stocktakingButton.Click += (_, _) => new StocktakingForm().ShowDialog(this);
        stocktakingPage.Controls.Add(stocktakingButton);
        tabs.TabPages.Add(stocktakingPage);
        var reportPage = new TabPage("گزارش موجودی و سود");
        var reportButton = new Button { Text = "باز کردن گزارش موجودی و سود", Dock = DockStyle.Top, Height = 70 };
        reportButton.Click += (_, _) => new InventoryReportForm().ShowDialog(this);
        reportPage.Controls.Add(reportButton);
        tabs.TabPages.Add(reportPage);
        var documentSearchPage = new TabPage("جستجوی اسناد");
        var documentSearchButton = new Button { Text = "باز کردن جستجوی اسناد", Dock = DockStyle.Top, Height = 70 };
        documentSearchButton.Click += (_, _) => new DocumentSearchForm().ShowDialog(this);
        documentSearchPage.Controls.Add(documentSearchButton);
        tabs.TabPages.Add(documentSearchPage);
        var debtorsPage = new TabPage("بدهکاران و بستانکاران");
        var debtorsButton = new Button { Text = "باز کردن گزارش بدهکاران و بستانکاران", Dock = DockStyle.Top, Height = 70 };
        debtorsButton.Click += (_, _) => new DebtorsCreditorsForm().ShowDialog(this);
        debtorsPage.Controls.Add(debtorsButton);
        tabs.TabPages.Add(debtorsPage);
        var stage2Page = new TabPage("کنترل و گزارش مرحله دوم");
        var stage2Button = new Button { Text = "گزارش سود، بهای میانگین، هشدار، تغییر گروهی قیمت، گردش کالا و بستن شیفت", Dock = DockStyle.Top, Height = 70 };
        stage2Button.Click += (_, _) => new Stage2ReportsForm().ShowDialog(this);
        stage2Page.Controls.Add(stage2Button);
        tabs.TabPages.Add(stage2Page);
        var settingsPage = new TabPage("تنظیمات");
        var settingsButton = new Button { Text = "باز کردن تنظیمات BATIR", Dock = DockStyle.Top, Height = 70 };
        settingsButton.Click += (_, _) => new SettingsForm().ShowDialog(this);
        settingsPage.Controls.Add(settingsButton);
        tabs.TabPages.Add(settingsPage);
        var financePage = new TabPage("مالی روزانه");
        var financeButton = new Button { Text = "دریافت، پرداخت تأمین‌کننده و هزینه‌ها", Dock = DockStyle.Top, Height = 70 };
        financeButton.Click += (_,_) => new FinancialOperationsForm().ShowDialog(this);
        financePage.Controls.Add(financeButton); tabs.TabPages.Add(financePage);
        var inventoryOpsPage = new TabPage("اصلاح موجودی");
        var inventoryOpsButton = new Button { Text = "اصلاح و ثبت گردش موجودی", Dock = DockStyle.Top, Height = 70 };
        inventoryOpsButton.Click += (_,_) => new InventoryTransferForm().ShowDialog(this);
        inventoryOpsPage.Controls.Add(inventoryOpsButton); tabs.TabPages.Add(inventoryOpsPage);

        var top = new Panel { Dock = DockStyle.Top, Height = 55 };
        var title = new Label { Text = "  باتیر | حسابداری و انبارداری", Dock = DockStyle.Left, Width = 350,
            Font = new Font("Tahoma", 14, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        var restore = new Button { Text = "بازیابی پشتیبان", Dock = DockStyle.Right, Width = 120 };
        restore.Click += (_, _) => RestoreBackup();
        var verify = new Button { Text = "بررسی پشتیبان", Dock = DockStyle.Right, Width = 120 };
        verify.Click += (_, _) => VerifyBackup();
        var backup = new Button { Text = "پشتیبان‌گیری", Dock = DockStyle.Right, Width = 120 };
        backup.Click += (_, _) => Backup();
        top.Controls.Add(restore); top.Controls.Add(verify); top.Controls.Add(backup); top.Controls.Add(title);

        status.Dock = DockStyle.Bottom; status.Height = 28; status.Text = "آماده | ریال";
        Controls.Add(tabs); Controls.Add(status); Controls.Add(top);
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
        invoiceTotal.Text = "جمع فاکتور: ۰ ریال";
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

        var form = new TableLayoutPanel { Dock = DockStyle.Top, Height = 125, ColumnCount = 4, RowCount = 2, Padding = new Padding(4) };
        for (int i = 0; i < 4; i++) form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        AddText(form, "نام مشتری", customerName, 0, 0);
        AddText(form, "تلفن", customerPhone, 1, 0);
        AddText(form, "آدرس", customerAddress, 2, 0);
        AddNum(form, "سقف اعتبار", customerCreditLimit, 3, 0);
        AddText(form, "توضیحات", customerNotes, 0, 1);

        var save = new Button { Text = "ثبت / ویرایش مشتری", Dock = DockStyle.Fill };
        save.Click += (_, _) => SaveCustomer();
        form.Controls.Add(save, 1, 1);

        var payment = new Button { Text = "ثبت دریافت از مشتری", Dock = DockStyle.Fill };
        payment.Click += (_, _) => RecordCustomerPayment();
        form.Controls.Add(payment, 2, 1);

        var cancel = new Button { Text = "پاک کردن فرم", Dock = DockStyle.Fill };
        cancel.Click += (_, _) => ClearCustomerForm();
        form.Controls.Add(cancel, 3, 1);

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
        var dt = Database.Query("SELECT Name,Phone,Address,CreditLimit,Balance,Notes FROM Customers WHERE Id=@id",
            new SqliteParameter("@id", editingCustomerId));
        if (dt.Rows.Count == 0) return;
        customerName.Text = Convert.ToString(dt.Rows[0]["Name"]) ?? "";
        customerPhone.Text = Convert.ToString(dt.Rows[0]["Phone"]) ?? "";
        customerAddress.Text = Convert.ToString(dt.Rows[0]["Address"]) ?? "";
        customerCreditLimit.Value = Math.Max(0, Convert.ToDecimal(dt.Rows[0]["CreditLimit"]));
        customerNotes.Text = Convert.ToString(dt.Rows[0]["Notes"]) ?? "";
        customerPayment.Value = 0;
    }

    void SaveCustomer()
    {
        if (string.IsNullOrWhiteSpace(customerName.Text))
        {
            MessageBox.Show("نام مشتری را وارد کنید.");
            return;
        }
        if (editingCustomerId == 0)
        {
            Database.Execute(@"INSERT INTO Customers(Name,Phone,Address,CreditLimit,Balance,Notes)
VALUES(@name,@phone,@address,@limit,0,@notes)",
                new SqliteParameter("@name", customerName.Text.Trim()),
                new SqliteParameter("@phone", customerPhone.Text.Trim()),
                new SqliteParameter("@address", customerAddress.Text.Trim()),
                new SqliteParameter("@limit", (long)customerCreditLimit.Value),
                new SqliteParameter("@notes", customerNotes.Text.Trim()));
            status.Text = "مشتری جدید ثبت شد";
        }
        else
        {
            Database.Execute(@"UPDATE Customers SET Name=@name,Phone=@phone,Address=@address,
CreditLimit=@limit,Notes=@notes WHERE Id=@id",
                new SqliteParameter("@name", customerName.Text.Trim()),
                new SqliteParameter("@phone", customerPhone.Text.Trim()),
                new SqliteParameter("@address", customerAddress.Text.Trim()),
                new SqliteParameter("@limit", (long)customerCreditLimit.Value),
                new SqliteParameter("@notes", customerNotes.Text.Trim()),
                new SqliteParameter("@id", editingCustomerId));
            status.Text = "اطلاعات مشتری ویرایش شد";
        }
        ClearCustomerForm();
        LoadCustomers(customerSearch.Text.Trim());
    }

    void RecordCustomerPayment()
    {
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
        customerCreditLimit.Value = 0;
        customerPayment.Value = 0;
    }

    TabPage BuildChecksPage()
    {
        var p = new TabPage("چک‌ها");
        var b = new Button { Text = "باز کردن مدیریت چک‌ها", Dock = DockStyle.Top, Height = 70 };
        b.Click += (_, _) => new ChecksForm().ShowDialog(this);
        p.Controls.Add(b);
        return p;
    }

    TabPage SimplePage(string title, string text)
    {
        var p = new TabPage(title);
        p.Controls.Add(new Label { Dock = DockStyle.Fill, Text = text + "\n\nساختار پایگاه داده این بخش آماده است و فرم عملیاتی آن در مرحله بعد تکمیل می‌شود.",
            Padding = new Padding(30), TextAlign = ContentAlignment.TopRight });
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
        grid.DataSource = dt; status.Text = "تعداد کالا: " + dt.Rows.Count.ToString("N0") + " | ریال";
    }

    void AddProduct(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(name.Text)) { MessageBox.Show("نام کالا را وارد کنید."); return; }
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

    bool NegativeStockAllowed()
    {
        var dt = Database.Query("SELECT Value FROM Settings WHERE Key='NegativeStockAllowed' LIMIT 1");
        return dt.Rows.Count > 0 && string.Equals(Convert.ToString(dt.Rows[0]["Value"]), "true", StringComparison.OrdinalIgnoreCase);
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
        long price = Convert.ToInt64(dt.Rows[0]["SalePrice"]);
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
                row["تعداد"] = Convert.ToInt64(row["تعداد"]) + qty;
                row["تخفیف"] = Convert.ToInt64(row["تخفیف"]) + (long)invoiceDiscount.Value;
                row["جمع"] = Convert.ToInt64(row["تعداد"]) * Convert.ToInt64(row["فی"]) - Convert.ToInt64(row["تخفیف"]);
                UpdateInvoiceTotal();
                SaveDraftInvoice();
                invoiceSearch.Clear();
                invoiceQty.Value = 1;
                invoiceDiscount.Value = 0;
                return;
            }
        }

        long discount = (long)invoiceDiscount.Value;
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
                status.Text = "پیش‌نویس فاکتور از آخرین جلسه بازیابی شد | ریال";
        }
        catch (Exception ex)
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
        invoiceTotal.Text = "جمع: " + InvoiceTotal().ToString("N0") + " ریال | پرداختی: " + paid.ToString("N0") + " | مانده: " + remaining.ToString("N0") + " ریال";
    }

    void SaveInvoice()
    {
        if (invoiceItems.Rows.Count == 0) { MessageBox.Show("فاکتور خالی است."); return; }

        long total = InvoiceTotal();
        long paid = (long)invoicePaid.Value;
        if (paid > total) { MessageBox.Show("مبلغ پرداختی نمی‌تواند بیشتر از مبلغ فاکتور باشد."); return; }
        long outstanding = total - paid;
        if (paymentMethod.SelectedItem?.ToString() == "اعتباری" && outstanding == 0)
            paymentMethod.SelectedIndex = 0;
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
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
