using Microsoft.Data.Sqlite;

namespace BATIR;

public class ChecksForm : Form
{
    readonly DataGridView grid = new();
    readonly TextBox no = new();
    readonly TextBox bank = new();
    readonly TextBox party = new();
    readonly TextBox due = new();
    readonly TextBox notes = new();
    readonly NumericUpDown amount = new() { Minimum = 1, Maximum = 999999999999, ThousandsSeparator = true };
    readonly ComboBox type = new();
    readonly ComboBox state = new();
    long editId;
    string oldStatus = "";

    public ChecksForm()
    {
        Text = "BATIR | مدیریت چک‌ها";
        Width = 1080; Height = 650;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Build();
        LoadChecks();
    }

    void Build()
    {
        var form = new TableLayoutPanel { Dock = DockStyle.Top, Height = 135, ColumnCount = 4, RowCount = 3, Padding = new Padding(8) };
        for (int i=0;i<4;i++) form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        AddText(form,"شماره چک",no,0,0); AddText(form,"بانک",bank,1,0);
        AddNum(form,"مبلغ",amount,2,0); AddText(form,"سررسید",due,3,0);
        AddText(form,"طرف حساب",party,0,1); AddCombo(form,"نوع",type,1,1); AddCombo(form,"وضعیت",state,2,1); AddText(form,"توضیحات",notes,3,1);
        var save=new Button{Text="ثبت / ویرایش",Dock=DockStyle.Fill}; save.Click+=(_,_)=>Save();
        var clear=new Button{Text="پاک کردن",Dock=DockStyle.Fill}; clear.Click+=(_,_)=>Clear();
        form.Controls.Add(save,0,2); form.Controls.Add(clear,1,2);
        type.Items.AddRange(new object[]{"دریافتی","پرداختی"});
        state.Items.AddRange(new object[]{"در انتظار","وصول شد","برگشت خورد","لغو شد"});
        type.SelectedIndex=0; state.SelectedIndex=0;
        grid.Dock=DockStyle.Fill; grid.ReadOnly=true; grid.AllowUserToAddRows=false;
        grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect; grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;
        grid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0) Edit(e.RowIndex);};
        Controls.Add(grid); Controls.Add(form);
    }

    void AddText(TableLayoutPanel t,string label,TextBox box,int c,int r)
    {
        var p=new Panel{Dock=DockStyle.Fill}; box.Dock=DockStyle.Fill;
        p.Controls.Add(box); p.Controls.Add(new Label{Text=label,Dock=DockStyle.Right,Width=90,TextAlign=ContentAlignment.MiddleRight}); t.Controls.Add(p,c,r);
    }
    void AddCombo(TableLayoutPanel t,string label,ComboBox box,int c,int r)
    {
        box.Dock=DockStyle.Fill; var p=new Panel{Dock=DockStyle.Fill}; p.Controls.Add(box);
        p.Controls.Add(new Label{Text=label,Dock=DockStyle.Right,Width=90,TextAlign=ContentAlignment.MiddleRight}); t.Controls.Add(p,c,r);
    }
    void AddNum(TableLayoutPanel t,string label,NumericUpDown box,int c,int r)
    {
        box.Dock=DockStyle.Fill; var p=new Panel{Dock=DockStyle.Fill}; p.Controls.Add(box);
        p.Controls.Add(new Label{Text=label,Dock=DockStyle.Right,Width=90,TextAlign=ContentAlignment.MiddleRight}); t.Controls.Add(p,c,r);
    }
    void LoadChecks()
    {
        grid.DataSource=Database.Query(@"SELECT Id,CheckNo AS [شماره],Bank AS [بانک],Amount AS [مبلغ],
DueDate AS [سررسید],Type AS [نوع],Status AS [وضعیت],PartyName AS [طرف حساب],
CASE WHEN CustomerId IS NOT NULL THEN 'مشتری' WHEN SupplierId IS NOT NULL THEN 'تأمین‌کننده' ELSE '' END AS [حساب مرتبط],Notes AS [توضیحات]
FROM Checks ORDER BY Id DESC");
    }
    void Edit(int row)
    {
        editId=Convert.ToInt64(grid.Rows[row].Cells["Id"].Value);
        var d=Database.Query("SELECT CheckNo,Bank,Amount,DueDate,Type,Status,PartyName,Notes FROM Checks WHERE Id=@id",new SqliteParameter("@id",editId));
        if(d.Rows.Count==0)return;
        no.Text=Convert.ToString(d.Rows[0]["CheckNo"])??""; bank.Text=Convert.ToString(d.Rows[0]["Bank"])??"";
        amount.Value=Convert.ToDecimal(d.Rows[0]["Amount"]); due.Text=Convert.ToString(d.Rows[0]["DueDate"])??"";
        type.Text=Convert.ToString(d.Rows[0]["Type"])??"دریافتی"; state.Text=Convert.ToString(d.Rows[0]["Status"])??"در انتظار"; oldStatus=state.Text;
        party.Text=Convert.ToString(d.Rows[0]["PartyName"])??""; notes.Text=Convert.ToString(d.Rows[0]["Notes"])??"";
    }
    void Save()
    {
        if (!PermissionService.Require("Checks.Edit", this)) return;
        if (amount.Value <= 0) { MessageBox.Show("مبلغ چک را وارد کنید."); return; }
        var checkNo = no.Text.Trim();
        if (string.IsNullOrWhiteSpace(checkNo)) { MessageBox.Show("شماره چک را وارد کنید."); return; }

        var partyName = party.Text.Trim();
        long customerId = 0, supplierId = 0;
        if (partyName != "")
        {
            if (type.Text == "دریافتی") customerId = FindPartyId("Customers", partyName);
            else supplierId = FindPartyId("Suppliers", partyName);
            if (customerId == 0 && supplierId == 0)
            {
                if (MessageBox.Show("طرف حساب پیدا نشد. چک بدون لینک حساب ذخیره شود؟", "تأیید",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            }
        }

        try
        {
            using var cn = Database.Open();
            using var tx = cn.BeginTransaction();

            long id;
            string before = "";
            long oldAmount = 0;
            string oldType = type.Text;
            string oldParty = partyName;
            long oldCustomer = 0, oldSupplier = 0;

            if (editId == 0)
            {
                using var duplicate = cn.CreateCommand();
                duplicate.Transaction = tx;
                duplicate.CommandText = "SELECT COUNT(*) FROM Checks WHERE CheckNo=@no AND Type=@type";
                duplicate.Parameters.AddWithValue("@no", checkNo);
                duplicate.Parameters.AddWithValue("@type", type.Text);
                if (Convert.ToInt64(duplicate.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("این شماره چک با همین نوع قبلاً ثبت شده است.");

                using var cmd = cn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO Checks(CheckNo,Bank,Amount,DueDate,Type,Status,PartyName,Notes,CustomerId,SupplierId,LedgerStatus)
VALUES(@no,@bank,@amount,@due,@type,@state,@party,@notes,@customer,@supplier,NULL);
SELECT last_insert_rowid();";
                AddCheckParameters(cmd, checkNo, customerId, supplierId);
                id = Convert.ToInt64(cmd.ExecuteScalar());
            }
            else
            {
                id = editId;
                using var old = cn.CreateCommand();
                old.Transaction = tx;
                old.CommandText = "SELECT Amount,Type,Status,PartyName,CustomerId,SupplierId FROM Checks WHERE Id=@id";
                old.Parameters.AddWithValue("@id", id);
                using var reader = old.ExecuteReader();
                if (!reader.Read()) throw new InvalidOperationException("چک موردنظر پیدا نشد.");
                oldAmount = Convert.ToInt64(reader["Amount"]);
                oldType = Convert.ToString(reader["Type"]) ?? type.Text;
                before = Convert.ToString(reader["Status"]) ?? "";
                oldParty = Convert.ToString(reader["PartyName"]) ?? "";
                oldCustomer = reader["CustomerId"] == DBNull.Value ? 0 : Convert.ToInt64(reader["CustomerId"]);
                oldSupplier = reader["SupplierId"] == DBNull.Value ? 0 : Convert.ToInt64(reader["SupplierId"]);
                reader.Close();

                using var duplicate = cn.CreateCommand();
                duplicate.Transaction = tx;
                duplicate.CommandText = "SELECT COUNT(*) FROM Checks WHERE CheckNo=@no AND Type=@type AND Id<>@id";
                duplicate.Parameters.AddWithValue("@no", checkNo);
                duplicate.Parameters.AddWithValue("@type", type.Text);
                duplicate.Parameters.AddWithValue("@id", id);
                if (Convert.ToInt64(duplicate.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("این شماره چک با همین نوع قبلاً ثبت شده است.");

                using var cmd = cn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"UPDATE Checks SET CheckNo=@no,Bank=@bank,Amount=@amount,DueDate=@due,Type=@type,
Status=@state,PartyName=@party,Notes=@notes,CustomerId=@customer,SupplierId=@supplier WHERE Id=@id";
                AddCheckParameters(cmd, checkNo, customerId, supplierId);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }

            if (editId != 0 && before == "وصول شد" && oldAmount != (long)amount.Value)
                throw new InvalidOperationException("مبلغ چک وصول‌شده را نمی‌توان تغییر داد. ابتدا وضعیت چک را اصلاح کنید.");

            var changedTypeOrParty = editId != 0 && (oldType != type.Text || oldCustomer != customerId || oldSupplier != supplierId || oldParty != partyName);
            if (editId != 0 && before == "وصول شد" && changedTypeOrParty)
                throw new InvalidOperationException("چک وصول‌شده را نمی‌توان با تغییر نوع یا طرف حساب ویرایش کرد.");

            if (before != state.Text || editId == 0)
            {
                string? ledgerType = null;
                if (before == "وصول شد" && state.Text != "وصول شد")
                    ledgerType = oldType == "دریافتی" ? "CheckReceiptReversal" : "CheckPaymentReversal";
                else if (state.Text == "وصول شد")
                    ledgerType = type.Text == "دریافتی" ? "CheckClearedReceipt" : "CheckClearedPayment";

                if (ledgerType != null)
                {
                    using var cash = cn.CreateCommand();
                    cash.Transaction = tx;
                    cash.CommandText = @"INSERT INTO CashTransactions(DateText,Type,Amount,Description,UserName,CustomerId,SupplierId,PaymentMethod,ReferenceType,ReferenceId)
VALUES(@d,@t,@a,@desc,'کاربر',@customer,@supplier,'چک','Check',@id)";
                    cash.Parameters.AddWithValue("@d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    cash.Parameters.AddWithValue("@t", ledgerType);
                    cash.Parameters.AddWithValue("@a", amount.Value);
                    cash.Parameters.AddWithValue("@desc", "چک " + checkNo + " | " + partyName);
                    cash.Parameters.AddWithValue("@customer", customerId > 0 ? (object)customerId : DBNull.Value);
                    cash.Parameters.AddWithValue("@supplier", supplierId > 0 ? (object)supplierId : DBNull.Value);
                    cash.Parameters.AddWithValue("@id", id);
                    cash.ExecuteNonQuery();
                }
            }

            using var mark = cn.CreateCommand();
            mark.Transaction = tx;
            mark.CommandText = "UPDATE Checks SET LedgerStatus=@s WHERE Id=@id";
            mark.Parameters.AddWithValue("@s", state.Text == "وصول شد" ? "Cleared" : (state.Text == "برگشت خورد" ? "Returned" : state.Text));
            mark.Parameters.AddWithValue("@id", id);
            mark.ExecuteNonQuery();

            // هر چک ثبت‌شده یک یادآوری سررسید قابل پیگیری ایجاد/به‌روزرسانی می‌کند.
            using (var reminder = cn.CreateCommand())
            {
                reminder.Transaction = tx;
                reminder.CommandText = @"UPDATE Reminders SET Title=@title,DueDate=@due,Type=@type,PartyName=@party,Amount=@amount,Notes=@notes,Done=@done
WHERE ReferenceType='Check' AND ReferenceId=@id;
INSERT INTO Reminders(Title,DueDate,Type,PartyName,Amount,ReferenceType,ReferenceId,Done,Notes)
SELECT @title,@due,@type,@party,@amount,'Check',@id,@done,@notes
WHERE changes()=0;";
                reminder.Parameters.AddWithValue("@title", "سررسید چک " + checkNo);
                reminder.Parameters.AddWithValue("@due", due.Text.Trim());
                reminder.Parameters.AddWithValue("@type", type.Text == "دریافتی" ? "چک دریافتی" : "چک پرداختی");
                reminder.Parameters.AddWithValue("@party", partyName);
                reminder.Parameters.AddWithValue("@amount", (long)amount.Value);
                reminder.Parameters.AddWithValue("@id", id);
                reminder.Parameters.AddWithValue("@done", state.Text == "وصول شد" || state.Text == "لغو شد" ? 1 : 0);
                reminder.Parameters.AddWithValue("@notes", notes.Text.Trim());
                reminder.ExecuteNonQuery();
            }

            using var audit = cn.CreateCommand();
            audit.Transaction = tx;
            audit.CommandText = @"INSERT INTO AuditLog(DateText,UserName,Action,Entity,EntityId,Details)
VALUES(@d,'کاربر',@action,'Check',@id,@details)";
            audit.Parameters.AddWithValue("@d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            audit.Parameters.AddWithValue("@action", editId == 0 ? "ثبت چک" : "ویرایش چک");
            audit.Parameters.AddWithValue("@id", id);
            audit.Parameters.AddWithValue("@details", checkNo + " | مبلغ: " + amount.Value + " | وضعیت: " + state.Text);
            audit.ExecuteNonQuery();

            tx.Commit();
            Clear();
            LoadChecks();
        }
        catch (Exception ex)
        {
            MessageBox.Show("ثبت چک انجام نشد و تغییرات ذخیره نشد.\n" + ex.Message, "خطا",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void AddCheckParameters(SqliteCommand cmd, string checkNo, long customerId, long supplierId)
    {
        cmd.Parameters.AddWithValue("@no", checkNo);
        cmd.Parameters.AddWithValue("@bank", bank.Text.Trim());
        cmd.Parameters.AddWithValue("@amount", (long)amount.Value);
        cmd.Parameters.AddWithValue("@due", due.Text.Trim());
        cmd.Parameters.AddWithValue("@type", type.Text);
        cmd.Parameters.AddWithValue("@state", state.Text);
        cmd.Parameters.AddWithValue("@party", party.Text.Trim());
        cmd.Parameters.AddWithValue("@notes", notes.Text.Trim());
        cmd.Parameters.AddWithValue("@customer", customerId > 0 ? (object)customerId : DBNull.Value);
        cmd.Parameters.AddWithValue("@supplier", supplierId > 0 ? (object)supplierId : DBNull.Value);
    }

    long FindPartyId(string table,string name)
    {
        using var cn=Database.Open();
        using var cmd=cn.CreateCommand();
        cmd.CommandText=$"SELECT Id FROM {table} WHERE Name=@name COLLATE NOCASE ORDER BY Id LIMIT 1";
        cmd.Parameters.AddWithValue("@name",name);
        var value=cmd.ExecuteScalar();
        return value==null || value==DBNull.Value ? 0 : Convert.ToInt64(value);
    }

    SqliteParameter[] P(params SqliteParameter[] extra)
    {
        var a=new List<SqliteParameter>{
            new("@no",no.Text.Trim()),new("@bank",bank.Text.Trim()),new("@amount",(long)amount.Value),
            new("@due",due.Text.Trim()),new("@type",type.Text),new("@state",state.Text),new("@party",party.Text.Trim()),new("@notes",notes.Text.Trim())};
        a.AddRange(extra); return a.ToArray();
    }
    void PostLedgerTransition(long id, string before, string after, long value, string checkType, string partyName, string checkNo, long customerId, long supplierId)
    {
        if (before == after) return;
        using var cn=Database.Open();
        using var tx=cn.BeginTransaction();
        string? ledgerType=null; long signed=value;
        if (before=="وصول شد" && after!="وصول شد") ledgerType=checkType=="دریافتی" ? "CheckReceiptReversal" : "CheckPaymentReversal";
        else if (after=="وصول شد") ledgerType=checkType=="دریافتی" ? "CheckClearedReceipt" : "CheckClearedPayment";
        if (ledgerType!=null)
        {
            using var cmd=cn.CreateCommand(); cmd.Transaction=tx;
            cmd.CommandText=@"INSERT INTO CashTransactions(DateText,Type,Amount,Description,UserName,CustomerId,SupplierId,PaymentMethod,ReferenceType,ReferenceId)
VALUES(@d,@t,@a,@desc,'کاربر',@customer,@supplier,'چک','Check',@id)";
            cmd.Parameters.AddWithValue("@d",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@t",ledgerType); cmd.Parameters.AddWithValue("@a",value);
            cmd.Parameters.AddWithValue("@desc","چک "+checkNo+" | "+partyName);
            cmd.Parameters.AddWithValue("@customer",customerId>0?(object)customerId:DBNull.Value);
            cmd.Parameters.AddWithValue("@supplier",supplierId>0?(object)supplierId:DBNull.Value);
            cmd.Parameters.AddWithValue("@id",id); cmd.ExecuteNonQuery();
        }
        using var mark=cn.CreateCommand(); mark.Transaction=tx;
        mark.CommandText="UPDATE Checks SET LedgerStatus=@s WHERE Id=@id";
        mark.Parameters.AddWithValue("@s",after=="وصول شد" ? "Cleared" : (after=="برگشت خورد" ? "Returned" : after));
        mark.Parameters.AddWithValue("@id",id); mark.ExecuteNonQuery();
        tx.Commit();
    }

    void Clear(){editId=0;oldStatus="";no.Clear();bank.Clear();party.Clear();due.Clear();notes.Clear();amount.Value=1;type.SelectedIndex=0;state.SelectedIndex=0;}
}