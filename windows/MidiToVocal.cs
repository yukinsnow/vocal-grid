using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using System.Drawing;
using System.Web.Script.Serialization;

public class MidiToVocal {
    const long Quarter=705600000, Grid=Quarter/8;
    static JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=int.MaxValue, RecursionLimit=100 };
    static Dictionary<string,object> D(params object[] p) { var d=new Dictionary<string,object>(); for(int i=0;i<p.Length;i+=2)d[(string)p[i]]=p[i+1];return d; }
    static string Id(){return Guid.NewGuid().ToString();}
    class Note { public long Start,End; public int Pitch,Channel,Order; public string Lyric; }
    class Midi { public int Ppq; public List<List<Note>> Tracks=new List<List<Note>>(); public List<string> Warnings=new List<string>(); }
    class Reader {
        public byte[] B; public int P,Limit;
        public Reader(byte[] b){ B=b;Limit=b.Length; }
        public int U8(){if(P>=Limit)throw new Exception("MIDI 数据意外结束。");return B[P++];}
        public int U16(){return U8()*256+U8();}
        public int I32(){long n=0;for(int i=0;i<4;i++)n=n*256+U8();if(n>int.MaxValue)throw new Exception("MIDI 块过大。");return (int)n;}
        public long Var(){long n=0;for(int i=0;i<4;i++){int b=U8();n=n*128+(b&127);if(b<128)return n;}throw new Exception("无效的 MIDI 可变长度整数。");}
        public string Tag(){return Encoding.ASCII.GetString(Bytes(4));}
        public byte[] Bytes(int n){if(n<0||n>Limit-P)throw new Exception("MIDI 块长度无效。");var a=new byte[n];Buffer.BlockCopy(B,P,a,0,n);P+=n;return a;}
    }
    static string Decode(byte[] b){try{return new UTF8Encoding(false,true).GetString(b);}catch{return Encoding.GetEncoding(936).GetString(b);}}
    static Midi Parse(string path,bool skipDrums){
        var r=new Reader(File.ReadAllBytes(path));var m=new Midi();
        if(r.Tag()!="MThd")throw new Exception("请选择标准 MIDI 文件（.mid / .midi）。");
        int h=r.I32();if(h<6)throw new Exception("MIDI 文件头不完整。");int fmt=r.U16(),count=r.U16(),div=r.U16();r.Bytes(h-6);
        if(fmt>1)throw new Exception("暂不支持格式 2 MIDI，请先转换为格式 0 或 1。");
        if((div&32768)!=0||div==0)throw new Exception("不支持 SMPTE 时间制 MIDI，请转换为 PPQ 时间制。");m.Ppq=div;
        for(int ti=0;ti<count;ti++){
            if(r.Tag()!="MTrk")throw new Exception("找不到 MIDI 轨道块。");int size=r.I32();if(size>r.Limit-r.P)throw new Exception("MIDI 轨道长度无效。");int end=r.P+size,outer=r.Limit;r.Limit=end;
            var notes=new List<Note>();var active=new Dictionary<int,Queue<Note>>();var lyrics=new Dictionary<long,Queue<string>>();long tick=0;int running=0,order=0;
            while(r.P<end){
                tick+=r.Var();int s=r.U8(),first=-1;
                if(s<128){if(running==0)throw new Exception("无效的 MIDI running status。");first=s;s=running;}
                else if(s<240)running=s;
                if(s==255){int type=r.U8();int len=(int)r.Var();byte[] data=r.Bytes(len);if(type==5){if(!lyrics.ContainsKey(tick))lyrics[tick]=new Queue<string>();lyrics[tick].Enqueue(Decode(data).Trim());}if(type==47)break;continue;}
                if(s==240||s==247){running=0;r.Bytes((int)r.Var());continue;}
                if(s>=240)throw new Exception("不支持的 MIDI 系统事件。");
                int kind=s>>4,ch=s&15,a=first>=0?first:r.U8(),b=(kind==12||kind==13)?0:r.U8();if(a>127||b>127)throw new Exception("无效 MIDI 事件数据。");
                if(skipDrums&&ch==9)continue;int key=ch*128+a;
                if(kind==9&&b>0){var n=new Note{Start=tick,Pitch=a,Channel=ch,Order=order++};if(!active.ContainsKey(key))active[key]=new Queue<Note>();active[key].Enqueue(n);}
                else if(kind==8||(kind==9&&b==0)){if(active.ContainsKey(key)&&active[key].Count>0){var n=active[key].Dequeue();n.End=tick;if(n.End>n.Start)notes.Add(n);}}
            }
            foreach(var q in active.Values)foreach(var n in q){n.End=Math.Max(n.Start+Math.Max(1,div/8),tick);notes.Add(n);m.Warnings.Add("有未配对的 Note On，已在轨道末尾补齐。");}
            foreach(var n in notes.OrderBy(n=>n.Start).ThenBy(n=>n.Order)){Queue<string> q;if(lyrics.TryGetValue(n.Start,out q)&&q.Count>0)n.Lyric=q.Dequeue();}
            m.Tracks.Add(notes);r.P=end;r.Limit=outer;
        }
        return m;
    }
    static long Snap(long tick,int ppq){return ((tick*16+ppq)/(2*ppq))*Grid;}
    static object Parameters(){var p=D();foreach(var k in new[]{"pitchDelta","vibratoEnv","loudness","tension","breathiness","voicing","gender","toneShift","mouthOpening"})p[k]=D("mode","cubic","points",new object[0]);return p;}
    static object MakeTrack(List<Note> lane,int i,string fallback,string singer,bool generation2){
        string gid=Id(),name="Melody"+i;var notes=new List<object>();
        foreach(var n in lane)notes.Add(D("uuid",Guid.NewGuid().ToString("N").Substring(0,16),"musicalType","singing","onset",n.Start,"duration",n.End-n.Start,"pitch",n.Pitch,"detune",0,"lyrics",string.IsNullOrWhiteSpace(n.Lyric)?fallback:n.Lyric,"phonemes","","accent","","attributes",D("evenSyllableDuration",true,"muted",false,"languageOverride","mandarin","phonesetOverride","xsampa")));
        return D("name",name,"dispColor","ff7db235","dispOrder",i-1,"renderEnabled",true,
            "mixer",D("gainDecibel",0.0,"pan",0.0,"mute",false,"solo",false,"display",true,"fxPresetName","","fxParams",D("room",D("enabled",false),"postRoomEq",D("enabled",false),"compressor",D("enabled",false),"reverb",D("enabled",false))),
            "mainGroup",D("name",name,"uuid",gid,"parameters",Parameters(),"vocalModes",D(),"pitchControls",new object[0],"notes",notes,"musicalScale",D("type","Major","root","C")),
            "mainRef",D("uuid",Id(),"groupID",gid,"blickAbsoluteBegin",0,"blickAbsoluteEnd",-1,"blickOffset",0,"pitchOffset",0,"mute",false,"isInstrumental",false,"database",D("name",singer,"language","japanese","phoneset","romaji","languageOverride","mandarin","phonesetOverride","xsampa","backendType",generation2?"SVR3":"SVR2AI","version",generation2?"201":(singer=="Mai"?"104":"100")),"dictionary","","voice",D("vocalModeInherited",true,"vocalModePreset","","vocalModeParams",D()),"voicePresetName",""),"groups",new object[0]);
    }
    public static string ConvertFile(string input,double bpm,int numerator,int denominator,string destination,string fallback,bool skipDrums,string singer="Mai",bool generation2=false){
        if(double.IsNaN(bpm)||double.IsInfinity(bpm)||bpm<5||bpm>999)throw new Exception("BPM 范围为 5–999。");
        if(numerator<1||numerator>64||!new[]{1,2,4,8,16,32,64}.Contains(denominator))throw new Exception("拍号无效。");
        if(string.IsNullOrWhiteSpace(singer))throw new Exception("请输入歌姬代号，例如 Mai。");singer=singer.Trim();
        var midi=Parse(input,skipDrums);var tracks=new List<object>();int source=0,total=0,split=0;
        foreach(var raw in midi.Tracks){if(raw.Count==0)continue;source++;
            foreach(var channel in raw.GroupBy(n=>n.Channel).OrderBy(g=>g.Key)){
                var lanes=new List<List<Note>>();
                foreach(var n in channel.OrderBy(n=>n.Start).ThenBy(n=>n.Order)){
                    n.Start=Snap(n.Start,midi.Ppq);n.End=Math.Max(n.Start+Grid,Snap(n.End,midi.Ppq));
                    var lane=lanes.FirstOrDefault(l=>l[l.Count-1].End<=n.Start);if(lane==null){lane=new List<Note>();lanes.Add(lane);}lane.Add(n);total++;
                }
                split+=Math.Max(0,lanes.Count-1);foreach(var lane in lanes)tracks.Add(MakeTrack(lane,tracks.Count+1,fallback,singer,generation2));
            }
        }
        if(tracks.Count==0)throw new Exception("MIDI 中没有可转换的音符（打击乐默认排除）。");
        var project=D("version",196,"uuid",Id(),"time",D("meter",new[]{D("index",0,"numerator",numerator,"denominator",denominator)},"tempo",new[]{D("position",0,"bpm",bpm)},"startTimeSeconds",0.0),"library",new object[0],"tracks",tracks,
            "renderConfig",D("destination",Path.GetDirectoryName(Path.GetFullPath(destination)),"filename","vocal","numChannels",2,"aspirationFormat","noAspiration","bitDepth",24,"sampleRate",48000,"exportMixDown",false,"exportPitch",false,"bypassPan",true,"bypassGain",true,"bypassEffects",true),"projectMixer",D("linkRoomSettings",true));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination)));
        string temp=destination+".tmp";File.WriteAllText(temp,json.Serialize(project),new UTF8Encoding(false));
        if(File.Exists(destination)){string backup=destination+"."+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".bak";File.Replace(temp,destination,backup);}else File.Move(temp,destination);
        return string.Format("完成：{0} 个 MIDI 音符轨 → {1} 个声部轨，共 {2} 个音符。\r\n复音新增声部：{3}。\r\n{4}\r\n保存：{5}",source,tracks.Count,total,split,string.Join("\r\n",midi.Warnings.Distinct()),destination);
    }
    [STAThread] public static int Main(string[] args){
        if(args.Length>0&&args[0]=="--convert"){
            try{string[] meter=args[3].Split('/');string result=ConvertFile(args[1],double.Parse(args[2],CultureInfo.InvariantCulture),int.Parse(meter[0]),int.Parse(meter[1]),args[4],"啦",true,args.Length>5?args[5]:"Mai",args.Length>6&&args[6]=="2");File.WriteAllText(args[4]+".log.txt",result,Encoding.UTF8);return 0;}catch(Exception ex){File.WriteAllText(args.Length>4?args[4]+".error.txt":"conversion.error.txt",ex.ToString());return 1;}
        }
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length==2&&args[0]=="--preview"){
            using(var f=new ConverterWindow())using(var image=new Bitmap(f.Width,f.Height)){f.Show();Application.DoEvents();f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);f.Close();}return 0;
        }
        Application.Run(new ConverterWindow());return 0;
    }
}