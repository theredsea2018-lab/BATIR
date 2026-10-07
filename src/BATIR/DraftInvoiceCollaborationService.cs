using System.Data;
using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class DraftInvoiceCollaborationService
{
    public static bool TryAcquireLock(SqliteConnection cn, long draftId, string userName, TimeSpan duration)
    {
        using var tx = cn.BeginTransaction();
        using var cleanup = cn.CreateCommand(); cleanup.Transaction = tx; cleanup.CommandText = "DELETE FROM DraftInvoiceLocks WHERE ExpiresAt <= datetime('now');"; cleanup.ExecuteNonQuery();
        using var check = cn.CreateCommand(); check.Transaction = tx; check.CommandText = "SELECT UserName FROM DraftInvoiceLocks WHERE DraftId=@id LIMIT 1"; check.Parameters.AddWithValue("@id", draftId);
        var owner = Convert.ToString(check.ExecuteScalar());
        if (!string.IsNullOrWhiteSpace(owner) && !string.Equals(owner, userName, StringComparison.OrdinalIgnoreCase)) { tx.Rollback(); return false; }
        using var upsert = cn.CreateCommand(); upsert.Transaction = tx; upsert.CommandText = "INSERT INTO DraftInvoiceLocks(DraftId,UserName,AcquiredAt,ExpiresAt) VALUES(@id,@user,datetime('now'),datetime('now',@duration)) ON CONFLICT(DraftId) DO UPDATE SET UserName=excluded.UserName,AcquiredAt=excluded.AcquiredAt,ExpiresAt=excluded.ExpiresAt";
        upsert.Parameters.AddWithValue("@id", draftId); upsert.Parameters.AddWithValue("@user", userName); upsert.Parameters.AddWithValue("@duration", $"+{Math.Max(1,(int)duration.TotalSeconds)} seconds"); upsert.ExecuteNonQuery();
        tx.Commit(); return true;
    }

    public static void ReleaseLock(SqliteConnection cn, long draftId, string userName)
    {
        using var cmd=cn.CreateCommand(); cmd.CommandText="DELETE FROM DraftInvoiceLocks WHERE DraftId=@id AND UserName=@user"; cmd.Parameters.AddWithValue("@id",draftId); cmd.Parameters.AddWithValue("@user",userName); cmd.ExecuteNonQuery();
    }

    public static void TouchCollaborator(SqliteConnection cn, long draftId, string userName, string role="Editor")
    {
        using var cmd=cn.CreateCommand(); cmd.CommandText="INSERT INTO DraftInvoiceCollaborators(DraftId,UserName,Role,JoinedAt,LastSeenAt,Active) VALUES(@id,@user,@role,datetime('now'),datetime('now'),1) ON CONFLICT(DraftId,UserName) DO UPDATE SET Role=excluded.Role,LastSeenAt=datetime('now'),Active=1";
        cmd.Parameters.AddWithValue("@id",draftId); cmd.Parameters.AddWithValue("@user",userName); cmd.Parameters.AddWithValue("@role",role); cmd.ExecuteNonQuery();
    }

    public static DataTable ListCollaborators(SqliteConnection cn, long draftId)
    {
        using var cmd=cn.CreateCommand(); cmd.CommandText="SELECT UserName,Role,JoinedAt,LastSeenAt,Active FROM DraftInvoiceCollaborators WHERE DraftId=@id ORDER BY JoinedAt"; cmd.Parameters.AddWithValue("@id",draftId);
        using var reader=cmd.ExecuteReader(); var table=new DataTable(); table.Load(reader); return table;
    }
}
