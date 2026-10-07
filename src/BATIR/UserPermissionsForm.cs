using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class UserPermissionsForm : Form
{
    readonly DataGridView rolesGrid = new();
    readonly CheckedListBox permissions = new();
    readonly TextBox roleName = new();
    readonly CheckBox systemRole = new() { Text = "نقش سیستمی", AutoSize = true };
    readonly Button saveRole = new() { Text = "ذخیره نقش و دسترسی‌ها", Dock = DockStyle.Bottom, Height = 48 };
    long selectedRoleId;

    static readonly (string Key, string Text)[] PermissionCatalog =
    {
        ("Products.View", "مشاهده کالا و موجودی"),
        ("Products.Edit", "ثبت و ویرایش کالا"),
        ("Sales.Create", "ثبت فاکتور فروش"),
        ("Sales.Return", "ثبت برگشت از فروش"),
        ("Purchases.Create", "ثبت فاکتور خرید"),
        ("Purchases.Return", "ثبت برگشت از خرید"),
        ("Customers.Edit", "مدیریت مشتریان"),
        ("Suppliers.Edit", "مدیریت تأمین‌کنندگان"),
        ("Checks.Edit", "ثبت و مدیریت چک‌ها"),
        ("Finance.Edit", "دریافت، پرداخت و هزینه‌ها"),
        ("Inventory.Edit", "اصلاح، انتقال و انبارگردانی"),
        ("Reports.View", "مشاهده گزارش‌ها"),
        ("Settings.Edit", "تغییر تنظیمات برنامه"),
        ("Backup.Restore", "بازیابی پشتیبان"),
        ("Users.Edit", "مدیریت کاربران و دسترسی‌ها"),
        ("Approvals.Approve", "تأیید عملیات حساس"),
        ("Audit.View", "مشاهده تاریخچه عملیات")
    };

    public UserPermissionsForm()
    {
        Text = "BATIR | نقش‌ها و سطح دسترسی"; Width = 980; Height = 650;
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;

        rolesGrid.Dock = DockStyle.Fill; rolesGrid.ReadOnly = true; rolesGrid.AllowUserToAddRows = false;
        rolesGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; rolesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        rolesGrid.SelectionChanged += (_, _) => LoadSelectedRole();

        permissions.Dock = DockStyle.Fill; permissions.CheckOnClick = true;
        foreach (var item in PermissionCatalog) permissions.Items.Add(item.Text);

        roleName.Dock = DockStyle.Top;
        var rolePanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        rolePanel.Controls.Add(rolesGrid);
        var add = new Button { Text = "نقش جدید", Dock = DockStyle.Bottom, Height = 42 };
        add.Click += (_, _) => NewRole(); rolePanel.Controls.Add(add);

        var editPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        editPanel.Controls.Add(permissions); editPanel.Controls.Add(systemRole); editPanel.Controls.Add(roleName);
        editPanel.Controls.Add(saveRole);
        saveRole.Click += (_, _) => SaveRole();

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 390 };
        split.Panel1.Controls.Add(rolePanel); split.Panel2.Controls.Add(editPanel);
        Controls.Add(split);
        LoadRoles();
    }

    void LoadRoles()
    {
        rolesGrid.DataSource = Database.Query("SELECT Id,Name AS [نام نقش],CASE WHEN IsSystem=1 THEN 'سیستمی' ELSE 'سفارشی' END AS [نوع] FROM UserRoles ORDER BY IsSystem DESC,Name");
        if (rolesGrid.Rows.Count > 0) rolesGrid.Rows[0].Selected = true;
    }

    void LoadSelectedRole()
    {
        if (rolesGrid.CurrentRow == null) return;
        selectedRoleId = Convert.ToInt64(rolesGrid.CurrentRow.Cells["Id"].Value);
        roleName.Text = Convert.ToString(rolesGrid.CurrentRow.Cells["نام نقش"].Value) ?? "";
        var role = Database.Query("SELECT IsSystem FROM UserRoles WHERE Id=@id", new SqliteParameter("@id", selectedRoleId));
        systemRole.Checked = role.Rows.Count > 0 && Convert.ToInt32(role.Rows[0]["IsSystem"]) == 1;
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dt = Database.Query("SELECT PermissionKey FROM UserPermissions WHERE RoleId=@id AND Allowed=1", new SqliteParameter("@id", selectedRoleId));
        foreach (DataRow row in dt.Rows) allowed.Add(Convert.ToString(row["PermissionKey"]) ?? "");
        for (int i = 0; i < PermissionCatalog.Length; i++) permissions.SetItemChecked(i, allowed.Contains(PermissionCatalog[i].Key));
    }

    void NewRole()
    {
        selectedRoleId = 0; roleName.Clear(); systemRole.Checked = false;
        for (int i = 0; i < permissions.Items.Count; i++) permissions.SetItemChecked(i, false);
        roleName.Focus();
    }

    void SaveRole()
    {
        if (!PermissionService.Require("Users.Edit", this)) return;
        var name = roleName.Text.Trim();
        if (name.Length == 0) { MessageBox.Show("نام نقش را وارد کنید."); return; }
        using var cn = Database.Open(); using var tx = cn.BeginTransaction();
        long id = selectedRoleId;
        if (id == 0)
        {
            using var insert = cn.CreateCommand(); insert.Transaction = tx;
            insert.CommandText = "INSERT INTO UserRoles(Name,IsSystem) VALUES(@name,0); SELECT last_insert_rowid();";
            insert.Parameters.AddWithValue("@name", name); id = Convert.ToInt64(insert.ExecuteScalar());
        }
        else
        {
            var system = Convert.ToInt32(Database.Query("SELECT IsSystem FROM UserRoles WHERE Id=@id", new SqliteParameter("@id", id)).Rows[0]["IsSystem"]) == 1;
            if (system && !name.Equals(Convert.ToString(rolesGrid.CurrentRow?.Cells["نام نقش"].Value), StringComparison.Ordinal))
            { MessageBox.Show("نام نقش سیستمی را تغییر ندهید."); return; }
            using var update = cn.CreateCommand(); update.Transaction = tx; update.CommandText = "UPDATE UserRoles SET Name=@name WHERE Id=@id";
            update.Parameters.AddWithValue("@name", name); update.Parameters.AddWithValue("@id", id); update.ExecuteNonQuery();
        }
        using (var clear = cn.CreateCommand()) { clear.Transaction = tx; clear.CommandText = "DELETE FROM UserPermissions WHERE RoleId=@id"; clear.Parameters.AddWithValue("@id", id); clear.ExecuteNonQuery(); }
        for (int i = 0; i < PermissionCatalog.Length; i++)
        {
            using var add = cn.CreateCommand(); add.Transaction = tx;
            add.CommandText = "INSERT INTO UserPermissions(RoleId,PermissionKey,Allowed) VALUES(@role,@key,@allowed)";
            add.Parameters.AddWithValue("@role", id); add.Parameters.AddWithValue("@key", PermissionCatalog[i].Key); add.Parameters.AddWithValue("@allowed", permissions.GetItemChecked(i) ? 1 : 0); add.ExecuteNonQuery();
        }
        tx.Commit();
        LoadRoles();
        MessageBox.Show("نقش و سطح دسترسی با موفقیت ذخیره شد.");
    }
}
