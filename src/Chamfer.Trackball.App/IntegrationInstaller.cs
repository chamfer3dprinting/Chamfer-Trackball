using System.Reflection;
namespace Chamfer.Trackball.App;
internal static class IntegrationInstaller{
    public static string Install(string app){
        if(app=="blender"){
            var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Blender Foundation","Blender");
            if(!Directory.Exists(root))throw new IOException("Run Blender once, then use this button again.");
            var versions=Directory.GetDirectories(root).Where(d=>Version.TryParse(Path.GetFileName(d),out _)).ToArray();
            if(versions.Length==0)throw new IOException("No Blender user configuration found. Run Blender once first.");
            foreach(var version in versions){var addons=Path.Combine(version,"scripts","addons");Directory.CreateDirectory(addons);Write("blender.chamfer_trackball.py",Path.Combine(addons,"chamfer_trackball.py"));}
            return "Blender integration installed. In Blender, open Preferences > Add-ons and enable Chamfer Trackball.";
        }
        if(app=="fusion"){
            var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Autodesk","Autodesk Fusion 360","API","AddIns","ChamferTrackball");
            Directory.CreateDirectory(root);Write("fusion.ChamferTrackball.py",Path.Combine(root,"ChamferTrackball.py"));Write("fusion.ChamferTrackball.manifest",Path.Combine(root,"ChamferTrackball.manifest"));
            return "Fusion integration installed. Open Utilities > Add-ins > Scripts and Add-ins, select ChamferTrackball, then Run and Run on Startup.";
        }
        throw new ArgumentException("Unknown integration.");
    }
    static void Write(string suffix,string destination){
        var assembly=Assembly.GetExecutingAssembly();var name=assembly.GetManifestResourceNames().Single(n=>n.EndsWith(suffix,StringComparison.Ordinal));
        using var resource=assembly.GetManifestResourceStream(name)!;using var file=File.Create(destination);resource.CopyTo(file);
    }
}
