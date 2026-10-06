using Microsoft.Data.Sqlite;
using System.Data;

namespace BATIR;

public sealed class RemindersForm : Form
{
    readonly DataGridView grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    readonly TextBox title = new();
    readonly TextBox party = new();
    readonly TextBox amount = new();
    readonly TextBox notes = new();
    readonly DateTimePicker dueDate = new() { Format = DateTimePickerFormat.Short };
    readonly ComboBox type = new();

    public RemindersForm()
    {
        Text = "BATIR | یادآوری سررسیدها"; Width = 1050; Height = 650;
        StartPosition = FormStartPosition.CenterParent; RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        type.Items.AddRange(new object[] { "بدهی مشتری", "طلب از تأمین‌کننده", "چک دریافتی", "چک پرداختی", "سایر" });
        type.SelectedIndex = 0;

        var editor = new TableLayoutPanel { Dock = DockStyle.Top, Height = 125, ColumnCount = 4, RowCount = 2, Padding = new Padding(8) };
        for (int i = 0; i < 4; i++) editor.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        Add(editor, "عنوان", title, 0, 0); Add(editor, "طرف حساب", party, 1, 0); Add(editor, "مبلغ", amount, 2, 0); Add(editor, "نوع", type, 3, 0);
        Add(editor, "سررسید", dueDate, 0, 1); Add(editor, "توضیحات", notes, 1, 1);
        var add = new Button { Text = "ثبت یادآوری", Dock = DockStyle.Fill };
        add.Click += (_, _) => AddReminder(); editor.Controls.Add(add, 2, 1);
        var done = new Button { Text = "علامت‌گذاری انجام‌شده", Dock = DockStyle.Fill };
        done.Click += (_, _) => MarkDone(); editor.Controls.Add(done, 3, 1);
        Controls.Add(grid); Controls.Add(editor);
        LoadReminders();
    }

    static void Add(TableLayoutPanel panel, string label, Control control, int column, int row)
    {
        var box = new Panel { Dock = DockStyle.Fill, Padding = new Padding(3) };
        box.Controls.Add(control); control.Dock = DockStyle.Fill;
        box.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, Height = 22 });
        panel.Controls.Add(box, column, row);
    }

    void AddReminder()
    {
        if (string.IsNullOrWhiteSpace(title.Text)) { MessageBox.Show("عنوان یادآوری را وارد کنید."); return; }
        if (!long.TryParse(amount.Text.Trim(), out var value)) value = 0;
        Database.Execute(@"INSERT INTO Reminders(Title,DueDate,Type,PartyName,Amount,Notes)
VALUES(@title,@date,@type,@party,@amount,@notes)",
            new SqliteParameter("@title", title.Text.Trim()),
            new SqliteParameter("@date", dueDate.Value.ToString("yyyy-MM-dd")),
            new SqliteParameter("@type", type.SelectedItem?.ToString() ?? "سایر"),
            new SqliteParameter("@party", party.Text.Trim()),
            new SqliteParameter("@amount", value),
            new SqliteParameter("@notes", notes.Text.Trim()));
        title.Clear(); party.Clear(); amount.Clear(); notes.Clear(); LoadReminders();
    }

    void MarkDone()
    {
        if (grid.CurrentRow == null) return;
        var id = Convert.ToInt64(grid.CurrentRow.Cells["Id"].Value);
        Database.Execute("UPDATE Reminders SET Done=1 WHERE Id=@id", new SqliteParameter("@id", id));
        LoadReminders();
    }

    void LoadReminders()
    {
        var dt = Database.Query(@"SELECT Id,Title AS 'عنوان',DueDate AS 'سررسید',Type AS 'نوع',PartyName AS 'طرف حساب',Amount AS 'مبلغ',
CASE WHEN Done=1 THEN 'انجام‌شده' WHEN DueDate < date('now','localtime') THEN 'معوق' ELSE 'باز' END AS 'وضعیت',Notes AS 'توضیحات'
FROM Reminders ORDER BY Done ASC, DueDate ASC, Id DESC");
        grid.DataSource = dt;
        if (grid.Columns.Contains("Id")) grid.Columns["Id"].Visible = false;
    }
}
