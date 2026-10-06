using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class ApprovalRequestsForm : Form
{
    readonly DataGridView grid = new();
    readonly TextBox action = new();
    readonly TextBox entity = new();
    readonly NumericUpDown entityId = new() { Minimum = 0, Maximum = 999999999 };
    readonly TextBox details = new();

    public ApprovalRequestsForm()
    {
        Text = "BATIR | تأیید عملیات حساس"; Width = 980; Height = 620;
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        var form = new TableLayoutPanel { Dock = DockStyle.Top, Height = 120, ColumnCount = 4, RowCount = 2 };
        for (var i = 0; i < 4; i++) form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        Add(form, "عملیات", action, 0, 0); Add(form, "موجودیت", entity, 1, 0); Add(form, "شناسه", entityId, 2, 0); Add(form, "جزئیات", details, 3, 0);
        var request = new Button { Text = "ثبت درخواست تأیید", Dock = DockStyle.Fill }; request.Click += Request; form.Controls.Add(request, 0, 1);
        var approve = new Button { Text = "تأیید انتخاب‌شده", Dock = DockStyle.Fill }; approve.Click += (_, _) => ChangeStatus("Approved"); form.Controls.Add(approve, 1, 1);
        var reject = new Button { Text = "رد انتخاب‌شده", Dock = DockStyle.Fill }; reject.Click += (_, _) => ChangeStatus("Rejected"); form.Controls.Add(reject, 2, 1);
        var refresh = new Button { Text = "بازخوانی", Dock = DockStyle.Fill }; refresh.Click += (_, _) => LoadGrid(); form.Controls.Add(refresh, 3, 1);
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        root.Controls.Add(grid); root.Controls.Add(form); Controls.Add(root); LoadGrid();
    }

    void Add(TableLayoutPanel table, string label, Control control, int col, int row)
    {
        var panel = new Panel { Dock = DockStyle.Fill }; panel.Controls.Add(control); control.Dock = DockStyle.Fill;
        panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Right, Width = 70, TextAlign = ContentAlignment.MiddleRight }); table.Controls.Add(panel, col, row);
    }

    void LoadGrid()
    {
        grid.DataSource = Database.Query(@"SELECT Id,DateText AS [تاریخ],UserName AS [درخواست‌کننده],Action AS [عملیات],Entity AS [موجودیت],EntityId AS [شناسه],Status AS [وضعیت],ApprovedBy AS [تأییدکننده],ApprovedAt AS [زمان تأیید],Details AS [جزئیات] FROM ApprovalRequests ORDER BY Id DESC");
    }

    void Request(object? sender, EventArgs e)
    {
        if (!FeatureSettings.IsEnabled("ApprovalWorkflow", true)) { MessageBox.Show(this, "گردش تأیید در تنظیمات خاموش است."); return; }
        if (string.IsNullOrWhiteSpace(action.Text)) { MessageBox.Show(this, "نوع عملیات را وارد کنید."); return; }
        Database.Execute(@"INSERT INTO ApprovalRequests(DateText,UserName,Action,Entity,EntityId,Status,Details)
VALUES(@date,'کاربر',@action,@entity,@id,'Pending',@details)",
            new SqliteParameter("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
            new SqliteParameter("@action", action.Text.Trim()), new SqliteParameter("@entity", entity.Text.Trim()),
            new SqliteParameter("@id", (long)entityId.Value), new SqliteParameter("@details", details.Text.Trim()));
        LoadGrid(); MessageBox.Show(this, "درخواست تأیید ثبت شد.");
    }

    void ChangeStatus(string status)
    {
        if (!PermissionService.Require("Approvals.Approve", this)) return;
        if (grid.CurrentRow == null) return;
        var id = Convert.ToInt64(grid.CurrentRow.Cells["Id"].Value);
        Database.Execute("UPDATE ApprovalRequests SET Status=@status,ApprovedBy='کاربر',ApprovedAt=@date WHERE Id=@id AND Status='Pending'",
            new SqliteParameter("@status", status), new SqliteParameter("@date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), new SqliteParameter("@id", id));
        LoadGrid();
    }
}
