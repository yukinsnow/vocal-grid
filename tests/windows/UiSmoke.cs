using System;using System.IO;using System.Reflection;using System.Linq;using System.Windows.Forms;
class UiSmoke {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static T Field<T>(object f,string name){return (T)f.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f);}
 static void Drop(Control c,string path){var data=new DataObject(DataFormats.FileDrop,new[]{path});var e=new DragEventArgs(data,0,0,0,DragDropEffects.Copy,DragDropEffects.None);typeof(Control).GetMethod("OnDragDrop",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{e});}
 [STAThread]static void Main(string[] args){Application.EnableVisualStyles();using(var f=new ConverterWindow()){
  var input=Field<TextBox>(f,"input");var output=Field<TextBox>(f,"output");
  Check(input.AllowDrop&&output.AllowDrop,"Drop targets not enabled");
  Check(Field<TextBox>(f,"singer").Text=="Mai","Default voice changed");
  Check(Field<ConverterWindow.ModernSelect>(f,"generation").SelectedIndex==0,"Default generation changed");
  Check((int)Field<ConverterWindow.ModernSelect>(f,"den").SelectedItem==4,"Default meter changed");
  string root=Path.GetFullPath(args[0]);string midi=Path.Combine(root,"poly.mid");
  Drop(input,midi);Check(input.Text==midi,"MIDI drop event failed");
  Drop(output,root);Check(output.Text==root,"Directory drop event failed");
  string one=Path.Combine(root,"single");Directory.CreateDirectory(one);File.Copy(midi,Path.Combine(one,"one.MID"),true);
  Drop(input,one);Check(input.Text==Path.Combine(one,"one.MID"),"Import directory drop event failed");
  var candidates=ConverterWindow.MidiCandidates(new[]{root});Check(candidates.Length>=3,"Multiple MIDI discovery failed");
  Check(!candidates.Any(x=>x.EndsWith(".svp")),"Non-MIDI included");
 }Console.WriteLine("PASS: native drop events (MIDI and output folder), import folder, multi-file discovery, default voice/generation/meter.");}
}
