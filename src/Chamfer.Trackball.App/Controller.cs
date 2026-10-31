using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Chamfer.Trackball.Core;
namespace Chamfer.Trackball.App;
internal sealed class Controller:IDisposable{
    readonly ConfigStore store;readonly InputDriver input;readonly NativeServer server;
    readonly System.Windows.Forms.Timer timer=new(){Interval=16};
    volatile JsonObject cfg;volatile JsonObject? profile;NativeServer.Client? native;
    readonly ComboTracker combos=new();readonly HashSet<int> pressed=[];readonly HashSet<int> comboUsed=[];
    readonly Dictionary<int,JsonObject> intercepted=[];
    readonly Queue<JsonObject> logs=new();long seq;long revision=-1;
    readonly HashSet<int> sentMods=[];readonly HashSet<string> sentButtons=[];
    volatile NavMode mode=NavMode.Idle;NavMode latched=NavMode.Idle;string gestureButton="";
    readonly CircleZoom circle=new();readonly MotionGlide glide=new();readonly PointerMotion pointer=new();
    double gx,gy;readonly MotionFrames pending=new(),motionSamples=new();readonly FractionalInput fractions=new();NavMode emulationMode=NavMode.Idle;
    bool stoppedByClick,emulating;volatile bool nativeFocusAllowed=true;int foregroundPid;volatile nint foregroundWindow;
    Rectangle pad;bool padVisible;Point padOrigin;
    double ox,oy,px,py,zoom,zoomLog,rawX,rawY;bool fit;string lastBtn="",lastAction="";DateTime lastBtnTime;long lastBtnSeq;
    CancellationTokenSource? macro;
    public string Mode=>circle.Active?"Zoom":mode.ToString();
    public JsonObject GuideState()=>new(){["mode"]=Mode,["snapTurn"]=mode==NavMode.Snap?server.SnapState(native):null,["program"]=ConfigStore.S(cfg["programs"]?[ConfigStore.I(cfg,"activeProgram")],"name","Normal mouse"),["app"]=AppName,["keys"]=KeysState(),["shortcuts"]=ShortcutState(),["buttons"]=new JsonArray(Enumerable.Range(1,4).Select(i=>(JsonNode?)JsonValue.Create(pressed.Contains(i))).ToArray()),["bindings"]=cfg["buttons"]!.DeepClone()};
public string AppName{get;private set;}="";
    public string Integration{get;private set;}="Normal mouse";
    public bool IsCad=>profile!=null&&nativeFocusAllowed&&ConfigStore.B(cfg,"enabled")&&Win32.GetForegroundWindow()==foregroundWindow;
    public Controller(ConfigStore store,InputDriver input,NativeServer server){
        this.store=store;this.input=input;this.server=server;cfg=store.Snapshot();
        input.IsCad=()=>IsCad;input.IsCapture=()=>mode!=NavMode.Idle||PointerCapture;input.SelectedDevice=()=>ConfigStore.S(cfg,"selectedDevice");
        input.SwapButtons=()=>ConfigStore.B(cfg,"trackballSwapButtons");input.SwapWheels=()=>ConfigStore.B(cfg,"trackballSwapWheels");input.Enabled=()=>ConfigStore.B(cfg,"enabled");
        input.Identified+=device=>{store.IdentifyDevice(device);cfg=store.Snapshot();Log("Trackball identified; original clicks and wheel axes are preserved unless you enable a correction.");};
        input.IsNavigationChord=keys=>ModeResolver.Resolve(cfg,keys)!=NavMode.Idle;
        input.RoutingReset+=()=>{End();latched=NavMode.Idle;pressed.Clear();intercepted.Clear();comboUsed.Clear();combos.Clear();stoppedByClick=false;};
        input.KeysChanged+=KeysChanged;input.Motion+=Motion;input.MouseEvent+=MouseEvent;input.OtherPointerMotion+=pointer.Reset;
        timer.Tick+=Tick;timer.Start();Tick(null,EventArgs.Empty);
    }
    public void Log(string text,string kind="ok"){logs.Enqueue(new(){["seq"]=++seq,["time"]=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),["text"]=text,["kind"]=kind});while(logs.Count>100)logs.Dequeue();}
    public JsonArray LogAfter(long s)=>new(logs.Where(x=>ConfigStore.D(x,"seq")>s).Select(x=>x.DeepClone()).ToArray());
    void Tick(object? sender,EventArgs e){
        const int idleInterval=50;
        int interval=mode==NavMode.Idle&&!pointer.Active?idleInterval:16;
        if(timer.Interval!=interval)timer.Interval=interval;
        if(revision!=store.Revision){End();cfg=store.Snapshot();revision=store.Revision;foregroundWindow=0;latched=NavMode.Idle;combos.Clear();}
        var w=Win32.GetForegroundWindow();
        if(w!=foregroundWindow){
            End();latched=NavMode.Idle;combos.Clear();pressed.Clear();intercepted.Clear();comboUsed.Clear();foregroundWindow=w;
            Win32.GetWindowThreadProcessId(w,out var pid);foregroundPid=(int)pid;AppName="";
            string exe="";try{exe=Process.GetProcessById(foregroundPid).ProcessName+".exe";AppName=exe;}catch(ArgumentException){ }catch(System.ComponentModel.Win32Exception){ }
            var title=new StringBuilder(1024);Win32.GetWindowText(w,title,title.Capacity);
            profile=cfg["profiles"]!.AsArray().Select(x=>x?.AsObject()).FirstOrDefault(p=>p!=null&&ConfigStore.B(p,"enabled")&&
                (p["exe"]?.AsArray().Any(x=>Match(exe,x?.GetValue<string>()??""))==true||p["title"]?.AsArray().Any(x=>title.ToString().Contains(x?.GetValue<string>()??"\0",StringComparison.OrdinalIgnoreCase))==true));
        }
        if(profile==null&&padVisible&&Win32.GetCursorPos(out var cursor)&&new Rectangle(padOrigin.X+pad.X,padOrigin.Y+pad.Y,pad.Width,pad.Height).Contains(cursor.X,cursor.Y)){
            Win32.GetWindowThreadProcessId(w,out var pid);if(pid==Environment.ProcessId)profile=new(){["id"]="demo",["name"]="Try it here",["enabled"]=true};
        }else if(profile!=null&&ConfigStore.S(profile,"id")=="demo"){
            if(!padVisible||!Win32.GetCursorPos(out var cursor2)||!new Rectangle(padOrigin.X+pad.X,padOrigin.Y+pad.Y,pad.Width,pad.Height).Contains(cursor2.X,cursor2.Y)){End();profile=null;}
        }
        nativeFocusAllowed=profile==null||ConfigStore.S(profile,"id")=="demo"||server.AllowsNavigation(ConfigStore.S(profile,"id"),foregroundPid);
        if(mode==NavMode.Idle&&input.Ready&&ConfigStore.B(cfg,"enabled")&&PointerCapture){
            var movement=pointer.Tick(Environment.TickCount64,GlideSeconds,ConfigStore.B(cfg,"smooth",true)&&pressed.Count==0,PointerGain);
            if(movement.X!=0||movement.Y!=0)try{Win32.Mouse(1,movement.X,movement.Y);}catch(InvalidOperationException ex){pointer.Reset();Log(ex.Message,"err");}
        }else pointer.Reset();
        if(!input.Ready){if(mode!=NavMode.Idle)End();Integration=input.DeviceStatus;return;}
        if(!IsCad){if(mode!=NavMode.Idle)End();Integration=server.Error.Length>0?server.Error:"Normal mouse";return;}
        var desired=Resolve();
        if(desired==NavMode.Idle)stoppedByClick=false;
        desired=MotionPolicy.WhilePaused(desired,stoppedByClick,HeldNavigation);
        if(desired!=mode)Change(desired);
        if(native!=null&&mode!=NavMode.Idle){
            if(native.Version>=2&&native.Pose==null&&DateTime.UtcNow-native.Began>TimeSpan.FromMilliseconds(300)){End();stoppedByClick=true;Integration="Place the pointer over a 3D viewport, release the keys, and try again.";return;}
            if(server.Find(ConfigStore.S(profile,"id"),foregroundPid)!=native){End();Integration="Integration disconnected";return;}
        }
        if(mode!=NavMode.Idle)Flush();
    }
    static bool Match(string text,string pattern)=>pattern.Length>0&&pattern.Length<200&&Regex.IsMatch(text,"^"+Regex.Escape(pattern).Replace("\\*",".*")+"$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(10));
    NavMode Resolve(){
        var keyboard=ModeResolver.Resolve(cfg,input.Keys);if(keyboard!=NavMode.Idle)return keyboard;
        if(gestureButton.Length>0)return NavMode.Gestures;
        foreach(var button in pressed){var bind=cfg["buttons"]?[ButtonName(button)];switch(ConfigStore.S(bind,"action")){case "orbit":return NavMode.Orbit;case "pan":return NavMode.PanZoom;case "zoom":return NavMode.Zoom;}}
        if(latched!=NavMode.Idle)return latched;
        return ConfigStore.S(cfg,"ball","pointer") switch{"orbit"=>NavMode.Orbit,"pan"=>NavMode.PanZoom,"zoom"=>NavMode.Zoom,_=>NavMode.Idle};
    }
    bool HeldNavigation=>ModeResolver.Resolve(cfg,input.Keys)!=NavMode.Idle||pressed.Any(b=>ConfigStore.S(cfg["buttons"]?[ButtonName(b)],"action") is "orbit" or "pan" or "zoom");
    void KeysChanged(){if(!IsCad||!input.Ready)return;var n=Resolve();if(n==NavMode.Idle)stoppedByClick=false;n=MotionPolicy.WhilePaused(n,stoppedByClick,HeldNavigation);if(n!=mode)Change(n);}
    void Change(NavMode next){
        if(mode!=NavMode.Idle&&IsCad&&input.Ready)Flush();
        End();if(next==NavMode.Idle||!IsCad||!input.Ready)return;
        if(next==NavMode.Gestures){mode=next;timer.Interval=16;return;}
        native=server.Find(ConfigStore.S(profile,"id"),foregroundPid);
        bool demo=ConfigStore.S(profile,"id")=="demo";
        if(native!=null&&!server.Supports(native,next))native=null;
        if(native==null&&!demo&&next is NavMode.Snap or NavMode.Navigate or NavMode.Roll){
            Integration=next+" requires the native integration for "+ConfigStore.S(profile,"name");return;
        }
        if(native==null&&!demo&&!ConfigStore.B(cfg,"allowEmulation",true)){Integration="Native integration required";return;}
        mode=next;timer.Interval=16;Integration=native!=null?"Native camera + cursor pivot":demo?"Navigation preview":"Mouse emulation";
        if(native!=null){Win32.GetCursorPos(out var at);server.Begin(native,next,at.X,at.Y);}
    }
    void End(){
        server.End(native);native=null;pending.Reset();motionSamples.Reset();circle.Reset();glide.Reset();pointer.Reset();
        ReleaseEmulation();mode=NavMode.Idle;
    }
    void ReleaseEmulation(){
        foreach(var b in sentButtons.ToArray())try{Button(b,false);}catch(InvalidOperationException){ }sentButtons.Clear();
        foreach(var k in sentMods.ToArray())try{Win32.Key(k,false);}catch(InvalidOperationException){ }sentMods.Clear();
        // Restore only modifiers the person is still physically holding.
        if(emulating)foreach(var k in input.Keys.Where(x=>x is 16 or 17 or 18))try{Win32.Key(k,true);}catch(InvalidOperationException){ }
        emulating=false;emulationMode=NavMode.Idle;fractions.Reset();
    }
    bool PointerCapture=>mode==NavMode.Idle&&PointerPolicy.Capture(ConfigStore.I(cfg,"pointerSpeed",10),ConfigStore.B(cfg,"smooth",true));
    float PointerGain=>PointerPolicy.Gain(ConfigStore.I(cfg,"pointerSpeed",10));
    float OrbitGain=>MotionPolicy.Gain(ConfigStore.D(cfg,"orbitSens",50));
    float PanGain=>MotionPolicy.Gain(ConfigStore.D(cfg,"panSens",50));
    float ZoomGain=>MotionPolicy.Gain(ConfigStore.D(cfg,"zoomSens",50));
    float GlideSeconds=>(float)(.05+Math.Clamp(ConfigStore.D(cfg,"glide",5),1,100)/100*1.45);
    void Motion(int x,int y,string device,bool captured){
        rawX+=x;rawY+=y;
        if(!captured)return;
        if(mode==NavMode.Idle&&IsCad&&input.Ready){
            var desired=Resolve();
            if(MotionPolicy.ResumeAfterClick(stoppedByClick,desired,HeldNavigation,x!=0||y!=0)){
                stoppedByClick=false;Change(desired);
            }
        }
        if(mode==NavMode.Idle){
            var move=pointer.Move(x,y,PointerGain);if(move.X!=0||move.Y!=0)Win32.Mouse(1,move.X,move.Y);
            if(ConfigStore.B(cfg,"smooth",true))timer.Interval=16;return;
        }
        if(mode==NavMode.Gestures){gx+=x;gy+=y;return;}
        motionSamples.Move(x,y);
    }
    void Flush(){
        // Classify each newly sampled path once, even if a camera handshake delays delivery.
        var sample=motionSamples.Peek();motionSamples.ClearMove();bool consumed=false;
        bool freeBall=ModeResolver.Resolve(cfg,input.Keys)==NavMode.Idle&&!pressed.Any(b=>ConfigStore.S(cfg["buttons"]?[ButtonName(b)],"action") is "orbit" or "pan" or "zoom");
        if(!MotionPolicy.Circle(mode,ConfigStore.B(cfg,"circleZoom"),!freeBall))circle.Reset();
        else{
            var dial=circle.Add(sample.X,sample.Y,Environment.TickCount64,(float)ConfigStore.D(cfg,"circleSize",160));
            consumed=dial.Consumed;
            if(consumed){glide.Reset();pending.ClearMove();pending.Dial(dial.Wheel);}
        }
        if(!consumed)pending.Move(sample.X,sample.Y);
        var frame=pending.Peek();
        float seconds=GlideSeconds;
        var smooth=glide.Step(frame.X,frame.Y,Environment.TickCount64,seconds,mode!=NavMode.Snap&&ConfigStore.B(cfg,"smooth",true));
        if(frame.HasPrimary||smooth.X!=0||smooth.Y!=0){
            if(!SendMotion(mode,smooth.X,smooth.Y,frame.Wheel))return;
            pending.ClearPrimary();
        }
        if(frame.HasDial){
            if(!SendMotion(NavMode.Zoom,0,0,frame.Dial))return;
            pending.ClearDial();
        }
    }
    bool SendMotion(NavMode output,float x,float y,float w){

        if(output==NavMode.Orbit){x*=ConfigStore.B(cfg,"invertOrbitX")?-1:1;y*=ConfigStore.B(cfg,"invertOrbitY")?-1:1;}
        if(output==NavMode.PanZoom&&ConfigStore.B(cfg,"invertPan")){x=-x;y=-y;}
        if(output==NavMode.Zoom&&ConfigStore.B(cfg,"invertZoom"))y=-y;
        if(ConfigStore.B(cfg,"invertZoom"))w=-w;
        var scaled=MotionPolicy.Scale(output,x,y,w,OrbitGain,PanGain,ZoomGain);
        // Snap receives unscaled X/Y so dial recognition is independent of Orbit speed.
        float nativeGain=output==NavMode.Snap?MotionPolicy.SnapGain(ConfigStore.D(cfg,"orbitSens",50)):1;
        if(output!=NavMode.Snap){x=scaled.X;y=scaled.Y;w=scaled.Wheel;}
        bool demo=ConfigStore.S(profile,"id")=="demo";
        if(native!=null)try{if(!server.Move(native,output,x,y,w,input.Keys.Contains(18),nativeGain))return false;}
        catch(ArgumentException ex){Log("Camera movement stopped: "+ex.Message,"err");End();stoppedByClick=true;Integration="Release the keys and use the normal view controls before trying again.";return false;}
        if(native==null&&!demo){
            try{
                PrepareEmulation(output);
                var whole=fractions.Add(output is NavMode.Orbit or NavMode.PanZoom?x:0,output is NavMode.Orbit or NavMode.PanZoom?y:0,output==NavMode.Zoom?MotionPolicy.ZoomAmount(output,y,w)/.0015f:w);
                if(whole.X!=0||whole.Y!=0)Win32.Mouse(1,whole.X,whole.Y);
                if(whole.Wheel!=0)Win32.Mouse(0x800,0,0,whole.Wheel);
            }catch(Exception ex){Log(ex.Message,"err");End();return false;}
        }
        if(output==NavMode.Orbit){ox+=x;oy+=y;}if(output is NavMode.PanZoom or NavMode.Navigate){px+=x;py+=y;}
        zoomLog+=MotionPolicy.ZoomAmount(output,y,w);
        zoom+=output==NavMode.Zoom?-y*5.6f+w:w;return true;
    }
    void PrepareEmulation(NavMode output){
        if(emulating&&emulationMode==output)return;
        ReleaseEmulation();emulating=true;emulationMode=output;
        foreach(var k in new[]{16,17,18})if(input.Keys.Contains(k))Win32.Key(k,false);
        var gesture=profile?[output==NavMode.Orbit?"orbit":"pan"];
        if(output==NavMode.Zoom)return;
        foreach(var m in gesture?["mods"]?.AsArray()??new JsonArray()){
            int vk=KeyChord.VirtualKey(m!.GetValue<string>());Win32.Key(vk,true);sentMods.Add(vk);
        }
        foreach(var b in gesture?["buttons"]?.AsArray()??new JsonArray()){
            var name=b!.GetValue<string>();Button(name,true);sentButtons.Add(name);
        }
    }
    bool MouseEvent(int msg,int data){
        bool wheel=msg is 0x20a or 0x20e;
        if(wheel&&mode!=NavMode.Idle){if(circle.Active||(mode==NavMode.Orbit&&!ConfigStore.B(cfg,"twist",true)))pending.Dial(data);else pending.Wheel(data);return true;}
        if(wheel){
            var prefix=msg==0x20a?"right":"left";var wheelAction=ConfigStore.S(cfg,prefix+"Wheel","normal");
            if(wheelAction=="normal")return false;
            if(!IsCad&&wheelAction is not "keys" and not "program" and not "goProgram"&&!ConfigStore.B(cfg,"shortcutsEverywhere"))return false;
            if(wheelAction=="invert"){Win32.Mouse(msg==0x20a?0x800u:0x1000u,0,0,-data);return true;}
            if(wheelAction=="disabled")return true;
            if(wheelAction=="zoom"&&IsCad){Win32.Mouse(0x800,0,0,data);return true;}
            if(wheelAction is "program" or "goProgram"){int i=wheelAction=="program"?ConfigStore.I(cfg,"activeProgram")+(data>0?1:-1):cfg["wheelPrograms"]?[(prefix=="left"?0:2)+(data>0?0:1)]?.GetValue<int>()??0;int count=cfg["programs"]!.AsArray().Count;store.Select((i%count+count)%count);return true;}
            if(wheelAction=="keys"){Shortcut(ConfigStore.S(cfg,prefix+"Wheel"+(data>0?"UpKeys":"DownKeys")));return true;}
            if(wheelAction=="cycle"&&IsCad){latched=latched switch{NavMode.Idle=>NavMode.Orbit,NavMode.Orbit=>NavMode.PanZoom,NavMode.PanZoom=>NavMode.Roll,_=>NavMode.Idle};Change(latched);return true;}
            return false;
        }
        var (id,down)=msg switch{0x201=>(3,true),0x202=>(3,false),0x204=>(4,true),0x205=>(4,false),0x207=>(1,true),0x208=>(1,false),0x20b=>(data==1?2:5,true),0x20c=>(data==1?2:5,false),_=>(0,false)};
        if(id==0)return false;
        lastBtn=ButtonName(id);lastBtnTime=DateTime.UtcNow;lastBtnSeq++;
        pointer.Reset();if(down)pressed.Add(id);else pressed.Remove(id);
        var combo=combos.Change(id,down,cfg["combos"]?.AsArray());
        bool member=cfg["combos"]?.AsArray().Any(c=>c?["buttons"] is JsonArray ids&&ids.Count(v=>v!=null)>=2&&ids.Any(v=>v?.GetValue<int>()==id)&&ConfigStore.S(c?["bind"],"action") is not "" and not "normal"&&(IsCad||ActionPolicy.Global(ConfigStore.S(c?["bind"],"action"))))==true;
        if(combo!=null&&(IsCad||ActionPolicy.Global(ConfigStore.S(combo,"action")))){foreach(var p in pressed)comboUsed.Add(p);Dispatch(combo);return true;}
        if(down&&mode!=NavMode.Idle&&id is 3 or 4){End();latched=NavMode.Idle;stoppedByClick=true;}
        var bind=cfg["buttons"]?[ButtonName(id)]?.AsObject()??new JsonObject{["action"]="normal"};
        if(latched!=NavMode.Idle&&cfg["modeButtons"]?[ButtonName(id)] is JsonObject alternate&&ConfigStore.S(alternate,"action")!="normal")bind=alternate;
        string action=ConfigStore.S(bind,"action","normal");
        bool mapped=(IsCad||ActionPolicy.Global(action)||ConfigStore.B(cfg,"shortcutsEverywhere"))&&action!="normal"||member;
        if(!mapped)return false;
        if(down){
            intercepted[id]=(JsonObject)bind.DeepClone();
            if(action is "orbit" or "pan" or "zoom")Change(Resolve());
            else if(action=="gestures"){gestureButton=ButtonName(id);gx=gy=0;Change(NavMode.Gestures);}
            else if(!member)Dispatch(bind);
        }else if(intercepted.Remove(id,out var original)){
            if(gestureButton==ButtonName(id)){
                gestureButton="";End();if(Math.Sqrt(gx*gx+gy*gy)>32){var angle=(Math.Atan2(gy,gx)+Math.PI*2)%(Math.PI*2);var dirs=new[]{"right","downRight","down","downLeft","left","upLeft","up","upRight"};var g=cfg["programs"]![ConfigStore.I(cfg,"activeProgram")]?["gestures"]?[dirs[(int)Math.Round(angle/(Math.PI/4))%8]]?.AsObject();if(g!=null)Dispatch(g);}
            }else if(!comboUsed.Contains(id)){
                if(member&&ConfigStore.S(original,"action")=="normal")Click(ButtonName(id));else if(member)Dispatch(original);
            }
            if(ConfigStore.S(original,"action") is "orbit" or "pan" or "zoom")Change(Resolve());
        }
        if(!down){comboUsed.Remove(id);}
        return true;
    }
    static string ButtonName(int id)=>id switch{1=>"middle",2=>"back",3=>"left",4=>"right",_=>"forward"};
    void Dispatch(JsonObject b){
        var action=ConfigStore.S(b,"action",ConfigStore.S(b,"type"));lastAction=action;
        switch(action){
            case "normal":break;
            case "disabled":break;
            case "keys":Shortcut(ConfigStore.S(b,"keys",ConfigStore.S(b,"value")));break;
            case "macro":var m=cfg["macros"]!.AsArray().FirstOrDefault(m=>ConfigStore.S(m,"id")==ConfigStore.S(b,"macro"));if(m is JsonObject macroObj)PlayMacro(macroObj);break;
            case "sendLeft":Click("left");break;case "sendRight":Click("right");break;case "sendMiddle":Click("middle");break;case "sendX1":Click("back");break;case "sendX2":Click("forward");break;
            case "nextProgram":store.Select((ConfigStore.I(cfg,"activeProgram")+1)%cfg["programs"]!.AsArray().Count);break;
            case "program":store.Select(ConfigStore.I(b,"program"));break;
            case "fit":if(profile!=null)Shortcut(ConfigStore.S(profile,"fit"));fit=true;break;
            case "zoomIn":Win32.Mouse(0x800,0,0,120);break;case "zoomOut":Win32.Mouse(0x800,0,0,-120);break;
            case "lockRotate":case "lock3d":Toggle(NavMode.Orbit);break;case "lockTranslate":Toggle(NavMode.PanZoom);break;case "lockZoom":Toggle(NavMode.Zoom);break;case "lockTwist":Toggle(NavMode.Roll);break;
            case "panToggle":
                if(IsCad){var ball=ConfigStore.S(cfg,"ball")=="pan"?NavMode.PanZoom:NavMode.Orbit;latched=BallNavigation.ToggleOrbitPan(latched,ball);stoppedByClick=false;Change(latched);}break;
        }
        Log(action);
    }
    void Toggle(NavMode n){latched=latched==n?NavMode.Idle:n;Change(latched);}
    static void Button(string name,bool down){
        uint flag=name switch{"left"=>down?2u:4u,"right"=>down?8u:16u,"middle"=>down?32u:64u,"back" or "forward"=>down?128u:256u,_=>0};
        if(flag!=0)Win32.Mouse(flag,0,0,name=="back"?1:name=="forward"?2:0);
    }
    static void Click(string name){if(name=="double"){Click("left");Click("left");return;}Button(name,true);Button(name,false);}
    void Shortcut(string chord){
        if(string.IsNullOrWhiteSpace(chord))return;
        var sent=new List<int>();var restore=input.Keys.Where(k=>k is 16 or 17 or 18).ToArray();
        try{
            foreach(var k in restore)Win32.Key(k,false);
            foreach(var part in chord.Split(',')){
                var keys=part.Split('+',StringSplitOptions.RemoveEmptyEntries).Select(KeyChord.VirtualKey).ToArray();
                foreach(var k in keys){Win32.Key(k,true);sent.Add(k);}
                foreach(var k in sent.AsEnumerable().Reverse())Win32.Key(k,false);sent.Clear();
            }
        }finally{foreach(var k in sent.AsEnumerable().Reverse())Win32.Key(k,false);foreach(var k in restore.Where(input.Keys.Contains))Win32.Key(k,true);}
    }
    public async void PlayMacro(JsonObject m){
        if(macro!=null){macro.Cancel();macro=null;return;}var cancellation=macro=new();
        try{
            await Task.Delay(1500,cancellation.Token);
            foreach(var step in m["steps"]?.AsArray()??new JsonArray()){
                cancellation.Token.ThrowIfCancellationRequested();
                switch(ConfigStore.S(step,"type")){
                    case "keys":Shortcut(ConfigStore.S(step,"value"));break;
                    case "text":Win32.Text(ConfigStore.S(step,"value"));break;
                    case "click":Click(ConfigStore.S(step,"value","left"));break;
                    case "scroll":Win32.Mouse(0x800,0,0,Math.Clamp(ConfigStore.I(step,"amount",1),-50,50)*120);break;
                    case "wait":await Task.Delay(Math.Clamp(ConfigStore.I(step,"ms",500),1,60000),cancellation.Token);break;
                }
            }
        }catch(OperationCanceledException){ }catch(Exception ex){Log(ex.Message,"err");}finally{if(macro==cancellation)macro=null;cancellation.Dispose();}
    }
    JsonArray KeysState()=>new(input.Keys.Order().Select(key=>(JsonNode?)JsonValue.Create(key)).ToArray());
    JsonArray ShortcutState()=>new(new[]{("orbitKeys","Orbit"),("panKeys","Pan"),("zoomKeys","Zoom"),("snapKeys","Snap"),("navigateKeys","Navigate")}.Select(pair=>(JsonNode)new JsonObject{["chord"]=ConfigStore.S(cfg,pair.Item1,"none"),["label"]=pair.Item2}).ToArray());
    public JsonObject Status(){
        var result=new JsonObject{["mode"]=Mode,["snapTurn"]=mode==NavMode.Snap?server.SnapState(native):null,["navigationMode"]=Mode,["keys"]=KeysState(),["pointerGain"]=PointerGain,["orbitGain"]=OrbitGain,["panGain"]=PanGain,["zoomGain"]=ZoomGain,["cfgRev"]=store.Revision,["enabled"]=ConfigStore.B(cfg,"enabled"),["app"]=profile==null?AppName:ConfigStore.S(profile,"name"),["appState"]=IsCad?(input.Ready?"cad":"setup"):"mouse",["deviceReady"]=input.Ready,["identifying"]=input.Identifying,["device"]=input.Ready?"Trackball identified":input.DeviceStatus,["integration"]=Integration,["lastBtnSeq"]=lastBtnSeq,["lastBtn"]=lastBtn,["lastBtnAge"]=(DateTime.UtcNow-lastBtnTime).TotalMilliseconds,["lastAction"]=lastAction,["ox"]=ox,["oy"]=oy,["px"]=px,["py"]=py,["zoomLog"]=zoomLog,["zoom"]=zoom,["rawX"]=rawX,["rawY"]=rawY,["fit"]=fit,["lock3d"]=latched!=NavMode.Idle,["ballDir"]="",["sense"]="",["buttons"]=new JsonArray(pressed.Select(x=>JsonValue.Create(x)).ToArray())};
        ox=oy=px=py=zoom=zoomLog=rawX=rawY=0;fit=false;return result;
    }
    public void Pad(int x,int y,int w,int h,bool visible,Point origin){pad=new(x,y,w,h);padVisible=visible;padOrigin=origin;}
    public void Dispose(){timer.Stop();timer.Dispose();macro?.Cancel();End();}
}
