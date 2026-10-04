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

    public ChecksForm()
    {
        Text = "BATIR | مدیریت چک‌ها";
        Width = 1050; Height = 650;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Build();
        LoadChecks();
    }

    void Build()
    {
        var form = new TableLayoutPanel { Dock = DockStyle.Top, Height = 160, ColumnCount = 4, RowCount = 3, Padding = new Padding(8) };
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
DueDate AS [سررسید],Type AS [نوع],Status AS [وضعیت],PartyName AS [طرف حساب],Notes AS [توضیحات]
FROM Checks ORDER BY Id DESC");
    }
    void Edit(int row)
    {
        editId=Convert.ToInt64(grid.Rows[row].Cells["Id"].Value);
        var d=Database.Query("SELECT CheckNo,Bank,Amount,DueDate,Type,Status,PartyName,Notes FROM Checks WHERE Id=@id",new SqliteParameter("@id",editId));
        if(d.Rows.Count==0)return;
        no.Text=Convert.ToString(d.Rows[0]["CheckNo"])??""; bank.Text=Convert.ToString(d.Rows[0]["Bank"])??"";
        amount.Value=Convert.ToDecimal(d.Rows[0]["Amount"]); due.Text=Convert.ToString(d.Rows[0]["DueDate"])??"";
        type.Text=Convert.ToString(d.Rows[0]["Type"])??"دریافتی"; state.Text=Convert.ToString(d.Rows[0]["Status"])??"در انتظار";
        party.Text=Convert.ToString(d.Rows[0]["PartyName"])??""; notes.Text=Convert.ToString(d.Rows[0]["Notes"])??"";
    }
    void Save()
    {
        if(amount.Value<=0){MessageBox.Show("مبلغ چک را وارد کنید.");return;}
        if(editId==0) Database.Execute(@"INSERT INTO Checks(CheckNo,Bank,Amount,DueDate,Type,Status,PartyName,Notes)
VALUES(@no,@bank,@amount,@due,@type,@state,@party,@notes)",P());
        else Database.Execute(@"UPDATE Checks SET CheckNo=@no,Bank=@bank,Amount=@amount,DueDate=@due,Type=@type,Status=@state,PartyName=@party,Notes=@notes WHERE Id=@id",P(new SqliteParameter("@id",editId)));
        Clear(); LoadChecks();
    }
    SqliteParameter[] P(params SqliteParameter[] extra)
    {
        var a=new List<SqliteParameter>{
            new("@no",no.Text.Trim()),new("@bank",bank.Text.Trim()),new("@amount",(long)amount.Value),
            new("@due",due.Text.Trim()),new("@type",type.Text),new("@state",state.Text),new("@party",party.Text.Trim()),new("@notes",notes.Text.Trim())};
        a.AddRange(extra); return a.ToArray();
    }
    void Clear(){editId=0;no.Clear();bank.Clear();party.Clear();due.Clear();notes.Clear();amount.Value=1;type.SelectedIndex=0;state.SelectedIndex=0;}
}