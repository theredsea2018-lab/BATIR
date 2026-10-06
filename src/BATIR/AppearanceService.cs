using System.Drawing.Drawing2D;
using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class AppearanceService
{
    public static void Apply(Form form)
    {
        form.RightToLeft = RightToLeft.Yes;
        form.RightToLeftLayout = true;
        var fontName = Get("UiFont", "Tahoma");
        var sizeText = Get("UiFontSize", "9");
        if (!float.TryParse(sizeText, out var size) || size < 7 || size > 24) size = 9;
        form.Font = new Font(fontName, size, FontStyle.Regular);
        var back = Get("UiBackgroundColor", "");
        if (Color.TryParse(back, out var color)) form.BackColor = color;
        var imagePath = Get("UiBackgroundImage", "");
        if (File.Exists(imagePath)) { try { using var source=Image.FromFile(imagePath); form.BackgroundImage=new Bitmap(source); form.BackgroundImageLayout=ImageLayout.Stretch; } catch { } }
    }
    public static void Save(string font, float size, Color color, string imagePath)
    {
        using var cn=Database.Open(); using var tx=cn.BeginTransaction();
        Set(cn,tx,"UiFont",font); Set(cn,tx,"UiFontSize",size.ToString(System.Globalization.CultureInfo.InvariantCulture)); Set(cn,tx,"UiBackgroundColor",ColorToString(color)); Set(cn,tx,"UiBackgroundImage",imagePath); tx.Commit();
    }
    static string Get(string key,string fallback){ using var cn=Database.Open(); using var cmd=cn.CreateCommand(); cmd.CommandText="SELECT Value FROM Settings WHERE Key=@k LIMIT 1"; cmd.Parameters.AddWithValue("@k",key); return Convert.ToString(cmd.ExecuteScalar())??fallback; }
    static void Set(SqliteConnection cn,SqliteTransaction tx,string key,string value){using var cmd=cn.CreateCommand();cmd.Transaction=tx;cmd.CommandText="INSERT INTO Settings(Key,Value,UpdatedAt) VALUES(@k,@v,datetime('now')) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value,UpdatedAt=datetime('now')";cmd.Parameters.AddWithValue("@k",key);cmd.Parameters.AddWithValue("@v",value);cmd.ExecuteNonQuery();}
    static string ColorToString(Color c)=>$"{c.R},{c.G},{c.B}";
}

internal static class ColorExtensions
{
    public static bool TryParse(this Color _, string value, out Color color)
    {
        color=Color.Empty; var p=value.Split(','); if(p.Length!=3) return false; if(!int.TryParse(p[0],out var r)||!int.TryParse(p[1],out var g)||!int.TryParse(p[2],out var b)) return false; if(r<0||r>255||g<0||g>255||b<0||b>255)return false; color=Color.FromArgb(r,g,b); return true;
    }
}
