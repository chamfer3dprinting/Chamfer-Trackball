using System.Runtime.InteropServices;
using System.Text;
namespace Chamfer.Trackball.App;
internal static class Win32 {
    internal const long Marker=0x43485442;
    internal delegate nint Hook(int code,nint w,nint l);
    [DllImport("user32.dll",SetLastError=true)]internal static extern nint SetWindowsHookEx(int type,Hook proc,nint module,uint thread);
    [DllImport("user32.dll")]internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]internal static extern nint CallNextHookEx(nint hook,int code,nint w,nint l);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]internal static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll")]internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll",SetLastError=true)]internal static extern bool PostMessage(nint window,int message,nint w,nint l);
    [DllImport("user32.dll")]internal static extern uint GetWindowThreadProcessId(nint window,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern int GetWindowText(nint w,StringBuilder text,int n);
    [DllImport("user32.dll")]internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")]internal static extern bool GetCursorPos(out Point p);
    [DllImport("user32.dll",SetLastError=true)]internal static extern uint SendInput(uint n,Input[] inputs,int size);
    [DllImport("user32.dll",SetLastError=true)]internal static extern bool RegisterRawInputDevices(RawDevice[] devices,uint n,uint size);
    [DllImport("user32.dll")]internal static extern uint GetRawInputData(nint input,uint command,nint data,ref uint size,uint headerSize);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]internal static extern uint GetRawInputDeviceInfo(nint device,uint command,StringBuilder? text,ref uint size);
    [StructLayout(LayoutKind.Sequential)]internal struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]internal struct MouseHook{public Point Point;public uint Data,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)]internal struct KeyHook{public uint Key,Scan,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)]internal struct RawDevice{public ushort Page,Usage;public uint Flags;public nint Target;}
    [StructLayout(LayoutKind.Sequential)]internal struct RawHeader{public uint Type,Size;public nint Device,W;}
    [StructLayout(LayoutKind.Sequential)]internal struct Input{public uint Type;public Union Data;}
    [StructLayout(LayoutKind.Explicit)]internal struct Union{[FieldOffset(0)]public MouseInput Mouse;[FieldOffset(0)]public KeyInput Key;}
    [StructLayout(LayoutKind.Sequential)]internal struct MouseInput{public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)]internal struct KeyInput{public ushort Key,Scan;public uint Flags,Time;public nuint Extra;}
    internal static void Mouse(uint flags,int x=0,int y=0,int data=0)=>Send([new Input{Type=0,Data=new Union{Mouse=new MouseInput{X=x,Y=y,Data=unchecked((uint)data),Flags=flags,Extra=(nuint)Marker}}}]);
    internal static void Key(int key,bool down)=>Send([new Input{Type=1,Data=new Union{Key=new KeyInput{Key=(ushort)key,Flags=down?0u:2u,Extra=(nuint)Marker}}}]);
    internal static void Text(string text){foreach(var c in text)Send([new Input{Type=1,Data=new Union{Key=new KeyInput{Scan=c,Flags=4,Extra=(nuint)Marker}}},new Input{Type=1,Data=new Union{Key=new KeyInput{Scan=c,Flags=6,Extra=(nuint)Marker}}}]);}
    static void Send(Input[] input){if(SendInput((uint)input.Length,input,Marshal.SizeOf<Input>())!=input.Length)throw new InvalidOperationException("Windows refused input. Run the CAD app and Trackball at the same permission level.");}
}
