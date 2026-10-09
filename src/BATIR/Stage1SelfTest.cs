using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class Stage1SelfTest
{
    public static int Run()
    {
        try
        {
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
        Require(Database.VerifyBackup(backupPath), "backup integrity check");

        Database.Execute("UPDATE Settings SET Value='after',UpdatedAt=datetime('now') WHERE Key='Stage1BackupRestoreProbe';");
        var safetyPath = Database.RestoreFrom(backupPath);
        Require(File.Exists(safetyPath), "pre-restore safety backup exists");
        Require(Database.VerifyBackup(safetyPath), "pre-restore safety backup integrity");
        var restored = Database.Query("SELECT Value FROM Settings WHERE Key='Stage1BackupRestoreProbe';");
        Require(restored.Rows.Count == 1 && Convert.ToString(restored.Rows[0]["Value"]) == "before", "restore returns database to backup state");
        Console.WriteLine("Stage 1 backup/restore: passed (backup integrity, safety copy, data restoration).");
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
