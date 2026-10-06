using Microsoft.Data.Sqlite;

namespace BATIR;

public class OcrForm : Form
{
    readonly TextBox file = new();
    readonly TextBox text = new();
    readonly Label status = new();

    public OcrForm()
    {
        Text = "BATIR | ثبت هوشمند فاکتور از تصویر"; Width = 900; Height = 650;
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var top = new Panel { Dock = DockStyle.Top, Height = 85, Padding = new Padding(10) };
        file.Dock = DockStyle.Fill; var choose = new Button { Text = "انتخاب تصویر", Dock = DockStyle.Right, Width = 130 }; choose.Click += Choose; top.Controls.Add(file); top.Controls.Add(choose);
        var run = new Button { Text = "اجرای OCR", Dock = DockStyle.Bottom, Height = 44 }; run.Click += RunOcr; top.Controls.Add(run);
        text.Dock = DockStyle.Fill; text.Multiline = true; text.ScrollBars = ScrollBars.Both; text.Font = new Font("Tahoma", 10);
        status.Dock = DockStyle.Bottom; status.Height = 35; status.TextAlign = ContentAlignment.MiddleCenter;
        Controls.Add(text); Controls.Add(status); Controls.Add(top);
    }

    void Choose(object? sender, EventArgs e)
    {
        using var d = new OpenFileDialog { Filter = "تصاویر فاکتور|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff" };
        if (d.ShowDialog(this) == DialogResult.OK) file.Text = d.FileName;
    }

    void RunOcr(object? sender, EventArgs e)
    {
        if (!FeatureSettings.IsEnabled("SmartInvoiceOCR", false)) { MessageBox.Show(this, "امکان OCR در تنظیمات خاموش است."); return; }
        if (!PermissionService.Require("Purchases.Create", this) || string.IsNullOrWhiteSpace(file.Text)) return;
        try
        {
            status.Text = "در حال خواندن تصویر..."; text.Text = OcrService.ExtractImageText(file.Text.Trim());
            Database.Execute("INSERT INTO OcrDocuments(DateText,FileName,DocumentType,ExtractedText,Status) VALUES(@date,@file,'Invoice',@text,'Extracted')", new SqliteParameter("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), new SqliteParameter("@file", Path.GetFileName(file.Text)), new SqliteParameter("@text", text.Text));
            status.Text = "متن استخراج شد و سند OCR ذخیره شد. برای جلوگیری از ثبت اشتباه، ایجاد فاکتور از متن نیازمند تأیید کاربر است.";
        }
        catch (Exception ex) { status.Text = "OCR ناموفق: " + ex.Message; }
    }
}
