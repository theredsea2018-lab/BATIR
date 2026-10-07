using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class Operational100SelfTest
{
    public static int Run()
    {
        try
        {
            using var cn = Database.Open();
            AdvancedFeaturesMigration.Run(cn);
            for (var round = 1; round <= 5; round++)
            {
                using var tx = cn.BeginTransaction();
                var suffix = Guid.NewGuid().ToString("N");
                var key = "BATIR_TEST_" + suffix;
                long customerId;
                long productId;
                long invoiceId;

                Exec(cn, tx, "INSERT INTO AuditLog(DateText,UserName,Action,Entity,Details) VALUES(datetime('now'),'self-test','100-op','Operational100',@d);", ("@d", "round-" + round)); // 1
                var auditId = Scalar(cn, tx, "SELECT last_insert_rowid();"); // 2
                Exec(cn, tx, "UPDATE AuditLog SET Details=@d WHERE Id=@id;", ("@d", "round-" + round + "-updated"), ("@id", auditId)); // 3
                Require((TextScalar(cn, tx, "SELECT Details FROM AuditLog WHERE Id=@id;", ("@id", auditId)) ?? "").EndsWith("-updated"), "Audit update"); // 4

                Exec(cn, tx, "INSERT INTO Settings(Key,Value,UpdatedAt) VALUES(@k,'one',datetime('now'));", ("@k", key)); // 5
                Require((TextScalar(cn, tx, "SELECT Value FROM Settings WHERE Key=@k;", ("@k", key)) ?? "") == "one", "Setting insert"); // 6
                Exec(cn, tx, "UPDATE Settings SET Value='two',UpdatedAt=datetime('now') WHERE Key=@k;", ("@k", key)); // 7
                Require((TextScalar(cn, tx, "SELECT Value FROM Settings WHERE Key=@k;", ("@k", key)) ?? "") == "two", "Setting update"); // 8
                Exec(cn, tx, "DELETE FROM Settings WHERE Key=@k;", ("@k", key)); // 9
                Require(Convert.ToInt64(Scalar(cn, tx, "SELECT COUNT(*) FROM Settings WHERE Key=@k;", ("@k", key))) == 0, "Setting delete"); // 10

                Exec(cn, tx, "INSERT INTO Customers(Name,Phone,Notes) VALUES(@n,@p,@note);", ("@n", "تست ۱۰۰ عملیات " + round), ("@p", "000"), ("@note", suffix)); // 11
                customerId = Scalar(cn, tx, "SELECT last_insert_rowid();"); // 12
                Exec(cn, tx, "INSERT INTO Products(Code,Barcode,Name,PurchasePrice,SalePrice,Stock,MinStock,CreatedAt,UnitName) VALUES(@c,@b,@n,100,150,10,2,datetime('now'),'عدد');", ("@c", "TEST-" + suffix), ("@b", "TESTBAR-" + suffix), ("@n", "محصول تست " + round)); // 13
                productId = Scalar(cn, tx, "SELECT last_insert_rowid();"); // 14
                Exec(cn, tx, "INSERT INTO Invoices(InvoiceNo,Type,CustomerId,UserName,DateText,Total,Paid,Notes) VALUES(@no,'فروش',@customer,'self-test',datetime('now'),150,0,@note);", ("@no", "TEST-" + suffix), ("@customer", customerId), ("@note", suffix)); // 15
                invoiceId = Scalar(cn, tx, "SELECT last_insert_rowid();"); // 16
                Exec(cn, tx, "INSERT INTO InvoiceItems(InvoiceId,ProductId,Quantity,UnitPrice,CostPrice,Discount) VALUES(@invoice,@product,1,150,100,0);", ("@invoice", invoiceId), ("@product", productId)); // 17
                Require(Convert.ToInt64(Scalar(cn, tx, "SELECT COUNT(*) FROM InvoiceItems WHERE InvoiceId=@invoice AND ProductId=@product;", ("@invoice", invoiceId))) == 1, "Invoice item insert"); // 18
                Require(Convert.ToInt64(Scalar(cn, tx, "SELECT COUNT(*) FROM InvoiceItems i JOIN Invoices v ON v.Id=i.InvoiceId WHERE v.CustomerId=@customer;", ("@customer", customerId))) == 1, "Invoice join"); // 19
                Exec(cn, tx, "DELETE FROM Invoices WHERE Id=@id;", ("@id", invoiceId)); // 20
                tx.Rollback();
            }
            Console.WriteLine("BATIR 100-operation smoke test passed: 5 rounds × 20 transactional operations.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("BATIR 100-operation smoke test failed: " + ex.Message);
            return 1;
        }
    }

    static void Exec(SqliteConnection cn, SqliteTransaction tx, string sql, params (string Key, object Value)[] parameters)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    static string? TextScalar(SqliteConnection cn, SqliteTransaction tx, string sql, params (string Key, object Value)[] parameters)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
        var value = cmd.ExecuteScalar();
        return value == null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    static long Scalar(SqliteConnection cn, SqliteTransaction tx, string sql, params (string Key, object Value)[] parameters)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("100-operation check failed: " + name);
    }
}
