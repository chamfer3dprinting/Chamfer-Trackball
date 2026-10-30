using System.Runtime.InteropServices;
using System.Text;
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
    public Func<string> SelectedDevice=()=>"";
    public string LastDevice {get;private set;}="";
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
        if(!Win32.RegisterRawInputDevices([new Win32.RawDevice{Page=1,Usage=2,Flags=0x100,Target=Handle}],1,(uint)Marshal.SizeOf<Win32.RawDevice>())){Dispose();throw new InvalidOperationException("Cannot register Raw Input.");}
    }
    nint OnKey(int code,nint w,nint l){
        if(code<0)return Win32.CallNextHookEx(keyHook,code,w,l);
        var e=Marshal.PtrToStructure<Win32.KeyHook>(l);
        if((e.Flags&16)!=0||e.Extra==(nuint)Win32.Marker)return Win32.CallNextHookEx(keyHook,code,w,l);
        var down=(int)w is 0x100 or 0x104;int k=(int)e.Key;
        if(down)physical.Add(k);else physical.Remove(k);
        // Defer standalone Alt/backtick in CAD so a held navigation key does not type
        // or open a menu. An unused tap is replayed. Other Alt shortcuts pass through.
        bool trigger=k is 18 or 164 or 165 or 192;
        if(down&&trigger&&IsCad()){deferred.Add(k);KeysChanged?.Invoke();return 1;}
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
            if(msg==0x200&&IsCapture()){
                foreach(var key in deferred)usedDeferred.Add(key);
                return 1;
            }
            if(MouseEvent?.Invoke(msg,unchecked((short)(e.Data>>16)))==true){foreach(var key in deferred)usedDeferred.Add(key);return 1;}
        }catch(Exception ex){System.Diagnostics.Debug.WriteLine(ex);}
        return Win32.CallNextHookEx(mouseHook,code,w,l);
    }
    protected override void WndProc(ref Message m){
        if(m.Msg==0xff){
            uint size=0;uint header=(uint)Marshal.SizeOf<Win32.RawHeader>();
            Win32.GetRawInputData(m.LParam,0x10000003,0,ref size,header);
            if(size>0&&size<=65536){
                var p=Marshal.AllocHGlobal((int)size);
                try{
                    if(Win32.GetRawInputData(m.LParam,0x10000003,p,ref size,header)==size){
                        var h=Marshal.PtrToStructure<Win32.RawHeader>(p);
                        if(h.Type==0){
                            // RAWMOUSE: flags@0, buttons union@4, rawButtons@8, lastX@12, lastY@16.
                            int offset=(int)header;ushort flags=(ushort)Marshal.ReadInt16(p,offset);
                            if((flags&1)==0){
                                if(!deviceNames.TryGetValue(h.Device,out var deviceName)){
                                uint len=0;Win32.GetRawInputDeviceInfo(h.Device,0x20000007,null,ref len);
                                var name=new StringBuilder((int)Math.Min(len+1,4096));
                                Win32.GetRawInputDeviceInfo(h.Device,0x20000007,name,ref len);
                                deviceName=name.ToString();deviceNames[h.Device]=deviceName;}
                                LastDevice=deviceName;var selected=SelectedDevice();
                                if(selected.Length==0||selected==LastDevice)
                                    Motion?.Invoke(Marshal.ReadInt32(p,offset+12),Marshal.ReadInt32(p,offset+16),LastDevice);
                            }
                        }
                    }
                }finally{Marshal.FreeHGlobal(p);}
            }
        }
        base.WndProc(ref m);
    }
    public void Dispose(){
        if(mouseHook!=0)Win32.UnhookWindowsHookEx(mouseHook);if(keyHook!=0)Win32.UnhookWindowsHookEx(keyHook);
        mouseHook=keyHook=0;deferred.Clear();usedDeferred.Clear();physical.Clear();DestroyHandle();
    }
}
