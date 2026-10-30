using System.Reflection;
namespace Chamfer.Trackball.App;
internal static class Brand{
 public static readonly byte[] Png=Convert.FromBase64String(Read("logo.base64"));
 public static readonly Icon Icon=LoadIcon();
 public static string DataUrl=>"data:image/png;base64,"+Convert.ToBase64String(Png);
 static string Read(string file){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("ChamferTrackball."+file)!;using var r=new StreamReader(s);return r.ReadToEnd();}
 static Icon LoadIcon(){using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("ChamferTrackball.logo.ico")!;using var icon=new Icon(s);return (Icon)icon.Clone();}
 public static Image Image(){using var s=new MemoryStream(Png);using var im=System.Drawing.Image.FromStream(s);return new Bitmap(im);}
}
