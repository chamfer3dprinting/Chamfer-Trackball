# Read-only timing diagnostic. Run in PowerShell 7 on Windows.
# It creates no visible window and changes no system timer settings.
Add-Type -AssemblyName System.Windows.Forms
$timerProbeRefs=@(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object FullName)+@([System.Windows.Forms.Timer].Assembly.Location)
Add-Type -TypeDefinition @'
using System;using System.Linq;using System.Collections.Generic;using System.Diagnostics;using System.Windows.Forms;
public static class TrackballTimerProbe {
 public static string Measure(int interval) {
  var gaps=new List<double>();var clock=Stopwatch.StartNew();var last=clock.Elapsed.TotalMilliseconds;
  using(var context=new ApplicationContext())using(var tick=new Timer()){
   tick.Interval=interval;tick.Tick+=(s,e)=>{double now=clock.Elapsed.TotalMilliseconds;gaps.Add(now-last);last=now;if(gaps.Count>=45){tick.Stop();context.ExitThread();}};tick.Start();Application.Run(context);
  }
  gaps.Sort();return "Interval "+interval+" ms: median="+gaps[gaps.Count/2].ToString("F2")+", p95="+gaps[(int)(gaps.Count*.95)].ToString("F2")+", max="+gaps.Last().ToString("F2")+" ms";
 }
}
'@ -ReferencedAssemblies $timerProbeRefs
[TrackballTimerProbe]::Measure(16)
[TrackballTimerProbe]::Measure(8)
