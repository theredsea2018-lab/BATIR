using Microsoft.Data.Sqlite;

namespace BATIR;

internal static class AppearanceService
{
    static readonly Color Window = Color.FromArgb(238, 238, 238);
    static readonly Color Surface = Color.FromArgb(245, 245, 245);
    static readonly Color Header = Color.FromArgb(218, 226, 234);
    static readonly Color HeaderDark = Color.FromArgb(62, 82, 101);
    static readonly Color Border = Color.FromArgb(154, 164, 174);
    static readonly Color Accent = Color.FromArgb(211, 224, 237);
    static readonly Color AccentStrong = Color.FromArgb(188, 207, 226);
    static readonly HashSet<Form> StyledForms = new();

    public static void Apply(Form form)
    {
        form.RightToLeft = RightToLeft.Yes;
        form.RightToLeftLayout = true;

        var fontName = Get("UiFont", "Tahoma");
        var sizeText = Get("UiFontSize", "9");
        if (!float.TryParse(sizeText, out var size) || size < 7 || size > 24) size = 9;

        form.Font = new Font(fontName, size, FontStyle.Regular);
        form.BackColor = Window;
        form.ForeColor = Color.FromArgb(35, 35, 35);

        ApplyControls(form, size);

        var back = Get("UiBackgroundColor", "");
        if (AppearanceColorParser.TryParseColor(back, out var color)) form.BackColor = color;

        var imagePath = Get("UiBackgroundImage", "");
        if (File.Exists(imagePath))
        {
            try
            {
                using var source = Image.FromFile(imagePath);
                form.BackgroundImage = new Bitmap(source);
                form.BackgroundImageLayout = ImageLayout.Stretch;
            }
            catch { }
        }
    }

    public static void ApplyOpenForms()
    {
        foreach (var form in Application.OpenForms.Cast<Form>().ToArray())
        {
            if (form.IsDisposed) { StyledForms.Remove(form); continue; }
            if (StyledForms.Contains(form)) continue;
            try
            {
                Apply(form);
                StyledForms.Add(form);
            }
            catch
            {
                // Styling must never prevent an accounting window from opening.
            }
        }
    }

    static void ApplyControls(Control root, float fontSize)
    {
        foreach (Control c in root.Controls)
        {
            c.Font = new Font(root.Font.FontFamily, Math.Max(8, fontSize), c.Font.Bold ? FontStyle.Bold : FontStyle.Regular);

            if (c is Panel panel)
            {
                if (panel.Dock == DockStyle.Top || panel.Dock == DockStyle.Bottom)
                    panel.BackColor = Header;
                else if (panel.BackColor == Color.Empty || panel.BackColor == SystemColors.Control)
                    panel.BackColor = Surface;
            }
            else if (c is Label label)
            {
                label.ForeColor = label.Font.Bold ? HeaderDark : Color.FromArgb(45, 55, 65);
            }
            else if (c is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = AccentStrong;
                button.FlatAppearance.MouseDownBackColor = Accent;
                button.BackColor = Accent;
                button.ForeColor = Color.FromArgb(30, 45, 60);
                button.Padding = new Padding(4, 1, 4, 1);
                button.UseVisualStyleBackColor = false;
            }
            else if (c is TextBox textBox)
            {
                textBox.BackColor = Color.White;
                textBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (c is ComboBox combo)
            {
                combo.BackColor = Color.White;
                combo.FlatStyle = FlatStyle.Standard;
            }
            else if (c is NumericUpDown numeric)
            {
                numeric.BackColor = Color.White;
            }
            else if (c is CheckBox check)
            {
                check.ForeColor = Color.FromArgb(45, 55, 65);
            }
            else if (c is GroupBox group)
            {
                group.ForeColor = HeaderDark;
            }
            else if (c is DataGridView grid)
            {
                grid.BackgroundColor = Surface;
                grid.BorderStyle = BorderStyle.FixedSingle;
                grid.GridColor = Border;
                grid.EnableHeadersVisualStyles = false;
                grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderDark;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
                grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                grid.RowHeadersDefaultCellStyle.BackColor = Header;
                grid.DefaultCellStyle.BackColor = Color.White;
                grid.DefaultCellStyle.ForeColor = Color.FromArgb(30, 30, 30);
                grid.DefaultCellStyle.SelectionBackColor = AccentStrong;
                grid.DefaultCellStyle.SelectionForeColor = Color.Black;
                grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 249, 250);
                grid.RowTemplate.Height = Math.Max(25, grid.RowTemplate.Height);
            }
            else if (c is TabControl tabs)
            {
                tabs.BackColor = Window;
            }

            if (c.Controls.Count > 0)
                ApplyControls(c, fontSize);
        }
    }

    public static void Save(string font, float size, Color color, string imagePath)
    {
        using var cn = Database.Open();
        using var tx = cn.BeginTransaction();
        Set(cn, tx, "UiFont", string.IsNullOrWhiteSpace(font) ? "Tahoma" : font);
        Set(cn, tx, "UiFontSize", size.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Set(cn, tx, "UiBackgroundColor", ColorToString(color));
        Set(cn, tx, "UiBackgroundImage", imagePath);
        tx.Commit();
        StyledForms.Clear();
    }

    static string Get(string key, string fallback)
    {
        using var cn = Database.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM Settings WHERE Key=@k LIMIT 1";
        cmd.Parameters.AddWithValue("@k", key);
        return Convert.ToString(cmd.ExecuteScalar()) ?? fallback;
    }

    static void Set(SqliteConnection cn, SqliteTransaction tx, string key, string value)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Settings(Key,Value,UpdatedAt) VALUES(@k,@v,datetime('now')) ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value,UpdatedAt=datetime('now')";
        cmd.Parameters.AddWithValue("@k", key);
        cmd.Parameters.AddWithValue("@v", value);
        cmd.ExecuteNonQuery();
    }

    static string ColorToString(Color c) => $"{c.R},{c.G},{c.B}";
}

internal static class AppearanceColorParser
{
    public static bool TryParseColor(string value, out Color color)
    {
        color = Color.Empty;
        var p = value.Split(',');
        if (p.Length != 3) return false;
        if (!int.TryParse(p[0], out var r) || !int.TryParse(p[1], out var g) || !int.TryParse(p[2], out var b)) return false;
        if (r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255) return false;
        color = Color.FromArgb(r, g, b);
        return true;
    }
}