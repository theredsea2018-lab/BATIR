using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class NetworkLedgerSyncService
{
    public const int Port = 47822;
    const long MaxPayloadBytes = 16L * 1024 * 1024;
    const string Magic = "BATL";

    [DataContract]
    sealed class SyncBatch
    {
        [DataMember(Order = 1)]
        public List<SyncInvoicePacket> Operations { get; set; } = new();
    }

    [DataContract]
    sealed class SyncInvoicePacket
    {
        [DataMember(Order = 1)] public string OperationId { get; set; } = "";
        [DataMember(Order = 2)] public string DeviceId { get; set; } = "";
        [DataMember(Order = 3)] public string InvoiceExternalId { get; set; } = "";
        [DataMember(Order = 4)] public string InvoiceNo { get; set; } = "";
        [DataMember(Order = 5)] public string Type { get; set; } = "";
        [DataMember(Order = 6)] public string DateText { get; set; } = "";
        [DataMember(Order = 7)] public long Total { get; set; }
        [DataMember(Order = 8)] public long Paid { get; set; }
        [DataMember(Order = 9)] public string Notes { get; set; } = "";
        [DataMember(Order = 10)] public string CustomerExternalId { get; set; } = "";
        [DataMember(Order = 11)] public string CustomerName { get; set; } = "";
        [DataMember(Order = 12)] public string SupplierExternalId { get; set; } = "";
        [DataMember(Order = 13)] public string SupplierName { get; set; } = "";
        [DataMember(Order = 14)] public string PaymentMethod { get; set; } = "نقدی";
        [DataMember(Order = 15)] public List<SyncProductPacket> Items { get; set; } = new();
    }

    [DataContract]
    sealed class SyncProductPacket
    {
        [DataMember(Order = 1)] public string ExternalId { get; set; } = "";
        [DataMember(Order = 2)] public string Name { get; set; } = "";
        [DataMember(Order = 3)] public string Barcode { get; set; } = "";
        [DataMember(Order = 4)] public string Code { get; set; } = "";
        [DataMember(Order = 5)] public string Brand { get; set; } = "";
        [DataMember(Order = 6)] public string Category { get; set; } = "";
        [DataMember(Order = 7)] public long PurchasePrice { get; set; }
        [DataMember(Order = 8)] public long SalePrice { get; set; }
        [DataMember(Order = 9)] public string UnitName { get; set; } = "عدد";
        [DataMember(Order = 10)] public string SecondaryUnitName { get; set; } = "";
        [DataMember(Order = 11)] public long UnitConversionFactor { get; set; } = 1;
        [DataMember(Order = 12)] public long Quantity { get; set; }
        [DataMember(Order = 13)] public long UnitPrice { get; set; }
        [DataMember(Order = 14)] public long CostPrice { get; set; }
        [DataMember(Order = 15)] public long Discount { get; set; }
    }

    public static string EnsureDeviceId(SqliteConnection cn)
    {
        var existing = TextScalar(cn, null, "SELECT Value FROM Settings WHERE Key='SyncDeviceId' LIMIT 1;");
        if (!string.IsNullOrWhiteSpace(existing)) return existing!;

        var id = Guid.NewGuid().ToString("N");
        Exec(cn, null, @"INSERT INTO Settings(Key,Value,UpdatedAt) VALUES('SyncDeviceId',@id,datetime('now'))
ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value,UpdatedAt=datetime('now');", ("@id", id));
        return id;
    }

    public static void RecordInvoice(SqliteConnection cn, SqliteTransaction tx, long invoiceId)
    {
        var packet = BuildInvoicePacket(cn, tx, invoiceId);
        var json = Serialize(packet);
        Exec(cn, tx, @"INSERT INTO SyncOperations(OperationId,DeviceId,OperationType,Payload,CreatedAt,Status)
VALUES(@op,@device,'Invoice',@payload,datetime('now'),'Pending');",
            ("@op", packet.OperationId), ("@device", packet.DeviceId), ("@payload", json));
    }

    public static int PendingCount()
    {
        return Convert.ToInt32(Database.Query("SELECT COUNT(*) FROM SyncOperations WHERE Status='Pending'").Rows[0][0]);
    }

    public static async Task SendPendingAsync(string host, CancellationToken cancellationToken = default)
    {
        if (!FeatureSettings.IsEnabled("MultiComputerLedger", true))
            throw new InvalidOperationException("همگام‌سازی حساب چندکامپیوتر در تنظیمات خاموش است.");

        var batch = new SyncBatch();
        var table = Database.Query(@"SELECT Id,Payload FROM SyncOperations
WHERE Status='Pending' ORDER BY Id LIMIT 250;");
        foreach (System.Data.DataRow row in table.Rows)
        {
            var payload = Convert.ToString(row["Payload"]);
            if (string.IsNullOrWhiteSpace(payload)) continue;
            var packet = Deserialize<SyncInvoicePacket>(payload);
            if (packet != null) batch.Operations.Add(packet);
        }
        if (batch.Operations.Count == 0) throw new InvalidOperationException("سند همگام‌نشده‌ای برای ارسال وجود ندارد.");

        var bytes = SerializeBytes(batch);
        ValidatePayload(bytes);
        var hash = SHA256.HashData(bytes);
        var mac = ComputeMac(hash);

        using var client = new TcpClient();
        await client.ConnectAsync(host, Port);
        using var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(Magic), 0, 4, cancellationToken);
        await stream.WriteAsync(hash, 0, hash.Length, cancellationToken);
        await stream.WriteAsync(mac, 0, mac.Length, cancellationToken);
        await WriteInt64Async(stream, bytes.Length, cancellationToken);
        await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        await stream.FlushAsync(cancellationToken);

        var ack = new byte[4];
        await ReadExactlyAsync(stream, ack, cancellationToken);
        if (Encoding.ASCII.GetString(ack) != "OKAY")
            throw new InvalidDataException("کامپیوتر مقصد عملیات شبکه را تأیید نکرد.");

        using var cn = Database.Open();
        using var tx = cn.BeginTransaction();
        foreach (var operation in batch.Operations)
            Exec(cn, tx, "UPDATE SyncOperations SET Status='Sent' WHERE OperationId=@op AND Status='Pending';", ("@op", operation.OperationId));
        tx.Commit();
    }

    public static async Task<int> ReceiveAndApplyAsync(CancellationToken cancellationToken = default)
    {
        if (!FeatureSettings.IsEnabled("MultiComputerLedger", true))
            throw new InvalidOperationException("همگام‌سازی حساب چندکامپیوتر در تنظیمات خاموش است.");

        var listener = new TcpListener(IPAddress.Any, Port);
        listener.Start();
        try
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var magic = new byte[4];
            await ReadExactlyAsync(stream, magic, cancellationToken);
            if (Encoding.ASCII.GetString(magic) != Magic) throw new InvalidDataException("پروتکل همگام‌سازی حساب BATIR معتبر نیست.");

            var expectedHash = new byte[32];
            var expectedMac = new byte[32];
            await ReadExactlyAsync(stream, expectedHash, cancellationToken);
            await ReadExactlyAsync(stream, expectedMac, cancellationToken);

            var length = await ReadInt64Async(stream, cancellationToken);
            if (length <= 0 || length > MaxPayloadBytes) throw new InvalidDataException("حجم بسته همگام‌سازی نامعتبر است.");

            var bytes = new byte[length];
            await ReadExactlyAsync(stream, bytes, cancellationToken);
            var actualHash = SHA256.HashData(bytes);
            if (!FixedEquals(expectedHash, actualHash) || !FixedEquals(expectedMac, ComputeMac(actualHash)))
                throw new InvalidDataException("صحت یا کلید امنیتی بسته شبکه تأیید نشد.");

            var batch = Deserialize<SyncBatch>(bytes) ?? throw new InvalidDataException("محتوای بسته شبکه قابل خواندن نیست.");
            var applied = 0;
            foreach (var operation in batch.Operations)
            {
                if (ApplyInvoiceOperation(operation)) applied++;
            }

            await stream.WriteAsync(Encoding.ASCII.GetBytes("OKAY"), 0, 4, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return applied;
        }
        finally
        {
            listener.Stop();
        }
    }

    static bool ApplyInvoiceOperation(SyncInvoicePacket packet)
    {
        if (string.IsNullOrWhiteSpace(packet.OperationId) || string.IsNullOrWhiteSpace(packet.InvoiceExternalId))
            throw new InvalidDataException("شناسه عملیات یا فاکتور شبکه خالی است.");

        using var cn = Database.Open();

        var existingOperation = Convert.ToInt32(Scalar(cn, null,
            "SELECT COUNT(*) FROM SyncOperations WHERE OperationId=@op AND Status IN ('Sent','Applied');",
            ("@op", packet.OperationId)));
        if (existingOperation > 0) return false;

        var existingInvoice = Scalar(cn, null,
            "SELECT COUNT(*) FROM Invoices WHERE ExternalId=@externalId;",
            ("@externalId", packet.InvoiceExternalId));

        if (existingInvoice > 0)
        {
            Exec(cn, null, "INSERT INTO SyncOperations(OperationId,DeviceId,OperationType,Payload,CreatedAt,AppliedAt,Status) VALUES(@op,@device,'Invoice',@payload,datetime('now'),datetime('now'),'Applied') ON CONFLICT(OperationId) DO UPDATE SET Status='Applied',AppliedAt=datetime('now'),ErrorMessage=NULL;",
                ("@op", packet.OperationId), ("@device", packet.DeviceId), ("@payload", Serialize(packet)));
            return false;
        }

        Exec(cn, null, @"INSERT INTO SyncOperations(OperationId,DeviceId,OperationType,Payload,CreatedAt,Status)
VALUES(@op,@device,'Invoice',@payload,datetime('now'),'Pending')
ON CONFLICT(OperationId) DO UPDATE SET Payload=excluded.Payload,Status='Pending',ErrorMessage=NULL;",
            ("@op", packet.OperationId), ("@device", packet.DeviceId), ("@payload", Serialize(packet)));

        try
        {
            using var tx = cn.BeginTransaction();
            long? customerId = ResolveParty(cn, tx, packet.DeviceId, "Customer", packet.CustomerExternalId, packet.CustomerName);
            long? supplierId = ResolveParty(cn, tx, packet.DeviceId, "Supplier", packet.SupplierExternalId, packet.SupplierName);

            var invoiceNo = packet.InvoiceNo;
            var duplicateNo = Convert.ToInt32(Scalar(cn, tx,
                "SELECT COUNT(*) FROM Invoices WHERE InvoiceNo=@no;",
                ("@no", invoiceNo)));
            if (duplicateNo > 0) invoiceNo = invoiceNo + "-NET-" + packet.OperationId.Substring(0, 8);

            using var inv = cn.CreateCommand();
            inv.Transaction = tx;
            inv.CommandText = @"INSERT INTO Invoices(InvoiceNo,Type,CustomerId,SupplierId,UserName,DateText,Total,Paid,Notes,ExternalId)
VALUES(@no,@type,@customer,@supplier,@user,@date,@total,@paid,@notes,@externalId);
SELECT last_insert_rowid();";
            inv.Parameters.AddWithValue("@no", invoiceNo);
            inv.Parameters.AddWithValue("@type", packet.Type);
            inv.Parameters.AddWithValue("@customer", (object?)customerId ?? DBNull.Value);
            inv.Parameters.AddWithValue("@supplier", (object?)supplierId ?? DBNull.Value);
            inv.Parameters.AddWithValue("@user", "شبکه");
            inv.Parameters.AddWithValue("@date", packet.DateText);
            inv.Parameters.AddWithValue("@total", packet.Total);
            inv.Parameters.AddWithValue("@paid", packet.Paid);
            inv.Parameters.AddWithValue("@notes", packet.Notes);
            inv.Parameters.AddWithValue("@externalId", packet.InvoiceExternalId);
            var invoiceId = Convert.ToInt64(inv.ExecuteScalar());

            foreach (var item in packet.Items)
            {
                var productId = ResolveProduct(cn, tx, packet.DeviceId, item);
                var oldStock = Convert.ToInt64(Scalar(cn, tx, "SELECT Stock FROM Products WHERE Id=@id;", ("@id", productId)));
                var oldCost = Convert.ToInt64(Scalar(cn, tx, "SELECT PurchasePrice FROM Products WHERE Id=@id;", ("@id", productId)));

                if (packet.Type.Equals("Sale", StringComparison.OrdinalIgnoreCase) && !NegativeStockAllowed(cn, tx))
                {
                    if (item.Quantity > oldStock)
                        throw new InvalidOperationException("همگام‌سازی فاکتور فروش با موجودی فعلی سازگار نیست؛ عملیات در وضعیت تعارض باقی ماند.");
                }

                using var row = cn.CreateCommand();
                row.Transaction = tx;
                row.CommandText = @"INSERT INTO InvoiceItems(InvoiceId,ProductId,Quantity,UnitPrice,CostPrice,Discount)
VALUES(@invoice,@product,@qty,@price,@cost,@discount);";
                row.Parameters.AddWithValue("@invoice", invoiceId);
                row.Parameters.AddWithValue("@product", productId);
                row.Parameters.AddWithValue("@qty", item.Quantity);
                row.Parameters.AddWithValue("@price", item.UnitPrice);
                row.Parameters.AddWithValue("@cost", item.CostPrice);
                row.Parameters.AddWithValue("@discount", item.Discount);
                row.ExecuteNonQuery();

                var newStock = packet.Type.Equals("Purchase", StringComparison.OrdinalIgnoreCase)
                    ? oldStock + item.Quantity
                    : oldStock - item.Quantity;

                long newCost = oldCost;
                if (packet.Type.Equals("Purchase", StringComparison.OrdinalIgnoreCase))
                {
                    var incomingCost = Math.Max(0, item.CostPrice);
                    newCost = newStock > 0
                        ? ((oldStock * oldCost) + (item.Quantity * incomingCost)) / newStock
                        : incomingCost;
                }

                Exec(cn, tx, "UPDATE Products SET Stock=@stock,PurchasePrice=@cost WHERE Id=@id;",
                    ("@stock", newStock), ("@cost", newCost), ("@id", productId));
            }

            var outstanding = Math.Max(0, packet.Total - packet.Paid);
            if (packet.Type.Equals("Sale", StringComparison.OrdinalIgnoreCase) && customerId.HasValue && outstanding > 0)
                Exec(cn, tx, "UPDATE Customers SET Balance=Balance+@amount WHERE Id=@id;",
                    ("@amount", outstanding), ("@id", customerId.Value));
            if (packet.Type.Equals("Purchase", StringComparison.OrdinalIgnoreCase) && supplierId.HasValue && outstanding > 0)
                Exec(cn, tx, "UPDATE Suppliers SET Balance=Balance+@amount WHERE Id=@id;",
                    ("@amount", outstanding), ("@id", supplierId.Value));

            if (packet.Paid > 0)
            {
                var isSale = packet.Type.Equals("Sale", StringComparison.OrdinalIgnoreCase);
                Exec(cn, tx, @"INSERT INTO CashTransactions(DateText,Type,Amount,Description,UserName,CustomerId,SupplierId,PaymentMethod,ReferenceType,ReferenceId)
VALUES(@date,@type,@amount,@desc,'شبکه',@customer,@supplier,@method,@refType,@refId);",
                    ("@date", packet.DateText),
                    ("@type", isSale ? "SalePayment" : "SupplierPayment"),
                    ("@amount", packet.Paid),
                    ("@desc", "همگام‌سازی فاکتور " + invoiceNo),
                    ("@customer", (object?)customerId ?? DBNull.Value),
                    ("@supplier", (object?)supplierId ?? DBNull.Value),
                    ("@method", string.IsNullOrWhiteSpace(packet.PaymentMethod) ? "نقدی" : packet.PaymentMethod),
                    ("@refType", isSale ? "SaleInvoice" : "Purchase"),
                    ("@refId", invoiceId));
            }

            Exec(cn, tx, "UPDATE SyncOperations SET Status='Applied',AppliedAt=datetime('now'),ErrorMessage=NULL WHERE OperationId=@op;",
                ("@op", packet.OperationId));

            tx.Commit();
            return true;
        }
        catch (Exception ex)
        {
            Exec(cn, null, "UPDATE SyncOperations SET Status='Conflict',ErrorMessage=@error WHERE OperationId=@op;",
                ("@error", ex.Message), ("@op", packet.OperationId));
            throw;
        }
    }

    static long? ResolveParty(SqliteConnection cn, SqliteTransaction? tx, string deviceId, string type, string externalId, string name)
    {
        if (string.IsNullOrWhiteSpace(externalId) && string.IsNullOrWhiteSpace(name)) return null;

        if (!string.IsNullOrWhiteSpace(externalId))
        {
            var mapped = Scalar(cn, tx, "SELECT LocalId FROM SyncEntityMap WHERE DeviceId=@device AND EntityType=@type AND ExternalId=@external LIMIT 1;",
                ("@device", deviceId), ("@type", type), ("@external", externalId));
            if (mapped > 0) return mapped;
        }

        var table = type == "Customer" ? "Customers" : "Suppliers";
        long? id = null;
        if (!string.IsNullOrWhiteSpace(externalId))
            id = Scalar(cn, tx, $"SELECT Id FROM {table} WHERE ExternalId=@external LIMIT 1;", ("@external", externalId));
        if ((!id.HasValue || id.Value <= 0) && !string.IsNullOrWhiteSpace(name))
            id = Scalar(cn, tx, $"SELECT Id FROM {table} WHERE Name=@name LIMIT 1;", ("@name", name));

        if (!id.HasValue || id.Value <= 0)
        {
            Exec(cn, tx, $"INSERT INTO {table}(Name,ExternalId) VALUES(@name,@external);", ("@name", string.IsNullOrWhiteSpace(name) ? "مشتری شبکه" : name), ("@external", Guid.NewGuid().ToString("N")));
            id = Scalar(cn, tx, "SELECT last_insert_rowid();");
        }

        if (!string.IsNullOrWhiteSpace(externalId))
            Exec(cn, tx, @"INSERT INTO SyncEntityMap(DeviceId,EntityType,ExternalId,LocalId)
VALUES(@device,@type,@external,@local)
ON CONFLICT(DeviceId,EntityType,ExternalId) DO UPDATE SET LocalId=excluded.LocalId;",
                ("@device", deviceId), ("@type", type), ("@external", externalId), ("@local", id!.Value));

        return id;
    }

    static long ResolveProduct(SqliteConnection cn, SqliteTransaction tx, string deviceId, SyncProductPacket item)
    {
        if (!string.IsNullOrWhiteSpace(item.ExternalId))
        {
            var mapped = Scalar(cn, tx, "SELECT LocalId FROM SyncEntityMap WHERE DeviceId=@device AND EntityType='Product' AND ExternalId=@external LIMIT 1;",
                ("@device", deviceId), ("@external", item.ExternalId));
            if (mapped > 0) return mapped;
        }

        long id = 0;
        if (!string.IsNullOrWhiteSpace(item.ExternalId))
            id = Scalar(cn, tx, "SELECT Id FROM Products WHERE ExternalId=@external LIMIT 1;", ("@external", item.ExternalId));
        if (id <= 0 && !string.IsNullOrWhiteSpace(item.Barcode))
            id = Scalar(cn, tx, "SELECT Id FROM Products WHERE Barcode=@barcode LIMIT 1;", ("@barcode", item.Barcode));

        if (id <= 0)
        {
            Exec(cn, tx, @"INSERT INTO Products(ExternalId,Code,Barcode,Name,Brand,Category,PurchasePrice,SalePrice,Stock,MinStock,Active,CreatedAt,UnitName,SecondaryUnitName,UnitConversionFactor)
VALUES(@external,@code,@barcode,@name,@brand,@category,@purchase,@sale,0,0,1,@created,@unit,@secondary,@factor);",
                ("@external", string.IsNullOrWhiteSpace(item.ExternalId) ? Guid.NewGuid().ToString("N") : item.ExternalId),
                ("@code", item.Code), ("@barcode", item.Barcode), ("@name", string.IsNullOrWhiteSpace(item.Name) ? "کالای واردشده از شبکه" : item.Name),
                ("@brand", item.Brand), ("@category", item.Category), ("@purchase", item.PurchasePrice), ("@sale", item.SalePrice),
                ("@created", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), ("@unit", string.IsNullOrWhiteSpace(item.UnitName) ? "عدد" : item.UnitName),
                ("@secondary", string.IsNullOrWhiteSpace(item.SecondaryUnitName) ? DBNull.Value : item.SecondaryUnitName),
                ("@factor", Math.Max(1, item.UnitConversionFactor)));
            id = Scalar(cn, tx, "SELECT last_insert_rowid();");
        }

        if (!string.IsNullOrWhiteSpace(item.ExternalId))
            Exec(cn, tx, @"INSERT INTO SyncEntityMap(DeviceId,EntityType,ExternalId,LocalId)
VALUES(@device,'Product',@external,@local)
ON CONFLICT(DeviceId,EntityType,ExternalId) DO UPDATE SET LocalId=excluded.LocalId;",
                ("@device", deviceId), ("@external", item.ExternalId), ("@local", id));

        return id;
    }

    static SyncInvoicePacket BuildInvoicePacket(SqliteConnection cn, SqliteTransaction tx, long invoiceId)
    {
        var row = QueryRow(cn, tx, @"SELECT i.InvoiceNo,i.ExternalId,i.Type,i.DateText,i.Total,i.Paid,i.Notes,
c.ExternalId AS CustomerExternalId,c.Name AS CustomerName,
s.ExternalId AS SupplierExternalId,s.Name AS SupplierName
FROM Invoices i
LEFT JOIN Customers c ON c.Id=i.CustomerId
LEFT JOIN Suppliers s ON s.Id=i.SupplierId
WHERE i.Id=@id;", ("@id", invoiceId));

        if (row == null) throw new InvalidOperationException("فاکتور برای ثبت در صف شبکه پیدا نشد.");

        var packet = new SyncInvoicePacket
        {
            OperationId = Guid.NewGuid().ToString("N"),
            DeviceId = EnsureDeviceId(cn),
            InvoiceExternalId = Convert.ToString(row["ExternalId"]) ?? "",
            InvoiceNo = Convert.ToString(row["InvoiceNo"]) ?? "",
            Type = Convert.ToString(row["Type"]) ?? "",
            DateText = Convert.ToString(row["DateText"]) ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Total = Convert.ToInt64(row["Total"]),
            Paid = Convert.ToInt64(row["Paid"]),
            Notes = Convert.ToString(row["Notes"]) ?? "",
            CustomerExternalId = Convert.ToString(row["CustomerExternalId"]) ?? "",
            CustomerName = Convert.ToString(row["CustomerName"]) ?? "",
            SupplierExternalId = Convert.ToString(row["SupplierExternalId"]) ?? ""
            ,SupplierName = Convert.ToString(row["SupplierName"]) ?? ""
        };

        var method = ScalarString(cn, tx, @"SELECT PaymentMethod FROM CashTransactions
WHERE ReferenceId=@id AND ReferenceType IN ('SaleInvoice','Purchase')
ORDER BY Id DESC LIMIT 1;", ("@id", invoiceId));
        if (!string.IsNullOrWhiteSpace(method)) packet.PaymentMethod = method!;

        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = @"SELECT p.ExternalId,p.Name,p.Barcode,p.Code,p.Brand,p.Category,p.PurchasePrice,p.SalePrice,
p.UnitName,p.SecondaryUnitName,p.UnitConversionFactor,ii.Quantity,ii.UnitPrice,ii.CostPrice,ii.Discount
FROM InvoiceItems ii JOIN Products p ON p.Id=ii.ProductId WHERE ii.InvoiceId=@id ORDER BY ii.Id;";
        cmd.Parameters.AddWithValue("@id", invoiceId);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            packet.Items.Add(new SyncProductPacket
            {
                ExternalId = reader["ExternalId"] as string ?? "",
                Name = Convert.ToString(reader["Name"]) ?? "",
                Barcode = Convert.ToString(reader["Barcode"]) ?? "",
                Code = Convert.ToString(reader["Code"]) ?? "",
                Brand = Convert.ToString(reader["Brand"]) ?? "",
                Category = Convert.ToString(reader["Category"]) ?? "",
                PurchasePrice = Convert.ToInt64(reader["PurchasePrice"]),
                SalePrice = Convert.ToInt64(reader["SalePrice"]),
                UnitName = Convert.ToString(reader["UnitName"]) ?? "عدد",
                SecondaryUnitName = Convert.ToString(reader["SecondaryUnitName"]) ?? "",
                UnitConversionFactor = Math.Max(1, Convert.ToInt64(reader["UnitConversionFactor"])),
                Quantity = Convert.ToInt64(reader["Quantity"]),
                UnitPrice = Convert.ToInt64(reader["UnitPrice"]),
                CostPrice = Convert.ToInt64(reader["CostPrice"]),
                Discount = Convert.ToInt64(reader["Discount"])
            });
        }

        return packet;
    }

    static Dictionary<string, object>? QueryRow(SqliteConnection cn, SqliteTransaction? tx, string sql, params (string Key, object Value)[] parameters)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++) result[reader.GetName(i)] = reader.IsDBNull(i) ? DBNull.Value : reader.GetValue(i);
        return result;
    }

    static long Scalar(SqliteConnection cn, SqliteTransaction? tx, string sql, params (string Key, object Value)[] parameters)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
        var value = cmd.ExecuteScalar();
        return value == null || value == DBNull.Value ? 0 : Convert.ToInt64(value);
    }

    static string? ScalarString(SqliteConnection cn, SqliteTransaction? tx, string sql, params (string Key, object Value)[] parameters)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
        var value = cmd.ExecuteScalar();
        return value == null || value == DBNull.Value ? null : Convert.ToString(value);
    }

    static string? TextScalar(SqliteConnection cn, SqliteTransaction? tx, string sql, params (string Key, object Value)[] parameters)
        => ScalarString(cn, tx, sql, parameters);

    static void Exec(SqliteConnection cn, SqliteTransaction? tx, string sql, params (string Key, object Value)[] parameters)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var p in parameters) cmd.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    static byte[] SerializeBytes<T>(T value)
    {
        using var ms = new MemoryStream();
        new DataContractJsonSerializer(typeof(T)).WriteObject(ms, value);
        return ms.ToArray();
    }

    static string Serialize<T>(T value) => Encoding.UTF8.GetString(SerializeBytes(value));

    static T? Deserialize<T>(string value)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(value));
        return (T?)new DataContractJsonSerializer(typeof(T)).ReadObject(ms);
    }

    static void ValidatePayload(byte[] bytes)
    {
        if (bytes.Length <= 0 || bytes.Length > MaxPayloadBytes)
            throw new InvalidDataException("بسته همگام‌سازی بیش از حد مجاز است.");
    }

    static byte[] ComputeMac(byte[] data)
    {
        var key = Database.Query("SELECT Value FROM Settings WHERE Key='NetworkSyncKey' LIMIT 1");
        var secret = key.Rows.Count > 0 ? Convert.ToString(key.Rows[0]["Value"]) : null;
        if (string.IsNullOrWhiteSpace(secret) || secret == "BATIR-LAN-DEFAULT-CHANGE-ME")
            throw new InvalidOperationException("کلید امنیتی شبکه BATIR تنظیم نشده است.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return hmac.ComputeHash(data);
    }

    static bool FixedEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }

    static async Task<long> ReadInt64Async(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[8];
        await ReadExactlyAsync(stream, buffer, ct);
        return BitConverter.ToInt64(buffer, 0);
    }

    static async Task WriteInt64Async(Stream stream, long value, CancellationToken ct)
    {
        var buffer = BitConverter.GetBytes(value);
        await stream.WriteAsync(buffer, 0, buffer.Length, ct);
    }

    static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, ct);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}
