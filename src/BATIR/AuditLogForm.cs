using Microsoft.Data.Sqlite;

namespace BATIR;

public class AuditLogForm : Form
{
    readonly DataGridView grid = new();
    readonly TextBox search = new();

    public AuditLogForm()
    {
        Text = "BATIR | تاریخچه عملیات"; Width = 1100; Height = 650;
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        var top = new Panel { Dock = DockStyle.Top, Height = 45, Padding = new Padding(8) };
        search.Dock = DockStyle.Fill; search.PlaceholderText = "جستجو در کاربر، عملیات، موجودیت و جزئیات..."; search.TextChanged += (_, _) => LoadData(); top.Controls.Add(search);
        grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        Controls.Add(grid); Controls.Add(top); LoadData();
    }

    void LoadData()
    {
        if (!PermissionService.Require("Audit.View", this)) { BeginInvoke(new Action(Close)); return; }
        var q = search.Text.Trim();
        var sql = "SELECT Id,DateText AS [تاریخ],UserName AS [کاربر],Action AS [عملیات],Entity AS [موجودیت],EntityId AS [شناسه],Details AS [جزئیات] FROM AuditLog";
        if (q.Length > 0) sql += " WHERE UserName LIKE @q OR Action LIKE @q OR Entity LIKE @q OR Details LIKE @q";
        sql += " ORDER BY Id DESC LIMIT 1000";
        grid.DataSource = q.Length == 0 ? Database.Query(sql) : Database.Query(sql, new SqliteParameter("@q", "%" + q + "%"));
    }
}
