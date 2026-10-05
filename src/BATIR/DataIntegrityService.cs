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

            // Product units and financial transaction safeguards.
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_Products_ValidUnitFactor
BEFORE INSERT ON Products WHEN NEW.UnitConversionFactor<=0
BEGIN SELECT RAISE(ABORT,'ضریب واحد باید بزرگتر از صفر باشد.'); END;");
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_Products_ValidUnitFactorUpdate
BEFORE UPDATE OF UnitConversionFactor ON Products WHEN NEW.UnitConversionFactor<=0
BEGIN SELECT RAISE(ABORT,'ضریب واحد باید بزرگتر از صفر باشد.'); END;");
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_CashTransactions_PositiveAmount
BEFORE INSERT ON CashTransactions WHEN NEW.Amount<=0
BEGIN SELECT RAISE(ABORT,'مبلغ عملیات مالی باید بیشتر از صفر باشد.'); END;");

            // 36-40: database-level validation safeguards.
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_InvoiceItems_PositiveQuantity
BEFORE INSERT ON InvoiceItems WHEN NEW.Quantity<=0
BEGIN SELECT RAISE(ABORT,'تعداد قلم فاکتور باید بیشتر از صفر باشد.'); END;");

            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_InvoiceItems_NonNegativeAmounts
BEFORE INSERT ON InvoiceItems WHEN NEW.UnitPrice<0 OR NEW.CostPrice<0 OR NEW.Discount<0
BEGIN SELECT RAISE(ABORT,'قیمت، بهای تمام‌شده و تخفیف نمی‌توانند منفی باشند.'); END;");

            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_DraftInvoiceItems_PositiveQuantity
BEFORE INSERT ON DraftInvoiceItems WHEN NEW.Quantity<=0
BEGIN SELECT RAISE(ABORT,'تعداد پیش‌نویس باید بیشتر از صفر باشد.'); END;");

            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_ReturnItems_PositiveQuantity
BEFORE INSERT ON SalesReturnItems WHEN NEW.Quantity<=0
BEGIN SELECT RAISE(ABORT,'تعداد برگشت فروش باید بیشتر از صفر باشد.'); END;");

            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_InventoryAdjustment_ValidDifference
BEFORE INSERT ON InventoryAdjustments WHEN NEW.Difference != (NEW.CountedStock-NEW.BeforeStock)
BEGIN SELECT RAISE(ABORT,'اختلاف انبارگردانی با موجودی قبل و شمارش واقعی همخوانی ندارد.'); END;");

            // 41-45: persistent inventory movement ledger. This is the foundation for
            // weighted-average costing, stock history and auditable inventory reports.
            Table(cn, tx, @"CREATE TABLE IF NOT EXISTS InventoryMovements(
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 DateText TEXT NOT NULL,
 ProductId INTEGER NOT NULL,
 Quantity INTEGER NOT NULL,
 Direction INTEGER NOT NULL,
 UnitCost INTEGER NOT NULL DEFAULT 0,
 ReferenceType TEXT NOT NULL,
 ReferenceId INTEGER,
 ReferenceItemId INTEGER,
 Notes TEXT,
 FOREIGN KEY(ProductId) REFERENCES Products(Id));");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_InventoryMovements_ProductDate ON InventoryMovements(ProductId,DateText);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_InventoryMovements_Reference ON InventoryMovements(ReferenceType,ReferenceId);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_InventoryMovements_ProductId ON InventoryMovements(ProductId);");

            // Sale: stock leaves the store. Purchase: stock enters the store.
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_InventoryMovement_InvoiceItem
AFTER INSERT ON InvoiceItems
BEGIN
  INSERT INTO InventoryMovements(DateText,ProductId,Quantity,Direction,UnitCost,ReferenceType,ReferenceId,ReferenceItemId,Notes)
  SELECT i.DateText,NEW.ProductId,NEW.Quantity,
         CASE WHEN i.Type='Purchase' THEN 1 ELSE -1 END,
         NEW.CostPrice,
         CASE WHEN i.Type='Purchase' THEN 'PurchaseInvoice' ELSE 'SaleInvoice' END,
         NEW.InvoiceId,NEW.Id,
         'ثبت خودکار از قلم فاکتور';
