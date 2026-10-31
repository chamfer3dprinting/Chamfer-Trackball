namespace Chamfer.Trackball.App;

// Global hooks have their own message pump so WebView/layout/settings work
// cannot hold up Windows input. Controller work stays on the app thread.
internal sealed class HookPump:IDisposable{
    const int Ping=0x8007;
    readonly Thread thread;
    readonly Win32.Hook mouseProc,keyProc;
    readonly ManualResetEventSlim ready=new();
    nint window,mouseHook,keyHook;
    int requested,acknowledged;
    volatile bool stopping;
    Exception? startupError;
    public bool Running=>Volatile.Read(ref window)!=0&&!stopping;
    public HookPump(Win32.Hook mouse,Win32.Hook key){
        mouseProc=mouse;keyProc=key;
        thread=new Thread(Run){IsBackground=true,Name="Chamfer Trackball input hooks"};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();
        if(!ready.Wait(3000)||startupError!=null){
            Dispose();throw new InvalidOperationException("Cannot start mouse/keyboard input.",startupError);
        }
    }
    void Run(){
        try{
            using var sink=new PumpWindow(this);
            Volatile.Write(ref window,sink.Handle);
            mouseHook=Win32.SetWindowsHookEx(14,mouseProc,Win32.GetModuleHandle(null),0);
            keyHook=Win32.SetWindowsHookEx(13,keyProc,Win32.GetModuleHandle(null),0);
            if(mouseHook==0||keyHook==0)throw new InvalidOperationException("Cannot listen for mouse/keyboard input.");
            ready.Set();if(!stopping)Application.Run();
        }catch(Exception ex){startupError=ex;ready.Set();}
        finally{Unhook();Volatile.Write(ref window,0);}
    }
    void Unhook(){
        var mouse=Interlocked.Exchange(ref mouseHook,0);if(mouse!=0)Win32.UnhookWindowsHookEx(mouse);
        var key=Interlocked.Exchange(ref keyHook,0);if(key!=0)Win32.UnhookWindowsHookEx(key);
    }
    public void VerifyResponsive(){
        int value=Interlocked.Increment(ref requested);
        // This wait deliberately does not pump the settings thread's messages.
        if(!Win32.PostMessage(Volatile.Read(ref window),Ping,value,0)||
            !SpinWait.SpinUntil(()=>Volatile.Read(ref acknowledged)==value,1000))
            throw new InvalidOperationException("Global input pump waited for the settings window.");
        Console.WriteLine("Global input pump responds independently while the settings thread is blocked.");
    }
    sealed class PumpWindow:NativeWindow,IDisposable{
        readonly HookPump owner;
        public PumpWindow(HookPump owner){
            this.owner=owner;CreateHandle(new CreateParams{Caption="Chamfer Trackball hooks",Parent=new nint(-3)});
        }
        protected override void WndProc(ref Message m){
            if(m.Msg==0x10){Application.ExitThread();return;}
            if(m.Msg==Ping){Volatile.Write(ref owner.acknowledged,(int)m.WParam);return;}
            base.WndProc(ref m);
        }
        public void Dispose()=>DestroyHandle();
    }
    public void Dispose(){
        stopping=true;Unhook();var target=Volatile.Read(ref window);
        if(target!=0)Win32.PostMessage(target,0x10,0,0);
        if(Thread.CurrentThread!=thread&&thread.IsAlive)thread.Join(1500);
    }
}
