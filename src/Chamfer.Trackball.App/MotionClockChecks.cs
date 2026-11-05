using System.Diagnostics;
namespace Chamfer.Trackball.App;
internal static class MotionClockChecks {
    public static async Task Run(){
        int calls=0,main=Environment.CurrentManagedThreadId;bool same=true;var gaps=new List<double>();var watch=Stopwatch.StartNew();double last=0;
        using var clock=new MotionClock(()=>{double now=watch.Elapsed.TotalMilliseconds;if(last!=0)gaps.Add(now-last);last=now;same&=main==Environment.CurrentManagedThreadId;calls++;});
        await Task.Delay(35);if(calls!=0)throw new InvalidOperationException("Idle navigation clock kept waking.");
        if(!clock.SetEnabled(true))throw new InvalidOperationException("Navigation clock could not start.");
        await Task.Delay(120);clock.SetEnabled(false);int stopped=calls;await Task.Delay(35);
        if(stopped<2||calls!=stopped||!same||clock.Enabled)throw new InvalidOperationException("Navigation clock lost UI-thread dispatch or continued after stop.");
        clock.SetEnabled(true);Thread.Sleep(40);clock.SetEnabled(false);int before=calls;await Task.Delay(35);
        if(calls!=before)throw new InvalidOperationException("A stale clock callback escaped a canceled navigation hold.");
        clock.SetEnabled(true);await Task.Delay(60);clock.SetEnabled(false);
        if(calls<=before)throw new InvalidOperationException("Navigation clock did not restart.");
        gaps.Sort();Console.WriteLine("Navigation clock verified: UI thread="+same+", high resolution="+clock.HighResolution+", callbacks="+calls+", median="+gaps[gaps.Count/2].ToString("F2")+" ms; quiet idle, canceled callbacks and restart pass.");
    }
}
