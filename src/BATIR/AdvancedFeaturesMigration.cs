using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class AdvancedFeaturesMigration
{
    public static void Run(SqliteConnection cn)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS UserRoles(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE, IsSystem INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS UserPermissions(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, RoleId INTEGER NOT NULL, PermissionKey TEXT NOT NULL, Allowed INTEGER NOT NULL DEFAULT 0,
 UNIQUE(RoleId,PermissionKey), FOREIGN KEY(RoleId) REFERENCES UserRoles(Id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS ApprovalRequests(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, DateText TEXT NOT NULL, UserName TEXT, Action TEXT NOT NULL,
 Entity TEXT, EntityId INTEGER, Status TEXT NOT NULL DEFAULT 'Pending', ApprovedBy TEXT, ApprovedAt TEXT, Details TEXT);
CREATE TABLE IF NOT EXISTS BankAccounts(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, BankName TEXT, AccountNo TEXT, Iban TEXT,
 OpeningBalance INTEGER NOT NULL DEFAULT 0, Active INTEGER NOT NULL DEFAULT 1);
CREATE TABLE IF NOT EXISTS BankTransactions(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, BankAccountId INTEGER NOT NULL, DateText TEXT NOT NULL,
 Amount INTEGER NOT NULL, Type TEXT NOT NULL, Description TEXT, ReferenceNo TEXT, Reconciled INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(BankAccountId) REFERENCES BankAccounts(Id) ON DELETE CASCADE);
CREATE TABLE IF NOT EXISTS BankReconciliations(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, BankAccountId INTEGER NOT NULL, FromDate TEXT, ToDate TEXT,
 StatementBalance INTEGER NOT NULL DEFAULT 0, BookBalance INTEGER NOT NULL DEFAULT 0, Difference INTEGER NOT NULL DEFAULT 0,
 Status TEXT NOT NULL DEFAULT 'Open', Notes TEXT, CreatedAt TEXT NOT NULL,
 FOREIGN KEY(BankAccountId) REFERENCES BankAccounts(Id));
CREATE TABLE IF NOT EXISTS Budgets(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, PeriodType TEXT NOT NULL, FromDate TEXT NOT NULL, ToDate TEXT NOT NULL,
 Amount INTEGER NOT NULL DEFAULT 0, Category TEXT, Notes TEXT);
CREATE TABLE IF NOT EXISTS Orders(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, OrderNo TEXT NOT NULL, Type TEXT NOT NULL, CustomerId INTEGER, SupplierId INTEGER,
 DateText TEXT NOT NULL, Status TEXT NOT NULL DEFAULT 'Open', Total INTEGER NOT NULL DEFAULT 0, Notes TEXT);
CREATE TABLE IF NOT EXISTS OrderItems(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, OrderId INTEGER NOT NULL, ProductId INTEGER NOT NULL,
 Quantity INTEGER NOT NULL, UnitPrice INTEGER NOT NULL DEFAULT 0, Discount INTEGER NOT NULL DEFAULT 0,
 FOREIGN KEY(OrderId) REFERENCES Orders(Id) ON DELETE CASCADE, FOREIGN KEY(ProductId) REFERENCES Products(Id));
CREATE TABLE IF NOT EXISTS RecurringDocuments(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, DocumentType TEXT NOT NULL, IntervalDays INTEGER NOT NULL,
 NextDate TEXT NOT NULL, Active INTEGER NOT NULL DEFAULT 1, TemplateJson TEXT);
CREATE TABLE IF NOT EXISTS Reminders(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, Title TEXT NOT NULL, DueDate TEXT NOT NULL, Type TEXT, PartyName TEXT,
 Amount INTEGER NOT NULL DEFAULT 0, ReferenceType TEXT, ReferenceId INTEGER, Done INTEGER NOT NULL DEFAULT 0, Notes TEXT);
CREATE TABLE IF NOT EXISTS FixedAssets(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, Code TEXT, Name TEXT NOT NULL, PurchaseDate TEXT, PurchaseCost INTEGER NOT NULL DEFAULT 0,
 ResidualValue INTEGER NOT NULL DEFAULT 0, UsefulLifeMonths INTEGER NOT NULL DEFAULT 0, DepreciationMethod TEXT,
 AccumulatedDepreciation INTEGER NOT NULL DEFAULT 0, Active INTEGER NOT NULL DEFAULT 1, Notes TEXT);
CREATE TABLE IF NOT EXISTS TaxSettings(
 Id INTEGER PRIMARY KEY CHECK(Id=1), TaxpayerId TEXT, EconomicCode TEXT, BranchCode TEXT, ApiMode TEXT, Enabled INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS TaxDocuments(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, InvoiceId INTEGER, DocumentNo TEXT, SubmissionId TEXT, Status TEXT,
 SubmittedAt TEXT, Response TEXT, ErrorMessage TEXT);
CREATE TABLE IF NOT EXISTS VoiceCommands(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, DateText TEXT NOT NULL, UserName TEXT, Transcript TEXT, Command TEXT, Success INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS OcrDocuments(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, DateText TEXT NOT NULL, FileName TEXT, DocumentType TEXT, ExtractedText TEXT, Status TEXT NOT NULL DEFAULT 'Pending', Notes TEXT);
CREATE INDEX IF NOT EXISTS IX_BankTransactions_Date ON BankTransactions(DateText);
CREATE INDEX IF NOT EXISTS IX_BankTransactions_Reconciled ON BankTransactions(Reconciled);
CREATE INDEX IF NOT EXISTS IX_BankReconciliations_Bank ON BankReconciliations(BankAccountId);
CREATE INDEX IF NOT EXISTS IX_Budgets_Period ON Budgets(FromDate,ToDate);
CREATE INDEX IF NOT EXISTS IX_Orders_Date ON Orders(DateText);
CREATE INDEX IF NOT EXISTS IX_Orders_TypeStatus ON Orders(Type,Status);
CREATE INDEX IF NOT EXISTS IX_Reminders_DueDate ON Reminders(DueDate,Done);
CREATE INDEX IF NOT EXISTS IX_TaxDocuments_Invoice ON TaxDocuments(InvoiceId);
";
        cmd.ExecuteNonQuery();

        using var seed = cn.CreateCommand();
        seed.CommandText = "INSERT OR IGNORE INTO UserRoles(Name,IsSystem) VALUES ('مدیر',1),('فروشنده',1),('انباردار',1); INSERT OR IGNORE INTO TaxSettings(Id,Enabled) VALUES(1,0);";
        seed.ExecuteNonQuery();

        var managerPermissions = new[] { "Products.View", "Products.Edit", "Sales.Create", "Sales.Return", "Purchases.Create", "Purchases.Return", "Customers.Edit", "Suppliers.Edit", "Checks.Edit", "Finance.Edit", "Inventory.Edit", "Reports.View", "Settings.Edit", "Backup.Restore", "Users.Edit", "Approvals.Approve", "Audit.View" };
        var sellerPermissions = new[] { "Products.View", "Sales.Create", "Sales.Return", "Customers.Edit", "Checks.Edit", "Reports.View" };
        var warehousePermissions = new[] { "Products.View", "Products.Edit", "Inventory.Edit", "Reports.View" };
        SeedPermissions(cn, "مدیر", managerPermissions);
        SeedPermissions(cn, "فروشنده", sellerPermissions);
        SeedPermissions(cn, "انباردار", warehousePermissions);

        using var settings = cn.CreateCommand();
        settings.CommandText = "SELECT COUNT(*) FROM Settings WHERE Key=@key";
        foreach (var item in FeatureSettings.Catalog)
        {
            settings.Parameters.Clear(); settings.Parameters.AddWithValue("@key", item.Key);
            if (Convert.ToInt32(settings.ExecuteScalar()) == 0)
            {
                using var add = cn.CreateCommand();
                add.CommandText = "INSERT INTO Settings(Key,Value,UpdatedAt) VALUES(@key,@value,datetime('now'))";
                add.Parameters.AddWithValue("@key", item.Key); add.Parameters.AddWithValue("@value", item.Default ? "true" : "false"); add.ExecuteNonQuery();
            }
        }

        using var roleSetting = cn.CreateCommand();
        roleSetting.CommandText = "INSERT OR IGNORE INTO Settings(Key,Value,UpdatedAt) SELECT 'CurrentRoleId',CAST(Id AS TEXT),datetime('now') FROM UserRoles WHERE Name='مدیر';";
        roleSetting.ExecuteNonQuery();
    }

    static void SeedPermissions(SqliteConnection cn, string roleName, IEnumerable<string> permissions)
    {
        using var role = cn.CreateCommand(); role.CommandText = "SELECT Id FROM UserRoles WHERE Name=@name LIMIT 1"; role.Parameters.AddWithValue("@name", roleName);
        var value = role.ExecuteScalar(); if (value == null) return; var roleId = Convert.ToInt64(value);
        foreach (var key in permissions)
        {
            using var add = cn.CreateCommand(); add.CommandText = "INSERT INTO UserPermissions(RoleId,PermissionKey,Allowed) VALUES(@role,@key,1) ON CONFLICT(RoleId,PermissionKey) DO UPDATE SET Allowed=1";
            add.Parameters.AddWithValue("@role", roleId); add.Parameters.AddWithValue("@key", key); add.ExecuteNonQuery();
        }
    }
}
