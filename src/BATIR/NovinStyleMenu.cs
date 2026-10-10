using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace BATIR;

/// <summary>
/// Adds the classic Novin 5-style menu structure while keeping BATIR's existing
/// tested screens and workflows as the actual destinations.
/// </summary>
internal static class NovinStyleMenu
{
    public static void Apply(Form mainForm)
    {
        var tabs = FindControl<TabControl>(mainForm);
        if (tabs == null) return;

        var bar = new MenuStrip
        {
            Dock = DockStyle.Top,
            RightToLeft = RightToLeft.Yes,
            Font = new Font("Tahoma", 9F, FontStyle.Regular),
            BackColor = Color.FromArgb(239, 235, 220),
            ForeColor = Color.FromArgb(45, 45, 45),
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(8, 4, 8, 4),
            Renderer = new ToolStripProfessionalRenderer(new NovinColorTable())
        };

        var documents = TopMenu("اسناد");
        AddPage(documents, tabs, "فاکتور فروش", "فاکتور فروش");
        AddPage(documents, tabs, "فاکتور خرید", "فاکتور خرید");
        AddPage(documents, tabs, "برگشت از فروش", "برگشت از فروش");
        AddPage(documents, tabs, "برگشت از خرید", "برگشت از خرید");
        documents.DropDownItems.Add(new ToolStripSeparator());
        AddForm(documents, "دریافت / پرداخت", () => new FinancialOperationsForm().ShowDialog(mainForm));
        AddPage(documents, tabs, "جستجوی اسناد", "جستجوی اسناد");

        var accounts = TopMenu("مشاهده حساب‌ها");
        AddForm(accounts, "دفتر حساب مشتریان", () => new CustomerStatementForm().ShowDialog(mainForm));
        AddForm(accounts, "دفتر حساب تأمین‌کنندگان", () => new SupplierStatementForm().ShowDialog(mainForm));
        AddPage(accounts, tabs, "چک‌ها و تعهدات", "چک‌ها");
        AddPage(accounts, tabs, "بدهکاران و بستانکاران", "بدهکاران / بستانکاران");

        var tables = TopMenu("جدول حساب‌ها");
        AddPage(tables, tabs, "کالاها و انبار", "کالا و انبار");
        AddPage(tables, tabs, "مشتریان و اشخاص", "مشتریان");
        AddPage(tables, tabs, "تأمین‌کنندگان", "تأمین‌کنندگان");
        tables.DropDownItems.Add(new ToolStripSeparator());
        AddForm(tables, "انبارگردانی", () => new StocktakingForm().ShowDialog(mainForm));
        AddForm(tables, "اصلاح / انتقال موجودی", () => new InventoryTransferForm().ShowDialog(mainForm));

        var reports = TopMenu("گزارش‌ها");
        AddForm(reports, "گزارش موجودی و سود کالا", () => new InventoryReportForm().ShowDialog(mainForm));
        AddForm(reports, "گزارش‌های تکمیلی", () => new Stage2ReportsForm().ShowDialog(mainForm));
        AddForm(reports, "گزارش بدهکاران و بستانکاران", () => new DebtorsCreditorsForm().ShowDialog(mainForm));
        AddForm(reports, "جستجو و سابقه اسناد", () => new DocumentSearchForm().ShowDialog(mainForm));

        var other = TopMenu("متفرقه");
        AddForm(other, "پشتیبان‌گیری", () => InvokeMain(mainForm, "Backup"));
        AddForm(other, "بازیابی پشتیبان", () => InvokeMain(mainForm, "RestoreBackup"));
        other.DropDownItems.Add(new ToolStripSeparator());
        AddForm(other, "تنظیمات برنامه", () => new SettingsForm().ShowDialog(mainForm));
        AddForm(other, "چاپ فاکتور و برچسب بارکد", () => new PrintCenterForm().ShowDialog(mainForm));
        AddForm(other, "امکانات پیشرفته", () => new AdvancedFeaturesForm().ShowDialog(mainForm));
        AddForm(other, "همگام‌سازی شبکه داخلی", () => new NetworkSyncForm().ShowDialog(mainForm));
        AddForm(other, "ثبت فاکتور از تصویر / PDF", () => new OcrForm().ShowDialog(mainForm));

        var exit = TopMenu("خروج");
        AddForm(exit, "خروج از باتیر", mainForm.Close);

        bar.Items.AddRange(new ToolStripItem[] { documents, accounts, tables, reports, other, exit });
        mainForm.Controls.Add(bar);
        bar.BringToFront();
    }

    static ToolStripMenuItem TopMenu(string text) => new(text) { AutoSize = true };

    static void AddPage(ToolStripMenuItem parent, TabControl tabs, string label, string pageName)
    {
        parent.DropDownItems.Add(new ToolStripMenuItem(label, null, (_, _) =>
        {
            var page = tabs.TabPages.Cast<TabPage>()
                .FirstOrDefault(p => p.Text.Equals(pageName, StringComparison.OrdinalIgnoreCase));
            if (page != null) tabs.SelectedTab = page;
            else MessageBox.Show("صفحه موردنظر در این نسخه پیدا نشد: " + pageName, "باتیر",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }));
    }

    static void AddForm(ToolStripMenuItem parent, string label, Action action) =>
        parent.DropDownItems.Add(new ToolStripMenuItem(label, null, (_, _) => action()));

    static void InvokeMain(Form form, string methodName)
    {
        var method = form.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            MessageBox.Show("این عملیات در نسخه فعلی در دسترس نیست: " + methodName, "باتیر",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        method.Invoke(form, null);
    }

    static T? FindControl<T>(Control root) where T : Control
    {
        if (root is T match) return match;
        foreach (Control child in root.Controls)
        {
            var found = FindControl<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    sealed class NovinColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(218, 226, 238);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(231, 237, 246);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(218, 226, 238);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(207, 218, 234);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(207, 218, 234);
        public override Color ToolStripDropDownBackground => Color.FromArgb(250, 249, 244);
        public override Color ImageMarginGradientBegin => Color.FromArgb(239, 235, 220);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(239, 235, 220);
        public override Color ImageMarginGradientEnd => Color.FromArgb(239, 235, 220);
    }
}
