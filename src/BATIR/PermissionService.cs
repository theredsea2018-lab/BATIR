using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class PermissionService
{
    public static long CurrentRoleId
    {
        get
        {
            var dt = Database.Query("SELECT Value FROM Settings WHERE Key='CurrentRoleId' LIMIT 1");
            return dt.Rows.Count > 0 && long.TryParse(Convert.ToString(dt.Rows[0]["Value"]), out var id) ? id : FindRoleId("مدیر");
        }
    }

    public static bool Has(string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey)) return false;
        var roleId = CurrentRoleId;
        if (roleId <= 0) return false;
        var dt = Database.Query("SELECT Allowed FROM UserPermissions WHERE RoleId=@role AND PermissionKey=@key LIMIT 1",
            new SqliteParameter("@role", roleId), new SqliteParameter("@key", permissionKey));
        return dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["Allowed"]) == 1;
    }

    public static bool Require(string permissionKey, IWin32Window owner)
    {
        if (Has(permissionKey)) return true;
        MessageBox.Show(owner, "شما مجوز انجام این عملیات را ندارید.", "عدم دسترسی", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    public static long FindRoleId(string name)
    {
        var dt = Database.Query("SELECT Id FROM UserRoles WHERE Name=@name LIMIT 1", new SqliteParameter("@name", name));
        return dt.Rows.Count == 0 ? 0 : Convert.ToInt64(dt.Rows[0]["Id"]);
    }

    public static void SetCurrentRole(long roleId)
    {
        if (roleId <= 0) return;
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "INSERT INTO Settings(Key,Value,UpdatedAt) VALUES('CurrentRoleId',@value,datetime('now')) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value,UpdatedAt=excluded.UpdatedAt";
        cmd.Parameters.AddWithValue("@value", roleId.ToString());
        cmd.ExecuteNonQuery();
    }
}
