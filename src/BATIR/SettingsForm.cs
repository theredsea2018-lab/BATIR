using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace BATIR;

public class SettingsForm : Form
{
    readonly ComboBox currency = new();
    readonly TextBox code = new();
    readonly ComboBox language = new();
    readonly ComboBox calendar = new();
    readonly CheckBox persianDigits = new() { Text = "نمایش اعداد فارسی در رابط کاربری", AutoSize = true };
    readonly NumericUpDown autoLock = new() { Minimum = 0, Maximum = 1440 };
    readonly TextBox autoLockPassword = new() { UseSystemPasswordChar = true };
    readonly TextBox autoLockPasswordConfirm = new() { UseSystemPasswordChar = true };
    readonly CheckBox negativeStock = new() { Text = "اجازه فروش با موجودی منفی", AutoSize = true };
    readonly Dictionary<string, CheckBox> featureChecks = new();
    readonly ComboBox currentRole = new();
    readonly Button save = new() { Text = "ذخیره تنظیمات", Dock = DockStyle.Bottom, Height = 55 };

    public SettingsForm()
    {
        Text = "BATIR | تنظیمات"; Width = 900; Height = 840; StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        currency.DropDownStyle = ComboBoxStyle.DropDown; currency.Items.AddRange(new object[] { "ریال", "تومان" });
        language.Items.AddRange(new object[] { "فارسی", "English" }); language.DropDownStyle = ComboBoxStyle.DropDownList;
        calendar.Items.AddRange(new object[] { "شمسی", "میلادی" }); calendar.DropDownStyle = ComboBoxStyle.DropDownList; currentRole.DropDownStyle = ComboBoxStyle.DropDownList;

        var root = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(18) };
        var table = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 16, AutoSize = true };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        Add(table, "واحد پول", currency, 0); Add(table, "کد واحد پول", code, 1); Add(table, "زبان", language, 2); Add(table, "تقویم", calendar, 3);
        Add(table, "قفل خودکار (دقیقه، صفر=خاموش)", autoLock, 4); Add(table, "رمز قفل خودکار (جدید)", autoLockPassword, 5); Add(table, "تکرار رمز قفل", autoLockPasswordConfirm, 6);
        table.Controls.Add(persianDigits, 1, 7); table.Controls.Add(negativeStock, 1, 8);
        var print = new Button { Text = "چاپ فاکتور و چاپ برچسب بارکد", Dock = DockStyle.Fill, Height = 42 }; print.Click += (_, _) => { if (PermissionService.Require("Settings.Edit", this)) new PrintCenterForm().ShowDialog(this); }; table.Controls.Add(print, 1, 9);
        var permissions = new Button { Text = "مدیریت نقش‌ها و سطح دسترسی کاربران", Dock = DockStyle.Fill, Height = 42 }; permissions.Click += (_, _) => { if (PermissionService.Require("Users.Edit", this)) new UserPermissionsForm().ShowDialog(this); }; table.Controls.Add(permissions, 1, 10);
        var approvals = new Button { Text = "مدیریت درخواست‌های تأیید عملیات", Dock = DockStyle.Fill, Height = 42 }; approvals.Click += (_, _) => new ApprovalRequestsForm().ShowDialog(this); table.Controls.Add(approvals, 1, 11);
        var network = new Button { Text = "انتقال اطلاعات/پشتیبان در شبکه داخلی بدون اینترنت", Dock = DockStyle.Fill, Height = 42 }; network.Click += (_, _) => new NetworkSyncForm().ShowDialog(this); table.Controls.Add(network, 1, 12);
        Add(table, "نقش فعال این کامپیوتر", currentRole, 13);
        var advanced = new Button { Text = "مرکز امکانات پیشرفته: بانک، بودجه، نقدینگی، ممیزی و شبکه", Dock = DockStyle.Fill, Height = 42 }; advanced.Click += (_, _) => new AdvancedFeaturesForm().ShowDialog(this); table.Controls.Add(advanced, 1, 14);
        root.Controls.Add(table);

        var group = new GroupBox { Text = "فعال‌سازی امکانات پیشرفته — هر مورد با تیک فعال می‌شود", Dock = DockStyle.Top, Height = 420, Padding = new Padding(12), RightToLeft = RightToLeft.Yes };
        var features = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true }; features.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); features.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        int row = 0; foreach (var item in FeatureSettings.Catalog) { var check = new CheckBox { Text = item.Text, AutoSize = true, Tag = item.Key, Margin = new Padding(8) }; featureChecks[item.Key] = check; features.RowStyles.Add(new RowStyle(SizeType.AutoSize)); features.Controls.Add(check, row % 2, row / 2); row++; }
        group.Controls.Add(features); root.Controls.Add(group); root.Controls.SetChildIndex(group, 0);
        root.Controls.Add(new Label { Text = "امکانات خاموش از فرآیندهای مربوطه استفاده نمی‌شوند. شبکه داخلی بدون اینترنت فقط روی LAN/Wi‑Fi مشترک کار می‌کند و قبل از انتقال، سلامت پشتیبان کنترل می‌شود.", AutoSize = true, MaximumSize = new Size(820, 0), Padding = new Padding(4), Dock = DockStyle.Top });
        Controls.Add(root); Controls.Add(save); save.Click += (_, _) => SaveSettings(); LoadSettings();
    }

    void Add(TableLayoutPanel t, string label, Control control, int row) { t.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, AutoSize = true }, 0, row); control.Dock = DockStyle.Fill; t.Controls.Add(control, 1, row); }

    void LoadSettings()
    {
        currentRole.Items.Clear(); var roles = Database.Query("SELECT Id,Name FROM UserRoles ORDER BY IsSystem DESC,Name"); foreach (System.Data.DataRow row in roles.Rows) currentRole.Items.Add(new RoleItem(Convert.ToInt64(row["Id"]), Convert.ToString(row["Name"]) ?? ""));
        foreach (var item in FeatureSettings.Catalog) featureChecks[item.Key].Checked = item.Default;
        var dt = Database.Query("SELECT Key,Value FROM Settings");
        foreach (System.Data.DataRow row in dt.Rows)
        {
            var key = Convert.ToString(row["Key"]); var value = Convert.ToString(row["Value"]) ?? "";
            switch (key)
            {
                case "CurrencyName": currency.Text = value; break; case "CurrencyCode": code.Text = value; break;
                case "Language": language.SelectedIndex = value.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 1 : 0; break;
                case "DateCalendar": calendar.SelectedIndex = value.StartsWith("G", StringComparison.OrdinalIgnoreCase) ? 1 : 0; break;
                case "PersianDigits": persianDigits.Checked = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case "AutoLockMinutes": if (int.TryParse(value, out var minutes)) autoLock.Value = Math.Max(0, Math.Min(1440, minutes)); break;
                case "NegativeStockAllowed": negativeStock.Checked = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                case "CurrentRoleId": if (long.TryParse(value, out var roleId)) SelectRole(roleId); break;
                default: if (featureChecks.TryGetValue(key, out var feature)) feature.Checked = value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
            }
        }
        if (currentRole.SelectedIndex < 0 && currentRole.Items.Count > 0) currentRole.SelectedIndex = 0;
    }

    void SelectRole(long roleId) { for (var i = 0; i < currentRole.Items.Count; i++) if (((RoleItem)currentRole.Items[i]).Id == roleId) { currentRole.SelectedIndex = i; break; } }

    void SaveSettings()
    {
        var existing = Database.Query("SELECT Value FROM Settings WHERE Key='AutoLockPasswordHash' LIMIT 1"); var existingHash = existing.Rows.Count == 0 ? "" : Convert.ToString(existing.Rows[0]["Value"]) ?? ""; var passwordHash = existingHash;
        if (autoLockPassword.Text.Length > 0) passwordHash = HashPassword(autoLockPassword.Text);
        if (autoLockPassword.Text.Length > 0 && autoLockPassword.Text != autoLockPasswordConfirm.Text) { MessageBox.Show("رمز قفل خودکار و تکرار آن یکسان نیست."); return; }
        if ((int)autoLock.Value > 0 && string.IsNullOrWhiteSpace(passwordHash)) { MessageBox.Show("برای فعال کردن قفل خودکار باید رمز تعیین کنید."); return; }
        if (!(currentRole.SelectedItem is RoleItem selectedRole)) { MessageBox.Show("نقش فعال را انتخاب کنید."); return; }
        var values = new Dictionary<string, string> { { "CurrencyName", currency.Text.Trim() }, { "CurrencyCode", code.Text.Trim().ToUpperInvariant() }, { "Language", language.SelectedIndex == 1 ? "en-US" : "fa-IR" }, { "DateCalendar", calendar.SelectedIndex == 1 ? "Gregorian" : "Shamsi" }, { "PersianDigits", persianDigits.Checked ? "true" : "false" }, { "AutoLockMinutes", ((int)autoLock.Value).ToString() }, { "NegativeStockAllowed", negativeStock.Checked ? "true" : "false" }, { "AutoLockPasswordHash", passwordHash }, { "CurrentRoleId", selectedRole.Id.ToString() } };
        if (string.IsNullOrWhiteSpace(values["CurrencyName"]) || string.IsNullOrWhiteSpace(values["CurrencyCode"])) { MessageBox.Show("واحد پول و کد آن نمی‌تواند خالی باشد."); return; }
        using var cn = Database.Open(); using var tx = cn.BeginTransaction(); foreach (var pair in values) Upsert(cn, tx, pair.Key, pair.Value); foreach (var item in FeatureSettings.Catalog) FeatureSettings.Set(cn, tx, item.Key, featureChecks[item.Key].Checked); tx.Commit(); PermissionService.SetCurrentRole(selectedRole.Id);
        MessageBox.Show("تنظیمات، نقش فعال و امکانات انتخاب‌شده ذخیره شد."); DialogResult = DialogResult.OK; Close();
    }

    static void Upsert(SqliteConnection cn, SqliteTransaction tx, string key, string value) { using var cmd = cn.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = "INSERT INTO Settings(Key,Value,UpdatedAt) VALUES(@key,@value,@date) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value,UpdatedAt=excluded.UpdatedAt"; cmd.Parameters.AddWithValue("@key", key); cmd.Parameters.AddWithValue("@value", value); cmd.Parameters.AddWithValue("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")); cmd.ExecuteNonQuery(); }
    static string HashPassword(string password) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(password))).Replace("-", "").ToLowerInvariant(); }
    sealed class RoleItem { public long Id { get; } public string Name { get; } public RoleItem(long id, string name) { Id = id; Name = name; } public override string ToString() => Name; }
}
