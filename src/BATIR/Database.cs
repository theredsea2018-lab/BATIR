using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

internal static class Database
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BATIR");
    public static string FilePath => Path.Combine(Folder, "batir.db");

    public static SqliteConnection Open()
    {
        Directory.CreateDirectory(Folder);
        var cn = new SqliteConnection($"Data Source={FilePath};Cache=Shared");
        cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA synchronous=FULL;";
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
 Active INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL);
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
CREATE TABLE IF NOT EXISTS Settings(
 Key TEXT PRIMARY KEY,
 Value TEXT NOT NULL,
 UpdatedAt TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS IX_Products_Barcode ON Products(Barcode);
CREATE INDEX IF NOT EXISTS IX_Products_Name ON Products(Name);
CREATE INDEX IF NOT EXISTS IX_Invoices_InvoiceNo ON Invoices(InvoiceNo);
CREATE INDEX IF NOT EXISTS IX_Invoices_DateText ON Invoices(DateText);
CREATE INDEX IF NOT EXISTS IX_InvoiceItems_InvoiceId ON InvoiceItems(InvoiceId);
CREATE INDEX IF NOT EXISTS IX_CashTransactions_DateText ON CashTransactions(DateText);
CREATE INDEX IF NOT EXISTS IX_AuditLog_DateText ON AuditLog(DateText);
";
        cmd.ExecuteNonQuery();

        // Defaults are inserted without overwriting values the user may have changed.
        using var seed = cn.CreateCommand();
        seed.CommandText = @"
INSERT OR IGNORE INTO Settings(Key,Value,UpdatedAt) VALUES
('CurrencyName','ریال',datetime('now')),
('CurrencyCode','IRR',datetime('now')),
('Language','fa-IR',datetime('now')),
('DateCalendar','Shamsi',datetime('now')),
('PersianDigits','true',datetime('now')),
('AutoLockMinutes','0',datetime('now')),
('NegativeStockAllowed','false',datetime('now'));";
        seed.ExecuteNonQuery();

        // Lightweight schema migrations for databases created by older BATIR builds.
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
CREATE INDEX IF NOT EXISTS IX_CashTransactions_SupplierId ON CashTransactions(SupplierId);";
        indexes.ExecuteNonQuery();
    }

    static void AddColumnIfMissing(SqliteConnection cn, string table, string column, string definition)
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
