using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class Stage1SelfTest
{
    public static int Run()
    {
        try
        {
            TestDraftPersistence();
            TestBackupAndRestore();
            TestLegacySchemaMigration();
            Console.WriteLine("BATIR Stage 1 test passed: isolated database, backup verification, restore, and legacy schema migration.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("BATIR Stage 1 test failed: " + ex);
            return 1;
        }
    }

    private static void TestDraftPersistence()
    {
        long productId;
        using (var cn = Database.Open())
        using (var cmd = cn.CreateCommand())
        {
            AdvancedFeaturesMigration.Run(cn);
            cmd.CommandText = @"INSERT INTO Products(Code,Barcode,Name,PurchasePrice,SalePrice,Stock,MinStock,CreatedAt,UnitName)
VALUES('STAGE1-DRAFT','STAGE1-DRAFT-BAR','Stage 1 draft persistence probe',100,150,5,0,datetime('now'),'عدد');";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "SELECT last_insert_rowid();";
            productId = Convert.ToInt64(cmd.ExecuteScalar());
        }

        Database.Execute(@"INSERT OR REPLACE INTO DraftInvoice(Id,CustomerName,Notes,Paid,PaymentMethod,UpdatedAt)
VALUES(1,'Stage 1 customer','autosave recovery probe',50,'کارت',datetime('now'));
DELETE FROM DraftInvoiceItems;");
        Database.Execute(@"INSERT INTO DraftInvoiceItems(ProductId,ProductName,Quantity,UnitPrice,Discount)
VALUES(@product,'Stage 1 draft persistence probe',2,150,10);", new SqliteParameter("@product", productId));

        var header = Database.Query("SELECT CustomerName,Notes,Paid,PaymentMethod FROM DraftInvoice WHERE Id=1;");
        var items = Database.Query("SELECT ProductId,Quantity,UnitPrice,Discount FROM DraftInvoiceItems;");
        Require(header.Rows.Count == 1, "draft header persisted");
        Require(Convert.ToString(header.Rows[0]["CustomerName"]) == "Stage 1 customer", "draft customer recovered");
        Require(Convert.ToString(header.Rows[0]["Notes"]) == "autosave recovery probe", "draft notes recovered");
        Require(Convert.ToInt64(header.Rows[0]["Paid"]) == 50, "draft paid amount recovered");
        Require(Convert.ToString(header.Rows[0]["PaymentMethod"]) == "کارت", "draft payment method recovered");
        Require(items.Rows.Count == 1 && Convert.ToInt64(items.Rows[0]["Quantity"]) == 2, "draft item recovered");
        Console.WriteLine("Stage 1 draft persistence: passed (customer, notes, paid amount, payment method, and line item round-trip).");
    }

    private static void TestBackupAndRestore()
    {
        using (var cn = Database.Open())
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "INSERT OR REPLACE INTO Settings(Key,Value,UpdatedAt) VALUES('Stage1BackupRestoreProbe','before',datetime('now'));";
            cmd.ExecuteNonQuery();
        }

        var root = Path.GetDirectoryName(Database.FilePath)!;
        var backupPath = Path.Combine(root, "stage1-verified-backup.db");
        Database.BackupTo(backupPath);
        Require(Database.VerifyBackup(backupPath), "backup integrity and BATIR schema check");
        TestRejectNonBatirDatabase(root);

        Database.Execute("UPDATE Settings SET Value='after',UpdatedAt=datetime('now') WHERE Key='Stage1BackupRestoreProbe';");
        var safetyPath = Database.RestoreFrom(backupPath);
        Require(File.Exists(safetyPath), "pre-restore safety backup exists");
        Require(Database.VerifyBackup(safetyPath), "pre-restore safety backup integrity");
        var restored = Database.Query("SELECT Value FROM Settings WHERE Key='Stage1BackupRestoreProbe';");
        Require(restored.Rows.Count == 1 && Convert.ToString(restored.Rows[0]["Value"]) == "before", "restore returns database to backup state");
        Console.WriteLine("Stage 1 backup/restore: passed (backup integrity, safety copy, data restoration).");
    }

    private static void TestRejectNonBatirDatabase(string root)
    {
        var foreignPath = Path.Combine(root, "stage1-not-a-batir-backup.db");
        using (var foreign = new SqliteConnection("Data Source=" + foreignPath))
        {
            foreign.Open();
            using var cmd = foreign.CreateCommand();
            cmd.CommandText = "CREATE TABLE UnrelatedData(Id INTEGER PRIMARY KEY, Value TEXT);";
            cmd.ExecuteNonQuery();
        }
        Require(!Database.VerifyBackup(foreignPath), "reject valid SQLite file without BATIR core tables");
        var rejectedRestore = false;
        try { Database.RestoreFrom(foreignPath); }
        catch (InvalidDataException) { rejectedRestore = true; }
        Require(rejectedRestore, "reject restoring a valid SQLite file without BATIR core tables");
        var probe = Database.Query("SELECT Value FROM Settings WHERE Key='Stage1BackupRestoreProbe';");
        Require(probe.Rows.Count == 1 && Convert.ToString(probe.Rows[0]["Value"]) == "before",
            "failed restore leaves current database unchanged");
        Console.WriteLine("Stage 1 backup validation: passed (rejects non-BATIR files and preserves current data on rejected restore).");
    }

    private static void TestLegacySchemaMigration()
    {
        var previousPath = Environment.GetEnvironmentVariable("BATIR_DATABASE_PATH");
        var legacyPath = Path.Combine(Path.GetTempPath(), "BATIR-LegacyMigration-" + Guid.NewGuid().ToString("N"), "legacy.db");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        try
        {
            using (var legacy = new SqliteConnection("Data Source=" + legacyPath))
            {
                legacy.Open();
                using var cmd = legacy.CreateCommand();
                cmd.CommandText = @"
CREATE TABLE Products(
 Id INTEGER PRIMARY KEY AUTOINCREMENT, Code TEXT, Barcode TEXT, Name TEXT NOT NULL, Brand TEXT, Category TEXT,
 PurchasePrice INTEGER NOT NULL DEFAULT 0, SalePrice INTEGER NOT NULL DEFAULT 0,
 Stock INTEGER NOT NULL DEFAULT 0, MinStock INTEGER NOT NULL DEFAULT 0,
 Active INTEGER NOT NULL DEFAULT 1, CreatedAt TEXT NOT NULL);
INSERT INTO Products(Code,Barcode,Name,PurchasePrice,SalePrice,Stock,MinStock,Active,CreatedAt)
VALUES('LEGACY-1','LEGACY-BAR-1','Legacy migration probe',100,150,7,1,1,datetime('now'));";
                cmd.ExecuteNonQuery();
            }

            Environment.SetEnvironmentVariable("BATIR_DATABASE_PATH", legacyPath);
            using (var migrated = Database.Open())
                AdvancedFeaturesMigration.Run(migrated);

            var product = Database.Query("SELECT Name,Stock,UnitName,UnitConversionFactor FROM Products WHERE Code='LEGACY-1';");
            Require(product.Rows.Count == 1, "legacy product retained");
            Require(Convert.ToString(product.Rows[0]["Name"]) == "Legacy migration probe", "legacy product name preserved");
            Require(Convert.ToInt64(product.Rows[0]["Stock"]) == 7, "legacy product stock preserved");
            Require(Convert.ToString(product.Rows[0]["UnitName"]) == "عدد", "legacy unit default migrated");
            Require(Convert.ToInt64(product.Rows[0]["UnitConversionFactor"]) == 1, "legacy unit conversion default migrated");
            Require(Database.VerifyBackup(legacyPath), "migrated legacy database integrity");
            Console.WriteLine("Stage 1 legacy migration: passed (existing product and stock preserved; missing unit fields added).");
        }
        finally
        {
            Environment.SetEnvironmentVariable("BATIR_DATABASE_PATH", previousPath);
        }
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("Stage 1 check failed: " + name);
    }
}
