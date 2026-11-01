using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
namespace Chamfer.Trackball.App;
internal sealed class InputDriver:NativeWindow,IDisposable {
    readonly Win32.Hook mouseProc,keyProc;
    HookPump? hooks;
    readonly object keysGate=new(),routingGate=new();
    readonly ConcurrentQueue<(int Key,bool Down)> keyReplays=new();
    readonly HashSet<int> delayedKeys=[];
    int keysPending,stalled,heldRouting;
    long uiPulse=Environment.TickCount64;
    string? hookError;
    readonly HashSet<int> physical=[];
    readonly Dictionary<nint,string> deviceNames=[];
    readonly HashSet<int> deferred=[];
    readonly HashSet<int> usedDeferred=[];
    public event Action<int,int,string,bool>? Motion;
    public event Action? OtherPointerMotion;
    public event Func<int,int,bool>? MouseEvent;
    public event Action? KeysChanged;
    public event Action? RoutingReset;
    public Func<bool> IsCapture=()=>false;
    public Func<bool> IsCad=()=>false;
    public Func<IReadOnlySet<int>,bool> IsNavigationChord=_=>false;
    public Func<string> SelectedDevice=()=>"";
    readonly DeviceMapping mapping=new();readonly MouseGate gate=new();
    readonly InputRecovery recovery=new();
    long selectedMovementReports,deliveredMovementReports,otherMovementReports,lastMovementAt;
    readonly MotionHealth motionHealth=new();readonly ConcurrentQueue<MouseMatch> matched=[];
    const int DispatchEdges=0x8001,DispatchKeys=0x8002,DispatchFailure=0x8003;
    readonly System.Windows.Forms.Timer watchdog=new(){Interval=30};
    readonly HashSet<(int,int)> consumed=[];
    volatile bool failed,connected,motionBlocked;
    bool learning,recoverOnInput;
    long learnUntil,nextRegistrationCheck,nextPresenceCheck;
    public Func<bool> SwapButtons=()=>false,SwapWheels=()=>false,Enabled=()=>true;
    public event Action<string>? Identified;
    public string DeviceStatus {get;private set;}="Identify your trackball to enable device-only controls.";
    public string LastDevice {get;private set;}="";
    public bool Ready=>connected&&!failed&&SelectedDevice().Length>0;
    public bool Identifying=>learning;
    public JsonObject SignalState()=>new(){
        ["selectedMoves"]=selectedMovementReports,["deliveredMoves"]=deliveredMovementReports,["otherMoves"]=otherMovementReports,
        ["lastMoveAgeMs"]=lastMovementAt==0?-1:Math.Max(0,Environment.TickCount64-lastMovementAt),
        ["hooksRunning"]=hooks?.Running==true,["ready"]=Ready
    };
    bool Routing=>(Ready&&(Enabled()||SwapButtons()||SwapWheels()))||Volatile.Read(ref heldRouting)>0;
    bool HookHealthy{
        get{
            if(hooks?.Running==true&&Environment.TickCount64-Volatile.Read(ref uiPulse)<=150)return true;
            Interlocked.Exchange(ref stalled,1);return false;
        }
    }
    void NotifyKeys(){if(Interlocked.Exchange(ref keysPending,1)==0)Win32.PostMessage(Handle,DispatchKeys,0,0);}
    void ReplayKey(int key,bool down){keyReplays.Enqueue((key,down));NotifyKeys();}
    void HookFailed(string message){failed=true;Interlocked.Exchange(ref hookError,message);Win32.PostMessage(Handle,DispatchFailure,0,0);}
    public void Identify(){learning=true;learnUntil=Environment.TickCount64+60000;DeviceStatus="Roll only the trackball now…";failed=false;recoverOnInput=false;}
    public void CancelIdentify(){learning=false;DeviceStatus=Ready?"Trackball connected":"Trackball not identified";}
    void Watchdog(object? sender,EventArgs e){
        var now=Environment.TickCount64;Volatile.Write(ref uiPulse,now);
        if(Interlocked.Exchange(ref stalled,0)!=0)FailOpen("App was busy. Normal mouse restored; reconnecting automatically.",true);
        if(now>=nextRegistrationCheck){EnsureRegistration();nextRegistrationCheck=now+250;}
        if(now>=nextPresenceCheck){RefreshPresence();nextPresenceCheck=now+1000;}
        if(learning&&now>learnUntil)CancelIdentify();
        bool expired;
        lock(routingGate){
            if(!IsCapture()){motionHealth.Reset();motionBlocked=false;}
            expired=gate.Expired(now)||motionHealth.Expired(now);
        }
        if(expired)FailOpen("Input interrupted. Normal mouse restored; reconnecting automatically.",true);
        RecoverIfHealthy(now);
    }
    void RecoverIfHealthy(long now){
        bool eligible=failed&&recoverOnInput&&connected&&hooks?.Running==true&&OwnsRegistration();
        if(!recovery.Observe(eligible,now))return;
        failed=false;recoverOnInput=false;recovery.Reset();DeviceStatus="Trackball connected";
    }
    void FailOpen(string message,bool reconnect=false){
        recovery.Reset();failed=true;recoverOnInput=reconnect;motionBlocked=false;lock(routingGate)motionHealth.Reset();DeviceStatus=message;
        RoutingReset?.Invoke();
        // Finish matched physical edges before releasing the mapped state.
        while(matched.TryDequeue(out var match))try{Edge(match.Device,match.Edge);}catch(InvalidOperationException){}
        foreach(var edge in mapping.ReleaseAll())try{Emit(edge);}catch(InvalidOperationException){}
        consumed.Clear();Volatile.Write(ref heldRouting,0);
        MouseEdge[] drained;lock(routingGate)drained=gate.Drain();
        foreach(var edge in drained)try{Emit(edge);}catch(InvalidOperationException){}
    }
    bool OwnsRegistration(){
        uint count=0,size=(uint)Marshal.SizeOf<Win32.RawDevice>();
        var query=Win32.GetRegisteredRawInputDevices(null,ref count,size);
        if(count>256||(query==uint.MaxValue&&Marshal.GetLastWin32Error()!=122))return false;
        var list=new Win32.RawDevice[count];
        if(Win32.GetRegisteredRawInputDevices(list,ref count,size)==uint.MaxValue)return false;
        // Windows reports INPUTSINK here without the DEVNOTIFY registration bit.
        return list.Any(d=>d.Page==1&&d.Usage==2&&d.Target==Handle&&(d.Flags&0x100)!=0);
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
        hooks?.VerifyResponsive();VerifyKeyDispatch();
        var original=SelectedDevice;var originalEnabled=Enabled;var originalButtons=SwapButtons;var originalWheels=SwapWheels;bool originalConnected=connected,originalFailed=failed,originalRecover=recoverOnInput;string originalStatus=DeviceStatus;
        try{
            SelectedDevice=()=>"saved-trackball";
            ApplyPresence(["other-mouse","SAVED-TRACKBALL"]);
            if(!Ready)throw new InvalidOperationException("Saved trackball did not reconnect before motion.");
            ApplyPresence(["other-mouse"]);
            if(Ready)throw new InvalidOperationException("Unrelated mouse was mistaken for the saved trackball.");
            ApplyPresence(["saved-trackball","other-mouse"]);
            if(!Ready)throw new InvalidOperationException("Reinserted trackball did not reconnect.");
            FailOpen("UI pause fixture",true);connected=true;
            RecoverIfHealthy(1000);RecoverIfHealthy(1100);RecoverIfHealthy(1200);
            if(Ready)throw new InvalidOperationException("Input resumed before the recovery grace period.");
            RecoverIfHealthy(1290);
            if(!Ready)throw new InvalidOperationException("A healthy saved trackball stayed disabled after a UI pause until physical motion.");
            FailOpen("Permanent input error fixture",false);RecoverIfHealthy(2000);RecoverIfHealthy(2300);
            if(Ready)throw new InvalidOperationException("A non-recoverable input error resumed automatically.");
            failed=false;recoverOnInput=false;recovery.Reset();
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
        }finally{SelectedDevice=original;Enabled=originalEnabled;SwapButtons=originalButtons;SwapWheels=originalWheels;connected=originalConnected;failed=originalFailed;recoverOnInput=originalRecover;DeviceStatus=originalStatus;recovery.Reset();RefreshPresence();EnsureRegistration();}
        Console.WriteLine("Input recovery verified: startup presence, exact-device identity, disconnect/reconnect, corrections with 3D off and replaced Raw Input registration.");
    }
    void VerifyKeyDispatch(){
        var selected=SelectedDevice;var enabled=Enabled;var cad=IsCad;var chord=IsNavigationChord;
        bool wasConnected=connected,wasFailed=failed;
        int directCalls=0;void Changed()=>directCalls++;
        if(!keyReplays.IsEmpty)throw new InvalidOperationException("Keyboard test requires an empty dispatch queue.");
        int[] originalKeys;lock(keysGate)originalKeys=physical.ToArray();
        try{
            SelectedDevice=()=>"keyboard-fixture";Enabled=()=>true;IsCad=()=>true;
            IsNavigationChord=keys=>keys.Contains(81)&&!keys.Contains(87);
            connected=true;failed=false;Volatile.Write(ref uiPulse,Environment.TickCount64);KeysChanged+=Changed;
            if(!RouteKey(81,true)||!RouteKey(81,false)||directCalls!=0)
                throw new InvalidOperationException("A held-key callback ran controller work or lost an unused tap.");
            if(!keyReplays.ToArray().SequenceEqual(new[]{(81,true),(81,false)}))
                throw new InvalidOperationException("An unused navigation key did not queue one tap.");
            while(keyReplays.TryDequeue(out _)){ }
            RouteKey(81,true);lock(keysGate)usedDeferred.Add(81);RouteKey(81,false);
            if(!keyReplays.IsEmpty)throw new InvalidOperationException("A used navigation hold replayed a stray key.");
            RouteKey(81,true);RouteKey(87,true);RouteKey(81,false);RouteKey(87,false);
            if(!keyReplays.ToArray().SequenceEqual(new[]{(81,true),(87,true),(81,false),(87,false)}))
                throw new InvalidOperationException("Deferred typing lost key order or a matching release.");
            while(keyReplays.TryDequeue(out _)){ }
            IsNavigationChord=keys=>keys.Contains(91)&&keys.All(key=>key is 91 or 18);
            foreach(int win in new[]{91,92}){
                if(!RouteKey(win,true)||!Keys.Contains(91)||!RouteKey(win,false)||!keyReplays.IsEmpty)
                    throw new InvalidOperationException("Windows navigation opened Start or lost right-Windows normalization.");
            }
            RouteKey(91,true);RouteKey(18,true);RouteKey(18,false);RouteKey(91,false);
            if(!keyReplays.IsEmpty)throw new InvalidOperationException("Unused Windows+Alt navigation opened a system menu.");
            RouteKey(91,true);RouteKey(68,true);RouteKey(91,false);RouteKey(68,false);
            if(!keyReplays.ToArray().SequenceEqual(new[]{(91,true),(68,true),(91,false),(68,false)}))
                throw new InvalidOperationException("Windows+D did not retain its ordered system shortcut.");
            while(keyReplays.TryDequeue(out _)){ }
            IsCad=()=>false;
            if(RouteKey(91,true)||RouteKey(91,false)||!keyReplays.IsEmpty)
                throw new InvalidOperationException("Windows was intercepted outside the 3D context.");
            IsCad=()=>true;Enabled=()=>false;
            if(RouteKey(92,true)||RouteKey(92,false))throw new InvalidOperationException("Disabled controls intercepted Windows.");
            if(directCalls!=0)throw new InvalidOperationException("Keyboard routing called the controller inside the hook.");
        }finally{
            KeysChanged-=Changed;SelectedDevice=selected;Enabled=enabled;IsCad=cad;IsNavigationChord=chord;
            connected=wasConnected;failed=wasFailed;
            lock(keysGate){physical.Clear();foreach(var key in originalKeys)physical.Add(key);deferred.Clear();usedDeferred.Clear();delayedKeys.Clear();}
            while(keyReplays.TryDequeue(out _)){ }Interlocked.Exchange(ref keysPending,0);
        }
        Console.WriteLine("Keyboard routing verified: queued unused tap, used hold, ordered typing and no synchronous controller callback.");
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
        else lock(keysGate)foreach(var k in deferred)usedDeferred.Add(k);
        Volatile.Write(ref heldRouting,mapping.HeldCount+consumed.Count);
    }
    void Match(MouseMatch? match){
        if(match is not MouseMatch item||!item.Blocked)return;
        matched.Enqueue(item);
        // Never inject or call controller actions from inside the low-level hook.
        Win32.PostMessage(Handle,DispatchEdges,0,0);
    }
    public IReadOnlySet<int> Keys {
        get{lock(keysGate){
            var result=new HashSet<int>(physical.Where(x=>x is not (160 or 161 or 162 or 163 or 164 or 165 or 91 or 92)));
            if(physical.Any(x=>x is 16 or 160 or 161))result.Add(16);
            if(physical.Any(x=>x is 17 or 162 or 163))result.Add(17);
            if(physical.Any(x=>x is 18 or 164 or 165))result.Add(18);
            if(physical.Any(x=>x is 91 or 92))result.Add(91);
            return result;
        }}
    }
    public InputDriver(){
        CreateHandle(new CreateParams{Caption="Chamfer Trackball input",Parent=new nint(-3)});
        // Start with no held keys. GetAsyncKeyState is an aggregate OS state,
        // including injected/stale keys; it cannot establish a physical hold.
        // The hook records real down/up edges after startup.
        mouseProc=OnMouse;keyProc=OnKey;
        try{hooks=new HookPump(mouseProc,keyProc);}
        catch{Dispose();throw;}
        if(!Win32.RegisterRawInputDevices([new Win32.RawDevice{Page=1,Usage=2,Flags=0x2100,Target=Handle}],1,(uint)Marshal.SizeOf<Win32.RawDevice>())){Dispose();throw new InvalidOperationException("Cannot register Raw Input.");}
        watchdog.Tick+=Watchdog;watchdog.Start();
    }
    bool RouteKey(int k,bool down){
        lock(keysGate){
            if(down)physical.Add(k);else physical.Remove(k);
            if(delayedKeys.Contains(k)){
                ReplayKey(k,down);if(!down)delayedKeys.Remove(k);return true;
            }
            bool trigger=k is not (16 or 17 or 160 or 161 or 162 or 163)&&HookHealthy&&IsNavigationChord(Keys);
            if(down&&trigger&&IsCad()&&Ready&&Enabled()){deferred.Add(k);if(k is 91 or 92||Keys.Contains(91))usedDeferred.Add(k);NotifyKeys();return true;}
            if(down&&!trigger&&k is not (16 or 17 or 160 or 161 or 162 or 163)&&deferred.Count>0){
                foreach(var old in deferred.ToArray()){
                    ReplayKey(old,true);delayedKeys.Add(old);deferred.Remove(old);usedDeferred.Remove(old);
                }
                ReplayKey(k,true);delayedKeys.Add(k);return true;
            }
            NotifyKeys();
            if(!down&&deferred.Remove(k)){
                if(!usedDeferred.Remove(k)){ReplayKey(k,true);ReplayKey(k,false);}
                return true;
            }
            return false;
        }
    }
    nint OnKey(int code,nint w,nint l){
        if(code<0)return Win32.CallNextHookEx(0,code,w,l);
        var e=Marshal.PtrToStructure<Win32.KeyHook>(l);
        if((e.Flags&16)!=0||e.Extra==(nuint)Win32.Marker)return Win32.CallNextHookEx(0,code,w,l);
        try{if(RouteKey((int)e.Key,(int)w is 0x100 or 0x104))return 1;}
        catch(Exception ex){HookFailed("Normal keyboard restored: "+ex.Message);}
        return Win32.CallNextHookEx(0,code,w,l);
    }
    nint OnMouse(int code,nint w,nint l){
        if(code<0)return Win32.CallNextHookEx(0,code,w,l);
        var e=Marshal.PtrToStructure<Win32.MouseHook>(l);
        if((e.Flags&1)!=0||e.Extra==(nuint)Win32.Marker)return Win32.CallNextHookEx(0,code,w,l);
        try{
            int msg=(int)w;
            if(msg==0x200&&Ready&&Enabled()&&HookHealthy&&IsCapture()){
                lock(routingGate){motionBlocked=true;motionHealth.Block(Environment.TickCount64);}
                lock(keysGate)foreach(var key in deferred)usedDeferred.Add(key);
                return 1;
            }
            if(msg is 0x201 or 0x202 or 0x204 or 0x205 or 0x207 or 0x208 or 0x20b or 0x20c or 0x20a or 0x20e){
                var data=msg is 0x20a or 0x20e or 0x20b or 0x20c?unchecked((short)(e.Data>>16)):0;
                bool blocked=HookHealthy&&Routing;
                MouseMatch? match;lock(routingGate)match=gate.Hook(new(msg,data),Environment.TickCount64,blocked);
                Match(match);
                if(blocked)return 1;
            }
        }catch(Exception ex){HookFailed("Normal input restored: "+ex.Message);}
        return Win32.CallNextHookEx(0,code,w,l);
    }
    void RawMouse(string device,ushort flags,ushort buttons,short data,int dx,int dy){
        if(learning&&(flags&1)==0&&(Math.Abs((long)dx)+Math.Abs((long)dy)>1)&&device.Length>0){
            learning=false;Identified?.Invoke(device);connected=true;failed=false;DeviceStatus="Trackball identified. Original inputs are preserved; corrections are optional.";
        }
        bool target=device.Length>0&&device.Equals(SelectedDevice(),StringComparison.OrdinalIgnoreCase);
        if(target){LastDevice=device;connected=true;if(recoverOnInput){failed=false;recoverOnInput=false;}if(!failed&&!learning)DeviceStatus="Trackball connected";}
        if(dx!=0||dy!=0){
            if(target){selectedMovementReports++;lastMovementAt=Environment.TickCount64;}else otherMovementReports++;
            lock(routingGate)motionHealth.Raw();
        }
        bool capture=Ready&&Enabled()&&HookHealthy&&IsCapture();
        if((flags&1)==0&&(dx!=0||dy!=0)){
            if(target&&Enabled()&&!failed&&Volatile.Read(ref stalled)==0){
                if(IsCad())lock(keysGate)foreach(var k in deferred)usedDeferred.Add(k);
                deliveredMovementReports++;Motion?.Invoke(dx,dy,device,motionBlocked||capture);
            }else{
                OtherPointerMotion?.Invoke();
                if(motionBlocked||capture)Win32.Mouse(1,dx,dy);
            }
            motionBlocked=false;
        }else if((flags&1)!=0&&(motionBlocked||capture)){
            // Absolute mice/tablets must also keep pointing during
            // trackball navigation. Preserve the reported coordinate space.
            Win32.Mouse(0x8001u|((flags&2)!=0?0x4000u:0u),dx,dy);motionBlocked=false;
        }
        foreach(var (flag,msg,value) in new (int,int,int)[]{(1,0x201,0),(2,0x202,0),(4,0x204,0),(8,0x205,0),(16,0x207,0),(32,0x208,0),(64,0x20b,1),(128,0x20c,1),(256,0x20b,2),(512,0x20c,2),(1024,0x20a,data),(2048,0x20e,data)})
            if((buttons&flag)!=0){MouseMatch? match;lock(routingGate)match=gate.Raw(device,new(msg,value),Environment.TickCount64);Match(match);}
    }
    internal void VerifyMotionDelivery(){
        var selected=SelectedDevice;var enabled=Enabled;var capture=IsCapture;var cad=IsCad;
        bool wasConnected=connected,wasFailed=failed,wasRecover=recoverOnInput,wasLearning=learning;
        string status=DeviceStatus,lastDevice=LastDevice;long selectedCount=selectedMovementReports,delivered=deliveredMovementReports,others=otherMovementReports,lastAt=lastMovementAt;
        int received=0;void Observe(int x,int y,string device,bool captured){if(x==24&&y==-12&&device=="sensor-fixture"&&!captured)received++;}
        Motion+=Observe;
        try{
            SelectedDevice=()=>"sensor-fixture";Enabled=()=>true;IsCapture=()=>false;IsCad=()=>false;
            connected=true;failed=recoverOnInput=learning=false;Interlocked.Exchange(ref stalled,0);Volatile.Write(ref uiPulse,Environment.TickCount64);
            RawMouse("other-sensor",0,0,0,31,16);
            RawMouse("sensor-fixture",0,0,0,24,-12);
            if(received!=1||selectedMovementReports!=selectedCount+1||deliveredMovementReports!=delivered+1||otherMovementReports!=others+1)
                throw new InvalidOperationException("The selected raw sensor did not reach the controller without pointer capture.");
        }finally{
            Motion-=Observe;SelectedDevice=selected;Enabled=enabled;IsCapture=capture;IsCad=cad;
            connected=wasConnected;failed=wasFailed;recoverOnInput=wasRecover;learning=wasLearning;DeviceStatus=status;LastDevice=lastDevice;
            selectedMovementReports=selectedCount;deliveredMovementReports=delivered;otherMovementReports=others;lastMovementAt=lastAt;
        }
        Console.WriteLine("Raw sensor delivery verified: selected-device filtering, uncaptured movement, persistent counters and controller callback.");
    }
    protected override void WndProc(ref Message m){
        if(m.Msg==DispatchKeys){
            Interlocked.Exchange(ref keysPending,0);
            try{
                while(keyReplays.TryDequeue(out var key))Win32.Key(key.Key,key.Down);
                KeysChanged?.Invoke();
            }catch(Exception ex){FailOpen("Normal keyboard restored: "+ex.Message);}
        }
        if(m.Msg==DispatchFailure)FailOpen(Interlocked.Exchange(ref hookError,null)??"Normal input restored.",true);
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
                            RawMouse(device,flags,buttons,data,dx,dy);
                        }
                    }
                }catch(Exception ex){FailOpen("Normal input restored: "+ex.Message);}
                finally{Marshal.FreeHGlobal(p);}
            }
        }
        base.WndProc(ref m);
    }
    public void Dispose(){
        watchdog.Stop();watchdog.Dispose();hooks?.Dispose();hooks=null;FailOpen("Closed");
        lock(keysGate){deferred.Clear();usedDeferred.Clear();delayedKeys.Clear();physical.Clear();}
        while(keyReplays.TryDequeue(out _)){ }DestroyHandle();
    }
}
