using System;using System.IO;using System.Reflection;using System.Linq;using System.Windows.Forms;using System.Web.Script.Serialization;using System.Collections.Generic;using System.Collections;
class UiSmoke {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static T Field<T>(object f,string name){return (T)f.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f);}
 static void Drop(Control c,string path){var d=new DataObject(DataFormats.FileDrop,new[]{path});typeof(Control).GetMethod("OnDragDrop",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{new DragEventArgs(d,0,0,0,DragDropEffects.Copy,DragDropEffects.None)});}
 static IEnumerable<Control> All(Control p){foreach(Control c in p.Controls){yield return c;foreach(var x in All(c))yield return x;}}
 static Dictionary<string,object> Load(string p){return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(p));}
 [STAThread]static void Main(string[] args){
  Application.EnableVisualStyles();string root=Path.GetFullPath(args[0]),settings=Path.Combine(root,"prefs-"+Guid.NewGuid()+".json"),midi=Path.Combine(root,"poly.mid");
  using(var f=new ConverterWindow(settings,false)){
   var input=Field<TextBox>(f,"input");var output=Field<TextBox>(f,"output");Check(input.AllowDrop&&output.AllowDrop,"Drop targets");Check(Field<TextBox>(f,"singer").Text=="Mai","Default singer");
   Check(!All(f).Any(x=>x.GetType().Name=="ModernSelect"),"Unsafe menu retained");Check(!All(f).OfType<CheckBox>().Any(x=>x.Text.Contains("打击乐")),"Drum option retained");
   Drop(input,midi);Check(input.Text==midi&&output.Text==root,"Follow-input folder");Drop(output,root);Check(!Field<CheckBox>(f,"followInput").Checked,"Manual override");
   string one=Path.Combine(root,"single");Directory.CreateDirectory(one);File.Copy(midi,Path.Combine(one,"one.MID"),true);f.SetInputs(new string[0]);Drop(input,one);Check(output.Text==root,"Manual output replaced");Field<CheckBox>(f,"followInput").Checked=true;Check(output.Text==one,"Follow re-enable");
   foreach(var combo in All(f).OfType<ComboBox>())for(int r=0;r<10;r++)for(int i=0;i<combo.Items.Count;i++)combo.SelectedIndex=i;
   Field<TextBox>(f,"singer").Text="Mai 2";var p=f.CapturePreferences();p.Options.GridDivision=64;p.Options.ScaleRoot="D";p.Options.ScaleType="Minor";p.Options.BitDepth=16;p.Options.SampleRate=96000;p.Save(settings);
  }
  string warning;var saved=AppPreferences.Load(settings,out warning);Check(saved.Singer=="Mai 2"&&saved.Options.GridDivision==64&&saved.Options.SampleRate==96000,"Preferences round trip");using(var f=new ConverterWindow(settings,false)){var p=f.CapturePreferences();Check(p.Singer==saved.Singer&&p.Options.GridDivision==64&&p.Options.BitDepth==16,"Startup restore");}
  string singer;bool gen;MidiToVocal.ParseSinger("MAI | 1",out singer,out gen);Check(singer=="Mai"&&!gen,"Mai syntax");MidiToVocal.ParseSinger("Custom Singer | 2",out singer,out gen);Check(singer=="Custom Singer"&&gen,"Gen2 syntax");bool threw=false;try{MidiToVocal.ParseSinger("Mai | 3",out singer,out gen);}catch{threw=true;}Check(threw,"Invalid generation accepted");
  foreach(int division in new[]{4,8,16,32,64}){
   var opt=new ProjectOptions{GridDivision=division,ScaleRoot="D",ScaleType="Minor",ExportStems=false,ExportMixDown=true,Channels=1,BitDepth=16,SampleRate=96000,Aspiration="asFiles",BypassGainPan=false,BypassEffects=false};string path=Path.Combine(root,"options-"+division+".svp");MidiToVocal.ConvertFile(midi,120,4,4,path,"啦",false,"Mai | 1",false,opt);var j=Load(path);var render=(Dictionary<string,object>)j["renderConfig"];Check((int)render["numChannels"]==1&&(int)render["sampleRate"]==96000&&(bool)render["exportMixDown"]&&!(bool)render["bypassEffects"],"Export options ignored");
   foreach(Dictionary<string,object> t in (IEnumerable)j["tracks"]){Check(!(bool)t["renderEnabled"],"Mix-only tracks selected");var group=(Dictionary<string,object>)t["mainGroup"];var scale=(Dictionary<string,object>)group["musicalScale"];Check((string)scale["root"]=="D"&&(string)scale["type"]=="NaturalMinor","Scale ignored");foreach(Dictionary<string,object> n in (IEnumerable)group["notes"]){long grid=705600000L/(division/4);Check(System.Convert.ToInt64(n["onset"])%grid==0&&System.Convert.ToInt64(n["duration"])%grid==0,"Grid incorrect");}}
  }
  string exact=Path.Combine(root,"exact.mid");File.WriteAllBytes(exact,new byte[]{77,84,104,100,0,0,0,6,0,0,0,1,0,100,77,84,114,107,0,0,0,12,1,144,60,100,1,128,60,0,0,255,47,0});string dest=Path.Combine(root,"exact.svp");MidiToVocal.ConvertFile(exact,120,4,4,dest,"啦",false,"Mai",false,new ProjectOptions{AlignNotes=false,ScaleEnabled=false});var g=(Dictionary<string,object>)((Dictionary<string,object>)((IEnumerable)Load(dest)["tracks"]).Cast<object>().First())["mainGroup"];Check(!g.ContainsKey("musicalScale"),"Scale off field");var note=(Dictionary<string,object>)((IEnumerable)g["notes"]).Cast<object>().First();Check(System.Convert.ToInt64(note["onset"])==7056000&&System.Convert.ToInt64(note["duration"])==7056000,"Align-off timing");
  var mai2=MidiToVocal.SingerDatabase("Mai 2");Check((string)mai2["name"]=="Mai 2"&&(string)mai2["backendType"]=="SVR3"&&!mai2.ContainsKey("version"),"Mai2 marker");
  var legacy=MidiToVocal.SingerDatabase("Mai | 2");Check((string)legacy["name"]=="Mai 2","Legacy gen2 marker");
  var star=MidiToVocal.SingerDatabase("MEDIUM5·Stardust");Check((string)star["name"]=="MEDIUM5·Stardust"&&(string)star["language"]=="mandarin","Authentic Stardust marker");
  string raw="{\"name\":\"Example 2\",\"backendType\":\"custom-engine\",\"version\":\"209\",\"language\":\"mandarin\",\"phoneset\":\"xsampa\"}";
  string merged=Path.Combine(root,"merged.svp");MidiToVocal.ConvertFiles(new[]{midi,exact},120,4,4,merged,"啦",false,raw,false,new ProjectOptions{AlignNotes=false,ScaleType="Minor"});
  var mergedTracks=((IEnumerable)Load(merged)["tracks"]).Cast<Dictionary<string,object>>().ToArray();Check(mergedTracks.Length==4,"Merge must preserve source lanes");
  for(int i=0;i<mergedTracks.Length;i++)Check((string)mergedTracks[i]["name"]=="Melody"+(i+1),"Merged numbering");
  var imported=(Dictionary<string,object>)mergedTracks[0]["mainRef"];var importedDb=(Dictionary<string,object>)imported["database"];Check((string)importedDb["backendType"]=="custom-engine"&&(string)importedDb["version"]=="209","Raw database modified");
  Check(MidiToVocal.ReadSingerFromProject(merged).Contains("209"),"Read voice from SVP");
  using(var voiceUi=new ConverterWindow(settings,false)){
   voiceUi.SetSingerMarker(raw);Check(Field<TextBox>(voiceUi,"singer").Text=="Example 2","Voice UI must show only name");
   Check(voiceUi.CapturePreferences().Singer.Contains("209"),"Imported version lost");
   voiceUi.CapturePreferences().Save(settings);
   Field<TextBox>(voiceUi,"singer").Text="Mai";Check(voiceUi.CapturePreferences().Singer=="Mai","Manual edit must replace imported metadata");
  }
  using(var restoredVoice=new ConverterWindow(settings,false)){Check(Field<TextBox>(restoredVoice,"singer").Text=="Example 2"&&restoredVoice.CapturePreferences().Singer.Contains("209"),"Imported voice restore");}
  var lastGroup=(Dictionary<string,object>)mergedTracks.Last()["mainGroup"];var lastNote=((IEnumerable)lastGroup["notes"]).Cast<Dictionary<string,object>>().First();Check(System.Convert.ToInt64(lastNote["onset"])==7056000,"Per-file PPQ normalization");
  using(var compact=new ConverterWindow(settings,false)){compact.Show();Application.DoEvents();Check(!Field<Panel>(compact,"content").AutoScroll,"Scrolling layout retained");Check(compact.Height<=Screen.FromControl(compact).WorkingArea.Height,"Window outside work area");compact.Close();}
  Console.WriteLine("PASS: controls, persistence, five quantization grids, NaturalMinor, direct/raw singer markers, multi-MIDI merge with mixed PPQ, compact screen fit.");
 }
}