END;");

            // Sales return puts goods back into stock.
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_InventoryMovement_SalesReturn
AFTER INSERT ON SalesReturnItems
BEGIN
  INSERT INTO InventoryMovements(DateText,ProductId,Quantity,Direction,UnitCost,ReferenceType,ReferenceId,ReferenceItemId,Notes)
  SELECT r.DateText,NEW.ProductId,NEW.Quantity,1,0,'SalesReturn',NEW.ReturnId,NEW.Id,'ثبت خودکار برگشت از فروش';
END;");

            // Purchase return removes goods from stock.
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_InventoryMovement_PurchaseReturn
AFTER INSERT ON PurchaseReturnItems
BEGIN
  INSERT INTO InventoryMovements(DateText,ProductId,Quantity,Direction,UnitCost,ReferenceType,ReferenceId,ReferenceItemId,Notes)
  SELECT r.DateText,NEW.ProductId,NEW.Quantity,-1,NEW.CostPrice,'PurchaseReturn',NEW.ReturnId,NEW.Id,'ثبت خودکار برگشت از خرید';
END;");

            // Stocktaking records the exact correction rather than only the final stock.
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_InventoryMovement_Adjustment
AFTER INSERT ON InventoryAdjustments
WHEN NEW.Difference != 0
BEGIN
  INSERT INTO InventoryMovements(DateText,ProductId,Quantity,Direction,UnitCost,ReferenceType,ReferenceId,ReferenceItemId,Notes)
  SELECT NEW.DateText,NEW.ProductId,ABS(NEW.Difference),CASE WHEN NEW.Difference>0 THEN 1 ELSE -1 END,
         COALESCE((SELECT PurchasePrice FROM Products WHERE Id=NEW.ProductId),0),
         'InventoryAdjustment',NEW.Id,NULL,NEW.Note;
