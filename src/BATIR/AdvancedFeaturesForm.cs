namespace BATIR;

public class AdvancedFeaturesForm : Form
{
    public AdvancedFeaturesForm()
    {
        Text = "BATIR | امکانات پیشرفته"; Width = 760; Height = 650;
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 5 };
        for (var i = 0; i < 2; i++) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        Add(panel, "مغایرت‌گیری بانکی", () => new BankReconciliationForm().ShowDialog(this), 0, 0);
        Add(panel, "بودجه‌بندی", () => new BudgetForm().ShowDialog(this), 1, 0);
        Add(panel, "گردش نقدینگی", () => new CashFlowForm().ShowDialog(this), 0, 1);
        Add(panel, "تأیید عملیات حساس", () => new ApprovalRequestsForm().ShowDialog(this), 1, 1);
        Add(panel, "تاریخچه عملیات", () => new AuditLogForm().ShowDialog(this), 0, 2);
        Add(panel, "شبکه داخلی بدون اینترنت", () => new NetworkSyncForm().ShowDialog(this), 1, 2);
        Add(panel, "نقش‌ها و سطح دسترسی", () => new UserPermissionsForm().ShowDialog(this), 0, 3);
        Add(panel, "OCR فاکتور از تصویر", () => new OcrForm().ShowDialog(this), 1, 3);
        var close = new Button { Text = "بستن", Dock = DockStyle.Fill, Height = 50 }; close.Click += (_, _) => Close(); panel.Controls.Add(close, 0, 4); panel.SetColumnSpan(close, 2);
        Controls.Add(panel);
    }

    void Add(TableLayoutPanel panel, string text, Action action, int col, int row)
    {
        var button = new Button { Text = text, Dock = DockStyle.Fill, Height = 60 }; button.Click += (_, _) => action(); panel.Controls.Add(button, col, row);
    }
}
