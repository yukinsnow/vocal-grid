using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

public class ProjectOptions {
    public bool AlignNotes = true;
    public int GridDivision = 32;
    public bool ScaleEnabled = true;
    public string ScaleRoot = "C";
    public string ScaleType = "Major";
    public bool ExportStems = true;
    public bool ExportMixDown = false;
    public int Channels = 2;
    public string Aspiration = "noAspiration";
    public int BitDepth = 24;
    public int SampleRate = 48000;
    public bool BypassGainPan = true;
    public bool BypassEffects = true;
    public static readonly string[] Roots={"C","C#","D","D#","E","F","F#","G","G#","A","A#","B"};
    public static readonly string[] Scales={"Major","Minor"};
    public void Validate(){
        if(!new[]{4,8,16,32,64}.Contains(GridDivision))throw new Exception("网格请选择 4、8、16、32 或 64。");
        if(ScaleEnabled&&(!Roots.Contains(ScaleRoot)||!Scales.Contains(ScaleType)))throw new Exception("调式音阶无效。");
        if(!ExportStems&&!ExportMixDown)throw new Exception("请至少启用分轨或混音输出。");
        if(!new[]{1,2}.Contains(Channels)||!new[]{16,24,32}.Contains(BitDepth)||!new[]{44100,48000,88200,96000}.Contains(SampleRate))throw new Exception("导出格式无效。");
        if(!new[]{"noAspiration","asFiles"}.Contains(Aspiration))throw new Exception("请选择气声输出方式。");
    }
}

public class AppPreferences {
    public string Singer = "Mai | 1";
    public string Lyric = "啦";
    public double Bpm = 120;
    public int Numerator = 4;
    public int Denominator = 4;
    public bool FollowInput = true;
    public string ManualOutput = "";
    public ProjectOptions Options = new ProjectOptions();
    public static string SettingsPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VocalGrid","settings.json"); } }
    public static AppPreferences Load(string path,out string warning){
        warning="";
        try{if(!File.Exists(path))return new AppPreferences();var p=new JavaScriptSerializer().Deserialize<AppPreferences>(File.ReadAllText(path));if(p==null||p.Options==null)throw new Exception();p.Options.Validate();if(p.Bpm<5||p.Bpm>999||double.IsNaN(p.Bpm)||double.IsInfinity(p.Bpm)||p.Numerator<1||p.Numerator>64||!new[]{1,2,4,8,16,32,64}.Contains(p.Denominator))throw new Exception();return p;}
        catch{warning="本机设置无法读取，已使用默认值。原设置文件仍保留。";return new AppPreferences();}
    }
    public void Save(string path){
        Options.Validate();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        string temp=path+".tmp";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false));
        if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
    }
}