END;");

            // 47-49: one-time reconstruction of the movement ledger for databases that already
            // contain invoices/returns/stocktakes. This makes the ledger useful immediately after upgrade.
            EnsureInventoryLedger(cn, tx);

            View(cn, tx, @"CREATE VIEW IF NOT EXISTS v_InventoryReconciliation AS
SELECT p.Id,p.Name,p.Stock,
       COALESCE(SUM(m.Direction*m.Quantity),0) AS LedgerStock,
       p.Stock-COALESCE(SUM(m.Direction*m.Quantity),0) AS Difference
FROM Products p LEFT JOIN InventoryMovements m ON m.ProductId=p.Id
GROUP BY p.Id,p.Name,p.Stock;");

            // 50-52: cash-shift control ledger for daily cashier closing and handover.
            Table(cn, tx, @"CREATE TABLE IF NOT EXISTS CashShifts(
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 Name TEXT NOT NULL,
 OpenedAt TEXT NOT NULL,
 ClosedAt TEXT,
 OpeningCash INTEGER NOT NULL DEFAULT 0,
 ExpectedCash INTEGER NOT NULL DEFAULT 0,
 ClosingCash INTEGER NOT NULL DEFAULT 0,
 Difference INTEGER NOT NULL DEFAULT 0,
 Status TEXT NOT NULL DEFAULT 'Open',
 Notes TEXT
);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_CashShifts_Status ON CashShifts(Status);");
            Index(cn, tx, "CREATE INDEX IF NOT EXISTS IX_CashShifts_OpenedAt ON CashShifts(OpenedAt);");

            // 53: prevent invalid negative cash-shift amounts.
            Trigger(cn, tx, @"CREATE TRIGGER IF NOT EXISTS trg_CashShifts_NonNegative
BEFORE INSERT ON CashShifts
WHEN NEW.OpeningCash<0 OR NEW.ExpectedCash<0 OR NEW.ClosingCash<0
BEGIN SELECT RAISE(ABORT,'مبالغ شیفت نمی‌توانند منفی باشند.'); END;");

            // 46: a compact movement summary for reports and future weighted-average costing.
            View(cn, tx, @"CREATE VIEW IF NOT EXISTS v_InventoryMovementSummary AS
SELECT ProductId,
       SUM(CASE WHEN Direction=1 THEN Quantity ELSE 0 END) AS TotalIn,
       SUM(CASE WHEN Direction=-1 THEN Quantity ELSE 0 END) AS TotalOut,
       SUM(Direction*Quantity) AS NetQuantity,
       SUM(CASE WHEN Direction=1 THEN Quantity*UnitCost ELSE 0 END) AS InValue,
       COUNT(*) AS MovementCount
FROM InventoryMovements GROUP BY ProductId;");

            tx.Commit();
        }
        catch
        {
            try { tx.Rollback(); } catch { }
            throw;
        }
    }


    static void EnsureInventoryLedger(SqliteConnection cn, SqliteTransaction tx)
    {
        using var check = cn.CreateCommand();
        check.Transaction = tx;
        check.CommandText = "SELECT Value FROM Settings WHERE Key='InventoryLedgerRebuilt' LIMIT 1;";
        var state = Convert.ToString(check.ExecuteScalar());
        if (string.Equals(state, "true", StringComparison.OrdinalIgnoreCase)) return;

        using (var clear = cn.CreateCommand())
        {
            clear.Transaction = tx;
            clear.CommandText = "DELETE FROM InventoryMovements;";
            clear.ExecuteNonQuery();
        }

        InsertLedger(cn, tx, @"INSERT INTO InventoryMovements(DateText,ProductId,Quantity,Direction,UnitCost,ReferenceType,ReferenceId,ReferenceItemId,Notes)
SELECT i.DateText,ii.ProductId,ii.Quantity,CASE WHEN i.Type='Purchase' THEN 1 ELSE -1 END,
       ii.CostPrice,CASE WHEN i.Type='Purchase' THEN 'PurchaseInvoice' ELSE 'SaleInvoice' END,
       i.Id,ii.Id,'بازسازی سابقه از فاکتور' FROM InvoiceItems ii JOIN Invoices i ON i.Id=ii.InvoiceId
WHERE i.Type IN ('Purchase','Sale');");

        InsertLedger(cn, tx, @"INSERT INTO InventoryMovements(DateText,ProductId,Quantity,Direction,UnitCost,ReferenceType,ReferenceId,ReferenceItemId,Notes)
SELECT r.DateText,ri.ProductId,ri.Quantity,1,0,'SalesReturn',r.Id,ri.Id,'بازسازی سابقه برگشت فروش'
FROM SalesReturnItems ri JOIN SalesReturns r ON r.Id=ri.ReturnId;");

        InsertLedger(cn, tx, @"INSERT INTO InventoryMovements(DateText,ProductId,Quantity,Direction,UnitCost,ReferenceType,ReferenceId,ReferenceItemId,Notes)
SELECT r.DateText,ri.ProductId,ri.Quantity,-1,ri.CostPrice,'PurchaseReturn',r.Id,ri.Id,'بازسازی سابقه برگشت خرید'
FROM PurchaseReturnItems ri JOIN PurchaseReturns r ON r.Id=ri.ReturnId;");

        InsertLedger(cn, tx, @"INSERT INTO InventoryMovements(DateText,ProductId,Quantity,Direction,UnitCost,ReferenceType,ReferenceId,ReferenceItemId,Notes)
SELECT a.DateText,a.ProductId,ABS(a.Difference),CASE WHEN a.Difference>0 THEN 1 ELSE -1 END,
       COALESCE(p.PurchasePrice,0),'InventoryAdjustment',a.Id,NULL,a.Note
FROM InventoryAdjustments a JOIN Products p ON p.Id=a.ProductId WHERE a.Difference<>0;");

        InsertLedger(cn, tx, @"INSERT INTO InventoryMovements(DateText,ProductId,Quantity,Direction,UnitCost,ReferenceType,ReferenceId,ReferenceItemId,Notes)
SELECT COALESCE(p.CreatedAt,datetime('now')),p.Id,ABS(p.Stock-COALESCE(m.NetQty,0)),
       CASE WHEN p.Stock-COALESCE(m.NetQty,0)>0 THEN 1 ELSE -1 END,
       p.PurchasePrice,'OpeningBalance',NULL,NULL,'موجودی افتتاحیه/تطبیق اولیه'
FROM Products p LEFT JOIN (
  SELECT ProductId,SUM(Direction*Quantity) AS NetQty FROM InventoryMovements GROUP BY ProductId
) m ON m.ProductId=p.Id
WHERE p.Stock-COALESCE(m.NetQty,0)<>0;");

        using var set = cn.CreateCommand();
        set.Transaction = tx;
        set.CommandText = "INSERT INTO Settings(Key,Value,UpdatedAt) VALUES('InventoryLedgerRebuilt','true',datetime('now')) ON CONFLICT(Key) DO UPDATE SET Value='true',UpdatedAt=datetime('now');";
        set.ExecuteNonQuery();
    }

    static void InsertLedger(SqliteConnection cn, SqliteTransaction tx, string sql)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
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

    static void Table(SqliteConnection cn, SqliteTransaction tx, string sql)
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
