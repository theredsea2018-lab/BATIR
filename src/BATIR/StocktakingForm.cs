using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public class StocktakingForm : Form
{
    readonly TextBox search = new();
    readonly TextBox note = new();
    readonly DataGridView grid = new();
    readonly Button save = new() { Text = "ثبت شمارش و اصلاح موجودی", Dock = DockStyle.Fill };
    DataTable products = new();

    public StocktakingForm()
    {
        Text = "BATIR | انبارگردانی";
        Width = 1050; Height = 700;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;

        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 80, ColumnCount = 3, Padding = new Padding(8) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        search.Dock = DockStyle.Fill;
        search.TextChanged += (_, _) => LoadProducts();
        note.Dock = DockStyle.Fill;
        top.Controls.Add(search, 0, 0);
        top.Controls.Add(note, 1, 0);
        top.Controls.Add(save, 2, 0);

        grid.Dock = DockStyle.Fill;
        grid.AllowUserToAddRows = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        save.Click += (_, _) => SaveAdjustments();

        Controls.Add(grid);
        Controls.Add(top);
        LoadProducts();
    }

    void LoadProducts()
    {
        var q = search.Text.Trim();
        products = string.IsNullOrWhiteSpace(q)
            ? Database.Query(@"SELECT Id,Name AS [کالا],Barcode AS [بارکد],Stock AS [موجودی سیستم]
                               FROM Products WHERE Active=1 ORDER BY Name")
            : Database.Query(@"SELECT Id,Name AS [کالا],Barcode AS [بارکد],Stock AS [موجودی سیستم]
                               FROM Products WHERE Active=1 AND
                               (Name LIKE @q OR Barcode LIKE @q OR Brand LIKE @q OR Category LIKE @q)
                               ORDER BY Name", new SqliteParameter("@q", "%" + q + "%"));

        if (!products.Columns.Contains("موجودی شمارش"))
            products.Columns.Add("موجودی شمارش", typeof(long));
        if (!products.Columns.Contains("اختلاف"))
            products.Columns.Add("اختلاف", typeof(long));

        foreach (DataRow row in products.Rows)
        {
            row["موجودی شمارش"] = Convert.ToInt64(row["موجودی سیستم"]);
            row["اختلاف"] = 0L;
        }

        grid.DataSource = products;
        if (grid.Columns["Id"] != null) grid.Columns["Id"].Visible = false;
        if (grid.Columns["موجودی سیستم"] != null) grid.Columns["موجودی سیستم"].ReadOnly = true;
        if (grid.Columns["اختلاف"] != null) grid.Columns["اختلاف"].ReadOnly = true;
        grid.CellValueChanged -= GridCellValueChanged;
        grid.CellValueChanged += GridCellValueChanged;
    }

    void GridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || grid.Columns["موجودی شمارش"] == null ||
            e.ColumnIndex != grid.Columns["موجودی شمارش"].Index) return;
        var row = grid.Rows[e.RowIndex];
        if (!long.TryParse(Convert.ToString(row.Cells["موجودی شمارش"].Value), out var counted) || counted < 0)
            counted = 0;
        var current = Convert.ToInt64(row.Cells["موجودی سیستم"].Value);
        row.Cells["اختلاف"].Value = counted - current;
    }

    void SaveAdjustments()
    {
        if (!PermissionService.Require("Inventory.Edit", this)) return;
        var changes = new List<(long id, long current, long counted, long diff)>();
        foreach (DataRow row in products.Rows)
        {
            var current = Convert.ToInt64(row["موجودی سیستم"]);
            var counted = Convert.ToInt64(row["موجودی شمارش"]);
            if (counted < 0) throw new InvalidOperationException("موجودی شمارش‌شده نمی‌تواند منفی باشد: " + Convert.ToString(row["کالا"]));
            var diff = counted - current;
            if (diff != 0) changes.Add((Convert.ToInt64(row["Id"]), current, counted, diff));
        }

        if (changes.Count == 0)
        {
            MessageBox.Show("هیچ اختلافی برای ثبت وجود ندارد.");
            return;
        }

        if (MessageBox.Show($"تعداد {changes.Count} کالا اصلاح می‌شود. ادامه؟",
            "انبارگردانی", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

        try
        {
            using var cn = Database.Open();
            using var tx = cn.BeginTransaction();
            var now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var item in changes)
            {
                using var update = cn.CreateCommand();
                update.Transaction = tx;
                update.CommandText = "UPDATE Products SET Stock=@stock WHERE Id=@id";
                update.Parameters.AddWithValue("@stock", item.counted);
                update.Parameters.AddWithValue("@id", item.id);
                update.ExecuteNonQuery();

                using var audit = cn.CreateCommand();
                audit.Transaction = tx;
                audit.CommandText = @"INSERT INTO InventoryAdjustments
(DateText,ProductId,BeforeStock,CountedStock,Difference,Note,UserName)
VALUES(@date,@product,@before,@counted,@difference,@note,'کاربر')";
                audit.Parameters.AddWithValue("@date", now);
                audit.Parameters.AddWithValue("@product", item.id);
                audit.Parameters.AddWithValue("@before", item.current);
                audit.Parameters.AddWithValue("@counted", item.counted);
                audit.Parameters.AddWithValue("@difference", item.diff);
                audit.Parameters.AddWithValue("@note", note.Text.Trim());
                audit.ExecuteNonQuery();

                using var log = cn.CreateCommand();
                log.Transaction = tx;
                log.CommandText = @"INSERT INTO AuditLog(DateText,UserName,Action,Entity,EntityId,Details)
VALUES(@date,'کاربر','اصلاح انبار','Product',@id,@details)";
                log.Parameters.AddWithValue("@date", now);
                log.Parameters.AddWithValue("@id", item.id);
                log.Parameters.AddWithValue("@details",
                    "موجودی قبل: " + item.current + " | شمارش: " + item.counted +
                    " | اختلاف: " + item.diff + " | علت: " + note.Text.Trim());
                log.ExecuteNonQuery();
            }

            tx.Commit();
            MessageBox.Show("انبارگردانی با موفقیت ثبت شد.");
            note.Clear();
            LoadProducts();
        }
        catch (Exception ex)
        {
            MessageBox.Show("انبارگردانی ثبت نشد و تغییری ذخیره نشد.\n" + ex.Message,
                "خطا", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
