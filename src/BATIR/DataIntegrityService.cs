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

            // 21-30: additional high-use indexes for real-world searches and ledgers.
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Customers_Phone ON Customers(Phone);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Customers_Name ON Customers(Name);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Suppliers_Phone ON Suppliers(Phone);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Suppliers_Name ON Suppliers(Name);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Invoices_InvoiceNoType ON Invoices(InvoiceNo,Type);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_SalesReturns_OriginalInvoice ON SalesReturns(OriginalInvoiceId);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_PurchaseReturns_OriginalInvoice ON PurchaseReturns(OriginalInvoiceId);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_CashTransactions_Reference ON CashTransactions(ReferenceType,ReferenceId);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_AuditLog_EntityDate ON AuditLog(Entity,EntityId,DateText);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_Settings_UpdatedAt ON Settings(UpdatedAt);");

            // 31: ready-to-use low-stock report view.
            View(cn, tx, @"CREATE VIEW IF NOT EXISTS v_LowStockProducts AS
SELECT Id,Code,Barcode,Name,Brand,Category,Stock,MinStock,PurchasePrice,SalePrice
FROM Products WHERE Active=1 AND Stock<=MinStock;");

            // 32: stock valuation view using current purchase cost.
            View(cn, tx, @"CREATE VIEW IF NOT EXISTS v_StockValuation AS
SELECT Id,Name,Stock,PurchasePrice,SalePrice,
       (CAST(Stock AS INTEGER) * CAST(PurchasePrice AS INTEGER)) AS PurchaseValue,
       (CAST(Stock AS INTEGER) * CAST(SalePrice AS INTEGER)) AS SaleValue
FROM Products WHERE Active=1;");

            // 33: per-item realized sales profit view.
            View(cn, tx, @"CREATE VIEW IF NOT EXISTS v_SalesProfit AS
SELECT ii.Id,ii.InvoiceId,ii.ProductId,p.Name AS ProductName,ii.Quantity,
       ii.UnitPrice,ii.CostPrice,ii.Discount,
       ((ii.UnitPrice*ii.Quantity)-ii.Discount) AS NetSales,
       (((ii.UnitPrice-ii.CostPrice)*ii.Quantity)-ii.Discount) AS Profit,
       i.InvoiceNo,i.DateText,i.CustomerId
FROM InvoiceItems ii JOIN Products p ON p.Id=ii.ProductId
JOIN Invoices i ON i.Id=ii.InvoiceId WHERE i.Type='Sale';");

            // 34: customer balance report with invoice and receipt totals.
            View(cn, tx, @"CREATE VIEW IF NOT EXISTS v_CustomerBalances AS
SELECT c.Id,c.Name,c.Phone,c.CreditLimit,c.Balance,
       COALESCE((SELECT SUM(i.Total) FROM Invoices i WHERE i.CustomerId=c.Id AND i.Type='Sale'),0) AS SalesTotal,
       COALESCE((SELECT SUM(ct.Amount) FROM CashTransactions ct WHERE ct.CustomerId=c.Id AND ct.Type='CustomerReceipt'),0) AS ReceiptsTotal
FROM Customers c;");

            // 35: supplier balance report with purchase and payment totals.
            View(cn, tx, @"CREATE VIEW IF NOT EXISTS v_SupplierBalances AS
SELECT s.Id,s.Name,s.Phone,s.Balance,
       COALESCE((SELECT SUM(i.Total) FROM Invoices i WHERE i.SupplierId=s.Id AND i.Type='Purchase'),0) AS PurchaseTotal,
       COALESCE((SELECT SUM(ct.Amount) FROM CashTransactions ct WHERE ct.SupplierId=s.Id AND ct.Type='SupplierPayment'),0) AS PaymentsTotal
FROM Suppliers s;");

            // 36: prevent impossible invoice quantities at database level.
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_InvoiceItems_PositiveQuantity
BEFORE INSERT ON InvoiceItems WHEN NEW.Quantity<=0
BEGIN SELECT RAISE(ABORT,'تعداد قلم فاکتور باید بیشتر از صفر باشد.'); END;");

            // 37: prevent negative invoice prices and costs.
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_InvoiceItems_NonNegativeAmounts
BEFORE INSERT ON InvoiceItems WHEN NEW.UnitPrice<0 OR NEW.CostPrice<0 OR NEW.Discount<0
BEGIN SELECT RAISE(ABORT,'قیمت، بهای تمام‌شده و تخفیف نمی‌توانند منفی باشند.'); END;");

            // 38: prevent invalid draft rows that could break crash recovery.
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_DraftInvoiceItems_PositiveQuantity
BEFORE INSERT ON DraftInvoiceItems WHEN NEW.Quantity<=0
BEGIN SELECT RAISE(ABORT,'تعداد پیش‌نویس باید بیشتر از صفر باشد.'); END;");

            // 39: prevent invalid return quantities.
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_ReturnItems_PositiveQuantity
BEFORE INSERT ON SalesReturnItems WHEN NEW.Quantity<=0
BEGIN SELECT RAISE(ABORT,'تعداد برگشت فروش باید بیشتر از صفر باشد.'); END;");

            // 40: keep stocktaking differences mathematically consistent.
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_InventoryAdjustment_ValidDifference
BEFORE INSERT ON InventoryAdjustments WHEN NEW.Difference != (NEW.CountedStock-NEW.BeforeStock)
BEGIN SELECT RAISE(ABORT,'اختلاف انبارگردانی با موجودی قبل و شمارش واقعی همخوانی ندارد.'); END;");

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

    static void View(SqliteConnection cn, SqliteTransaction tx, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    static void Trigger(SqliteConnection cn, SqliteTransaction tx, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
