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
 Status TEXT NOT NULL, PartyName TEXT, Notes TEXT);
CREATE TABLE IF NOT EXISTS CashTransactions(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, DateText TEXT NOT NULL,
 Type TEXT NOT NULL, Amount INTEGER NOT NULL, Description TEXT, UserName TEXT);
CREATE TABLE IF NOT EXISTS AuditLog(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, DateText TEXT NOT NULL,
 UserName TEXT, Action TEXT NOT NULL, Entity TEXT, EntityId INTEGER, Details TEXT);
CREATE TABLE IF NOT EXISTS SalesReturns(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, ReturnNo TEXT NOT NULL, OriginalInvoiceId INTEGER NOT NULL,
 CustomerId INTEGER, DateText TEXT NOT NULL, Total INTEGER NOT NULL DEFAULT 0, Notes TEXT);
CREATE TABLE IF NOT EXISTS SalesReturnItems(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, ReturnId INTEGER NOT NULL, ProductId INTEGER NOT NULL,
 Quantity INTEGER NOT NULL, UnitPrice INTEGER NOT NULL, Discount INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(ReturnId) REFERENCES SalesReturns(Id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS PurchaseReturns(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, ReturnNo TEXT NOT NULL, SupplierId INTEGER,
 DateText TEXT NOT NULL, Total INTEGER NOT NULL DEFAULT 0, Notes TEXT);
CREATE TABLE IF NOT EXISTS PurchaseReturnItems(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, ReturnId INTEGER NOT NULL, ProductId INTEGER NOT NULL,
 Quantity INTEGER NOT NULL, UnitPrice INTEGER NOT NULL, Discount INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(ReturnId) REFERENCES PurchaseReturns(Id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS DraftInvoice(
 Id INTEGER PRIMARY KEY CHECK(Id=1), CustomerName TEXT, Notes TEXT, UpdatedAt TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS DraftInvoiceItems(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, ProductId INTEGER NOT NULL,
 ProductName TEXT NOT NULL, Quantity INTEGER NOT NULL,
 UnitPrice INTEGER NOT NULL, Discount INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(ProductId) REFERENCES Products(Id));";
        cmd.ExecuteNonQuery();
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