namespace BATIR;

public class AppearanceSettingsForm : Form
{
    readonly TextBox font=new(){Text="Tahoma"};
    readonly NumericUpDown size=new(){Minimum=7,Maximum=24,Value=9,DecimalPlaces=1,Increment=.5m};
    readonly Button color=new(){Text="انتخاب رنگ زمینه",Dock=DockStyle.Fill};
    readonly TextBox image=new(){Dock=DockStyle.Fill};
    readonly Label preview=new(){Text="پیش‌نمایش BATIR",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Height=70};
    Color selected=SystemColors.Control;
    public AppearanceSettingsForm(){
        Text="BATIR | ظاهر برنامه"; Width=620; Height=360; StartPosition=FormStartPosition.CenterParent; RightToLeft=RightToLeft.Yes; RightToLeftLayout=true;
        var t=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),ColumnCount=2,RowCount=6}; t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35)); t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));
        Add(t,"فونت",font,0); Add(t,"اندازه فونت",size,1); Add(t,"رنگ زمینه",color,2);
        Add(t,"عکس پس‌زمینه",image,3);
        var choose=new Button{Text="انتخاب عکس",Dock=DockStyle.Fill}; choose.Click+=(_,_)=>{using var d=new OpenFileDialog{Filter="تصاویر|*.png;*.jpg;*.jpeg;*.bmp"};if(d.ShowDialog(this)==DialogResult.OK) image.Text=d.FileName;}; t.Controls.Add(choose,1,4);
        t.Controls.Add(preview,1,5); color.Click+=(_,_)=>{using var d=new ColorDialog{Color=selected};if(d.ShowDialog(this)==DialogResult.OK){selected=d.Color;preview.BackColor=selected;}};
        var save=new Button{Text="ذخیره و اعمال",Dock=DockStyle.Bottom,Height=48}; save.Click+=(_,_)=>{AppearanceService.Save(font.Text.Trim(),(float)size.Value,selected,image.Text.Trim()); AppearanceService.Apply(this); MessageBox.Show(this,"تنظیمات ظاهر ذخیره شد.");}; Controls.Add(t); Controls.Add(save);
    }
    void Add(TableLayoutPanel t,string label,Control c,int row){t.Controls.Add(new Label{Text=label,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight},0,row);t.Controls.Add(c,1,row);}
}
