using Chamfer.Trackball.Core;
namespace Chamfer.Trackball.App;
internal static class Program{
    [STAThread]static void Main(string[] args){
        if(args.Length==4&&args[0]=="--install-update"){Install(args);return;}
        ApplicationConfiguration.Initialize();
        using var single=new Mutex(true,"Local\\ChamferTrackballRebuilt",out bool first);
        if(!first){MessageBox.Show("Chamfer Trackball is already running. Open it from the tray.");return;}
        try{Application.Run(new MainForm(args.Contains("--smoke-test")));}
        catch(Exception ex){if(args.Contains("--smoke-test")){Console.Error.WriteLine(ex);Environment.Exit(1);}MessageBox.Show(ex.Message,"Chamfer Trackball",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
    static void Install(string[] a){
        try{
            var target=Path.GetFullPath(a[2]);var source=Environment.ProcessPath!;
            if(!Path.GetFileName(target).StartsWith("ChamferTrackball",StringComparison.OrdinalIgnoreCase)||!Path.GetExtension(target).Equals(".exe",StringComparison.OrdinalIgnoreCase)||source==target)throw new IOException("Invalid update destination.");
            if(int.TryParse(a[3],out int pid))try{var old=System.Diagnostics.Process.GetProcessById(pid);if(old.MainModule?.FileName!=target)throw new IOException("Update process mismatch.");if(!old.WaitForExit(20000))throw new IOException("The old app has not closed.");}catch(ArgumentException){ }
            File.Copy(target,target+".previous",true);File.Copy(source,target,true);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target){UseShellExecute=true});
        }catch(Exception ex){MessageBox.Show("Update was not installed: "+ex.Message);}
    }
}
