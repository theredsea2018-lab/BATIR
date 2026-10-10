using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

internal static class Database
{
    private static readonly string DefaultFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BATIR");
    public static string FilePath => Environment.GetEnvironmentVariable("BATIR_DATABASE_PATH") is { Length: > 0 } overridePath
        ? Path.GetFullPath(overridePath)
        : Path.Combine(DefaultFolder, "batir.db");
    private static string Folder => Path.GetDirectoryName(FilePath) ?? DefaultFolder;

    public static SqliteConnection Open()
    {
        Directory.CreateDirectory(Folder);
        var cn = new SqliteConnection($"Data Source={FilePath};Cache=Shared;Default Timeout=10;Pooling=True");
        cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA synchronous=FULL; PRAGMA busy_timeout=10000;";
        cmd.ExecuteNonQuery();
        Initialize(cn);
        return cn;
    }

    private static void Initialize(SqliteConnection cn)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Products(
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 Code TEXT, Barcode TEXT, Name TEXT NOT NULL, Brand TEXT, Category TEXT,
 PurchasePrice INTEGER NOT NULL DEFAULT 0, SalePrice INTEGER NOT NULL DEFAULT 0,
 Stock INTEGER NOT NULL DEFAULT 0, MinStock INTEGER NOT NULL DEFAULT 0,
 Active INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL, UnitName TEXT NOT NULL DEFAULT 'عدد', SecondaryUnitName TEXT, UnitConversionFactor INTEGER NOT NULL DEFAULT 1);
CREATE TABLE IF NOT EXISTS Customers(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Phone TEXT,
 Address TEXT, CreditLimit INTEGER NOT NULL DEFAULT 0,
 Balance INTEGER NOT NULL DEFAULT 0, Notes TEXT);
CREATE TABLE IF NOT EXISTS Suppliers(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Phone TEXT,
 Address TEXT, Balance INTEGER NOT NULL DEFAULT 0, Notes TEXT);
CREATE TABLE IF NOT EXISTS Invoices(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, InvoiceNo TEXT NOT NULL, Type TEXT NOT NULL,
 CustomerId INTEGER, SupplierId INTEGER, UserName TEXT, DateText TEXT NOT NULL,
 Total INTEGER NOT NULL DEFAULT 0, Paid INTEGER NOT NULL DEFAULT 0, Notes TEXT);
CREATE TABLE IF NOT EXISTS InvoiceItems(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, InvoiceId INTEGER NOT NULL,
 ProductId INTEGER NOT NULL, Quantity INTEGER NOT NULL,
 UnitPrice INTEGER NOT NULL, CostPrice INTEGER NOT NULL DEFAULT 0,
 Discount INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(InvoiceId) REFERENCES Invoices(Id) ON DELETE CASCADE,
 FOREIGN KEY(ProductId) REFERENCES Products(Id));
CREATE TABLE IF NOT EXISTS Checks(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, CheckNo TEXT, Bank TEXT,
 Amount INTEGER NOT NULL, DueDate TEXT, Type TEXT NOT NULL,
 Status TEXT NOT NULL, PartyName TEXT, Notes TEXT,
 CustomerId INTEGER, SupplierId INTEGER, LedgerStatus TEXT);
CREATE TABLE IF NOT EXISTS CashTransactions(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, DateText TEXT NOT NULL,
 Type TEXT NOT NULL, Amount INTEGER NOT NULL, Description TEXT, UserName TEXT,
 CustomerId INTEGER, SupplierId INTEGER, PaymentMethod TEXT, ReferenceType TEXT, ReferenceId INTEGER);
CREATE TABLE IF NOT EXISTS AuditLog(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, DateText TEXT NOT NULL,
 UserName TEXT, Action TEXT NOT NULL, Entity TEXT, EntityId INTEGER, Details TEXT);
CREATE TABLE IF NOT EXISTS SalesReturns(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, ReturnNo TEXT NOT NULL, OriginalInvoiceId INTEGER NOT NULL,
 CustomerId INTEGER, DateText TEXT NOT NULL, Total INTEGER NOT NULL DEFAULT 0, Notes TEXT);
CREATE TABLE IF NOT EXISTS SalesReturnItems(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, ReturnId INTEGER NOT NULL, ProductId INTEGER NOT NULL,
 Quantity INTEGER NOT NULL, UnitPrice INTEGER NOT NULL, Discount INTEGER NOT NULL DEFAULT 0, OriginalInvoiceItemId INTEGER,
 FOREIGN KEY(ReturnId) REFERENCES SalesReturns(Id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS PurchaseReturns(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, ReturnNo TEXT NOT NULL, OriginalInvoiceId INTEGER,
 SupplierId INTEGER, DateText TEXT NOT NULL, Total INTEGER NOT NULL DEFAULT 0, Notes TEXT);
CREATE TABLE IF NOT EXISTS PurchaseReturnItems(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, ReturnId INTEGER NOT NULL, ProductId INTEGER NOT NULL,
 Quantity INTEGER NOT NULL, UnitPrice INTEGER NOT NULL, Discount INTEGER NOT NULL DEFAULT 0, OriginalInvoiceItemId INTEGER, CostPrice INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(ReturnId) REFERENCES PurchaseReturns(Id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS DraftInvoice(
 Id INTEGER PRIMARY KEY CHECK(Id=1), CustomerName TEXT, Notes TEXT, UpdatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS DraftInvoiceItems(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, ProductId INTEGER NOT NULL,
 ProductName TEXT NOT NULL, Quantity INTEGER NOT NULL,
 UnitPrice INTEGER NOT NULL, Discount INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(ProductId) REFERENCES Products(Id));
CREATE TABLE IF NOT EXISTS DraftPurchase(
 Id INTEGER PRIMARY KEY CHECK(Id=1), SupplierName TEXT, Paid INTEGER NOT NULL DEFAULT 0,
 PaymentMethod TEXT, UpdatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS DraftPurchaseItems(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, ProductId INTEGER NOT NULL,
 ProductName TEXT NOT NULL, Quantity INTEGER NOT NULL,
 UnitPrice INTEGER NOT NULL, Discount INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(ProductId) REFERENCES Products(Id));
CREATE TABLE IF NOT EXISTS InventoryAdjustments(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, DateText TEXT NOT NULL, ProductId INTEGER NOT NULL,
 BeforeStock INTEGER NOT NULL, CountedStock INTEGER NOT NULL, Difference INTEGER NOT NULL,
 Note TEXT, UserName TEXT,
 FOREIGN KEY(ProductId) REFERENCES Products(Id));
CREATE TABLE IF NOT EXISTS Settings(
 Key TEXT PRIMARY KEY,
 Value TEXT NOT NULL,
 UpdatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_Products_Barcode ON Products(Barcode);
CREATE INDEX IF NOT EXISTS IX_Products_Name ON Products(Name);
CREATE INDEX IF NOT EXISTS IX_Products_Code ON Products(Code);
CREATE INDEX IF NOT EXISTS IX_Products_TechnicalCode ON Products(TechnicalCode);
CREATE INDEX IF NOT EXISTS IX_Invoices_InvoiceNo ON Invoices(InvoiceNo);
CREATE INDEX IF NOT EXISTS IX_Invoices_DateText ON Invoices(DateText);
CREATE INDEX IF NOT EXISTS IX_InvoiceItems_InvoiceId ON InvoiceItems(InvoiceId);
CREATE INDEX IF NOT EXISTS IX_CashTransactions_DateText ON CashTransactions(DateText);
CREATE INDEX IF NOT EXISTS IX_AuditLog_DateText ON AuditLog(DateText);
CREATE INDEX IF NOT EXISTS IX_InventoryAdjustments_DateText ON InventoryAdjustments(DateText);
CREATE INDEX IF NOT EXISTS IX_InventoryAdjustments_ProductId ON InventoryAdjustments(ProductId);
";
        cmd.ExecuteNonQuery();

        using var seed = cn.CreateCommand();
        seed.CommandText = @"
INSERT OR IGNORE INTO Settings(Key,Value,UpdatedAt) VALUES
('CurrencyName','ریال',datetime('now')),
('CurrencyCode','IRR',datetime('now')),
('Language','fa-IR',datetime('now')),
('DateCalendar','Shamsi',datetime('now')),
('PersianDigits','true',datetime('now')),
('AutoLockMinutes','0',datetime('now')),
('NegativeStockAllowed','false',datetime('now')),
('AutoLockPasswordHash','',datetime('now'));";
        seed.ExecuteNonQuery();

        using (var baseline = cn.CreateCommand())
        {
            baseline.CommandText = @"INSERT OR IGNORE INTO Settings(Key,Value,UpdatedAt) VALUES('BATIRSpecVersion','1.0',datetime('now'));";
            baseline.ExecuteNonQuery();
        }

        // Persist the agreed feature baseline so enabled/disabled state survives
        // application upgrades and is not dependent only on in-memory defaults.
        foreach (var feature in FeatureSettings.Catalog)
        {
            using var featureSeed = cn.CreateCommand();
            featureSeed.CommandText = @"INSERT OR IGNORE INTO Settings(Key,Value,UpdatedAt)
VALUES(@key,@value,@date);";
            featureSeed.Parameters.AddWithValue("@key", feature.Key);
            featureSeed.Parameters.AddWithValue("@value", feature.Default ? "true" : "false");
            featureSeed.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            featureSeed.ExecuteNonQuery();
        }

        AddColumnIfMissing(cn, "Products", "UnitName", "TEXT NOT NULL DEFAULT 'عدد'");
        AddColumnIfMissing(cn, "Products", "SecondaryUnitName", "TEXT");
        AddColumnIfMissing(cn, "Products", "UnitConversionFactor", "INTEGER NOT NULL DEFAULT 1");
        // Novin-style item-card fields; these repeatable additions preserve older BATIR databases.
        AddColumnIfMissing(cn, "Products", "TechnicalCode", "TEXT");
        AddColumnIfMissing(cn, "Products", "Description", "TEXT");
        AddColumnIfMissing(cn, "Products", "MaxStock", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(cn, "Products", "ReorderPoint", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(cn, "Products", "MinSalePrice", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(cn, "Products", "MaxSalePrice", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(cn, "CashTransactions", "CustomerId", "INTEGER");
        AddColumnIfMissing(cn, "CashTransactions", "SupplierId", "INTEGER");
        AddColumnIfMissing(cn, "CashTransactions", "PaymentMethod", "TEXT");
        AddColumnIfMissing(cn, "CashTransactions", "ReferenceType", "TEXT");
        AddColumnIfMissing(cn, "CashTransactions", "ReferenceId", "INTEGER");
        AddColumnIfMissing(cn, "PurchaseReturns", "OriginalInvoiceId", "INTEGER");
        AddColumnIfMissing(cn, "SalesReturnItems", "OriginalInvoiceItemId", "INTEGER");
        AddColumnIfMissing(cn, "PurchaseReturnItems", "OriginalInvoiceItemId", "INTEGER");
        AddColumnIfMissing(cn, "PurchaseReturnItems", "CostPrice", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(cn, "Checks", "CustomerId", "INTEGER");
        AddColumnIfMissing(cn, "Checks", "SupplierId", "INTEGER");
        AddColumnIfMissing(cn, "Checks", "LedgerStatus", "TEXT");

        using var indexes = cn.CreateCommand();
        indexes.CommandText = @"
CREATE INDEX IF NOT EXISTS IX_CashTransactions_Type ON CashTransactions(Type);
CREATE INDEX IF NOT EXISTS IX_CashTransactions_CustomerId ON CashTransactions(CustomerId);
CREATE INDEX IF NOT EXISTS IX_CashTransactions_SupplierId ON CashTransactions(SupplierId);
CREATE INDEX IF NOT EXISTS IX_Invoices_CustomerId ON Invoices(CustomerId);
CREATE INDEX IF NOT EXISTS IX_Invoices_SupplierId ON Invoices(SupplierId);
CREATE INDEX IF NOT EXISTS IX_SalesReturns_OriginalInvoiceId ON SalesReturns(OriginalInvoiceId);
CREATE INDEX IF NOT EXISTS IX_PurchaseReturns_OriginalInvoiceId ON PurchaseReturns(OriginalInvoiceId);
CREATE INDEX IF NOT EXISTS IX_SalesReturnItems_OriginalInvoiceItemId ON SalesReturnItems(OriginalInvoiceItemId);
CREATE INDEX IF NOT EXISTS IX_PurchaseReturnItems_OriginalInvoiceItemId ON PurchaseReturnItems(OriginalInvoiceItemId);
CREATE INDEX IF NOT EXISTS IX_Customers_Name ON Customers(Name);
CREATE INDEX IF NOT EXISTS IX_Customers_Phone ON Customers(Phone);
CREATE INDEX IF NOT EXISTS IX_Suppliers_Name ON Suppliers(Name);
CREATE INDEX IF NOT EXISTS IX_Suppliers_Phone ON Suppliers(Phone);";
        indexes.ExecuteNonQuery();

        // Apply repeatable integrity/performance hardening after legacy migrations.
        DataIntegrityService.Run(cn);
    }

    internal static void AddColumnIfMissing(SqliteConnection cn, string table, string column, string definition)
    {
        using var check = cn.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        using var reader = check.ExecuteReader();
        while (reader.Read())
            if (string.Equals(Convert.ToString(reader["name"]), column, StringComparison.OrdinalIgnoreCase))
                return;

        using var alter = cn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }

    public static void BackupTo(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination)) throw new ArgumentException("مسیر پشتیبان‌گیری خالی است.");
        if (string.Equals(Path.GetFullPath(destination), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("مسیر پشتیبان نمی‌تواند با فایل دیتابیس اصلی یکسان باشد.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? Folder);
        using var source = Open();
        using var target = new SqliteConnection($"Data Source={destination};");
        target.Open();
        source.BackupDatabase(target);
    }

    public static bool VerifyBackup(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("فایل پشتیبان پیدا نشد.", sourcePath);

        using var cn = new SqliteConnection("Data Source=" + sourcePath + ";");
        cn.Open();
        using (var integrity = cn.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check;";
            var result = Convert.ToString(integrity.ExecuteScalar());
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // An arbitrary, empty SQLite file can pass integrity_check. Do not treat it
        // as a BATIR backup: restoring it would replace the user's accounting data.
        using var schema = cn.CreateCommand();
        schema.CommandText = @"SELECT COUNT(*) FROM sqlite_master
WHERE type='table' AND name IN ('Products','Settings','Invoices','InvoiceItems');";
        return Convert.ToInt32(schema.ExecuteScalar()) == 4;
    }

    public static string RestoreFrom(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("فایل پشتیبان پیدا نشد.", sourcePath);
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("فایل پشتیبان باید با فایل پایگاه داده فعلی متفاوت باشد.");

        var safetyPath = Path.Combine(Folder, "BATIR-before-restore-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".db");
        using (var checkpoint = Open())
        using (var checkpointCommand = checkpoint.CreateCommand())
        {
            checkpointCommand.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            checkpointCommand.ExecuteNonQuery();
        }
        BackupTo(safetyPath);
        if (!VerifyBackup(safetyPath))
            throw new InvalidDataException("نسخه ایمن قبل از بازیابی قابل تأیید نیست؛ بازیابی متوقف شد.");

        var tempPath = FilePath + ".restore-" + Guid.NewGuid().ToString("N") + ".db";
        try
        {
            using (var source = new SqliteConnection("Data Source=" + sourcePath + ";Cache=Shared"))
            using (var target = new SqliteConnection("Data Source=" + tempPath + ";Cache=Shared"))
            {
                source.Open();
                target.Open();
                source.BackupDatabase(target);
            }

            if (!VerifyBackup(tempPath))
                throw new InvalidDataException("فایل پشتیبان از نظر ساختار SQLite معتبر نیست.");

            // Release pooled handles before replacing the active SQLite file on Windows.
            SqliteConnection.ClearAllPools();
            File.Copy(tempPath, FilePath, true);
            return safetyPath;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    public static DataTable Query(string sql, params SqliteParameter[] parameters)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.Add(p);
        using var reader = cmd.ExecuteReader();
        var dt = new DataTable();
        dt.Load(reader);
        return dt;
    }

    public static void Execute(string sql, params SqliteParameter[] parameters)
    {
        using var cn = Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.Add(p);
        cmd.ExecuteNonQuery();
    }
}
