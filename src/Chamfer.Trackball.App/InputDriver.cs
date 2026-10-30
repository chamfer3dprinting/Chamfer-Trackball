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
    public Func<bool> IsCapture=()=>false;
    public Func<bool> IsCad=()=>false;
    public Func<IReadOnlySet<int>,bool> IsNavigationChord=_=>false;
    public Func<string> SelectedDevice=()=>"";
    readonly DeviceMapping mapping=new();readonly MouseGate gate=new();
    readonly System.Windows.Forms.Timer watchdog=new(){Interval=30};
    readonly HashSet<(int,int)> consumed=[];
    bool failed,learning,connected,motionBlocked;
    long learnUntil,lastBlockedMotion,lastRaw;
    public Func<bool> SwapButtons=()=>false,SwapWheels=()=>false,Enabled=()=>true;
    public event Action<string>? Identified;
    public string DeviceStatus {get;private set;}="Identify your trackball to enable device-only controls.";
    public string LastDevice {get;private set;}="";
    public bool Ready=>connected&&!failed&&SelectedDevice().Length>0;
    bool Routing=>(Ready&&Enabled())||mapping.HeldCount>0||consumed.Count>0;
    public void Identify(){learning=true;learnUntil=Environment.TickCount64+15000;DeviceStatus="Roll only the trackball now…";failed=false;}
    public void CancelIdentify(){learning=false;DeviceStatus=Ready?"Trackball connected":"Trackball not identified";}
    void Watchdog(object? sender,EventArgs e){
        var now=Environment.TickCount64;
        if(learning&&now>learnUntil)CancelIdentify();
        if(gate.Expired(now)||(motionBlocked&&now-lastBlockedMotion>150&&now-lastRaw>150))FailOpen("Device input timed out. Normal mouse restored; identify the trackball again.");
    }
    void FailOpen(string message){
        failed=true;motionBlocked=false;DeviceStatus=message;
        foreach(var edge in mapping.ReleaseAll())try{Emit(edge);}catch(InvalidOperationException){}
        consumed.Clear();
        foreach(var edge in gate.Drain())try{Emit(edge);}catch(InvalidOperationException){}
    }
    static void Emit(MouseEdge edge){
        var flags=edge.Message switch{0x201=>2u,0x202=>4u,0x204=>8u,0x205=>16u,0x207=>32u,0x208=>64u,0x20b=>128u,0x20c=>256u,0x20a=>0x800u,0x20e=>0x1000u,_=>0u};
        if(flags!=0)Win32.Mouse(flags,0,0,edge.Data);
    }
    void Edge(string device,MouseEdge raw){
        // Only replay events that the hook actually withheld. Raw Input is also
        // delivered when the hook passes an event, so unconditional replay doubles clicks.
        if(!gate.Take(raw))return;
        var edge=mapping.Map(device,SelectedDevice(),raw,SwapButtons(),SwapWheels());
        bool target=device.Equals(SelectedDevice(),StringComparison.OrdinalIgnoreCase);
        var key=(edge.Button,edge.Data);
        bool handled=false;
        if(target)handled=MouseEvent?.Invoke(edge.Message,edge.Data)==true;
        if(edge.Down&&handled)consumed.Add(key);
        if(edge.Up&&consumed.Remove(key))handled=true;
        if(!handled)Emit(edge);
        else foreach(var k in deferred)usedDeferred.Add(k);
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
                motionBlocked=true;lastBlockedMotion=Environment.TickCount64;
                foreach(var key in deferred)usedDeferred.Add(key);
                return 1;
            }
            if(Routing&&msg is 0x201 or 0x202 or 0x204 or 0x205 or 0x207 or 0x208 or 0x20b or 0x20c or 0x20a or 0x20e){
                var data=msg is 0x20a or 0x20e or 0x20b or 0x20c?unchecked((short)(e.Data>>16)):0;
                gate.Block(new(msg,data),Environment.TickCount64);
                return 1;
            }
        }catch(Exception ex){FailOpen("Normal input restored: "+ex.Message);}
        return Win32.CallNextHookEx(mouseHook,code,w,l);
    }
    protected override void WndProc(ref Message m){
        if(m.Msg==0xfe){
            if(m.WParam==2&&deviceNames.TryGetValue(m.LParam,out var removed)&&removed.Equals(SelectedDevice(),StringComparison.OrdinalIgnoreCase)){
                connected=false;FailOpen("Trackball disconnected. Normal mouse remains available.");
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
                            lastRaw=Environment.TickCount64;
                            if(!deviceNames.TryGetValue(h.Device,out var device)){
                                uint len=0;Win32.GetRawInputDeviceInfo(h.Device,0x20000007,null,ref len);
                                var name=new StringBuilder((int)Math.Min(len+1,4096));
                                Win32.GetRawInputDeviceInfo(h.Device,0x20000007,name,ref len);
                                device=name.ToString();deviceNames[h.Device]=device;
                            }
                            var flags=(ushort)Marshal.ReadInt16(p,offset);
                            var buttons=(ushort)Marshal.ReadInt16(p,offset+4);
                            var data=(short)Marshal.ReadInt16(p,offset+6);
                            int dx=Marshal.ReadInt32(p,offset+12),dy=Marshal.ReadInt32(p,offset+16);
                            if(learning&&(flags&1)==0&&(Math.Abs((long)dx)+Math.Abs((long)dy)>1)&&device.Length>0){
                                learning=false;Identified?.Invoke(device);connected=true;failed=false;DeviceStatus="Trackball identified. Original inputs are preserved; corrections are optional.";
                            }
                            bool target=device.Length>0&&device.Equals(SelectedDevice(),StringComparison.OrdinalIgnoreCase);
                            if(target){LastDevice=device;connected=true;if(!failed&&!learning)DeviceStatus="Trackball connected";}
                            if((flags&1)==0&&(dx!=0||dy!=0)){
                                if(target&&Enabled()&&!failed){
                                    if(IsCad())foreach(var k in deferred)usedDeferred.Add(k);
                                    Motion?.Invoke(dx,dy,device);
                                }else if(motionBlocked)Win32.Mouse(1,dx,dy);
                                motionBlocked=false;
                            }else if((flags&1)!=0&&motionBlocked){
                                // Absolute mice/tablets must also keep pointing during
                                // trackball navigation. Preserve the reported coordinate space.
                                Win32.Mouse(0x8001u|((flags&2)!=0?0x4000u:0u),dx,dy);motionBlocked=false;
                            }
                            foreach(var (flag,msg,value) in new (int,int,int)[]{(1,0x201,0),(2,0x202,0),(4,0x204,0),(8,0x205,0),(16,0x207,0),(32,0x208,0),(64,0x20b,1),(128,0x20c,1),(256,0x20b,2),(512,0x20c,2),(1024,0x20a,data),(2048,0x20e,data)})
                                if((buttons&flag)!=0)Edge(device,new(msg,value));
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
