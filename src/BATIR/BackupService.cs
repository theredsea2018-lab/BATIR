using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class BackupService
{
    public static void RunAutomaticIfDue()
    {
        try
        {
            if (!FeatureSettings.IsEnabled("AutomaticBackup", true) || !IsEnabled()) return;
            var hours = GetInt("AutoBackupIntervalHours", 24, 1, 720);
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BATIR", "Backups");
            Directory.CreateDirectory(folder);
            var latest = Directory.GetFiles(folder, "BATIR-auto-*.db").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (latest != null && File.GetLastWriteTimeUtc(latest) > DateTime.UtcNow.AddHours(-hours)) return;

            var target = Path.Combine(folder, "BATIR-auto-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".db");
            Database.BackupTo(target);
            if (!Database.VerifyBackup(target)) { File.Delete(target); throw new InvalidDataException("پشتیبان خودکار قابل تأیید نیست."); }
            var retention = GetInt("AutoBackupRetention", 7, 2, 100);
            foreach (var old in Directory.GetFiles(folder, "BATIR-auto-*.db").OrderByDescending(File.GetLastWriteTimeUtc).Skip(retention))
                try { File.Delete(old); } catch { }
            Database.Execute("INSERT INTO AuditLog(DateText,UserName,Action,Entity,Details) VALUES(@d,'سیستم','پشتیبان خودکار','Backup',@x)",
                new SqliteParameter("@d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
                new SqliteParameter("@x", Path.GetFileName(target)));
        }
        catch (Exception ex)
        {
            try { Database.Execute("INSERT INTO AuditLog(DateText,UserName,Action,Entity,Details) VALUES(@d,'سیستم','خطای پشتیبان خودکار','Backup',@x)",
                new SqliteParameter("@d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), new SqliteParameter("@x", ex.Message)); } catch { }
        }
    }

    static bool IsEnabled()
    {
        var dt = Database.Query("SELECT Value FROM Settings WHERE Key='AutoBackupEnabled' LIMIT 1");
        return dt.Rows.Count == 0 || !string.Equals(Convert.ToString(dt.Rows[0]["Value"]), "0", StringComparison.OrdinalIgnoreCase);
    }

    static int GetInt(string key, int fallback, int min, int max)
    {
        var dt = Database.Query("SELECT Value FROM Settings WHERE Key=@k LIMIT 1", new SqliteParameter("@k", key));
        if (dt.Rows.Count == 0 || !int.TryParse(Convert.ToString(dt.Rows[0]["Value"]), out var value)) return fallback;
        return Math.Max(min, Math.Min(max, value));
    }
}