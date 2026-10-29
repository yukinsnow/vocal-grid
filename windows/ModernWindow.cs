using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading.Tasks;

public class ConverterWindow : Form {
    static readonly Color Ink=Color.FromArgb(27,40,55), Muted=Color.FromArgb(104,118,132), Accent=Color.FromArgb(12,126,112), Line=Color.FromArgb(222,231,234);
    TextBox input,output,singer,lyric;
    NumericUpDown bpm,num;
    ModernSelect den,generation;
    CheckBox drums;
    Label status;
    ModernButton run;
    ToolTip tips=new ToolTip();
    public ConverterWindow(){
        Text="Vocal Grid · MIDI 转 SVP";ClientSize=new Size(760,774);AutoScaleMode=AutoScaleMode.Dpi;
        BackColor=Color.FromArgb(244,247,248);ForeColor=Ink;Font=new Font("Microsoft YaHei UI",9F);
        StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        var logo=new PictureBox{Left=26,Top=25,Width=48,Height=48,SizeMode=PictureBoxSizeMode.Zoom,Image=Icon.ToBitmap()};Controls.Add(logo);
        AddLabel(this,"Vocal Grid",88,21,350,32,19,Ink,FontStyle.Bold);
        AddLabel(this,"把 MIDI 整理成可直接合成的人声工程",90,57,470,23,9,Muted);
        AddLabel(this,"32 分音符",619,37,110,24,10,Accent,FontStyle.Bold);
        var import=Card(26,100,708,134);
        AddLabel(import,"01  导入 MIDI",20,15,350,25,11,Ink,FontStyle.Bold);
        AddLabel(import,"拖入 .mid / .midi，也可拖入包含 MIDI 的文件夹",20,45,500,23,9,Muted);
        input=Field(import,"",20,81,523);input.ReadOnly=true;input.BackColor=Color.FromArgb(247,250,250);
        var browse=Button(import,"选择文件",558,78,128,false);browse.Click+=(s,e)=>BrowseMidi();
        BindDrop(import,false);
        var settings=Card(26,248,708,223);
        AddLabel(settings,"02  节奏与歌声",20,14,350,26,11,Ink,FontStyle.Bold);
        AddLabel(settings,"BPM",20,53,145,20,9,Muted);AddLabel(settings,"拍号",212,53,160,20,9,Muted);AddLabel(settings,"默认歌词",464,53,200,20,9,Muted);
        bpm=Number(settings,20,79,160,5,999,120);bpm.DecimalPlaces=3;
        num=Number(settings,212,79,75,1,64,4);AddLabel(settings,"/",298,83,20,24,12,Muted);
        den=Select(settings,326,79,89,new object[]{1,2,4,8,16,32,64});den.SelectedItem=4;
        lyric=Field(settings,"啦",464,79,222);
        AddLabel(settings,"歌姬代号",20,124,400,20,9,Muted);AddLabel(settings,"音源代际",464,124,220,20,9,Muted);
        singer=Field(settings,"Mai",20,150,395);
        generation=Select(settings,464,150,222,new object[]{"一代 AI 音源","二代 AI 音源"});generation.SelectedIndex=0;
        AddLabel(settings,"默认 Mai 一代 · 手动填写准确的声库名称 · 汉语普通话",20,192,666,22,9,Muted);
        var save=Card(26,485,708,108);
        AddLabel(save,"03  保存到",20,13,200,25,11,Ink,FontStyle.Bold);AddLabel(save,"拖入文件夹 · 文件名 vocal.svp",331,16,355,22,9,Muted);
        string folder=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);if(new DirectoryInfo(folder).Name=="outputs")folder=Directory.GetParent(folder).FullName;
        output=Field(save,folder,20,53,523);var choose=Button(save,"选择目录",558,50,128,false);
        choose.Click+=(s,e)=>{using(var d=new FolderBrowserDialog{SelectedPath=output.Text,Description="选择 vocal.svp 的保存目录"})if(d.ShowDialog(this)==DialogResult.OK)output.Text=d.SelectedPath;};
        BindDrop(save,true);
        drums=new CheckBox{Left=32,Top=608,Width=690,Height=25,Text="跳过打击乐通道 10",Checked=true,FlatStyle=FlatStyle.Flat};Controls.Add(drums);
        AddLabel(this,"仅分轨  ·  立体声  ·  无气声  ·  24 位 / 48 kHz",32,641,690,22,9,Muted);
        AddLabel(this,"已开启：旁通增益和声像、旁通效果",32,665,690,22,9,Muted);
        run=Button(this,"生成 vocal.svp   →",474,705,260,true);run.Height=46;run.Click+=async(s,e)=>await Convert();
        status=AddLabel(this,"就绪 · 已有工程会自动备份",32,712,426,38,9,Muted);
        tips.SetToolTip(singer,"工程中的声库名称，不是激活码。例如 Mai 或 Mai 2。");
        tips.SetToolTip(input,"拖放 MIDI 文件或文件夹；文件夹有多个 MIDI 时会显示选择列表。");
        input.TextChanged+=(s,e)=>tips.SetToolTip(input,input.Text);output.TextChanged+=(s,e)=>tips.SetToolTip(output,output.Text);
        AllowDrop=true;DragEnter+=(s,e)=>e.Effect=HasFiles(e)?DragDropEffects.Copy:DragDropEffects.None;
        DragDrop+=(s,e)=>HandleDrop(e,false);
        AutoScaleDimensions=new SizeF(96F,96F);
    }
    RoundedCard Card(int x,int y,int w,int h){var p=new RoundedCard{Left=x,Top=y,Width=w,Height=h,BackColor=Color.White};Controls.Add(p);return p;}
    static Label AddLabel(Control p,string text,int x,int y,int w,int h,float size,Color color,FontStyle style=FontStyle.Regular){var l=new Label{Text=text,Left=x,Top=y,Width=w,Height=h,Font=new Font("Microsoft YaHei UI",size,style),ForeColor=color,BackColor=Color.Transparent};p.Controls.Add(l);return l;}
    static TextBox Field(Control p,string value,int x,int y,int w){var box=new Panel{Left=x,Top=y,Width=w,Height=34,BackColor=Color.FromArgb(244,247,248),Padding=new Padding(9,6,9,4)};p.Controls.Add(box);var t=new TextBox{Text=value,BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,BackColor=box.BackColor,ForeColor=Ink,Font=new Font("Microsoft YaHei UI",10)};box.Controls.Add(t);return t;}
    static NumericUpDown Number(Control p,int x,int y,int w,int min,int max,int value){var n=new NumericUpDown{Left=x,Top=y,Width=w,Minimum=min,Maximum=max,Value=value,Font=new Font("Microsoft YaHei UI",11),BorderStyle=BorderStyle.FixedSingle,BackColor=Color.FromArgb(247,250,250),ForeColor=Ink};p.Controls.Add(n);return n;}
    static ModernSelect Select(Control p,int x,int y,int w,object[] values){var c=new ModernSelect{Left=x,Top=y,Width=w,Height=34,Font=new Font("Microsoft YaHei UI",10)};c.Items.AddRange(values);p.Controls.Add(c);return c;}
    static ModernButton Button(Control p,string text,int x,int y,int w,bool primary){var b=new ModernButton{Text=text,Left=x,Top=y,Width=w,Height=36,Primary=primary,Cursor=Cursors.Hand};p.Controls.Add(b);return b;}
    void BrowseMidi(){using(var d=new OpenFileDialog{Filter="MIDI 文件|*.mid;*.midi",Title="选择 MIDI 文件"})if(d.ShowDialog(this)==DialogResult.OK)input.Text=d.FileName;}
    static bool HasFiles(DragEventArgs e){return e.Data!=null&&e.Data.GetDataPresent(DataFormats.FileDrop);}
    void BindDrop(Control c,bool directory){c.AllowDrop=true;c.DragEnter+=(s,e)=>{e.Effect=HasFiles(e)?DragDropEffects.Copy:DragDropEffects.None;};c.DragDrop+=(s,e)=>HandleDrop(e,directory);foreach(Control child in c.Controls)BindDrop(child,directory);}
    static bool IsMidi(string p){return File.Exists(p)&&(string.Equals(Path.GetExtension(p),".mid",StringComparison.OrdinalIgnoreCase)||string.Equals(Path.GetExtension(p),".midi",StringComparison.OrdinalIgnoreCase));}
    // Shared by native drop events and the smoke checks; never mutates a dropped file.
    public static string[] MidiCandidates(string[] paths){return paths.SelectMany(p=>Directory.Exists(p)?Directory.GetFiles(p).Where(IsMidi):new[]{p}.Where(IsMidi)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();}
    void HandleDrop(DragEventArgs e,bool directory){try{
        var paths=e.Data.GetData(DataFormats.FileDrop) as string[];if(paths==null||paths.Length==0)return;
        if(directory){if(paths.Length!=1||!Directory.Exists(paths[0]))throw new Exception("请在保存区域拖入一个文件夹。");output.Text=paths[0];status.Text="保存目录已更新";return;}
        var files=MidiCandidates(paths);if(files.Length==0)throw new Exception("没有找到 MIDI 文件。文件夹只读取当前层级。");
        if(files.Length==1)input.Text=files[0];else{
            using(var picker=new Form{Text="选择要转换的 MIDI",ClientSize=new Size(620,330),StartPosition=FormStartPosition.CenterParent,Font=Font,MinimizeBox=false,MaximizeBox=false,FormBorderStyle=FormBorderStyle.FixedDialog}){
                var list=new ListBox{Left=16,Top=16,Width=588,Height=248,HorizontalScrollbar=true};list.Items.AddRange(files);list.SelectedIndex=0;picker.Controls.Add(list);
                var ok=new Button{Text="使用此文件",DialogResult=DialogResult.OK,Left=464,Top=280,Width=140,Height=34};picker.Controls.Add(ok);picker.AcceptButton=ok;
                list.DoubleClick+=(s,a)=>{picker.DialogResult=DialogResult.OK;picker.Close();};if(picker.ShowDialog(this)==DialogResult.OK)input.Text=(string)list.SelectedItem;
            }
        }
        status.Text="MIDI 已载入 · 可调整节奏与歌声";
    }catch(Exception ex){MessageBox.Show(this,ex.Message,"拖放提示",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
    async Task Convert(){
        try{
            if(!File.Exists(input.Text))throw new Exception("请先选择或拖入 MIDI 文件。");
            if(string.IsNullOrWhiteSpace(lyric.Text))throw new Exception("请输入默认歌词，例如：啦。");
            if(string.IsNullOrWhiteSpace(output.Text))throw new Exception("请选择保存目录。");
            string source=input.Text,dest=Path.Combine(output.Text,"vocal.svp"),code=singer.Text,fallback=lyric.Text.Trim();double tempo=(double)bpm.Value;int numerator=(int)num.Value,denominator=(int)den.SelectedItem;bool skip=drums.Checked,g2=generation.SelectedIndex==1;
            run.Enabled=false;run.Text="正在生成…";status.ForeColor=Muted;status.Text="正在量化音符、整理声部…";
            string result=await Task.Run(()=>MidiToVocal.ConvertFile(source,tempo,numerator,denominator,dest,fallback,skip,code,g2));
            status.ForeColor=Accent;status.Text="已生成 vocal.svp";
            MessageBox.Show(this,result+"\r\n\r\n请在 SV2 中打开工程；需要已安装并激活对应歌姬。","转换完成",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }catch(Exception ex){status.ForeColor=Color.FromArgb(168,62,58);status.Text="未完成 · 请检查输入";MessageBox.Show(this,ex.Message,"转换失败",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        finally{run.Enabled=true;run.Text="生成 vocal.svp   →";}
    }
    protected override void Dispose(bool disposing){if(disposing)tips.Dispose();base.Dispose(disposing);}
    internal static GraphicsPath Round(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    class RoundedCard : Panel { public RoundedCard(){DoubleBuffered=true;} protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Round(new Rectangle(0,0,Width-1,Height-1),12))using(var b=new SolidBrush(BackColor))e.Graphics.FillPath(b,p);} protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Round(new Rectangle(0,0,Width-1,Height-1),12))using(var pen=new Pen(Line))e.Graphics.DrawPath(pen,path);} }
    public class ModernSelect : Button {
        public System.Collections.Generic.List<object> Items=new System.Collections.Generic.List<object>();
        int selected=-1;
        public int SelectedIndex{get{return selected;}set{if(value<0||value>=Items.Count)throw new ArgumentOutOfRangeException();selected=value;AccessibleName=Items[value].ToString();Invalidate();}}
        public object SelectedItem{get{return selected<0?null:Items[selected];}set{SelectedIndex=Items.IndexOf(value);}}
        public ModernSelect(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.ComboBox;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnClick(EventArgs e){base.OnClick(e);var menu=new ContextMenuStrip{Font=Font};for(int i=0;i<Items.Count;i++){int index=i;var item=new ToolStripMenuItem(Items[i].ToString()){Checked=i==selected};item.Click+=(s,a)=>SelectedIndex=index;menu.Items.Add(item);}menu.Closed+=(s,a)=>menu.Dispose();menu.Show(this,new Point(0,Height));}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Down||e.KeyCode==Keys.Up){SelectedIndex=Math.Max(0,Math.Min(Items.Count-1,selected+(e.KeyCode==Keys.Down?1:-1)));e.Handled=true;}base.OnKeyDown(e);}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Color.FromArgb(244,247,248));TextRenderer.DrawText(e.Graphics,SelectedItem==null?"":SelectedItem.ToString(),Font,new Rectangle(8,0,Width-32,Height),Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);using(var pen=new Pen(Muted,1.5F)){float x=Width-18,y=Height/2F;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.DrawLines(pen,new[]{new PointF(x-4,y-2),new PointF(x,y+2),new PointF(x+4,y-2)});}if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,Width-6,Height-6));}
    }
    class ModernButton : Button {
        public bool Primary;bool hover;
        public ModernButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Font=new Font("Microsoft YaHei UI",10,FontStyle.Bold);SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Parent.BackColor);Color fill=!Enabled?Color.FromArgb(192,209,206):Primary?(hover?Color.FromArgb(9,109,97):Accent):(hover?Color.FromArgb(225,237,235):Color.FromArgb(237,244,243));using(var p=Round(new Rectangle(0,0,Width-1,Height-1),9))using(var b=new SolidBrush(fill))e.Graphics.FillPath(b,p);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Primary?Color.White:Accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(4,4,Width-8,Height-8));}
    }
}
