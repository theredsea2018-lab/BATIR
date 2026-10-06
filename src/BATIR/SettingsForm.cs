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
    readonly Button save = new() { Text = "ذخیره تنظیمات", Dock = DockStyle.Bottom, Height = 55 };

    public SettingsForm()
    {
        Text = "BATIR | تنظیمات"; Width = 620; Height = 500;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        currency.DropDownStyle = ComboBoxStyle.DropDown; currency.Items.AddRange(new object[] { "ریال", "تومان" });
        language.Items.AddRange(new object[] { "فارسی", "English" }); language.DropDownStyle = ComboBoxStyle.DropDownList;
        calendar.Items.AddRange(new object[] { "شمسی", "میلادی" }); calendar.DropDownStyle = ComboBoxStyle.DropDownList;
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10, Padding = new Padding(18) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        Add(table, "واحد پول", currency, 0); Add(table, "کد واحد پول", code, 1); Add(table, "زبان", language, 2); Add(table, "تقویم", calendar, 3);
        Add(table, "قفل خودکار (دقیقه، صفر=خاموش)", autoLock, 4); Add(table, "رمز قفل خودکار (جدید)", autoLockPassword, 5); Add(table, "تکرار رمز قفل", autoLockPasswordConfirm, 6);
        table.Controls.Add(persianDigits, 1, 7); table.Controls.Add(negativeStock, 1, 8);
        var print = new Button { Text = "چاپ فاکتور و چاپ برچسب بارکد", Dock = DockStyle.Fill, Height = 42 };
        print.Click += (_,_) => new PrintCenterForm().ShowDialog(this); table.Controls.Add(print, 1, 9);
        Controls.Add(table); Controls.Add(save); save.Click += (_,_) => SaveSettings(); LoadSettings();
    }

    void Add(TableLayoutPanel t,string label,Control control,int row){t.Controls.Add(new Label{Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight},0,row);control.Dock=DockStyle.Fill;t.Controls.Add(control,1,row);}

    void LoadSettings()
    {
        var dt=Database.Query("SELECT Key,Value FROM Settings");
        foreach(System.Data.DataRow row in dt.Rows){var key=Convert.ToString(row["Key"]);var value=Convert.ToString(row["Value"])??"";switch(key){
            case "CurrencyName":currency.Text=value;break;case "CurrencyCode":code.Text=value;break;case "Language":language.SelectedIndex=value.StartsWith("en",StringComparison.OrdinalIgnoreCase)?1:0;break;
            case "DateCalendar":calendar.SelectedIndex=value.StartsWith("G",StringComparison.OrdinalIgnoreCase)?1:0;break;case "PersianDigits":persianDigits.Checked=value.Equals("true",StringComparison.OrdinalIgnoreCase);break;
            case "AutoLockMinutes":if(int.TryParse(value,out var minutes))autoLock.Value=Math.Max(0,Math.Min(1440,minutes));break;case "NegativeStockAllowed":negativeStock.Checked=value.Equals("true",StringComparison.OrdinalIgnoreCase);break;}}
    }

    void SaveSettings()
    {
        var existing=Database.Query("SELECT Value FROM Settings WHERE Key='AutoLockPasswordHash' LIMIT 1");var existingHash=existing.Rows.Count==0?"":Convert.ToString(existing.Rows[0]["Value"])??"";var passwordHash=existingHash;
        if(autoLockPassword.Text.Length>0)passwordHash=HashPassword(autoLockPassword.Text);
        if(autoLockPassword.Text.Length>0&&autoLockPassword.Text!=autoLockPasswordConfirm.Text){MessageBox.Show("رمز قفل خودکار و تکرار آن یکسان نیست.");return;}
        if((int)autoLock.Value>0&&string.IsNullOrWhiteSpace(passwordHash)){MessageBox.Show("برای فعال کردن قفل خودکار باید رمز تعیین کنید.");return;}
        var values=new Dictionary<string,string>{{"CurrencyName",currency.Text.Trim()},{"CurrencyCode",code.Text.Trim().ToUpperInvariant()},{"Language",language.SelectedIndex==1?"en-US":"fa-IR"},{"DateCalendar",calendar.SelectedIndex==1?"Gregorian":"Shamsi"},{"PersianDigits",persianDigits.Checked?"true":"false"},{"AutoLockMinutes",((int)autoLock.Value).ToString()},{"NegativeStockAllowed",negativeStock.Checked?"true":"false"},{"AutoLockPasswordHash",passwordHash}};
        if(string.IsNullOrWhiteSpace(values["CurrencyName"])||string.IsNullOrWhiteSpace(values["CurrencyCode"])){MessageBox.Show("واحد پول و کد آن نمی‌تواند خالی باشد.");return;}
        using var cn=Database.Open();using var tx=cn.BeginTransaction();foreach(var pair in values){using var cmd=cn.CreateCommand();cmd.Transaction=tx;cmd.CommandText=@"INSERT INTO Settings(Key,Value,UpdatedAt) VALUES(@key,@value,@date) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value,UpdatedAt=excluded.UpdatedAt";cmd.Parameters.AddWithValue("@key",pair.Key);cmd.Parameters.AddWithValue("@value",pair.Value);cmd.Parameters.AddWithValue("@date",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));cmd.ExecuteNonQuery();}tx.Commit();MessageBox.Show("تنظیمات ذخیره شد.");DialogResult=DialogResult.OK;Close();
    }
    static string HashPassword(string password){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(password))).Replace("-","").ToLowerInvariant();}
}
