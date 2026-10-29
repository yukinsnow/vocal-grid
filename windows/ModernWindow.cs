using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

public class ConverterWindow : Form {
    static readonly Color Ink=Color.FromArgb(27,40,55), Muted=Color.FromArgb(104,118,132), Accent=Color.FromArgb(12,126,112), Line=Color.FromArgb(222,231,234);
    TextBox input,output,singer,lyric;
    string[] inputs=new string[0];
    public string[] SelectedInputs { get { return inputs.ToArray(); } }
    NumericUpDown bpm,num;
    ComboBox den,grid,scaleRoot,scaleType,exportMode,channels,aspiration,bits,rate;
    CheckBox align,scaleEnabled,followInput,bypassGain,bypassEffects;
    Label status;
    ModernButton run;
    Panel content;
    ToolTip tips=new ToolTip();
    Timer saveTimer=new Timer{Interval=500};
    string settingsPath;
    bool persist,busy;
    public ConverterWindow(string settingsPath=null,bool persist=true){
        this.settingsPath=settingsPath??AppPreferences.SettingsPath;this.persist=persist;
        string warning;var pref=AppPreferences.Load(this.settingsPath,out warning);var opt=pref.Options;

        Text="Vocal Grid · MIDI 转 SVP · v1.1.1";ClientSize=new Size(860,600);AutoScaleMode=AutoScaleMode.None;
        BackColor=Color.FromArgb(244,247,248);ForeColor=Ink;Font=new Font("Microsoft YaHei UI",9F);
        StartPosition=FormStartPosition.CenterScreen;MinimumSize=new Size(876,639);MaximizeBox=false;
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath)??SystemIcons.Application;
        var footer=new Panel{Dock=DockStyle.Bottom,Width=860,Height=64,BackColor=BackColor};Controls.Add(footer);
        run=Button(footer,"合并生成 vocal.svp  →",590,10,250,true);run.Anchor=AnchorStyles.Top|AnchorStyles.Right;run.Height=40;run.Click+=async(s,e)=>await Convert();
        status=AddLabel(footer,string.IsNullOrEmpty(warning)?"就绪 · 设置自动保存":warning,20,5,550,24,9,Muted);
        var credit=new LinkLabel{Text="GitHub @Yukinsnow",Left=20,Top=32,Width=250,Height=22,LinkColor=Accent,BackColor=Color.Transparent};footer.Controls.Add(credit);
        credit.LinkClicked+=(s,e)=>{try{System.Diagnostics.Process.Start("https://github.com/yukinsnow/vocal-grid");}catch(Exception ex){status.Text=ex.Message;}};
        content=new Panel{Dock=DockStyle.Fill,AutoScroll=false,BackColor=BackColor};Controls.Add(content);content.BringToFront();
        var logo=new PictureBox{Left=20,Top=14,Width=36,Height=36,SizeMode=PictureBoxSizeMode.Zoom,Image=Icon.ToBitmap()};content.Controls.Add(logo);
        AddLabel(content,"Vocal Grid",66,12,300,36,18,Ink,FontStyle.Bold);AddLabel(content,"多 MIDI → SVP",380,17,450,34,10,Muted);
        var import=Card(20,62,820,90);
        AddLabel(import,"导入 MIDI",14,9,110,22,10,Ink,FontStyle.Bold);AddLabel(import,"可多选或拖入文件 / 文件夹；输出默认在首个文件目录",132,10,665,22,9,Muted);
        input=Field(import,"",14,40,548);input.ReadOnly=true;
        var browse=Button(import,"添加 MIDI",574,38,128,false);browse.Click+=(s,e)=>BrowseMidi();
        var clear=Button(import,"清空",712,38,94,false);clear.Click+=(s,e)=>SetInputs(new string[0]);BindDrop(import,false);
        var settings=Card(20,164,430,266);
        AddLabel(settings,"节奏与歌声",14,9,400,23,10,Ink,FontStyle.Bold);
        AddLabel(settings,"BPM",14,37,90,20,9,Muted);AddLabel(settings,"拍号",128,37,155,20,9,Muted);AddLabel(settings,"歌词",300,37,100,20,9,Muted);
        bpm=Number(settings,14,60,100,5,999,(decimal)pref.Bpm);bpm.DecimalPlaces=2;
        num=Number(settings,128,60,60,1,64,pref.Numerator);AddLabel(settings,"/",192,62,16,23,10,Muted);den=Select(settings,212,60,70,new object[]{1,2,4,8,16,32,64},pref.Denominator);lyric=Field(settings,pref.Lyric,300,57,114);
        AddLabel(settings,"声库名称（默认 Mai 一代）",14,100,400,22,9,Muted);
        string singerText=pref.Singer;if(singerText.Contains("|")&&!singerText.StartsWith("{")){string n;bool g;MidiToVocal.ParseSinger(singerText,out n,out g);singerText=g&&n=="Mai"?"Mai 2":n;}
        singer=Field(settings,singerText,14,123,278);
        SetSingerMarker(singerText);
        var loadVoice=Button(settings,"从 SVP 读取",302,122,112,false);loadVoice.Click+=(s,e)=>{try{using(var d=new OpenFileDialog{Filter="SVP 工程|*.svp"})if(d.ShowDialog(this)==DialogResult.OK)SetSingerMarker(MidiToVocal.ReadSingerFromProject(d.FileName));}catch(Exception ex){MessageBox.Show(this,ex.Message,"声库读取");}};
        align=Check(settings,"量化音符",14,188,115,opt.AlignNotes);grid=Select(settings,136,186,75,new object[]{4,8,16,32,64},opt.GridDivision);
        AddLabel(settings,"分网格（SV2 工具栏网格为本机设置）",220,190,200,20,7.5F,Muted);
        align.CheckedChanged+=(s,e)=>grid.Enabled=align.Checked;grid.Enabled=align.Checked;
        scaleEnabled=Check(settings,"调式音阶",14,228,115,opt.ScaleEnabled);scaleRoot=Select(settings,136,225,75,ProjectOptions.Roots.Cast<object>().ToArray(),opt.ScaleRoot);
        scaleType=Select(settings,220,225,194,new object[]{"自然大调","自然小调"},opt.ScaleType=="Minor"?"自然小调":"自然大调");
        scaleEnabled.CheckedChanged+=(s,e)=>{scaleRoot.Enabled=scaleType.Enabled=scaleEnabled.Checked;};scaleRoot.Enabled=scaleType.Enabled=scaleEnabled.Checked;
        var exp=Card(462,164,378,266);AddLabel(exp,"音频导出",14,9,340,23,10,Ink,FontStyle.Bold);
        AddLabel(exp,"输出内容",14,37,170,20,9,Muted);AddLabel(exp,"声道",198,37,165,20,9,Muted);
        exportMode=Select(exp,14,60,170,new object[]{"仅分轨","仅混音","分轨 + 混音"},opt.ExportStems?(opt.ExportMixDown?"分轨 + 混音":"仅分轨"):"仅混音");
        channels=Select(exp,198,60,166,new object[]{"单声道","立体声"},opt.Channels==2?"立体声":"单声道");
        AddLabel(exp,"位深",14,100,170,20,9,Muted);AddLabel(exp,"采样率 / Hz",198,100,165,20,9,Muted);
        bits=Select(exp,14,123,170,new object[]{16,24,32},opt.BitDepth);rate=Select(exp,198,123,166,new object[]{44100,48000,88200,96000},opt.SampleRate);
        AddLabel(exp,"气声输出",14,166,100,24,9,Muted);aspiration=Select(exp,118,163,246,new object[]{"无","增加文件输出气声"},opt.Aspiration=="noAspiration"?"无":"增加文件输出气声");
        bypassGain=Check(exp,"旁通增益和声像",14,226,190,opt.BypassGainPan);bypassEffects=Check(exp,"旁通效果",220,226,145,opt.BypassEffects);
        var save=Card(20,442,820,84);AddLabel(save,"保存到",14,9,90,22,10,Ink,FontStyle.Bold);followInput=Check(save,"跟随首个 MIDI 目录",500,8,300,pref.FollowInput);
        output=Field(save,pref.ManualOutput,14,38,648);var choose=Button(save,"选择目录",674,36,132,false);
        choose.Click+=(s,e)=>{using(var d=new FolderBrowserDialog{SelectedPath=output.Text})if(d.ShowDialog(this)==DialogResult.OK){followInput.Checked=false;output.Text=d.SelectedPath;}};BindDrop(save,true);
        input.TextChanged+=(s,e)=>{if(followInput.Checked&&inputs.Length>0)output.Text=Path.GetDirectoryName(Path.GetFullPath(inputs[0]));tips.SetToolTip(input,string.Join(Environment.NewLine,inputs));};
        followInput.CheckedChanged+=(s,e)=>{if(followInput.Checked&&inputs.Length>0)output.Text=Path.GetDirectoryName(Path.GetFullPath(inputs[0]));};
        output.TextChanged+=(s,e)=>tips.SetToolTip(output,output.Text);
        AllowDrop=true;DragEnter+=(s,e)=>e.Effect=HasFiles(e)?DragDropEffects.Copy:DragDropEffects.None;DragDrop+=(s,e)=>HandleDrop(e,false);
        saveTimer.Tick+=(s,e)=>{saveTimer.Stop();SavePreferences();};RegisterChanges(content);
        FormClosing+=(s,e)=>{if(busy){e.Cancel=true;return;}saveTimer.Stop();SavePreferences();};
        ApplyDpiLayout();
    }
    void ApplyDpiLayout(){
        float dpi;using(var graphics=CreateGraphics())dpi=graphics.DpiX/96F;
        var area=Screen.FromControl(this).WorkingArea;
        float factor=Math.Min(dpi,Math.Min((area.Width-48)/876F,(area.Height-64)/639F));
        var list=new System.Collections.Generic.List<Control>();Action<Control> collect=null;collect=p=>{foreach(Control c in p.Controls){list.Add(c);collect(c);}};collect(this);
        var bounds=list.Select(c=>c.Bounds).ToArray();var docks=list.Select(c=>c.Dock).ToArray();var anchors=list.Select(c=>c.Anchor).ToArray();var fonts=list.Select(c=>c.Font).ToArray();
        SuspendLayout();foreach(var c in list){c.SuspendLayout();c.Dock=DockStyle.None;c.Anchor=AnchorStyles.Top|AnchorStyles.Left;}
        for(int i=0;i<list.Count;i++){var c=list[i];var r=bounds[i];c.Font=new Font(fonts[i].FontFamily,fonts[i].Size*factor/dpi,fonts[i].Style);c.Bounds=new Rectangle((int)(r.X*factor),(int)(r.Y*factor),(int)(r.Width*factor),(int)(r.Height*factor));var p=c.Padding;c.Padding=new Padding((int)(p.Left*factor),(int)(p.Top*factor),(int)(p.Right*factor),(int)(p.Bottom*factor));}
        MinimumSize=Size.Empty;ClientSize=new Size((int)(860*factor),(int)(600*factor));MinimumSize=Size;content.AutoScrollMinSize=Size.Empty;
        for(int i=0;i<list.Count;i++){list[i].Dock=docks[i];list[i].Anchor=anchors[i];list[i].ResumeLayout(true);}ResumeLayout(true);
    }
    public void SetSingerMarker(string marker){singer.Tag=null;try{if(marker.TrimStart().StartsWith("{")){var db=new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(marker);singer.Text=(string)db["name"];singer.Tag=marker;QueueSave();return;}}catch{}singer.Text=marker;}
    string SingerValue(){var marker=singer.Tag as string;if(marker!=null){var db=new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(marker);if((string)db["name"]==singer.Text.Trim())return marker;}return singer.Text;}
    public AppPreferences CapturePreferences(){return new AppPreferences{Singer=SingerValue(),Lyric=lyric.Text,Bpm=(double)bpm.Value,Numerator=(int)num.Value,Denominator=(int)den.SelectedItem,FollowInput=followInput.Checked,ManualOutput=followInput.Checked?"":output.Text,Options=new ProjectOptions{AlignNotes=align.Checked,GridDivision=(int)grid.SelectedItem,ScaleEnabled=scaleEnabled.Checked,ScaleRoot=(string)scaleRoot.SelectedItem,ScaleType=scaleType.SelectedIndex==0?"Major":"Minor",ExportStems=exportMode.SelectedIndex!=1,ExportMixDown=exportMode.SelectedIndex!=0,Channels=channels.SelectedIndex+1,Aspiration=aspiration.SelectedIndex==0?"noAspiration":"asFiles",BitDepth=(int)bits.SelectedItem,SampleRate=(int)rate.SelectedItem,BypassGainPan=bypassGain.Checked,BypassEffects=bypassEffects.Checked}};}
    void SavePreferences(){if(!persist)return;try{CapturePreferences().Save(settingsPath);}catch(Exception ex){status.Text="设置未保存："+ex.Message;}}
    void RegisterChanges(Control p){foreach(Control c in p.Controls){var text=c as TextBox;if(text!=null)text.TextChanged+=(s,e)=>QueueSave();var number=c as NumericUpDown;if(number!=null)number.ValueChanged+=(s,e)=>QueueSave();var combo=c as ComboBox;if(combo!=null)combo.SelectedIndexChanged+=(s,e)=>QueueSave();var check=c as CheckBox;if(check!=null)check.CheckedChanged+=(s,e)=>QueueSave();RegisterChanges(c);}}
    void QueueSave(){if(persist&&!IsDisposed){saveTimer.Stop();saveTimer.Start();}}
    RoundedCard Card(int x,int y,int w,int h){var p=new RoundedCard{Left=x,Top=y,Width=w,Height=h,BackColor=Color.White};content.Controls.Add(p);return p;}
    static Label AddLabel(Control p,string text,int x,int y,int w,int h,float size,Color color,FontStyle style=FontStyle.Regular){var l=new Label{Text=text,Left=x,Top=y,Width=w,Height=h,Font=new Font("Microsoft YaHei UI",size,style),ForeColor=color,BackColor=Color.Transparent};p.Controls.Add(l);return l;}
    static TextBox Field(Control p,string value,int x,int y,int w){var box=new Panel{Left=x,Top=y,Width=w,Height=34,BackColor=Color.FromArgb(244,247,248),Padding=new Padding(9,6,9,4)};p.Controls.Add(box);var t=new TextBox{Text=value,BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,BackColor=box.BackColor,ForeColor=Ink,Font=new Font("Microsoft YaHei UI",10)};box.Controls.Add(t);return t;}
    static NumericUpDown Number(Control p,int x,int y,int w,int min,int max,decimal value){var n=new NumericUpDown{Left=x,Top=y,Width=w,Minimum=min,Maximum=max,Value=value,Font=new Font("Microsoft YaHei UI",10),BorderStyle=BorderStyle.FixedSingle};p.Controls.Add(n);return n;}
    static ComboBox Select(Control p,int x,int y,int w,object[] values,object selected){var c=new ComboBox{Left=x,Top=y,Width=w,DropDownStyle=ComboBoxStyle.DropDownList,Font=new Font("Microsoft YaHei UI",10)};c.Items.AddRange(values);c.SelectedItem=selected;if(c.SelectedIndex<0)c.SelectedIndex=0;p.Controls.Add(c);return c;}
    static CheckBox Check(Control p,string text,int x,int y,int w,bool value){var c=new CheckBox{Text=text,Left=x,Top=y,Width=w,Height=24,Checked=value,AutoSize=false};p.Controls.Add(c);return c;}
    static ModernButton Button(Control p,string text,int x,int y,int w,bool primary){var b=new ModernButton{Text=text,Left=x,Top=y,Width=w,Height=36,Primary=primary,Cursor=Cursors.Hand};p.Controls.Add(b);return b;}
    public void SetInputs(string[] paths){inputs=paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();input.Text=inputs.Length==1?inputs[0]:inputs.Length==0?"":inputs.Length+" 个 MIDI："+string.Join("、",inputs.Select(Path.GetFileName));if(followInput.Checked&&inputs.Length>0)output.Text=Path.GetDirectoryName(Path.GetFullPath(inputs[0]));tips.SetToolTip(input,string.Join(Environment.NewLine,inputs));}
    void BrowseMidi(){using(var d=new OpenFileDialog{Filter="MIDI 文件|*.mid;*.midi",Multiselect=true})if(d.ShowDialog(this)==DialogResult.OK)SetInputs(inputs.Concat(d.FileNames).ToArray());}
    static bool HasFiles(DragEventArgs e){return e.Data!=null&&e.Data.GetDataPresent(DataFormats.FileDrop);}
    void BindDrop(Control c,bool directory){c.AllowDrop=true;c.DragEnter+=(s,e)=>{e.Effect=HasFiles(e)?DragDropEffects.Copy:DragDropEffects.None;};c.DragDrop+=(s,e)=>HandleDrop(e,directory);foreach(Control child in c.Controls)BindDrop(child,directory);}
    static bool IsMidi(string p){return File.Exists(p)&&(string.Equals(Path.GetExtension(p),".mid",StringComparison.OrdinalIgnoreCase)||string.Equals(Path.GetExtension(p),".midi",StringComparison.OrdinalIgnoreCase));}
    // Shared by native drop events and the smoke checks; never mutates a dropped file.
    public static string[] MidiCandidates(string[] paths){return paths.SelectMany(p=>Directory.Exists(p)?Directory.GetFiles(p).Where(IsMidi):new[]{p}.Where(IsMidi)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();}
    void HandleDrop(DragEventArgs e,bool directory){try{
        var paths=e.Data.GetData(DataFormats.FileDrop) as string[];if(paths==null||paths.Length==0)return;
        if(directory){if(paths.Length!=1||!Directory.Exists(paths[0]))throw new Exception("请在保存区域拖入一个文件夹。");followInput.Checked=false;output.Text=paths[0];status.Text="保存目录已更新";return;}
        var files=MidiCandidates(paths);if(files.Length==0)throw new Exception("没有找到 MIDI 文件。文件夹只读取当前层级。");
        SetInputs(inputs.Concat(files).ToArray());
        status.Text="MIDI 已载入 · 可调整节奏与歌声";
    }catch(Exception ex){MessageBox.Show(this,ex.Message,"拖放提示",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
    async Task Convert(){
        try{
            if(inputs.Length==0||inputs.Any(p=>!File.Exists(p)))throw new Exception("请先选择或拖入 MIDI 文件。");
            if(string.IsNullOrWhiteSpace(lyric.Text)||string.IsNullOrWhiteSpace(output.Text))throw new Exception("请填写默认歌词与输出目录。");
            var pref=CapturePreferences();MidiToVocal.SingerDatabase(pref.Singer);
            string[] sources=inputs.ToArray();string dest=Path.Combine(output.Text,"vocal.svp");SavePreferences();busy=true;content.Enabled=false;run.Enabled=false;run.Text="正在生成…";status.Text="正在整理音符与声部…";
            string result=await Task.Run(()=>MidiToVocal.ConvertFiles(sources,pref.Bpm,pref.Numerator,pref.Denominator,dest,pref.Lyric,false,pref.Singer,false,pref.Options));
            status.Text="已生成 vocal.svp";MessageBox.Show(this,result,"转换完成",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }catch(Exception ex){status.Text="未完成 · 请检查输入";MessageBox.Show(this,ex.Message,"转换提示",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        finally{busy=false;content.Enabled=true;run.Enabled=true;run.Text="生成 vocal.svp   →";}
    }
    protected override void Dispose(bool disposing){if(disposing){saveTimer.Dispose();tips.Dispose();}base.Dispose(disposing);}
    internal static GraphicsPath Round(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    class RoundedCard : Panel { public RoundedCard(){DoubleBuffered=true;} protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Round(new Rectangle(0,0,Width-1,Height-1),12))using(var b=new SolidBrush(BackColor))e.Graphics.FillPath(b,p);} protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Round(new Rectangle(0,0,Width-1,Height-1),12))using(var pen=new Pen(Line))e.Graphics.DrawPath(pen,path);} }
    class ModernButton : Button {
        public bool Primary;bool hover;
        public ModernButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Font=new Font("Microsoft YaHei UI",10,FontStyle.Bold);SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Parent.BackColor);Color fill=!Enabled?Color.FromArgb(192,209,206):Primary?(hover?Color.FromArgb(9,109,97):Accent):(hover?Color.FromArgb(225,237,235):Color.FromArgb(237,244,243));using(var p=Round(new Rectangle(0,0,Width-1,Height-1),9))using(var b=new SolidBrush(fill))e.Graphics.FillPath(b,p);TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Primary?Color.White:Accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);if(Focused)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(4,4,Width-8,Height-8));}
    }
}
