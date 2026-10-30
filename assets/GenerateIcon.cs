using System;using System.IO;using System.Drawing;using System.Drawing.Drawing2D;using System.Drawing.Imaging;using System.Collections.Generic;
class MakeIcon {
 static GraphicsPath R(float x,float y,float w,float h,float r){var p=new GraphicsPath();p.AddArc(x,y,r*2,r*2,180,90);p.AddArc(x+w-r*2,y,r*2,r*2,270,90);p.AddArc(x+w-r*2,y+h-r*2,r*2,r*2,0,90);p.AddArc(x,y+h-r*2,r*2,r*2,90,90);p.CloseFigure();return p;}
 static Bitmap Draw(int size){var b=new Bitmap(size,size);using(var g=Graphics.FromImage(b)){g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/256f,size/256f);
 using(var p=R(8,8,240,240,52))using(var fill=new SolidBrush(Color.FromArgb(37,37,37)))g.FillPath(fill,p);
 using(var pen=new Pen(Color.FromArgb(221,221,221),18)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,52,72,62,96);g.DrawLine(pen,69,113,79,137);g.DrawLine(pen,86,154,99,185);}
 using(var pen=new Pen(Color.FromArgb(120,226,27),22)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,100,185,149,65);}
 using(var pen=new Pen(Color.FromArgb(221,221,221),12)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,158,129,158,163);g.DrawLine(pen,181,99,181,190);g.DrawLine(pen,204,139,204,154);}
 }return b;}
 static void Main(string[] args){if(args.Length>1){using(var high=Draw(1024))high.Save(args[1],ImageFormat.Png);}int[] sizes={16,24,32,48,64,128,256};var images=new List<byte[]>();foreach(int s in sizes)using(var b=Draw(s))using(var ms=new MemoryStream()){b.Save(ms,ImageFormat.Png);images.Add(ms.ToArray());if(s==256)b.Save(Path.Combine(args[0],"VocalGrid.png"),ImageFormat.Png);}using(var w=new BinaryWriter(File.Create(Path.Combine(args[0],"VocalGrid.ico")))){w.Write((short)0);w.Write((short)1);w.Write((short)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((short)1);w.Write((short)32);w.Write(images[i].Length);w.Write(offset);offset+=images[i].Length;}foreach(var bytes in images)w.Write(bytes);}}
}
