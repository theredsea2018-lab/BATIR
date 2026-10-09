using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class Stage2SelfTest
{
    public static int Run()
    {
        try
        {
            using var cn = Database.Open();
            AdvancedFeaturesMigration.Run(cn);
            using (var integrity = cn.CreateCommand())
            {
                integrity.CommandText = "PRAGMA integrity_check;";
                if (!string.Equals(Convert.ToString(integrity.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("SQLite integrity check failed before Stage 2 tests.");
            }

            long productId;
            using (var insert = cn.CreateCommand())
            {
                insert.CommandText = @"INSERT INTO Products(Code,Barcode,Name,Brand,Category,PurchasePrice,SalePrice,Stock,MinStock,Active,CreatedAt,UnitName)
VALUES('STAGE2-TEST','STAGE2-TEST-BAR','Stage 2 report test','BATIR','Test',100,150,2,5,1,datetime('now'),'عدد');
SELECT last_insert_rowid();";
                productId = Convert.ToInt64(insert.ExecuteScalar());
            }

            // Execute each report query used by Stage2ReportsForm against the migrated schema.
            AssertQuery(cn, @"SELECT ProductName,SUM(Quantity),SUM(NetSales),SUM(CostPrice*Quantity),SUM(Profit)
FROM v_SalesProfit GROUP BY ProductId,ProductName;", "profit report");
            AssertQuery(cn, @"SELECT p.Id,p.Name,p.Stock,
COALESCE(SUM(CASE WHEN m.Direction=1 THEN m.Quantity ELSE 0 END),0)
FROM Products p LEFT JOIN InventoryMovements m ON m.ProductId=p.Id
WHERE p.Active=1 GROUP BY p.Id,p.Name,p.Stock,p.PurchasePrice;", "weighted cost report");
            using (var alert = cn.CreateCommand())
            {
                alert.CommandText = @"SELECT COUNT(*) FROM Products WHERE Id=@id AND Active=1 AND (Stock<=MinStock OR SalePrice<PurchasePrice);";
                alert.Parameters.AddWithValue("@id", productId);
                if (Convert.ToInt64(alert.ExecuteScalar()) != 1)
                    throw new InvalidOperationException("Low-stock report failed to identify the test product.");
            }
            AssertQuery(cn, @"SELECT m.DateText,p.Name,m.Quantity,m.Direction,m.UnitCost,m.ReferenceType,m.ReferenceId,m.Notes
FROM InventoryMovements m JOIN Products p ON p.Id=m.ProductId ORDER BY m.DateText DESC LIMIT 2000;", "inventory movement report");
            AssertQuery(cn, @"SELECT Id,Name,OpenedAt,ClosedAt,OpeningCash,ExpectedCash,ClosingCash,Difference,Status
FROM CashShifts ORDER BY Id DESC;", "cash shift report");

            // Exercise the same transactional price history contract used by bulk pricing and undo.
            var batch = Guid.NewGuid().ToString("N");
            using (var tx = cn.BeginTransaction())
            {
                using (var history = cn.CreateCommand())
                {
                    history.Transaction = tx;
                    history.CommandText = @"INSERT INTO PriceChangeHistory(BatchId,ProductId,ChangedAt,UserName,Mode,OldPurchasePrice,NewPurchasePrice,OldSalePrice,NewSalePrice)
VALUES(@batch,@id,@date,'test','قیمت فروش',100,100,150,180);";
                    history.Parameters.AddWithValue("@batch", batch);
                    history.Parameters.AddWithValue("@id", productId);
                    history.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    history.ExecuteNonQuery();
                }
                using (var update = cn.CreateCommand())
                {
                    update.Transaction = tx;
                    update.CommandText = "UPDATE Products SET SalePrice=180 WHERE Id=@id;";
                    update.Parameters.AddWithValue("@id", productId);
                    if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Bulk price update did not affect the test product.");
                }
                tx.Commit();
            }

            using (var undo = cn.CreateCommand())
            {
                undo.CommandText = @"UPDATE Products SET PurchasePrice=(SELECT OldPurchasePrice FROM PriceChangeHistory WHERE BatchId=@batch AND ProductId=Products.Id ORDER BY Id DESC LIMIT 1),
SalePrice=(SELECT OldSalePrice FROM PriceChangeHistory WHERE BatchId=@batch AND ProductId=Products.Id ORDER BY Id DESC LIMIT 1)
WHERE Id IN (SELECT ProductId FROM PriceChangeHistory WHERE BatchId=@batch);";
                undo.Parameters.AddWithValue("@batch", batch);
                if (undo.ExecuteNonQuery() != 1) throw new InvalidOperationException("Price history undo did not restore the test product.");
            }
            using (var verify = cn.CreateCommand())
            {
                verify.CommandText = "SELECT PurchasePrice*1000000+SalePrice FROM Products WHERE Id=@id;";
                verify.Parameters.AddWithValue("@id", productId);
                if (Convert.ToInt64(verify.ExecuteScalar()) != 100000150L)
                    throw new InvalidOperationException("Price history undo restored incorrect prices.");
            }
            using (var cleanup = cn.CreateCommand())
            {
                cleanup.CommandText = "DELETE FROM PriceChangeHistory WHERE BatchId=@batch; DELETE FROM Products WHERE Id=@id;";
                cleanup.Parameters.AddWithValue("@batch", batch);
                cleanup.Parameters.AddWithValue("@id", productId);
                cleanup.ExecuteNonQuery();
            }

            using (var integrity = cn.CreateCommand())
            {
                integrity.CommandText = "PRAGMA foreign_key_check;";
                using var reader = integrity.ExecuteReader();
                if (reader.Read()) throw new InvalidOperationException("Foreign-key check failed after Stage 2 tests.");
            }

            Console.WriteLine("BATIR Stage 2 tests passed: management report queries, stock alert, price history/undo, and database integrity.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("BATIR Stage 2 test failed: " + ex);
            return 1;
        }
    }

    private static void AssertQuery(SqliteConnection cn, string sql, string name)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) { }
        Console.WriteLine("Stage 2 query OK: " + name);
    }
}
