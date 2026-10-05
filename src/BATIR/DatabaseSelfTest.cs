using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class DatabaseSelfTest
{
    public static int Run()
    {
        try
        {
            using var cn = Database.Open();

            using (var integrity = cn.CreateCommand())
            {
                integrity.CommandText = "PRAGMA integrity_check;";
                var result = Convert.ToString(integrity.ExecuteScalar());
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("SQLite integrity_check failed: " + result);
            }

            using (var foreignKeys = cn.CreateCommand())
            {
                foreignKeys.CommandText = "PRAGMA foreign_key_check;";
                using var reader = foreignKeys.ExecuteReader();
                if (reader.Read())
                    throw new InvalidOperationException("SQLite foreign_key_check found an invalid reference.");
            }

            string[] views = { "v_LowStockProducts", "v_StockValuation", "v_SalesProfit", "v_CustomerBalances", "v_SupplierBalances" };
            string[] triggers = { "trg_InvoiceItems_PositiveQuantity", "trg_InvoiceItems_NonNegativeAmounts", "trg_DraftInvoiceItems_PositiveQuantity", "trg_ReturnItems_PositiveQuantity", "trg_InventoryAdjustment_ValidDifference" };
            foreach (var name in views) EnsureSchemaObject(cn, "view", name);
            foreach (var name in triggers) EnsureSchemaObject(cn, "trigger", name);

            using var query = cn.CreateCommand();
            query.CommandText = "SELECT COUNT(*) FROM Products;";
            _ = Convert.ToInt64(query.ExecuteScalar());

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("BATIR database self-test failed: " + ex.Message);
            return 1;
        }
    }

    static void EnsureSchemaObject(SqliteConnection cn, string type, string name)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type=$type AND name=$name;";
        cmd.Parameters.AddWithValue("$type", type);
        cmd.Parameters.AddWithValue("$name", name);
        if (Convert.ToInt64(cmd.ExecuteScalar()) != 1)
            throw new InvalidOperationException($"Missing SQLite {type}: {name}");
    }
}
