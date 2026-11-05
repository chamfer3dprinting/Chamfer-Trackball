using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace Chamfer.Trackball.App;

// A waitable clock posts at most one UI callback. It is armed only during a
// navigation hold; it never changes Windows-wide timer resolution or runs input
// hooks/camera code on a worker thread.
internal sealed class MotionClock : NativeWindow, IDisposable
{
    const int TickMessage=0x8000+73;
    readonly Action tick;
    readonly ManualResetEvent stop=new(false);
    readonly KernelWait? timer;
    readonly Thread? worker;
    volatile bool enabled,disposed;
    int queued,generation;
    public bool HighResolution {get;}
    public bool Enabled=>enabled;
    sealed class KernelWait : WaitHandle {public KernelWait(SafeWaitHandle handle){SafeWaitHandle=handle;}}

    [DllImport("kernel32.dll",EntryPoint="CreateWaitableTimerExW",SetLastError=true,CharSet=CharSet.Unicode)]
    static extern SafeWaitHandle CreateTimer(nint attributes,string? name,uint flags,uint access);
    [DllImport("kernel32.dll",SetLastError=true)]
    static extern bool SetWaitableTimerEx(SafeWaitHandle timer,ref long due,int period,nint completion,nint argument,nint reason,uint tolerance);
    [DllImport("kernel32.dll",SetLastError=true)]
    static extern bool CancelWaitableTimer(SafeWaitHandle timer);
    [DllImport("user32.dll",EntryPoint="PostMessageW",SetLastError=true)]
    static extern bool Post(nint window,int message,nint wParam,nint lParam);

    public MotionClock(Action tick)
    {
        this.tick=tick;
        var handle=CreateTimer(0,null,2,0x1f0003);
        HighResolution=!handle.IsInvalid;
        if(handle.IsInvalid){handle.Dispose();handle=CreateTimer(0,null,0,0x1f0003);}
        if(handle.IsInvalid){handle.Dispose();return;}
        timer=new KernelWait(handle);
        CreateHandle(new CreateParams{Caption="Chamfer held navigation clock",Parent=new nint(-3)});
        worker=new Thread(Loop){IsBackground=true,Name="Chamfer held navigation clock"};
        worker.Start();
    }
    public bool SetEnabled(bool value)
    {
        if(disposed||timer==null)return false;
        if(enabled==value)return enabled;
        generation++;enabled=value;
        if(value){
            long due=-80000;
            if(!SetWaitableTimerEx(timer.SafeWaitHandle,ref due,8,0,0,0,0))enabled=false;
        }else CancelWaitableTimer(timer.SafeWaitHandle);
        return enabled;
    }
    void Loop()
    {
        var waits=new WaitHandle[]{stop,timer!};
        while(WaitHandle.WaitAny(waits)==1){
            int epoch=Volatile.Read(ref generation);
            if(!enabled||Interlocked.CompareExchange(ref queued,1,0)!=0)continue;
            if(!Post(Handle,TickMessage,epoch,0))Interlocked.Exchange(ref queued,0);
        }
    }
    protected override void WndProc(ref Message message)
    {
        if(message.Msg==TickMessage){
            Interlocked.Exchange(ref queued,0);
            if(enabled&&!disposed&&message.WParam.ToInt32()==generation)tick();
            return;
        }
        base.WndProc(ref message);
    }
    public void Dispose()
    {
        if(disposed)return;
        SetEnabled(false);disposed=true;stop.Set();worker?.Join();
        timer?.Dispose();stop.Dispose();if(Handle!=0)DestroyHandle();
    }
}
