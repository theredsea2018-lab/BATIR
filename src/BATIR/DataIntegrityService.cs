using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class DataIntegrityService
{
    public static void Run(SqliteConnection cn)
    {
        using var tx = cn.BeginTransaction();
        try
        {
            // 01-04: stable default settings.
            EnsureSetting(cn, tx, "PaymentMethods", "نقدی|کارتخوان|انتقال بانکی|اعتباری");
            EnsureSetting(cn, tx, "BackupVerification", "true");
            EnsureSetting(cn, tx, "InvoiceAutosave", "true");
            EnsureSetting(cn, tx, "LowStockAlert", "true");

            // 05-08: product lookup indexes.
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Products_Brand ON Products(Brand);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Products_Category ON Products(Category);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Products_Code ON Products(Code);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Products_Active ON Products(Active);");

            // 09-12: invoice filtering/report indexes.
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Invoices_Type ON Invoices(Type);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Invoices_CustomerId ON Invoices(CustomerId);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Invoices_SupplierId ON Invoices(SupplierId);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_InvoiceItems_ProductId ON InvoiceItems(ProductId);");

            // 13-16: check indexes.
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Checks_DueDate ON Checks(DueDate);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Checks_Status ON Checks(Status);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Checks_CustomerId ON Checks(CustomerId);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Checks_SupplierId ON Checks(SupplierId);");

            // 17-20: return/draft/audit indexes.
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_SalesReturns_CustomerId ON SalesReturns(CustomerId);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_PurchaseReturns_SupplierId ON PurchaseReturns(SupplierId);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_DraftInvoiceItems_ProductId ON DraftInvoiceItems(ProductId);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_AuditLog_Entity ON AuditLog(Entity,EntityId);");

            tx.Commit();
        }
        catch
        {
            try { tx.Rollback(); } catch { }
            throw;
        }
    }

    static void EnsureSetting(SqliteConnection cn, SqliteTransaction tx, string key, string value)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT OR IGNORE INTO Settings(Key,Value,UpdatedAt) VALUES($key,$value,datetime('now'));";
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }

    static void Index(SqliteConnection cn, SqliteTransaction tx, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
