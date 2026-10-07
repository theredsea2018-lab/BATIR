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
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("SQLite integrity_check failed: " + result);
            }
            using (var foreignKeys = cn.CreateCommand())
            {
                foreignKeys.CommandText = "PRAGMA foreign_key_check;";
                using var reader = foreignKeys.ExecuteReader();
                if (reader.Read()) throw new InvalidOperationException("SQLite foreign_key_check found an invalid reference.");
            }

            string[] tables = { "AuditLog", "DraftInvoice", "DraftInvoiceItems", "InventoryMovements", "CashShifts", "Settings", "UserRoles", "UserPermissions", "ApprovalRequests", "BankAccounts", "BankTransactions", "BankReconciliations", "Budgets", "Orders", "OrderItems", "Reminders", "FixedAssets", "TaxSettings", "TaxDocuments", "VoiceCommands", "OcrDocuments", "PriceChangeHistory", "DraftInvoiceCollaborators", "DraftInvoiceLocks" };
            foreach (var name in tables) EnsureSchemaObject(cn, "table", name);
            string[] views = { "v_LowStockProducts", "v_StockValuation", "v_SalesProfit", "v_CustomerBalances", "v_SupplierBalances", "v_InventoryMovementSummary", "v_InventoryReconciliation" };
            string[] triggers = { "trg_InvoiceItems_PositiveQuantity", "trg_InvoiceItems_NonNegativeAmounts", "trg_DraftInvoiceItems_PositiveQuantity", "trg_ReturnItems_PositiveQuantity", "trg_InventoryAdjustment_ValidDifference", "trg_InventoryMovement_InvoiceItem", "trg_InventoryMovement_SalesReturn", "trg_InventoryMovement_PurchaseReturn", "trg_InventoryMovement_Adjustment", "trg_Products_ValidUnitFactor", "trg_Products_ValidUnitFactorUpdate", "trg_CashTransactions_PositiveAmount", "trg_CashShifts_NonNegative" };
            foreach (var name in views) EnsureSchemaObject(cn, "view", name);
            foreach (var name in triggers) EnsureSchemaObject(cn, "trigger", name);

            using (var reconciliation = cn.CreateCommand())
            {
                reconciliation.CommandText = "SELECT COUNT(*) FROM v_InventoryReconciliation WHERE Difference<>0;";
                var mismatches = Convert.ToInt64(reconciliation.ExecuteScalar());
                if (mismatches != 0) throw new InvalidOperationException("Inventory ledger reconciliation found " + mismatches + " product mismatch(es).");
            }
            using (var settings = cn.CreateCommand())
            {
                settings.CommandText = "SELECT COUNT(*) FROM Settings WHERE Key IN ('InvoiceAutosave','BackupVerification','AutoLockMinutes','CurrencyName','DateCalendar','CurrentRoleId');";
                if (Convert.ToInt64(settings.ExecuteScalar()) < 6) throw new InvalidOperationException("Safety settings or active role setting are incomplete.");
            }
            using (var baseline = cn.CreateCommand())
            {
                baseline.CommandText = "SELECT Value FROM Settings WHERE Key='BATIRSpecVersion' LIMIT 1;";
                if (Convert.ToString(baseline.ExecuteScalar()) != "1.0") throw new InvalidOperationException("BATIR stable specification baseline is missing.");
            }
            using (var roles = cn.CreateCommand())
            {
                roles.CommandText = "SELECT COUNT(*) FROM UserRoles WHERE Name IN ('مدیر','فروشنده','انباردار');";
                if (Convert.ToInt64(roles.ExecuteScalar()) < 3) throw new InvalidOperationException("Default roles are incomplete.");
            }
            using (var manager = cn.CreateCommand())
            {
                manager.CommandText = "SELECT COUNT(*) FROM UserPermissions p JOIN UserRoles r ON r.Id=p.RoleId WHERE r.Name='مدیر' AND p.Allowed=1;";
                if (Convert.ToInt64(manager.ExecuteScalar()) < 17) throw new InvalidOperationException("Manager permissions are incomplete.");
            }
            using (var query = cn.CreateCommand()) { query.CommandText = "SELECT COUNT(*) FROM Products;"; _ = Convert.ToInt64(query.ExecuteScalar()); }
            using (var units = cn.CreateCommand())
            {
                units.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Products') WHERE name IN ('UnitName','SecondaryUnitName','UnitConversionFactor');";
                if (Convert.ToInt64(units.ExecuteScalar()) != 3) throw new InvalidOperationException("Product unit schema is incomplete.");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("BATIR database self-test failed: " + ex.Message); return 1;
        }
    }

    static void EnsureSchemaObject(SqliteConnection cn, string type, string name)
    {
        using var cmd = cn.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type=$type AND name=$name;";
        cmd.Parameters.AddWithValue("$type", type); cmd.Parameters.AddWithValue("$name", name);
        if (Convert.ToInt64(cmd.ExecuteScalar()) != 1) throw new InvalidOperationException($"Missing SQLite {type}: {name}");
    }
}
