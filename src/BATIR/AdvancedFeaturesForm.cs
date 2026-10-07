namespace BATIR;

public class AdvancedFeaturesForm : Form
{
    public AdvancedFeaturesForm()
    {
        Text = "BATIR | امکانات پیشرفته"; Width = 760; Height = 650;
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 5 };
        for (var i = 0; i < 2; i++) panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        AddFeature(panel, "مغایرت‌گیری بانکی", "BankReconciliation", () => new BankReconciliationForm().ShowDialog(this), 0, 0);
        AddFeature(panel, "بودجه‌بندی", "Budgeting", () => new BudgetForm().ShowDialog(this), 1, 0);
        AddFeature(panel, "گردش نقدینگی", "CashFlowForecast", () => new CashFlowForm().ShowDialog(this), 0, 1);
        AddFeature(panel, "تأیید عملیات حساس", "ApprovalWorkflow", () => new ApprovalRequestsForm().ShowDialog(this), 1, 1);
        AddFeature(panel, "تاریخچه عملیات", "AuditTrail", () => new AuditLogForm().ShowDialog(this), 0, 2);
        AddFeature(panel, "شبکه داخلی بدون اینترنت", "NetworkSync", () => new NetworkSyncForm().ShowDialog(this), 1, 2);
        AddFeature(panel, "نقش‌ها و سطح دسترسی", "UserPermissions", () => new UserPermissionsForm().ShowDialog(this), 0, 3);
        AddFeature(panel, "OCR فاکتور از تصویر", "SmartInvoiceOCR", () => new OcrForm().ShowDialog(this), 1, 3);
        var close = new Button { Text = "بستن", Dock = DockStyle.Fill, Height = 50 }; close.Click += (_, _) => Close(); panel.Controls.Add(close, 0, 4); panel.SetColumnSpan(close, 2);
        Controls.Add(panel);
    }

    void AddFeature(TableLayoutPanel panel, string text, string featureKey, Action action, int col, int row)
    {
        if (!FeatureSettings.IsEnabled(featureKey, false)) return;
        Add(panel, text, action, col, row);
    }

    void Add(TableLayoutPanel panel, string text, Action action, int col, int row)
    {
        var button = new Button { Text = text, Dock = DockStyle.Fill, Height = 60 };
        button.Click += (_, _) => action();
        panel.Controls.Add(button, col, row);
    }
}
