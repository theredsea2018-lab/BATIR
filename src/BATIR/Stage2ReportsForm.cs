using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public sealed class Stage2ReportsForm : Form
{
    readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    readonly DataGridView profitGrid = Grid();
    readonly DataGridView costGrid = Grid();
    readonly DataGridView alertGrid = Grid();
    readonly DataGridView movementGrid = Grid();
    readonly DataGridView cashGrid = Grid();
    readonly DataGridView priceGrid = Grid();
    readonly TextBox productFilter = new();
    readonly NumericUpDown pricePercent = new() { Minimum = -100, Maximum = 100000, DecimalPlaces = 2 };
    readonly ComboBox priceMode = new();
    readonly TextBox shiftName = new();
    readonly NumericUpDown openingCash = new() { Minimum = 0, Maximum = 999999999999 };
    readonly NumericUpDown closingCash = new() { Minimum = 0, Maximum = 999999999999 };

    public Stage2ReportsForm()
    {
        Text = "BATIR | مرحله دوم - کنترل و گزارش‌های مدیریتی";
        Width = 1250; Height = 760;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Build();
    }

    static DataGridView Grid() => new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };

    void Build()
    {
        tabs.TabPages.Add(BuildProfit());
        tabs.TabPages.Add(BuildCost());
        tabs.TabPages.Add(BuildAlerts());
        tabs.TabPages.Add(BuildPrice());
        tabs.TabPages.Add(BuildMovement());
        tabs.TabPages.Add(BuildCash());
        Controls.Add(tabs);
        LoadAll();
    }

    TabPage BuildProfit()
    {
        var p = new TabPage("سود و فروش");
        var top = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(6) };
        var refresh = new Button { Text = "به‌روزرسانی گزارش", Dock = DockStyle.Right, Width = 130 };
        refresh.Click += (_, _) => LoadProfit();
        top.Controls.Add(refresh);
        p.Controls.Add(profitGrid); p.Controls.Add(top);
        return p;
    }

    TabPage BuildCost()
    {
        var p = new TabPage("بهای تمام‌شده میانگین");
        var top = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(6) };
        var refresh = new Button { Text = "محاسبه مجدد", Dock = DockStyle.Right, Width = 130 };
        refresh.Click += (_, _) => LoadCost();
        top.Controls.Add(refresh);
        p.Controls.Add(costGrid); p.Controls.Add(top);
        return p;
    }

    TabPage BuildAlerts()
    {
        var p = new TabPage("هشدارهای انبار");
        var top = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(6) };
        var refresh = new Button { Text = "بررسی هشدارها", Dock = DockStyle.Right, Width = 130 };
        refresh.Click += (_, _) => LoadAlerts();
        top.Controls.Add(refresh);
        p.Controls.Add(alertGrid); p.Controls.Add(top);
        return p;
    }

    TabPage BuildPrice()
    {
        var p = new TabPage("افزایش / کاهش قیمت");
        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 92, ColumnCount = 4, Padding = new Padding(6) };
        for (var i = 0; i < 4; i++) top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        top.Controls.Add(new Label { Text = "فیلتر نام / برند / دسته / بارکد", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 0);
        productFilter.Dock = DockStyle.Fill; top.Controls.Add(productFilter, 1, 0);
        priceMode.Items.AddRange(new object[] { "قیمت فروش", "قیمت خرید", "هر دو" }); priceMode.SelectedIndex = 0;
        top.Controls.Add(priceMode, 2, 0);
        var apply = new Button { Text = "اعمال درصد روی موارد فیلترشده", Dock = DockStyle.Fill };
        apply.Click += (_, _) => ApplyBulkPrice();
        top.Controls.Add(apply, 3, 0);
        top.Controls.Add(new Label { Text = "درصد (مثبت افزایش، منفی کاهش)", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter }, 0, 1);
        top.Controls.Add(pricePercent, 1, 1);
        var refresh = new Button { Text = "نمایش کالاها", Dock = DockStyle.Fill }; refresh.Click += (_, _) => LoadPrices();
        top.Controls.Add(refresh, 2, 1);
        var rollback = new Button { Text = "برگشت آخرین تغییر قیمت", Dock = DockStyle.Fill }; rollback.Click += (_, _) => RollbackLastPriceBatch();
        top.Controls.Add(rollback, 3, 1);
        p.Controls.Add(priceGrid); p.Controls.Add(top);
        return p;
    }

    TabPage BuildMovement()
    {
        var p = new TabPage("گردش کالا");
        var top = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(6) };
        var refresh = new Button { Text = "به‌روزرسانی", Dock = DockStyle.Right, Width = 120 };
        refresh.Click += (_, _) => LoadMovement();
        top.Controls.Add(refresh);
        p.Controls.Add(movementGrid); p.Controls.Add(top);
        return p;
    }

    TabPage BuildCash()
    {
        var p = new TabPage("صندوق و شیفت");
        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 125, ColumnCount = 5, Padding = new Padding(6) };
        for (var i = 0; i < 5; i++) top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        Add(top, "نام شیفت", shiftName, 0, 0);
        Add(top, "موجودی اول شیفت", openingCash, 1, 0);
        Add(top, "موجودی شمارش‌شده پایان", closingCash, 2, 0);
        var close = new Button { Text = "بستن شیفت", Dock = DockStyle.Fill };
        close.Click += (_, _) => CloseShift();
        top.Controls.Add(close, 3, 0);
        var refresh = new Button { Text = "گزارش شیفت‌ها", Dock = DockStyle.Fill };
        refresh.Click += (_, _) => LoadCash();
        top.Controls.Add(refresh, 4, 0);
        var note = new Label { Text = "مبلغ مورد انتظار بر اساس عملیات نقدی ثبت‌شده محاسبه می‌شود.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
        top.Controls.Add(note, 0, 1); top.SetColumnSpan(note, 5);
        p.Controls.Add(cashGrid); p.Controls.Add(top);
        return p;
    }

    static void Add(TableLayoutPanel p, string label, Control c, int col, int row)
    {
        var box = new Panel { Dock = DockStyle.Fill };
        var l = new Label { Text = label, Dock = DockStyle.Top, Height = 22 };
        c.Dock = DockStyle.Fill; box.Controls.Add(c); box.Controls.Add(l); p.Controls.Add(box, col, row);
    }

    void LoadAll() { LoadProfit(); LoadCost(); LoadAlerts(); LoadPrices(); LoadMovement(); LoadCash(); }

    void LoadProfit()
    {
        profitGrid.DataSource = Database.Query(@"SELECT ProductName AS [کالا],
SUM(Quantity) AS [تعداد فروش],
SUM(NetSales) AS [فروش خالص],
SUM(CostPrice*Quantity) AS [بهای تمام‌شده],
SUM(Profit) AS [سود],
CASE WHEN SUM(NetSales)=0 THEN 0 ELSE ROUND(SUM(Profit)*100.0/SUM(NetSales),2) END AS [حاشیه سود درصد]
FROM v_SalesProfit GROUP BY ProductId,ProductName ORDER BY [سود] DESC;");
    }

    void LoadCost()
    {
        costGrid.DataSource = Database.Query(@"SELECT p.Name AS [کالا],p.Stock AS [موجودی],
COALESCE(SUM(CASE WHEN m.Direction=1 THEN m.Quantity ELSE 0 END),0) AS [ورودی],
COALESCE(SUM(CASE WHEN m.Direction=1 THEN m.Quantity*m.UnitCost ELSE 0 END),0) AS [ارزش خرید],
CASE WHEN COALESCE(SUM(CASE WHEN m.Direction=1 THEN m.Quantity ELSE 0 END),0)=0
THEN p.PurchasePrice
ELSE ROUND(CAST(SUM(CASE WHEN m.Direction=1 THEN m.Quantity*m.UnitCost ELSE 0 END) AS REAL) /
SUM(CASE WHEN m.Direction=1 THEN m.Quantity ELSE 0 END),0) END AS [بهای میانگین خرید],
CASE WHEN p.Stock=0 THEN 0 ELSE ROUND(p.Stock * (
CASE WHEN COALESCE(SUM(CASE WHEN m.Direction=1 THEN m.Quantity ELSE 0 END),0)=0
THEN p.PurchasePrice ELSE CAST(SUM(CASE WHEN m.Direction=1 THEN m.Quantity*m.UnitCost ELSE 0 END) AS REAL) /
SUM(CASE WHEN m.Direction=1 THEN m.Quantity ELSE 0 END) END),0) END AS [ارزش موجودی]
FROM Products p LEFT JOIN InventoryMovements m ON m.ProductId=p.Id
WHERE p.Active=1 GROUP BY p.Id,p.Name,p.Stock,p.PurchasePrice ORDER BY p.Name;");
    }

    void LoadAlerts()
    {
        alertGrid.DataSource = Database.Query(@"SELECT Name AS [کالا],Barcode AS [بارکد],Stock AS [موجودی],MinStock AS [حداقل موجودی],
PurchasePrice AS [خرید],SalePrice AS [فروش],
CASE WHEN Stock<=0 THEN 'موجودی صفر یا منفی'
     WHEN Stock<=MinStock THEN 'موجودی زیر حداقل'
     WHEN SalePrice<PurchasePrice THEN 'فروش زیر قیمت خرید'
     ELSE 'هشدار' END AS [هشدار]
FROM Products WHERE Active=1 AND (Stock<=MinStock OR SalePrice<PurchasePrice) ORDER BY Stock ASC,Name;");
    }

    void LoadPrices()
    {
        var f = productFilter.Text.Trim();
        var sql = @"SELECT Id,Name AS [کالا],Barcode AS [بارکد],Brand AS [برند],Category AS [دسته],
PurchasePrice AS [خرید],SalePrice AS [فروش] FROM Products WHERE Active=1";
        if (!string.IsNullOrWhiteSpace(f)) sql += " AND (Name LIKE @q OR Barcode LIKE @q OR Brand LIKE @q OR Category LIKE @q)";
        sql += " ORDER BY Name";
        priceGrid.DataSource = string.IsNullOrWhiteSpace(f) ? Database.Query(sql) : Database.Query(sql, new SqliteParameter("@q","%"+f+"%"));
    }

    void ApplyBulkPrice()
    {
        var pct = (decimal)pricePercent.Value;
        if (pct == 0) { MessageBox.Show("درصد تغییر نمی‌تواند صفر باشد."); return; }
        var f = productFilter.Text.Trim();
        var where = "Active=1";
        using var cn = Database.Open();
        using var tx = cn.BeginTransaction();
        try
        {
            using var cmd = cn.CreateCommand(); cmd.Transaction = tx;
            if (!string.IsNullOrWhiteSpace(f)) { where += " AND (Name LIKE @q OR Barcode LIKE @q OR Brand LIKE @q OR Category LIKE @q)"; cmd.Parameters.AddWithValue("@q","%"+f+"%"); }
            var factor = 1m + pct/100m;
            var mode = priceMode.SelectedItem?.ToString() ?? "قیمت فروش";
            var batchId = Guid.NewGuid().ToString("N");
            using (var snapshot = cn.CreateCommand())
            {
                snapshot.Transaction = tx;
                snapshot.CommandText = $"SELECT Id,PurchasePrice,SalePrice FROM Products WHERE {where}";
                using var reader = snapshot.ExecuteReader();
                var rows = new List<(long Id,long Purchase,long Sale)>();
                while (reader.Read()) rows.Add((reader.GetInt64(0), reader.GetInt64(1), reader.GetInt64(2)));
                foreach (var row in rows)
                {
                    var np = mode == "قیمت فروش" ? row.Purchase : (long)Math.Round(row.Purchase * factor, MidpointRounding.AwayFromZero);
                    var ns = mode == "قیمت خرید" ? row.Sale : (long)Math.Round(row.Sale * factor, MidpointRounding.AwayFromZero);
                    if (mode == "هر دو") { np = (long)Math.Round(row.Purchase * factor, MidpointRounding.AwayFromZero); ns = (long)Math.Round(row.Sale * factor, MidpointRounding.AwayFromZero); }
                    using var h = cn.CreateCommand(); h.Transaction = tx;
                    h.CommandText = "INSERT INTO PriceChangeHistory(BatchId,ProductId,ChangedAt,UserName,Mode,OldPurchasePrice,NewPurchasePrice,OldSalePrice,NewSalePrice) VALUES(@b,@p,@d,'کاربر',@m,@op,@np,@os,@ns)";
                    h.Parameters.AddWithValue("@b",batchId); h.Parameters.AddWithValue("@p",row.Id); h.Parameters.AddWithValue("@d",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); h.Parameters.AddWithValue("@m",mode);
                    h.Parameters.AddWithValue("@op",row.Purchase); h.Parameters.AddWithValue("@np",np); h.Parameters.AddWithValue("@os",row.Sale); h.Parameters.AddWithValue("@ns",ns); h.ExecuteNonQuery();
                }
            }
            cmd.CommandText = mode switch
            {
                "قیمت خرید" => $"UPDATE Products SET PurchasePrice=CAST(ROUND(PurchasePrice*{factor.ToString(System.Globalization.CultureInfo.InvariantCulture)},0) AS INTEGER) WHERE {where};",
                "هر دو" => $"UPDATE Products SET PurchasePrice=CAST(ROUND(PurchasePrice*{factor.ToString(System.Globalization.CultureInfo.InvariantCulture)},0) AS INTEGER), SalePrice=CAST(ROUND(SalePrice*{factor.ToString(System.Globalization.CultureInfo.InvariantCulture)},0) AS INTEGER) WHERE {where};",
                _ => $"UPDATE Products SET SalePrice=CAST(ROUND(SalePrice*{factor.ToString(System.Globalization.CultureInfo.InvariantCulture)},0) AS INTEGER) WHERE {where};"
            };
            var affected = cmd.ExecuteNonQuery();
            if (affected == 0) { tx.Rollback(); MessageBox.Show("کالایی برای تغییر پیدا نشد."); return; }
            using var audit = cn.CreateCommand(); audit.Transaction=tx;
            audit.CommandText=@"INSERT INTO AuditLog(DateText,UserName,Action,Entity,EntityId,Details)
VALUES(@d,'کاربر','تغییر گروهی قیمت','Products',NULL,@details);";
            audit.Parameters.AddWithValue("@d",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            audit.Parameters.AddWithValue("@details",$"شناسه تغییر: {batchId} | درصد: {pct} | نوع: {mode} | فیلتر: {f} | تعداد: {affected}");
            audit.ExecuteNonQuery();
            tx.Commit();
            MessageBox.Show($"تغییر قیمت برای {affected} کالا انجام شد.");
            LoadPrices(); LoadAlerts();
        }
        catch(Exception ex){ try{tx.Rollback();}catch{} MessageBox.Show("تغییر قیمت انجام نشد و اطلاعات برگردانده شد.\n"+ex.Message); }
    }


    void RollbackLastPriceBatch()
    {
        if (!PermissionService.Require("Products.Edit", this)) return;
        try
        {
            using var cn = Database.Open();
            using var tx = cn.BeginTransaction();
            string? batchId = null;
            using (var find = cn.CreateCommand())
            {
                find.Transaction = tx;
                find.CommandText = "SELECT BatchId FROM PriceChangeHistory ORDER BY Id DESC LIMIT 1";
                batchId = Convert.ToString(find.ExecuteScalar());
            }
            if (string.IsNullOrWhiteSpace(batchId))
            {
                tx.Rollback();
                MessageBox.Show("تغییر قیمتی برای برگشت وجود ندارد.");
                return;
            }

            using (var restore = cn.CreateCommand())
            {
                restore.Transaction = tx;
                restore.CommandText = @"UPDATE Products
SET PurchasePrice=(SELECT OldPurchasePrice FROM PriceChangeHistory h WHERE h.BatchId=@batch AND h.ProductId=Products.Id ORDER BY h.Id DESC LIMIT 1),
    SalePrice=(SELECT OldSalePrice FROM PriceChangeHistory h WHERE h.BatchId=@batch AND h.ProductId=Products.Id ORDER BY h.Id DESC LIMIT 1)
WHERE Id IN (SELECT ProductId FROM PriceChangeHistory WHERE BatchId=@batch);";
                restore.Parameters.AddWithValue("@batch", batchId);
                restore.ExecuteNonQuery();
            }

            using (var audit = cn.CreateCommand())
            {
                audit.Transaction = tx;
                audit.CommandText = @"INSERT INTO AuditLog(DateText,UserName,Action,Entity,EntityId,Details)
VALUES(@d,'کاربر','برگشت آخرین تغییر قیمت','Products',NULL,@details);";
                audit.Parameters.AddWithValue("@d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                audit.Parameters.AddWithValue("@details", "شناسه تغییر برگشتی: " + batchId);
                audit.ExecuteNonQuery();
            }

            using (var remove = cn.CreateCommand())
            {
                remove.Transaction = tx;
                remove.CommandText = "DELETE FROM PriceChangeHistory WHERE BatchId=@batch";
                remove.Parameters.AddWithValue("@batch", batchId);
                remove.ExecuteNonQuery();
            }

            tx.Commit();
            MessageBox.Show("آخرین تغییر گروهی قیمت با موفقیت برگشت داده شد.");
            LoadPrices();
            LoadAlerts();
        }
        catch (Exception ex)
        {
            MessageBox.Show("برگشت قیمت انجام نشد.\n" + ex.Message, "خطا",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void LoadMovement()
    {
        movementGrid.DataSource = Database.Query(@"SELECT m.DateText AS [تاریخ],p.Name AS [کالا],m.Quantity AS [تعداد],
CASE WHEN m.Direction=1 THEN 'ورودی' ELSE 'خروجی' END AS [نوع],m.UnitCost AS [بهای واحد],
m.ReferenceType AS [مرجع],m.ReferenceId AS [شماره مرجع],m.Notes AS [توضیحات]
FROM InventoryMovements m JOIN Products p ON p.Id=m.ProductId ORDER BY m.DateText DESC LIMIT 2000;");
    }

    void CloseShift()
    {
        var name = shiftName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name)) { MessageBox.Show("نام شیفت را وارد کنید."); return; }
        using var cn=Database.Open(); using var tx=cn.BeginTransaction();
        try
        {
            using var cmd=cn.CreateCommand(); cmd.Transaction=tx;
            cmd.CommandText=@"SELECT COALESCE(SUM(CASE WHEN Type IN ('CustomerReceipt','SalePayment') AND PaymentMethod='نقدی' THEN Amount ELSE 0 END),0)
-COALESCE(SUM(CASE WHEN Type IN ('SupplierPayment','Expense') AND PaymentMethod='نقدی' THEN Amount ELSE 0 END),0) FROM CashTransactions WHERE DateText>=COALESCE((SELECT MAX(ClosedAt) FROM CashShifts),'0000-00-00 00:00:00');";
            var net=Convert.ToInt64(cmd.ExecuteScalar()??0);
            var expected=Convert.ToInt64(openingCash.Value)+net;
            using var ins=cn.CreateCommand(); ins.Transaction=tx;
            ins.CommandText=@"INSERT INTO CashShifts(Name,OpenedAt,ClosedAt,OpeningCash,ExpectedCash,ClosingCash,Difference,Status,Notes)
VALUES(@n,@o,@c,@op,@ex,@cl,@df,'Closed','بستن شیفت از گزارش مرحله دوم');";
            ins.Parameters.AddWithValue("@n",name); ins.Parameters.AddWithValue("@o",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            ins.Parameters.AddWithValue("@c",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); ins.Parameters.AddWithValue("@op",Convert.ToInt64(openingCash.Value));
            ins.Parameters.AddWithValue("@ex",expected); ins.Parameters.AddWithValue("@cl",Convert.ToInt64(closingCash.Value)); ins.Parameters.AddWithValue("@df",Convert.ToInt64(closingCash.Value)-expected);
            ins.ExecuteNonQuery(); tx.Commit();
            MessageBox.Show($"شیفت بسته شد. موجودی مورد انتظار: {expected:N0} | اختلاف: {Convert.ToInt64(closingCash.Value)-expected:N0}");
            LoadCash();
        }catch(Exception ex){try{tx.Rollback();}catch{} MessageBox.Show("بستن شیفت انجام نشد.\n"+ex.Message);}
    }

    void LoadCash()
    {
        cashGrid.DataSource=Database.Query(@"SELECT Id AS [شناسه],Name AS [شیفت],OpenedAt AS [شروع],ClosedAt AS [پایان],
OpeningCash AS [اول شیفت],ExpectedCash AS [موجودی مورد انتظار],ClosingCash AS [شمارش پایان],
Difference AS [اختلاف],Status AS [وضعیت] FROM CashShifts ORDER BY Id DESC;");
    }
}
