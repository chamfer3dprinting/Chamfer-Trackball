using System.Runtime.InteropServices;
using System.Text;
using Chamfer.Trackball.Core;
namespace Chamfer.Trackball.App;
internal sealed class InputDriver:NativeWindow,IDisposable {
    readonly Win32.Hook mouseProc,keyProc;
    nint mouseHook,keyHook;
    readonly HashSet<int> physical=[];
    readonly Dictionary<nint,string> deviceNames=[];
    readonly HashSet<int> deferred=[];
    readonly HashSet<int> usedDeferred=[];
    public event Action<int,int,string>? Motion;
    public event Func<int,int,bool>? MouseEvent;
    public event Action? KeysChanged;
    public event Action? RoutingReset;
    public Func<bool> IsCapture=()=>false;
    public Func<bool> IsCad=()=>false;
    public Func<IReadOnlySet<int>,bool> IsNavigationChord=_=>false;
    public Func<string> SelectedDevice=()=>"";
    readonly DeviceMapping mapping=new();readonly MouseGate gate=new();
    readonly MotionHealth motionHealth=new();readonly Queue<MouseMatch> matched=[];
    const int DispatchEdges=0x8001;
    readonly System.Windows.Forms.Timer watchdog=new(){Interval=30};
    readonly HashSet<(int,int)> consumed=[];
    bool failed,learning,connected,motionBlocked,recoverOnInput;
    long learnUntil,nextRegistrationCheck,nextPresenceCheck;
    public Func<bool> SwapButtons=()=>false,SwapWheels=()=>false,Enabled=()=>true;
    public event Action<string>? Identified;
    public string DeviceStatus {get;private set;}="Identify your trackball to enable device-only controls.";
    public string LastDevice {get;private set;}="";
    public bool Ready=>connected&&!failed&&SelectedDevice().Length>0;
    bool Routing=>(Ready&&(Enabled()||SwapButtons()||SwapWheels()))||mapping.HeldCount>0||consumed.Count>0;
    public void Identify(){learning=true;learnUntil=Environment.TickCount64+15000;DeviceStatus="Roll only the trackball now…";failed=false;recoverOnInput=false;}
    public void CancelIdentify(){learning=false;DeviceStatus=Ready?"Trackball connected":"Trackball not identified";}
    void Watchdog(object? sender,EventArgs e){
        var now=Environment.TickCount64;
        if(now>=nextRegistrationCheck){EnsureRegistration();nextRegistrationCheck=now+250;}
        if(now>=nextPresenceCheck){RefreshPresence();nextPresenceCheck=now+1000;}
        if(learning&&now>learnUntil)CancelIdentify();
        if(!IsCapture()){motionHealth.Reset();motionBlocked=false;}
        if(gate.Expired(now)||motionHealth.Expired(now))FailOpen("Input interrupted. Normal mouse restored; reconnecting automatically.",true);
    }
    void FailOpen(string message,bool reconnect=false){
        failed=true;recoverOnInput=reconnect;motionBlocked=false;motionHealth.Reset();DeviceStatus=message;
        RoutingReset?.Invoke();
        // Finish matched physical edges before releasing the mapped state.
        while(matched.TryDequeue(out var match))try{Edge(match.Device,match.Edge);}catch(InvalidOperationException){}
        foreach(var edge in mapping.ReleaseAll())try{Emit(edge);}catch(InvalidOperationException){}
        consumed.Clear();
        foreach(var edge in gate.Drain())try{Emit(edge);}catch(InvalidOperationException){}
    }
    bool OwnsRegistration(){
        uint count=0,size=(uint)Marshal.SizeOf<Win32.RawDevice>();
        var query=Win32.GetRegisteredRawInputDevices(null,ref count,size);
        if(count>256||(query==uint.MaxValue&&Marshal.GetLastWin32Error()!=122))return false;
        var list=new Win32.RawDevice[count];
        if(Win32.GetRegisteredRawInputDevices(list,ref count,size)==uint.MaxValue)return false;
        return list.Any(d=>d.Page==1&&d.Usage==2&&d.Target==Handle&&(d.Flags&0x2100)==0x2100);
    }
    void EnsureRegistration(){
        if(OwnsRegistration())return;
        if(!Win32.RegisterRawInputDevices([new Win32.RawDevice{Page=1,Usage=2,Flags=0x2100,Target=Handle}],1,(uint)Marshal.SizeOf<Win32.RawDevice>())){
            FailOpen("Trackball input unavailable. Normal mouse restored.",true);return;
        }
        // WebView2 or another host component can replace a process's sole mouse
        // registration. Restore our dedicated input window before routing again.
        if(recoverOnInput){failed=false;recoverOnInput=false;motionHealth.Reset();}
        nextPresenceCheck=0;
    }
    static string DeviceName(nint handle){
        uint length=0;Win32.GetRawInputDeviceInfo(handle,0x20000007,null,ref length);
        if(length==0||length>4095)return "";
        var name=new StringBuilder((int)length+1);
        return Win32.GetRawInputDeviceInfo(handle,0x20000007,name,ref length)==uint.MaxValue?"":name.ToString();
    }
    static string[] PresentMice(){
        uint count=0,size=(uint)Marshal.SizeOf<Win32.RawList>();
        if(Win32.GetRawInputDeviceList(null,ref count,size)==uint.MaxValue||count>1024)return [];
        var list=new Win32.RawList[count];
        if(Win32.GetRawInputDeviceList(list,ref count,size)==uint.MaxValue)return [];
        return list.Where(d=>d.Type==0).Select(d=>DeviceName(d.Handle)).Where(n=>n.Length>0).ToArray();
    }
    void RefreshPresence()=>ApplyPresence(PresentMice());
    void ApplyPresence(IEnumerable<string> names){
        string selected=SelectedDevice();
        bool present=selected.Length>0&&names.Any(n=>n.Equals(selected,StringComparison.OrdinalIgnoreCase));
        if(!present){
            if(connected)FailOpen("Trackball disconnected. Normal mouse remains available.",true);
            connected=false;return;
        }
        if(!connected&&recoverOnInput){failed=false;recoverOnInput=false;}
        connected=true;
        if(!failed&&!learning)DeviceStatus="Trackball connected";
    }
    internal void VerifyRecovery(){
        var original=SelectedDevice;var originalEnabled=Enabled;var originalButtons=SwapButtons;var originalWheels=SwapWheels;bool originalConnected=connected,originalFailed=failed,originalRecover=recoverOnInput;string originalStatus=DeviceStatus;
        try{
            SelectedDevice=()=>"saved-trackball";
            ApplyPresence(["other-mouse","SAVED-TRACKBALL"]);
            if(!Ready)throw new InvalidOperationException("Saved trackball did not reconnect before motion.");
            ApplyPresence(["other-mouse"]);
            if(Ready)throw new InvalidOperationException("Unrelated mouse was mistaken for the saved trackball.");
            ApplyPresence(["saved-trackball","other-mouse"]);
            if(!Ready)throw new InvalidOperationException("Reinserted trackball did not reconnect.");
            Enabled=()=>false;SwapButtons=()=>true;SwapWheels=()=>false;
            if(!Routing)throw new InvalidOperationException("Disabling 3D navigation lost the click correction.");
            SwapButtons=()=>false;SwapWheels=()=>true;
            if(!Routing)throw new InvalidOperationException("Disabling 3D navigation lost the wheel correction.");
            SwapWheels=()=>false;
            if(Routing)throw new InvalidOperationException("Disabled controls without corrections captured input.");
            using var replacement=new RegistrationProbe();
            if(!Win32.RegisterRawInputDevices([new Win32.RawDevice{Page=1,Usage=2,Flags=0x2100,Target=replacement.Handle}],1,(uint)Marshal.SizeOf<Win32.RawDevice>()))throw new InvalidOperationException("Cannot create registration-replacement fixture.");
            if(OwnsRegistration())throw new InvalidOperationException("Replacement registration was not detected.");
            EnsureRegistration();
            if(!OwnsRegistration())throw new InvalidOperationException("Raw Input registration was not restored.");
        }finally{SelectedDevice=original;Enabled=originalEnabled;SwapButtons=originalButtons;SwapWheels=originalWheels;connected=originalConnected;failed=originalFailed;recoverOnInput=originalRecover;DeviceStatus=originalStatus;RefreshPresence();EnsureRegistration();}
        Console.WriteLine("Input recovery verified: startup presence, exact-device identity, disconnect/reconnect, corrections with 3D off and replaced Raw Input registration.");
    }
    sealed class RegistrationProbe:NativeWindow,IDisposable{
        public RegistrationProbe()=>CreateHandle(new CreateParams{Caption="Input recovery fixture",Parent=new nint(-3)});
        public void Dispose()=>DestroyHandle();
    }
    static void Emit(MouseEdge edge){
        var flags=edge.Message switch{0x201=>2u,0x202=>4u,0x204=>8u,0x205=>16u,0x207=>32u,0x208=>64u,0x20b=>128u,0x20c=>256u,0x20a=>0x800u,0x20e=>0x1000u,_=>0u};
        if(flags!=0)Win32.Mouse(flags,0,0,edge.Data);
    }
    void Edge(string device,MouseEdge raw){
        var edge=mapping.Map(device,SelectedDevice(),raw,SwapButtons(),SwapWheels());
        bool target=device.Equals(SelectedDevice(),StringComparison.OrdinalIgnoreCase);
        var key=(edge.Button,edge.Data);
        bool handled=false;
        if(target&&Enabled())handled=MouseEvent?.Invoke(edge.Message,edge.Data)==true;
        if(edge.Down&&handled)consumed.Add(key);
        if(edge.Up&&consumed.Remove(key))handled=true;
        if(!handled)Emit(edge);
        else foreach(var k in deferred)usedDeferred.Add(k);
    }
    void Match(MouseMatch? match){
        if(match is not MouseMatch item||!item.Blocked)return;
        matched.Enqueue(item);
        // Never inject or call controller actions from inside the low-level hook.
        Win32.PostMessage(Handle,DispatchEdges,0,0);
    }
    public IReadOnlySet<int> Keys {
        get{
            var result=new HashSet<int>(physical.Where(x=>x is not (160 or 161 or 162 or 163 or 164 or 165)));
            if(physical.Any(x=>x is 16 or 160 or 161))result.Add(16);
            if(physical.Any(x=>x is 17 or 162 or 163))result.Add(17);
            if(physical.Any(x=>x is 18 or 164 or 165))result.Add(18);
            return result;
        }
    }
    public InputDriver(){
        CreateHandle(new CreateParams{Caption="Chamfer Trackball input",Parent=new nint(-3)});
        foreach(var k in new[]{160,161,162,163,164,165,192})if(Win32.GetAsyncKeyState(k)<0)physical.Add(k);
        mouseProc=OnMouse;keyProc=OnKey;
        mouseHook=Win32.SetWindowsHookEx(14,mouseProc,Win32.GetModuleHandle(null),0);
        keyHook=Win32.SetWindowsHookEx(13,keyProc,Win32.GetModuleHandle(null),0);
        if(mouseHook==0||keyHook==0){Dispose();throw new InvalidOperationException("Cannot listen for mouse/keyboard input.");}
        if(!Win32.RegisterRawInputDevices([new Win32.RawDevice{Page=1,Usage=2,Flags=0x2100,Target=Handle}],1,(uint)Marshal.SizeOf<Win32.RawDevice>())){Dispose();throw new InvalidOperationException("Cannot register Raw Input.");}
        watchdog.Tick+=Watchdog;watchdog.Start();
    }
    nint OnKey(int code,nint w,nint l){
        if(code<0)return Win32.CallNextHookEx(keyHook,code,w,l);
        var e=Marshal.PtrToStructure<Win32.KeyHook>(l);
        if((e.Flags&16)!=0||e.Extra==(nuint)Win32.Marker)return Win32.CallNextHookEx(keyHook,code,w,l);
        var down=(int)w is 0x100 or 0x104;int k=(int)e.Key;
        if(down)physical.Add(k);else physical.Remove(k);
        // Defer the configured non-modifier navigation key (or Alt) only when
        // that chord is active on the identified trackball. An unused tap is replayed.
        bool trigger=k is not (16 or 17 or 160 or 161 or 162 or 163) && IsNavigationChord(Keys);
        if(down&&trigger&&IsCad()&&Ready&&Enabled()){deferred.Add(k);KeysChanged?.Invoke();return 1;}
        if(down&&!trigger&&k is not (16 or 17 or 160 or 161 or 162 or 163)){
            if(deferred.Count>0){
                foreach(var old in deferred.ToArray()){Win32.Key(old,true);deferred.Remove(old);usedDeferred.Remove(old);}
                Win32.Key(k,true);KeysChanged?.Invoke();return 1;
            }
        }
        KeysChanged?.Invoke();
        if(!down&&deferred.Remove(k)){
            if(!usedDeferred.Remove(k)){Win32.Key(k,true);Win32.Key(k,false);}
            return 1;
        }
        return Win32.CallNextHookEx(keyHook,code,w,l);
    }
    nint OnMouse(int code,nint w,nint l){
        if(code<0)return Win32.CallNextHookEx(mouseHook,code,w,l);
        var e=Marshal.PtrToStructure<Win32.MouseHook>(l);
        if((e.Flags&1)!=0||e.Extra==(nuint)Win32.Marker)return Win32.CallNextHookEx(mouseHook,code,w,l);
        try{
            int msg=(int)w;
            if(msg==0x200&&Ready&&Enabled()&&IsCapture()){
                motionBlocked=true;motionHealth.Block(Environment.TickCount64);
                foreach(var key in deferred)usedDeferred.Add(key);
                return 1;
            }
            if(msg is 0x201 or 0x202 or 0x204 or 0x205 or 0x207 or 0x208 or 0x20b or 0x20c or 0x20a or 0x20e){
                var data=msg is 0x20a or 0x20e or 0x20b or 0x20c?unchecked((short)(e.Data>>16)):0;
                bool blocked=Routing;
                Match(gate.Hook(new(msg,data),Environment.TickCount64,blocked));
                if(blocked)return 1;
            }
        }catch(Exception ex){FailOpen("Normal input restored: "+ex.Message);}
        return Win32.CallNextHookEx(mouseHook,code,w,l);
    }
    protected override void WndProc(ref Message m){
        if(m.Msg==DispatchEdges){
            while(matched.TryDequeue(out var match))try{Edge(match.Device,match.Edge);}catch(Exception ex){FailOpen("Normal input restored: "+ex.Message);}
        }
        if(m.Msg==0xfe){
            if(m.WParam==2&&deviceNames.TryGetValue(m.LParam,out var removed)&&removed.Equals(SelectedDevice(),StringComparison.OrdinalIgnoreCase)){
                connected=false;FailOpen("Trackball disconnected. Normal mouse remains available.",true);
            }
            deviceNames.Clear();
        }
        if(m.Msg==0xff){
            uint size=0;uint header=(uint)Marshal.SizeOf<Win32.RawHeader>();
            Win32.GetRawInputData(m.LParam,0x10000003,0,ref size,header);
            if(size>=header+24&&size<=65536){
                var p=Marshal.AllocHGlobal((int)size);
                try{
                    if(Win32.GetRawInputData(m.LParam,0x10000003,p,ref size,header)==size){
                        var h=Marshal.PtrToStructure<Win32.RawHeader>(p);int offset=(int)header;
                        if(h.Type==0&&h.Device!=0&&unchecked((uint)Marshal.ReadInt32(p,offset+20))!=(uint)Win32.Marker){
                            if(!deviceNames.TryGetValue(h.Device,out var device)){
                                device=DeviceName(h.Device);deviceNames[h.Device]=device;
                            }
                            var flags=(ushort)Marshal.ReadInt16(p,offset);
                            var buttons=(ushort)Marshal.ReadInt16(p,offset+4);
                            var data=(short)Marshal.ReadInt16(p,offset+6);
                            int dx=Marshal.ReadInt32(p,offset+12),dy=Marshal.ReadInt32(p,offset+16);
                            if(learning&&(flags&1)==0&&(Math.Abs((long)dx)+Math.Abs((long)dy)>1)&&device.Length>0){
                                learning=false;Identified?.Invoke(device);connected=true;failed=false;DeviceStatus="Trackball identified. Original inputs are preserved; corrections are optional.";
                            }
                            bool target=device.Length>0&&device.Equals(SelectedDevice(),StringComparison.OrdinalIgnoreCase);
                            if(target){LastDevice=device;connected=true;if(recoverOnInput){failed=false;recoverOnInput=false;}if(!failed&&!learning)DeviceStatus="Trackball connected";}
                            if(dx!=0||dy!=0)motionHealth.Raw();
                            bool capture=Ready&&Enabled()&&IsCapture();
                            if((flags&1)==0&&(dx!=0||dy!=0)){
                                if(target&&Enabled()&&!failed){
                                    if(IsCad())foreach(var k in deferred)usedDeferred.Add(k);
                                    Motion?.Invoke(dx,dy,device);
                                }else if(motionBlocked||capture)Win32.Mouse(1,dx,dy);
                                motionBlocked=false;
                            }else if((flags&1)!=0&&(motionBlocked||capture)){
                                // Absolute mice/tablets must also keep pointing during
                                // trackball navigation. Preserve the reported coordinate space.
                                Win32.Mouse(0x8001u|((flags&2)!=0?0x4000u:0u),dx,dy);motionBlocked=false;
                            }
                            foreach(var (flag,msg,value) in new (int,int,int)[]{(1,0x201,0),(2,0x202,0),(4,0x204,0),(8,0x205,0),(16,0x207,0),(32,0x208,0),(64,0x20b,1),(128,0x20c,1),(256,0x20b,2),(512,0x20c,2),(1024,0x20a,data),(2048,0x20e,data)})
                                if((buttons&flag)!=0)Match(gate.Raw(device,new(msg,value),Environment.TickCount64));
                        }
                    }
                }catch(Exception ex){FailOpen("Normal input restored: "+ex.Message);}
                finally{Marshal.FreeHGlobal(p);}
            }
        }
        base.WndProc(ref m);
    }
    public void Dispose(){
        watchdog.Stop();watchdog.Dispose();FailOpen("Closed");
        if(mouseHook!=0)Win32.UnhookWindowsHookEx(mouseHook);if(keyHook!=0)Win32.UnhookWindowsHookEx(keyHook);
        mouseHook=keyHook=0;deferred.Clear();usedDeferred.Clear();physical.Clear();DestroyHandle();
    }
}
