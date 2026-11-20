using System.Diagnostics;
namespace Chamfer.Trackball.App;
internal static class Program{
    [STAThread]static void Main(string[] args){
        if(args.Length==4&&args[0]=="--install-update"){Install(args);return;}
        ApplicationConfiguration.Initialize();
        using var single=new Mutex(true,"Local\\ChamferTrackballRebuilt",out bool first);
        if(!first){MessageBox.Show("Chamfer Trackball is already running. Open it from the tray.");return;}
        try{Application.Run(new MainForm(args.Contains("--smoke-test"),args.Contains("--tray")));}
        catch(Exception ex){if(args.Contains("--smoke-test")){File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-ui-smoke.error"),ex.ToString());Console.Error.WriteLine(ex);Environment.Exit(1);}MessageBox.Show(ex.Message,"Chamfer Trackball",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
    static void Install(string[] args)
    {
        try
        {
            var target = Path.GetFullPath(args[2]);
            var source = Environment.ProcessPath!;
            if (!Path.GetFileName(target).StartsWith("ChamferTrackball", StringComparison.OrdinalIgnoreCase) ||
                !Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                source.Equals(target, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Invalid update destination.");

            using var installer = new Mutex(true, @"Local\ChamferTrackballInstalling", out bool first);
            if (!first)
                throw new IOException("A Trackball update is already being installed.");

            if (!int.TryParse(args[3], out int pid) || pid <= 0 || pid == Environment.ProcessId)
                throw new IOException("Invalid update process.");

            WaitForPreviousApp(pid, target);
            ReplaceExecutable(source, target);
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception error)
        {
            MessageBox.Show("Update was not installed: " + error.Message);
        }
    }

    static void WaitForPreviousApp(int pid, string target)
    {
        Process old;
        try { old = Process.GetProcessById(pid); }
        catch (ArgumentException) { return; }

        using (old)
        {
            if (old.HasExited) return;
            string? path;
            try { path = old.MainModule?.FileName; }
            catch (InvalidOperationException) when (old.HasExited) { return; }
            if (!string.Equals(path, target, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Update process mismatch.");
            if (!old.WaitForExit(20000))
                throw new IOException("The previous Trackball app has not closed.");
        }
    }

    internal static void ReplaceExecutable(string source, string target)
    {
        var staging = target + "." + Guid.NewGuid().ToString("N") + ".update";
        var previous = target + ".previous";
        try
        {
            File.Copy(source, staging);
            var timer = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    // Use a sibling file so Windows can replace the executable in one step.
                    File.Replace(staging, target, previous);
                    return;
                }
                catch (IOException error) when ((error.HResult & 0xffff) is 32 or 33 or 1175 or 1176)
                {
                    // Windows can keep the executable locked briefly after the app exits.
                    if (timer.Elapsed >= TimeSpan.FromSeconds(15))
                        throw new IOException("Windows still has the app file open. Reopen Trackball and try Update again.", error);
                    Thread.Sleep(250);
                }
                catch (IOException error) when ((error.HResult & 0xffff) == 1177 && !File.Exists(target))
                {
                    // Windows moved the original aside but could not move the new file into place.
                    File.Move(previous, target);
                    throw;
                }
            }
        }
        finally
        {
            try { File.Delete(staging); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
