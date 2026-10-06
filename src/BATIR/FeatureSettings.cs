using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class FeatureSettings
{
    public static bool IsEnabled(string key, bool defaultValue = false)
    {
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key=@key LIMIT 1";
        cmd.Parameters.AddWithValue("@key", key);
        var value = Convert.ToString(cmd.ExecuteScalar());
        return value == null ? defaultValue : value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public static void Set(SqliteConnection cn, SqliteTransaction tx, string key, bool enabled)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Settings(Key,Value,UpdatedAt) VALUES(@key,@value,@date) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value,UpdatedAt=excluded.UpdatedAt";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", enabled ? "true" : "false");
        cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
    }

    public static readonly (string Key, string Text, bool Default)[] Catalog =
    {
        ("BankReconciliation", "مغایرت‌گیری بانکی", true),
        ("CashFlowForecast", "گردش و پیش‌بینی نقدینگی", true),
        ("Budgeting", "بودجه‌بندی ماهانه و سالانه", true),
        ("UserPermissions", "سطح دسترسی کاربران", true),
        ("ApprovalWorkflow", "تأیید عملیات حساس", true),
        ("AuditTrail", "ثبت تاریخچه تغییرات", true),
        ("SalesProforma", "پیش‌فاکتور و تبدیل به فاکتور", true),
        ("PurchaseOrders", "سفارش خرید", true),
        ("SalesOrders", "سفارش فروش", true),
        ("RecurringDocuments", "اسناد و فاکتورهای تکرارشونده", false),
        ("DueReminders", "یادآوری بدهی و سررسید چک", true),
        ("FixedAssets", "دارایی ثابت", false),
        ("Payroll", "حقوق و دستمزد", false),
        ("SmartInvoiceOCR", "ثبت هوشمند فاکتور از عکس/PDF", false),
        ("TaxpayerIntegration", "اتصال و امکانات مالیاتی/مودیان", false),
        ("NetworkSync", "همگام‌سازی شبکه داخلی بدون اینترنت", true),
        ("MultiComputerLedger", "تجمیع حساب‌های چند کامپیوتر", true),
        ("BarcodeLabelPrinting", "چاپ سریع برچسب بارکد", true),
        ("VoiceEntry", "ثبت و جستجوی صوتی", false),
        ("EcommerceIntegration", "اتصال فروشگاه اینترنتی", false)
    };
}
