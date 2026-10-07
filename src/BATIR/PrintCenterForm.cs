using Microsoft.Data.Sqlite;
using System.Data;
using System.Drawing.Printing;

namespace BATIR;

public sealed class PrintCenterForm : Form
{
    readonly TextBox invoiceNo = new();
    readonly TextBox barcode = new();
    readonly ComboBox paper = new();
    readonly PrintDocument document = new();
    PrintMode mode;
    DataTable? header;
    DataTable? items;
    enum PrintMode { Invoice, Barcode }

    public PrintCenterForm()
    {
        Text = "BATIR | چاپ فاکتور و بارکد"; Width = 760; Height = 430;
        StartPosition = FormStartPosition.CenterParent;
        RightToLeft = RightToLeft.Yes; RightToLeftLayout = true;
        Build(); document.PrintPage += PrintPage;
    }

    void Build()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7, Padding = new Padding(14) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        Add(t, "شماره فاکتور", invoiceNo, 0); Add(t, "بارکد کالا", barcode, 1);
        paper.Items.AddRange(new object[] { "A4", "80 میلی‌متر" }); paper.SelectedIndex = 0; paper.Dock = DockStyle.Fill;
        t.Controls.Add(new Label { Text="نوع کاغذ", Dock=DockStyle.Fill, TextAlign=ContentAlignment.MiddleRight },0,2); t.Controls.Add(paper,1,2);
        var p = new Button { Text="پیش‌نمایش فاکتور", Dock=DockStyle.Fill, Height=55 }; p.Click += (_,_) => { mode=PrintMode.Invoice; Preview(); };
        var f = new Button { Text="چاپ فاکتور", Dock=DockStyle.Fill, Height=55 }; f.Click += (_,_) => { mode=PrintMode.Invoice; Print(); };
        var bp = new Button {
        if(!PermissionService.Require(mode == PrintMode.Barcode ? "Products.View" : "Sales.Print", this)) return; Text="پیش‌نمایش برچسب", Dock=DockStyle.Fill, Height=55 }; bp.Click += (_,_) => { mode=PrintMode.Barcode; Preview(); };
        var bf = new Button { Text="چاپ برچسب بارکد", Dock=DockStyle.Fill, Height=55 }; bf.Click += (_,_) => { mode=PrintMode.Barcode; Print(); };
        t.Controls.Add(p,0,4); t.Controls.Add(f,1,4); t.Controls.Add(bp,0,5); t.Controls.Add(bf,1,5);
        var h = new Label { Text="فاکتور: شماره سند را وارد کنید  |  بارکد: کد یا بارکد کالا را وارد کنید", Dock=DockStyle.Fill, TextAlign=ContentAlignment.MiddleCenter };
        t.Controls.Add(h,0,6); t.SetColumnSpan(h,2); Controls.Add(t);
    }

    static void Add(TableLayoutPanel t,string text,Control c,int row)
    { c.Dock=DockStyle.Fill; t.Controls.Add(new Label{Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight},0,row); t.Controls.Add(c,1,row); }

    void Prepare()
    {
        if (mode == PrintMode.Invoice)
        {
            var n=invoiceNo.Text.Trim(); if(n=="") throw new InvalidOperationException("شماره فاکتور را وارد کنید.");
            header=Database.Query("SELECT i.*,COALESCE(c.Name,'') CustomerDisplay FROM Invoices i LEFT JOIN Customers c ON c.Id=i.CustomerId WHERE i.InvoiceNo=@n LIMIT 1",new SqliteParameter("@n",n));
            if(header.Rows.Count==0) throw new InvalidOperationException("فاکتور پیدا نشد.");
            var id=Convert.ToInt64(header.Rows[0]["Id"]);
            items=Database.Query("SELECT ii.Quantity,ii.UnitPrice,ii.Discount,p.Name FROM InvoiceItems ii JOIN Products p ON p.Id=ii.ProductId WHERE ii.InvoiceId=@id ORDER BY ii.Id",new SqliteParameter("@id",id));
        }
        else
        {
            var b=barcode.Text.Trim(); if(b=="") throw new InvalidOperationException("بارکد کالا را وارد کنید.");
            header=Database.Query("SELECT Id,Name,Code,Barcode,SalePrice FROM Products WHERE Active=1 AND (Barcode=@b OR Code=@b) LIMIT 1",new SqliteParameter("@b",b));
            if(header.Rows.Count==0) throw new InvalidOperationException("کالا با این بارکد پیدا نشد.");
        }
        document.DefaultPageSettings.PaperSize = paper.SelectedIndex==1 ? new PaperSize("BATIR80",315,1000) : new PaperSize("A4",827,1169);
        document.DefaultPageSettings.Margins=new Margins(25,25,25,25);
    }

    void Preview(){try{Prepare();using var d=new PrintPreviewDialog{Document=document,Width=1100,Height=800};d.ShowDialog(this);}catch(Exception ex){MessageBox.Show(ex.Message,"BATIR");}}
    void Print(){try{Prepare();using var d=new PrintDialog{Document=document,UseEXDialog=true};if(d.ShowDialog(this)!=DialogResult.OK)return;document.PrintController=new StandardPrintController();document.Print();}catch(Exception ex){MessageBox.Show("چاپ انجام نشد.\n"+ex.Message,"BATIR");}}

    void PrintPage(object? sender,PrintPageEventArgs e)
    {
        e.HasMorePages=false; using var title=new Font("Tahoma",14,FontStyle.Bold); using var bold=new Font("Tahoma",9,FontStyle.Bold); using var normal=new Font("Tahoma",9);
        var b=e.MarginBounds; var y=b.Top;
        if(mode==PrintMode.Barcode){DrawBarcode(e.Graphics,b,bold,normal);return;}
        Draw(e.Graphics,"باتیر | BATIR",title,b,ref y,32); var h=header!.Rows[0];
        Draw(e.Graphics,"فاکتور "+(Convert.ToString(h["Type"])=="Purchase"?"خرید":"فروش")+"  شماره: "+h["InvoiceNo"],bold,b,ref y,24);
        Draw(e.Graphics,"تاریخ: "+h["DateText"]+"    طرف حساب: "+h["CustomerDisplay"],normal,b,ref y,22);
        using var pen=new Pen(Color.Black);e.Graphics.DrawLine(pen,b.Left,y,b.Right,y);y+=8;
        Draw(e.Graphics,"کالا                         تعداد          فی             تخفیف             جمع",bold,b,ref y,24);
        long total=0;
        foreach(DataRow r in items!.Rows){long q=Convert.ToInt64(r["Quantity"]),u=Convert.ToInt64(r["UnitPrice"]),d=Convert.ToInt64(r["Discount"]),sum=q*u-d;total+=sum;Draw(e.Graphics,$"{r["Name"]}    {q:N0}    {u:N0}    {d:N0}    {sum:N0}",normal,b,ref y,23);if(y>b.Bottom-100)break;}
        e.Graphics.DrawLine(pen,b.Left,y,b.Right,y);y+=8;Draw(e.Graphics,"جمع کل: "+total.ToString("N0")+" ریال",bold,b,ref y,27);Draw(e.Graphics,"پرداختی: "+Convert.ToInt64(h["Paid"]).ToString("N0")+" ریال",normal,b,ref y,23);Draw(e.Graphics,"توضیحات: "+Convert.ToString(h["Notes"]),normal,b,ref y,23);
    }

    void DrawBarcode(Graphics g,Rectangle b,Font bold,Font normal)
    {
        var p=header!.Rows[0];var code=Convert.ToString(p["Barcode"]);if(string.IsNullOrWhiteSpace(code))code=Convert.ToString(p["Code"]);var y=b.Top+8;Draw(g,Convert.ToString(p["Name"])??"",bold,b,ref y,28);
        var r=new Rectangle(b.Left+10,y,b.Width-20,100);Code39(g,code??"",r);y+=110;Draw(g,code??"",normal,b,ref y,24);Draw(g,Convert.ToInt64(p["SalePrice"]).ToString("N0")+" ریال",bold,b,ref y,26);
    }

    static void Draw(Graphics g,string text,Font font,Rectangle b,ref int y,int h){using var br=new SolidBrush(Color.Black);var sf=new StringFormat{Alignment=StringAlignment.Far,LineAlignment=StringAlignment.Center,FormatFlags=StringFormatFlags.DirectionRightToLeft};g.DrawString(text,font,br,new Rectangle(b.Left,y,b.Width,h),sf);y+=h;}

    static readonly Dictionary<char,string> C39=new(){
        ['0']="nnnwwnwnn",['1']="wnnwnnnnw",['2']="nnwwnnnnw",['3']="wnwwnnnnn",['4']="nnnwwnnnw",['5']="wnnwwnnnn",['6']="nnwwwnnnn",['7']="nnnwnnwnw",['8']="wnnwnnwnn",['9']="nnwwnnwnn",
        ['A']="wnnnnwnnw",['B']="nnwnnwnnw",['C']="wnwnnwnnn",['D']="nnnnwwnnw",['E']="wnnnwwnnn",['F']="nnwnwwnnn",['G']="nnnnnwwnw",['H']="wnnnnwwnn",['I']="nnwnnwwnn",['J']="nnnnwwwnn",
        ['-']="nnnwnnwnw",['*']="nwnnwnwnn"
    };
    static void Code39(Graphics g,string text,Rectangle r)
    {
        text=text.ToUpperInvariant();if(text.Any(c=>!C39.ContainsKey(c)||c=='*'))return;text="*"+text+"*";var seq=text.SelectMany(c=>C39[c]+"n").ToArray();int wide=seq.Count(c=>c=='w');int narrow=seq.Length-wide;float unit=Math.Max(1f,(r.Width-20)/(narrow+wide*2.2f));float x=r.Left+10;using var br=new SolidBrush(Color.Black);
        foreach(var c in seq){float w=(c=='w'?2.2f:1f)*unit;if(c!='n'&&((Array.IndexOf(seq,c)%2)==0)){};x+=w;}
        x=r.Left+10;int i=0;foreach(char c in seq){float w=(c=='w'?2.2f:1f)*unit;if(i%2==0)g.FillRectangle(br,x,r.Top,w,r.Height);x+=w;i++;}
    }
}
