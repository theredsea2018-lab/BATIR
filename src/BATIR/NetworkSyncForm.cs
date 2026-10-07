namespace BATIR;

public class NetworkSyncForm : Form
{
    readonly TextBox peer = new();
    readonly TextBox backupPath = new();
    readonly Label status = new();
    CancellationTokenSource? receiverCts;

    public NetworkSyncForm()
    {
        Text = "BATIR | انتقال شبکه داخلی بدون اینترنت"; Width = 720; Height = 330;
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 5 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        root.Controls.Add(new Label { Text = "IP کامپیوتر مقصد", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
        peer.Dock = DockStyle.Fill; peer.Text = "192.168.1."; root.Controls.Add(peer, 1, 0);
        root.Controls.Add(new Label { Text = "فایل پشتیبان", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 1);
        backupPath.Dock = DockStyle.Fill; root.Controls.Add(backupPath, 1, 1);
        var choose = new Button { Text = "انتخاب پشتیبان", Dock = DockStyle.Fill }; choose.Click += ChooseBackup; root.Controls.Add(choose, 1, 2);
        var send = new Button { Text = "ارسال پشتیبان به کامپیوتر دیگر", Dock = DockStyle.Fill, Height = 44 }; send.Click += Send; root.Controls.Add(send, 1, 3);
        var receive = new Button { Text = "آماده دریافت از شبکه", Dock = DockStyle.Fill, Height = 44 }; receive.Click += Receive; root.Controls.Add(receive, 0, 3);
        status.Text = "پورت شبکه داخلی: " + NetworkSyncService.Port; status.Dock = DockStyle.Fill; status.TextAlign = ContentAlignment.MiddleCenter; root.Controls.Add(status, 0, 4); root.SetColumnSpan(status, 2);
        Controls.Add(root);
        FormClosed += (_, _) => receiverCts?.Cancel();
    }

    void ChooseBackup(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog { Filter = "BATIR Database (*.db)|*.db" };
        if (dialog.ShowDialog(this) == DialogResult.OK) backupPath.Text = dialog.FileName;
    }

    async void Send(object? sender, EventArgs e)
    {
        if (!FeatureSettings.IsEnabled("NetworkSync", true)) { MessageBox.Show(this, "امکان همگام‌سازی شبکه داخلی در تنظیمات خاموش است."); return; }
        if (!PermissionService.Require("Backup.Create", this)) return;
        if (string.IsNullOrWhiteSpace(peer.Text) || string.IsNullOrWhiteSpace(backupPath.Text)) { MessageBox.Show(this, "IP مقصد و فایل پشتیبان را مشخص کنید."); return; }
        try
        {
            status.Text = "در حال ارسال...";
            await NetworkSyncService.SendBackupAsync(peer.Text.Trim(), backupPath.Text.Trim());
            status.Text = "ارسال با موفقیت انجام شد. SHA-256: " + NetworkSyncService.Sha256(backupPath.Text.Trim());
        }
        catch (Exception ex) { status.Text = "ارسال ناموفق: " + ex.Message; }
    }

    async void Receive(object? sender, EventArgs e)
    {
        if (!FeatureSettings.IsEnabled("NetworkSync", true)) { MessageBox.Show(this, "امکان همگام‌سازی شبکه داخلی در تنظیمات خاموش است."); return; }
        if (!PermissionService.Require("Backup.Restore", this)) return;
        using var dialog = new SaveFileDialog { Filter = "BATIR Database (*.db)|*.db", FileName = "BATIR-Network-Backup.db" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        receiverCts?.Cancel(); receiverCts = new CancellationTokenSource();
        try
        {
            status.Text = "در حال انتظار برای اتصال کامپیوتر دیگر...";
            await NetworkSyncService.ReceiveBackupAsync(dialog.FileName, receiverCts.Token);
            status.Text = "دریافت و کنترل سلامت با موفقیت انجام شد. SHA-256: " + NetworkSyncService.Sha256(dialog.FileName);
        }
        catch (Exception ex) { status.Text = "دریافت ناموفق: " + ex.Message; }
    }
}
