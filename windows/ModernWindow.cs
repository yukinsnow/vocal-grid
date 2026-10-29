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

        Text="Vocal Grid · MIDI 转 SVP";ClientSize=new Size(760,790);AutoScaleMode=AutoScaleMode.None;
        BackColor=Color.FromArgb(244,247,248);ForeColor=Ink;Font=new Font("Microsoft YaHei UI",9F);
        StartPosition=FormStartPosition.CenterScreen;MinimumSize=new Size(680,550);MaximizeBox=true;
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath)??SystemIcons.Application;
        var footer=new Panel{Dock=DockStyle.Bottom,Width=760,Height=86,BackColor=BackColor};Controls.Add(footer);
        run=Button(footer,"生成 vocal.svp   →",474,17,260,true);run.Anchor=AnchorStyles.Top|AnchorStyles.Right;run.Height=46;run.Click+=async(s,e)=>await Convert();
        status=AddLabel(footer,string.IsNullOrEmpty(warning)?"就绪 · 设置自动保存在本机":warning,26,18,430,36,9,Muted);
        var credit=new LinkLabel{Text="GitHub @Yukinsnow",Left=26,Top=55,Width=250,Height=22,LinkColor=Accent,ActiveLinkColor=Accent,BackColor=Color.Transparent};footer.Controls.Add(credit);
        credit.LinkClicked+=(s,e)=>{try{System.Diagnostics.Process.Start("https://github.com/yukinsnow/vocal-grid");}catch(Exception ex){status.Text=ex.Message;}};
        content=new Panel{Dock=DockStyle.Fill,AutoScroll=true,AutoScrollMinSize=new Size(0,945),BackColor=BackColor};Controls.Add(content);content.BringToFront();
        var logo=new PictureBox{Left=26,Top=22,Width=48,Height=48,SizeMode=PictureBoxSizeMode.Zoom,Image=Icon.ToBitmap()};content.Controls.Add(logo);
        AddLabel(content,"Vocal Grid",88,18,500,33,19,Ink,FontStyle.Bold);
        AddLabel(content,"MIDI → Synthesizer V · 自选参数 · 本机记忆",90,55,610,24,9,Muted);
        var import=Card(26,91,690,119);
        AddLabel(import,"01  导入 MIDI",18,13,350,25,11,Ink,FontStyle.Bold);
        AddLabel(import,"拖入文件或文件夹；多个 MIDI 会显示选择列表",18,43,620,23,9,Muted);
        input=Field(import,"",18,76,506);input.ReadOnly=true;
        var browse=Button(import,"选择文件",538,72,132,false);browse.Click+=(s,e)=>BrowseMidi();BindDrop(import,false);
        var settings=Card(26,224,690,314);
        AddLabel(settings,"02  节奏、歌声与音阶",18,13,620,26,11,Ink,FontStyle.Bold);
        AddLabel(settings,"BPM",18,49,150,22,9,Muted);AddLabel(settings,"拍号",212,49,200,22,9,Muted);AddLabel(settings,"默认歌词",472,49,180,22,9,Muted);
        bpm=Number(settings,18,74,160,5,999,(decimal)pref.Bpm);bpm.DecimalPlaces=3;
        num=Number(settings,212,74,75,1,64,pref.Numerator);AddLabel(settings,"/",299,77,20,24,12,Muted);
        den=Select(settings,326,74,90,new object[]{1,2,4,8,16,32,64},pref.Denominator);
        lyric=Field(settings,pref.Lyric,472,74,198);
        AddLabel(settings,"歌姬：工程内声库名称 | 代际（1 或 2）",18,118,650,23,9,Muted);
        singer=Field(settings,pref.Singer,18,144,652);
        AddLabel(settings,"例：Mai | 1、Mai 2 | 2。星尘请填本机工程中的准确声库名 | 代际。",18,184,652,22,8.5F,Muted);
        align=Check(settings,"对齐音符",18,222,180,opt.AlignNotes);
        AddLabel(settings,"网格 1/",212,225,83,24,9,Muted);grid=Select(settings,300,219,116,new object[]{4,8,16,32,64},opt.GridDivision);
        align.CheckedChanged+=(s,e)=>grid.Enabled=align.Checked;grid.Enabled=align.Checked;
        scaleEnabled=Check(settings,"设置调式音阶",18,270,180,opt.ScaleEnabled);
        scaleRoot=Select(settings,212,265,100,ProjectOptions.Roots.Cast<object>().ToArray(),opt.ScaleRoot);
        scaleType=Select(settings,326,265,196,new object[]{"大调 Major","小调 Minor"},opt.ScaleType=="Minor"?"小调 Minor":"大调 Major");
        scaleEnabled.CheckedChanged+=(s,e)=>{scaleRoot.Enabled=scaleType.Enabled=scaleEnabled.Checked;};scaleRoot.Enabled=scaleType.Enabled=scaleEnabled.Checked;
        var exp=Card(26,552,690,216);
        AddLabel(exp,"03  音频导出设置",18,13,652,25,11,Ink,FontStyle.Bold);
        AddLabel(exp,"输出内容",18,50,210,22,9,Muted);AddLabel(exp,"声道",248,50,140,22,9,Muted);AddLabel(exp,"气声输出",422,50,250,22,9,Muted);
        exportMode=Select(exp,18,75,206,new object[]{"仅分轨","仅混音","分轨 + 混音"},opt.ExportStems?(opt.ExportMixDown?"分轨 + 混音":"仅分轨"):"仅混音");
        channels=Select(exp,248,75,150,new object[]{"单声道","立体声"},opt.Channels==2?"立体声":"单声道");
        aspiration=Select(exp,422,75,248,new object[]{"无","增加文件输出气声"},opt.Aspiration=="noAspiration"?"无":"增加文件输出气声");
        AddLabel(exp,"位深",18,119,200,22,9,Muted);AddLabel(exp,"采样率 / Hz",248,119,300,22,9,Muted);
        bits=Select(exp,18,144,206,new object[]{16,24,32},opt.BitDepth);
        rate=Select(exp,248,144,150,new object[]{44100,48000,88200,96000},opt.SampleRate);
        bypassGain=Check(exp,"旁通增益和声像",18,187,285,opt.BypassGainPan);bypassEffects=Check(exp,"旁通效果",330,187,300,opt.BypassEffects);
        var save=Card(26,782,690,140);AddLabel(save,"04  保存到",18,13,250,25,11,Ink,FontStyle.Bold);
        followInput=Check(save,"跟随 MIDI 输入目录",298,13,360,pref.FollowInput);
        output=Field(save,pref.ManualOutput,18,52,506);var choose=Button(save,"选择目录",538,48,132,false);
        choose.Click+=(s,e)=>{using(var d=new FolderBrowserDialog{SelectedPath=output.Text})if(d.ShowDialog(this)==DialogResult.OK){followInput.Checked=false;output.Text=d.SelectedPath;}};
        AddLabel(save,"文件名 vocal.svp · 已有工程自动备份 · 可拖入目标文件夹",18,101,652,24,9,Muted);BindDrop(save,true);
        input.TextChanged+=(s,e)=>{if(followInput.Checked&&File.Exists(input.Text))output.Text=Path.GetDirectoryName(Path.GetFullPath(input.Text));tips.SetToolTip(input,input.Text);};
        followInput.CheckedChanged+=(s,e)=>{if(followInput.Checked&&File.Exists(input.Text))output.Text=Path.GetDirectoryName(Path.GetFullPath(input.Text));};
        output.TextChanged+=(s,e)=>tips.SetToolTip(output,output.Text);
        AllowDrop=true;DragEnter+=(s,e)=>e.Effect=HasFiles(e)?DragDropEffects.Copy:DragDropEffects.None;DragDrop+=(s,e)=>HandleDrop(e,false);
        saveTimer.Tick+=(s,e)=>{saveTimer.Stop();SavePreferences();};RegisterChanges(content);
        FormClosing+=(s,e)=>{if(busy){e.Cancel=true;return;}saveTimer.Stop();SavePreferences();};
        ApplyDpiLayout();
    }
    void ApplyDpiLayout(){
        float factor;using(var graphics=CreateGraphics())factor=graphics.DpiX/96F;
        if(factor<=1.01F)return;
        var list=new System.Collections.Generic.List<Control>();Action<Control> collect=null;collect=p=>{foreach(Control c in p.Controls){list.Add(c);collect(c);}};collect(this);
        var bounds=list.Select(c=>c.Bounds).ToArray();var docks=list.Select(c=>c.Dock).ToArray();var anchors=list.Select(c=>c.Anchor).ToArray();
        SuspendLayout();foreach(var c in list){c.SuspendLayout();c.Dock=DockStyle.None;c.Anchor=AnchorStyles.Top|AnchorStyles.Left;}
        for(int i=0;i<list.Count;i++){var c=list[i];var r=bounds[i];c.Bounds=new Rectangle((int)(r.X*factor),(int)(r.Y*factor),(int)(r.Width*factor),(int)(r.Height*factor));var p=c.Padding;c.Padding=new Padding((int)(p.Left*factor),(int)(p.Top*factor),(int)(p.Right*factor),(int)(p.Bottom*factor));}
        var area=Screen.FromControl(this).WorkingArea;ClientSize=new Size(Math.Min((int)(760*factor),area.Width-48),Math.Min((int)(790*factor),area.Height-80));MinimumSize=new Size((int)(640*factor),(int)(420*factor));content.AutoScrollMinSize=new Size(0,(int)(945*factor));
        for(int i=0;i<list.Count;i++){list[i].Dock=docks[i];list[i].Anchor=anchors[i];list[i].ResumeLayout(true);}ResumeLayout(true);
    }    public AppPreferences CapturePreferences(){return new AppPreferences{Singer=singer.Text,Lyric=lyric.Text,Bpm=(double)bpm.Value,Numerator=(int)num.Value,Denominator=(int)den.SelectedItem,FollowInput=followInput.Checked,ManualOutput=followInput.Checked?"":output.Text,Options=new ProjectOptions{AlignNotes=align.Checked,GridDivision=(int)grid.SelectedItem,ScaleEnabled=scaleEnabled.Checked,ScaleRoot=(string)scaleRoot.SelectedItem,ScaleType=scaleType.SelectedIndex==0?"Major":"Minor",ExportStems=exportMode.SelectedIndex!=1,ExportMixDown=exportMode.SelectedIndex!=0,Channels=channels.SelectedIndex+1,Aspiration=aspiration.SelectedIndex==0?"noAspiration":"asFiles",BitDepth=(int)bits.SelectedItem,SampleRate=(int)rate.SelectedItem,BypassGainPan=bypassGain.Checked,BypassEffects=bypassEffects.Checked}};}
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
    void BrowseMidi(){using(var d=new OpenFileDialog{Filter="MIDI 文件|*.mid;*.midi"})if(d.ShowDialog(this)==DialogResult.OK)input.Text=d.FileName;}
    static bool HasFiles(DragEventArgs e){return e.Data!=null&&e.Data.GetDataPresent(DataFormats.FileDrop);}
    void BindDrop(Control c,bool directory){c.AllowDrop=true;c.DragEnter+=(s,e)=>{e.Effect=HasFiles(e)?DragDropEffects.Copy:DragDropEffects.None;};c.DragDrop+=(s,e)=>HandleDrop(e,directory);foreach(Control child in c.Controls)BindDrop(child,directory);}
    static bool IsMidi(string p){return File.Exists(p)&&(string.Equals(Path.GetExtension(p),".mid",StringComparison.OrdinalIgnoreCase)||string.Equals(Path.GetExtension(p),".midi",StringComparison.OrdinalIgnoreCase));}
    // Shared by native drop events and the smoke checks; never mutates a dropped file.
    public static string[] MidiCandidates(string[] paths){return paths.SelectMany(p=>Directory.Exists(p)?Directory.GetFiles(p).Where(IsMidi):new[]{p}.Where(IsMidi)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();}
    void HandleDrop(DragEventArgs e,bool directory){try{
        var paths=e.Data.GetData(DataFormats.FileDrop) as string[];if(paths==null||paths.Length==0)return;
        if(directory){if(paths.Length!=1||!Directory.Exists(paths[0]))throw new Exception("请在保存区域拖入一个文件夹。");followInput.Checked=false;output.Text=paths[0];status.Text="保存目录已更新";return;}
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
            if(string.IsNullOrWhiteSpace(lyric.Text)||string.IsNullOrWhiteSpace(output.Text))throw new Exception("请填写默认歌词与输出目录。");
            var pref=CapturePreferences();string name;bool gen;MidiToVocal.ParseSinger(pref.Singer,out name,out gen);
            string source=input.Text,dest=Path.Combine(output.Text,"vocal.svp");SavePreferences();busy=true;content.Enabled=false;run.Enabled=false;run.Text="正在生成…";status.Text="正在整理音符与声部…";
            string result=await Task.Run(()=>MidiToVocal.ConvertFile(source,pref.Bpm,pref.Numerator,pref.Denominator,dest,pref.Lyric,false,name,gen,pref.Options));
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
