using System.Numerics;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
int count=0;void Check(bool pass,string name){count++;if(!pass)throw new Exception("FAILED: "+name);Console.WriteLine("PASS "+name);}
var defaults=ConfigStore.Defaults();
Check(ConfigStore.I(defaults,"activeProgram")==0&&ModeResolver.Resolve(defaults,new HashSet<int>{16})==NavMode.Idle,"Factory startup is an ordinary mouse");
ConfigStore.LoadProgram(defaults,1);
Check(ModeResolver.Resolve(defaults,new HashSet<int>{17})==NavMode.Idle,"Ctrl alone passes through");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{16})==NavMode.Orbit,"Shift orbits");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{16,17})==NavMode.PanZoom,"Ctrl+Shift wins over Shift");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{18})==NavMode.Snap,"Alt snaps");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{192})==NavMode.Navigate,"Backtick navigates");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{192,18})==NavMode.Navigate,"Alt modifies navigation height");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{18,70})==NavMode.Idle,"Alt shortcuts remain normal");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{16,65})==NavMode.Idle,"Shift typing remains normal");
Check(ModeResolver.Resolve(defaults,new HashSet<int>())==NavMode.Idle,"Release exits mode");
var c=new CameraPose(new(7,4,9),new(1,2,0),Vector3.UnitZ,new(1,2,0),.8f,10,1200,800,false);
var moved=CameraMath.Move(c,NavMode.Orbit,21,12,80);
Check(Vector3.Distance(c.Eye,moved.Eye)>.1,"Orbit moves camera");
Check(Math.Abs(Vector3.Distance(c.Eye,c.Pivot)-Vector3.Distance(moved.Eye,moved.Pivot))<.0001,"Orbit preserves pivot radius");
var p0=Vector3.Normalize(c.Target-c.Eye);var p1=Vector3.Normalize(moved.Target-moved.Eye);
Check(Vector3.Distance(p0,p1)>.01,"Orientation changes with orbit");
var onlyRoll=CameraMath.Move(c,NavMode.Orbit,0,0,100);
Check(Vector3.Distance(c.Up,onlyRoll.Up)>.01,"Independent wheel roll changes up vector");
Check(Vector3.Distance(c.Up,CameraMath.Move(c,NavMode.Roll,30,0).Up)>.01,"Dedicated roll mode uses horizontal ball motion");
var pan=CameraMath.Move(c,NavMode.PanZoom,40,10,120);
Check(Vector3.Distance(pan.Eye,c.Eye)>.1&&Vector3.Distance(pan.Target,c.Target)>.01,"Pan and zoom coexist in one update");
var ortho=CameraMath.Move(c with{Orthographic=true},NavMode.PanZoom,40,10,120);
Check(ortho.OrthoHeight<10,"Orthographic wheel zoom changes view extent");
var near=CameraMath.Move(c with{Eye=new(0,0,10),Target=Vector3.Zero,Pivot=Vector3.Zero,Up=Vector3.UnitY},NavMode.PanZoom,10,0);
var far=CameraMath.Move(c with{Eye=new(0,0,100),Target=Vector3.Zero,Pivot=Vector3.Zero,Up=Vector3.UnitY},NavMode.PanZoom,10,0);
Check(Math.Abs(far.Target.X/near.Target.X-10)<.001,"Pan scales with real scene depth");
var snap=CameraMath.Move(c,NavMode.Snap,120,30);
var dir=Vector3.Normalize(snap.Eye-snap.Pivot);var components=new[]{Math.Abs(dir.X),Math.Abs(dir.Y),Math.Abs(dir.Z)}.Where(x=>x>.001).ToArray();
Check(components.Max()-components.Min()<.001,"Snap aligns to face/edge/corner direction");
Vector2 ProjectPivot(CameraPose camera){
 var b=Vector3.Normalize(camera.Eye-camera.Target);var r=Vector3.Normalize(Vector3.Cross(camera.Up,b));var u=Vector3.Normalize(Vector3.Cross(b,r));var v=camera.Pivot-camera.Eye;
 return new(Vector3.Dot(v,r)/-Vector3.Dot(v,b),Vector3.Dot(v,u)/-Vector3.Dot(v,b));
}
var offCenter=c with{Pivot=new Vector3(3,-1,2)};
var offSnap=CameraMath.Move(offCenter,NavMode.Snap,95,-30);
Check(Vector2.Distance(ProjectPivot(offCenter),ProjectPivot(offSnap))<.0001,"Off-center snapping keeps the pivot under the cursor");
Check(Math.Abs(Vector3.Distance(offCenter.Eye,offCenter.Pivot)-Vector3.Distance(offSnap.Eye,offSnap.Pivot))<.0001,"Off-center snapping preserves the pivot radius");
var fly=CameraMath.Move(c,NavMode.Navigate,0,20,0,true);
Check(Vector3.Distance(fly.Eye,c.Eye)>.01&&fly.Valid,"Alt navigation applies height movement");
bool invalid=false;try{CameraMath.Move(c with{Pivot=new(float.NaN,0,0)},NavMode.Orbit,1,2);}catch(ArgumentException){invalid=true;}Check(invalid,"Reject non-finite camera data");
var combo=new ComboTracker();var list=new JsonArray(new JsonObject{["buttons"]=new JsonArray(1,2),["bind"]=new JsonObject{["action"]="fit"}},new JsonObject{["buttons"]=new JsonArray(),["bind"]=new JsonObject{["action"]="normal"}});
Check(combo.Change(1,true,list)==null,"First button does not fire combo");
Check(ConfigStore.S(combo.Change(2,true,list),"action")=="fit","Complete combination fires");
Check(combo.Change(2,true,list)==null,"Repeat cannot retrigger combination");
combo.Change(2,false,list);Check(combo.Change(2,true,list)!=null,"Combination can fire after release");
Check(ActionPolicy.Global("keys")&&ActionPolicy.Global("macro"),"Configured shortcuts and macros work outside CAD");
Check(!ActionPolicy.Global("orbit")&&!ActionPolicy.Global("fit"),"Navigation actions remain scoped to CAD");
var unassigned=new JsonArray(new JsonObject{["buttons"]=new JsonArray(1,2),["bind"]=new JsonObject{["action"]="normal"}});
var draftTracker=new ComboTracker();draftTracker.Change(1,true,unassigned);Check(draftTracker.Change(2,true,unassigned)==null,"Unassigned combination leaves normal buttons alone");
string dirPath=Path.Combine(Path.GetTempPath(),"chamfer-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dirPath);
try{
 var path=Path.Combine(dirPath,"settings.json");var store=new ConfigStore(path);var s=store.Snapshot();
 s["combos"]=new JsonArray(new JsonObject{["buttons"]=new JsonArray(),["bind"]=new JsonObject{["action"]="keys",["keys"]="Ctrl+S"}});
 store.Save(s);var reopened=new ConfigStore(path).Snapshot();
 Check(reopened["combos"]!.AsArray().Count==1&&reopened["combos"]![0]!["buttons"]!.AsArray().Count==0,"Incomplete combination persists through restart");
 s=reopened;s["combos"]=list.DeepClone();store.Save(s);
 Check(new ConfigStore(path).Snapshot()["combos"]!.AsArray().Count==2,"Complete combinations persist");
 store.Select(0);Check(ConfigStore.S(store.Snapshot(),"orbitKeys")=="none","Normal mouse has no navigation");
 store.Select(1);Check(ConfigStore.S(store.Snapshot(),"orbitKeys")=="shift","Program switch restores held keys");
 var preferences=store.Snapshot();preferences["orbitSens"]=73;preferences["programs"]![1]!["gestures"]!["up"]=new JsonObject{["type"]="keys",["keys"]="F"};
 store.Save(preferences);store.Select(0);store.Select(1);
 Check(ConfigStore.I(store.Snapshot(),"orbitSens")==73,"Navigation speed survives program switches");
 Check(ConfigStore.S(store.Snapshot()["programs"]![1]!["gestures"]!["up"],"keys")=="F","Gesture edits survive saving");
 var dialSettings=store.Snapshot();dialSettings["circleZoom"]=true;dialSettings["circleSize"]=210;
 dialSettings["orbitKeys"]="Alt+Q";dialSettings["panKeys"]="Ctrl+Space";dialSettings["zoomKeys"]="Shift+Z";
 store.Save(dialSettings);store.Select(0);
 Check(!ConfigStore.B(store.Snapshot(),"circleZoom"),"Normal program does not inherit circle zoom");
 store.Select(1);var custom=new ConfigStore(path).Snapshot();
 Check(ConfigStore.B(custom,"circleZoom")&&ConfigStore.I(custom,"circleSize")==210,"Circle settings survive program switches and restart");
 Check(ModeResolver.Resolve(custom,new HashSet<int>{18,81})==NavMode.Orbit,"Custom orbit chord is used");
 Check(ModeResolver.Resolve(custom,new HashSet<int>{17,32})==NavMode.PanZoom,"Custom pan chord is used");
 Check(ModeResolver.Resolve(custom,new HashSet<int>{16,90})==NavMode.Zoom,"Custom zoom chord is used");
 custom["trackballSwapButtons"]=true;custom["trackballSwapWheels"]=true;store.Save(custom);
 var reset=store.Reset();
 Check(ConfigStore.I(reset,"activeProgram")==0&&ModeResolver.Resolve(reset,new HashSet<int>{16})==NavMode.Idle,"Reset selects normal mouse without navigation");
 Check(!ConfigStore.B(reset,"trackballSwapButtons")&&!ConfigStore.B(reset,"trackballSwapWheels"),"Reset disables device corrections as well as bindings");Check(ConfigStore.S(reset,"leftWheel")=="normal"&&ConfigStore.S(reset,"rightWheel")=="normal","Reset restores both wheels");
 Check(reset["programs"]![0]!["buttons"]!.AsObject().All(b=>ConfigStore.S(b.Value,"action")=="normal")&&reset["programs"]![1]!["buttons"]!.AsObject().Where(b=>b.Key!="back").All(b=>ConfigStore.S(b.Value,"action")=="normal")&&ConfigStore.S(reset["programs"]![1]!["buttons"]!["back"],"action")=="panToggle","Reset restores normal mouse clicks and the 3D Back toggle");
 var bad=store.Snapshot();bad["orbitKeys"]="ctrl";bool rejected=false;try{store.Save(bad);}catch(InvalidDataException){rejected=true;}Check(rejected,"Saving Ctrl-only navigation is rejected");
 var migrated=ConfigStore.Defaults();migrated["configVersion"]=20;migrated["activeProgram"]=1;migrated["programs"]![1]!["orbitKeys"]="ctrl";File.WriteAllText(path,migrated.ToJsonString());var ms=new ConfigStore(path).Snapshot();
 Check(ConfigStore.S(ms,"orbitKeys")=="shift"&&ConfigStore.I(ms,"configVersion")==23,"Legacy config migrates without Ctrl-only orbit");
}finally{Directory.Delete(dirPath,true);}

var dm=new DeviceMapping();var leftDown=new MouseEdge(0x201);var rightDown=new MouseEdge(0x204);
Check(dm.Map("mouse","ball",leftDown,true,true)==leftDown,"Other mouse keeps left click");
Check(dm.Map("mouse","ball",new(0x202),true,true)==new MouseEdge(0x202),"Other mouse keeps left release");
Check(dm.Map("ball","ball",rightDown,true,true)==leftDown,"Optional trackball right-to-left correction");
Check(dm.Map("ball","other",new(0x205),false,false)==new MouseEdge(0x202),"Mapped release remains paired across settings/device changes");
Check(dm.Map("ball","ball",leftDown,true,true)==rightDown,"Optional trackball left-to-right correction");
Check(dm.Map("ball","ball",new(0x202),true,true)==new MouseEdge(0x205),"Right click receives matching release");
foreach(var delta in new[]{-240,-120,60,120,240}){
 Check(dm.Map("ball","ball",new(0x20a,delta),true,true)==new MouseEdge(0x20e,delta),"Trackball vertical-to-horizontal axis retains direction "+delta);
 Check(dm.Map("ball","ball",new(0x20e,delta),true,true)==new MouseEdge(0x20a,delta),"Trackball horizontal-to-vertical axis retains direction "+delta);
 Check(dm.Map("mouse","ball",new(0x20a,delta),true,true)==new MouseEdge(0x20a,delta),"Ordinary mouse wheel unchanged "+delta);
}
Check(dm.Map("ball","",leftDown,true,true)==leftDown,"Unidentified devices are never guessed");
dm.ReleaseAll();
dm.Map("ball","ball",rightDown,true,false);Check(dm.ReleaseAll().Single()==new MouseEdge(0x202)&&dm.HeldCount==0,"Disconnect clears held mapped buttons");
var gate=new MouseGate();
Check(gate.Hook(leftDown,10,true)==null&&gate.Count==1,"A withheld hook waits for device identity");
Check(gate.Raw("ball",new(0x20e,-120),11)==null&&gate.Count==1,"A different wheel axis cannot consume a physical event");
var hookFirst=gate.Raw("ball",leftDown,12);
Check(hookFirst==new MouseMatch("ball",leftDown,true)&&gate.Count==0,"Hook-first input routes exactly once");
Check(gate.Raw("ball",leftDown,13)==null,"A duplicate raw observation cannot replay a click");
Check(!gate.Expired(200)&&gate.Hook(leftDown,201,false)==null,"Stale raw input cannot match a later hook");
gate.Drain();
Check(gate.Raw("ball",rightDown,300)==null,"Raw-first input waits for its hook");
Check(gate.Hook(rightDown,301,true)==new MouseMatch("ball",rightDown,true),"Raw-first input retains its device and is replayed once");
Check(gate.Raw("mouse",leftDown,302)==null&&gate.Hook(leftDown,303,false)==new MouseMatch("mouse",leftDown,false),"Raw-first pass-through input is never duplicated");
Check(gate.Hook(new(0x202),304,false)==null&&gate.Raw("mouse",new(0x202),305)==new MouseMatch("mouse",new(0x202),false),"Hook-first pass-through release is never duplicated");
foreach(var edge in new[]{leftDown,new MouseEdge(0x202),rightDown,new MouseEdge(0x205),new MouseEdge(0x20b,1),new MouseEdge(0x20c,1),new MouseEdge(0x20a,120),new MouseEdge(0x20e,-120)}){
 var paired=new MouseGate();paired.Raw("ball",edge,500);
 Check(paired.Hook(edge,501,true)==new MouseMatch("ball",edge,true),"Raw-first button/wheel preserves message and data "+edge.Message);
}
gate.Hook(new(0x20a,-120),600,true);
Check(!gate.Expired(750)&&gate.Expired(752),"Truly unmatched blocked input still fails open");
Check(gate.Drain().Single()==new MouseEdge(0x20a,-120)&&gate.Count==0,"Fail-open replays only withheld physical input unchanged");
var health=new MotionHealth();
health.Raw();health.Block(1000);
Check(!health.Expired(2000),"Stopping after a raw-first movement is healthy");
health.Reset();health.Block(2000);health.Raw();
Check(!health.Expired(3000),"Stopping after a hook-first movement is healthy");
for(int i=0;i<200;i++){health.Raw();health.Block(3000+i*8);}
Check(!health.Expired(6000),"Repeated raw-first motion followed by idle cannot disable navigation");
health.Reset();health.Block(7000);health.Block(7510);
Check(health.Expired(7520),"Sustained blocked movement without Raw Input fails open");
health.Raw();Check(!health.Expired(7530),"A fresh raw movement clears the missing-input interval");
health.Block(8000);health.Reset();Check(!health.Expired(9000),"Leaving capture clears the motion watchdog");
JsonObject Rel(string tag,bool draft=false,bool preview=false)=>new(){["tag_name"]=tag,["draft"]=draft,["prerelease"]=preview};
var releases=new JsonArray(Rel("v99.0.0"),Rel("admin-v1.5.5-build.2",preview:true),Rel("admin-v1.5.5-build.3",preview:true),Rel("public-v1.0.0"),Rel("public-v1.0.1",draft:true),Rel("candidate-v1.0.1-build.3",draft:true));
Check(ReleasePolicy.Latest(releases,false,0,"1.0.0")==null,"Public updater ignores admin previews, candidates and drafts");
Check(ReleasePolicy.Latest(releases,true,2,"1.5.5")==null,"New updater ignores legacy build-number tags");
releases.Add(Rel("admin-v1.5.7",preview:true));releases.Add(Rel("admin-v1.5.9",draft:true));releases.Add(Rel("admin-v1.5.8-build.999",preview:true));
Check(ConfigStore.S(ReleasePolicy.Latest(releases,true,17,"1.5.5"),"tag_name")=="admin-v1.5.7","Admin migration selects the new application version");
Check(ReleasePolicy.Latest(releases,true,0,"1.5.7")==null,"Same version and build aliases cannot trigger a reinstall");
Check(ReleasePolicy.Latest(releases,true,0,"1.6.0")==null,"Admin update never downgrades the installed version");
releases.Add(Rel("admin-v1.5.10",preview:true));releases.Add(Rel("admin-v1.5.8",preview:true));
Check(ConfigStore.S(ReleasePolicy.Latest(releases,true,9999,"1.5.7"),"tag_name")=="admin-v1.5.10","Semantic patch comparison handles 10 after 8 regardless of internal build");
Check(ReleasePolicy.VersionText("admin-v1.5.10",true)=="1.5.10","Update notifications display only the application version");
Check(ReleasePolicy.VersionText("admin-v1.5.10-build.19",true)=="","Internal build alias is not shown as an update");
Check(ReleasePolicy.Latest(releases,true,0,"invalid")==null,"Invalid installed version cannot select an update");
Check(ReleasePolicy.Latest(releases,false,0,"1.0.0")==null,"Public channel still ignores Admin semantic versions");
releases.Add(Rel("public-v1.0.1"));
Check(ConfigStore.S(ReleasePolicy.Latest(releases,false,0,"1.0.0"),"tag_name")=="public-v1.0.1","Public updater offers explicitly published public versions");
Check(BallNavigation.ToggleOrbitPan(NavMode.Idle,NavMode.Orbit)==NavMode.PanZoom,"First back tap switches free orbit to pan");
Check(BallNavigation.ToggleOrbitPan(NavMode.PanZoom,NavMode.Orbit)==NavMode.Orbit,"Second back tap returns to orbit");
Check(BallNavigation.ToggleOrbitPan(NavMode.Orbit,NavMode.Orbit)==NavMode.PanZoom,"Orbit/pan toggle never inserts a zoom mode");
(float Wheel,int Captured) Trace(CircleZoom detector,Func<float,Vector2> shape,float end,int samples,long start=1000,float size=160){
 var previous=shape(0);float wheel=0;int captured=0;
 for(int i=1;i<=samples;i++){var at=shape(end*i/samples);var d=at-previous;previous=at;var step=detector.Add(d.X,d.Y,start+i*5,size);wheel+=step.Wheel;if(step.Consumed)captured++;}
 return (wheel,captured);
}
var clockwise=new CircleZoom();
var cw=Trace(clockwise,a=>new(30*MathF.Cos(a),30*MathF.Sin(a)),MathF.PI*6,540);
Check(cw.Captured>200&&cw.Wheel>500,"Clockwise circles continuously zoom in after deliberate recognition");
var ccw=Trace(new CircleZoom(),a=>new(30*MathF.Cos(a),-30*MathF.Sin(a)),MathF.PI*6,540);
Check(ccw.Captured>200&&ccw.Wheel< -500,"Anticlockwise circles continuously zoom out");
var line=Trace(new CircleZoom(),a=>new(a*10,0),30,300);
Check(line.Captured==0&&line.Wheel==0,"Straight rolling never zooms");
var curve=Trace(new CircleZoom(),a=>new(30*MathF.Cos(a),30*MathF.Sin(a)),MathF.PI,90);
Check(curve.Captured==0,"A curved half-turn orbit is not a zoom");
var broad=Trace(new CircleZoom(),a=>new(300*MathF.Cos(a),300*MathF.Sin(a)),MathF.PI*4,1500);
Check(broad.Captured==0,"Large circular orbits stay outside the configured gesture size");
var small=Trace(new CircleZoom(),a=>new(12*MathF.Cos(a),12*MathF.Sin(a)),MathF.PI*6,540);
Check(small.Wheel>300,"Small dial circles are recognized");
var firstLoop=Trace(new CircleZoom(),a=>new(8*MathF.Cos(a),8*MathF.Sin(a)),MathF.Tau,240);
Check(firstLoop.Wheel>250,"One small circle produces visible zoom when first recognized");
var slowLoop=new CircleZoom();var previousLoop=new Vector2(11,0);float slowWheel=0;
for(int i=1;i<=360;i++){
 var angle=MathF.Tau*i/360;var at=new Vector2((11+2*MathF.Sin(3*angle))*MathF.Cos(angle),8*MathF.Sin(angle));var delta=at-previousLoop;previousLoop=at;
 var result=slowLoop.Add(delta.X,delta.Y,1000+i*5+(i>180?450:0));slowWheel+=result.Wheel;
}
Check(slowLoop.Active&&slowWheel>250,"Imperfect small circles survive a brief pause and zoom on the first loop");
var framedCircle=new CircleZoom();Vector2 frameMove=Vector2.Zero,lastPosition=new(18,0);float framedWheel=0;int recognitionCalls=0;
for(int ms=1;ms<=3200;ms++){
 var angle=ms*MathF.Tau/1600;var at=new Vector2(MathF.Round(18*MathF.Cos(angle)),MathF.Round(14*MathF.Sin(angle)));
 frameMove+=at-lastPosition;lastPosition=at;
 if(ms%16==0){var result=framedCircle.Add(frameMove.X,frameMove.Y,1000+ms);framedWheel+=result.Wheel;recognitionCalls++;frameMove=Vector2.Zero;}
}
Check(recognitionCalls==200&&framedWheel>500,"Quantized 1000 Hz sensor input still recognizes circle zoom at 16 ms frames");
var longRoll=new CircleZoom();for(int i=0;i<30000;i++)longRoll.Add(30,0,1000+i*5);
Check(!longRoll.Active,"A long continuous straight roll stays bounded and never activates zoom");
var figureEight=Trace(new CircleZoom(),a=>new(35*MathF.Sin(a),20*MathF.Sin(2*a)),MathF.PI*6,800);
Check(figureEight.Captured==0,"Alternating figure-eight curvature does not activate dial zoom");
clockwise.Add(6,0,3705);clockwise.Add(6,0,3710);clockwise.Add(6,0,3715);clockwise.Add(6,0,3720);clockwise.Add(6,0,3725);
Check(!clockwise.Active,"Straight rolling returns from zoom to orbit/pan");
Trace(clockwise,a=>new(30*MathF.Cos(a),30*MathF.Sin(a)),MathF.PI*4,360,4000);
Check(clockwise.Active,"Continuous circles can re-enter zoom");
Check(!clockwise.Add(3,0,7000).Consumed&&!clockwise.Active,"A pause clears the previous circle");
var jitterExit=new CircleZoom();Trace(jitterExit,a=>new(30*MathF.Cos(a),30*MathF.Sin(a)),MathF.PI*6,540);
for(int i=1;i<=20;i++)jitterExit.Add(6,i%2==0?1:-1,3700+i*5);
Check(!jitterExit.Active,"Slightly uneven straight rolling also leaves zoom");
var largerDial=Trace(new CircleZoom(),a=>new(75*MathF.Cos(a),75*MathF.Sin(a)),MathF.PI*6,1500);
Check(largerDial.Wheel>300,"Larger valid circles are not mistaken for straight movement");
clockwise.Add(float.NaN,1,7010);Check(!clockwise.Active,"Invalid movement cannot produce a zoom");
Check(PublicVersion.Normalize(" v3.2.1 ")=="3.2.1","Owner chooses any canonical public version");
Check(PublicVersion.Normalize("12.34.56")=="12.34.56","Public version is not restricted to prepared candidates");
foreach(var badVersion in new[]{"1.2","01.2.3","1.2.3-beta","1.2.65535","1.2.3.4"}){
 bool invalidVersion=false;try{PublicVersion.Normalize(badVersion);}catch(ArgumentException){invalidVersion=true;}
 Check(invalidVersion,"Reject invalid executable version "+badVersion);
}

var sensorFrames=new MotionFrames();float totalX=0,totalY=0,totalWheel=0,totalDial=0;int deliveredFrames=0;
void DeliverFrame(){var frame=sensorFrames.Peek();if(frame.HasPrimary||frame.HasDial){totalX+=frame.X;totalY+=frame.Y;totalWheel+=frame.Wheel;totalDial+=frame.Dial;deliveredFrames++;}sensorFrames.Reset();}
for(int i=1;i<=1000;i++){sensorFrames.Move(1,-2);if(i%125==0)sensorFrames.Wheel(120);if(i%100==0)sensorFrames.Dial(5);if(i%16==0)DeliverFrame();}DeliverFrame();
Check(deliveredFrames<=64&&totalX==1000&&totalY==-2000&&totalWheel==960&&totalDial==50,"1000 Hz input becomes display-rate frames without lost motion, wheel or dial zoom");
Check(!sensorFrames.Peek().HasPrimary&&!sensorFrames.Peek().HasDial,"Idle frames do not resend old movement");
sensorFrames.Move(20,10);sensorFrames.Dial(7);sensorFrames.ClearPrimary();Check(!sensorFrames.Peek().HasPrimary&&sensorFrames.Peek().Dial==7,"A delayed zoom keeps its own pending sample");sensorFrames.Reset();
sensorFrames.Move(12,-6);sensorFrames.Wheel(120);sensorFrames.Dial(9);sensorFrames.ClearMove();
var preservedWheel=sensorFrames.Peek();Check(preservedWheel.X==0&&preservedWheel.Y==0&&preservedWheel.Wheel==120&&preservedWheel.Dial==9,"Circle detection consumes ball movement while preserving real wheel input");sensorFrames.Reset();
var fractions=new FractionalInput();int fractionalX=0,fractionalY=0,fractionalWheel=0;
for(int i=0;i<100;i++){var whole=fractions.Add(.1f,-.2f,.3f);fractionalX+=whole.X;fractionalY+=whole.Y;fractionalWheel+=whole.Wheel;}
Check(fractionalX==10&&fractionalY==-20&&fractionalWheel==30,"Fine emulated motion and zoom survive fractional sensitivity");
fractions.Reset();Check(fractions.Add(0,0,0)==(0,0,0),"Ending navigation clears fractional output");
Check(Math.Abs(MotionPolicy.Gain(50)-1)<.0001&&Math.Abs(MotionPolicy.Gain(30)-.5f)<.0001&&Math.Abs(MotionPolicy.Gain(70)-2)<.0001,"Sensitivity matches the speed shown in the interface");
var scaledPan=MotionPolicy.Scale(NavMode.PanZoom,20,-10,120,1,2,.25f);
Check(scaledPan==(40,-20,30),"Pan speed and simultaneous zoom speed are independent");
var scaledZoom=MotionPolicy.Scale(NavMode.Zoom,0,-20,120,1,2,.5f);
Check(Math.Abs(MotionPolicy.ZoomAmount(NavMode.Zoom,scaledZoom.Y,scaledZoom.Wheel)-.17f)<.00001,"Zoom sensitivity scales both ball and wheel zoom");
Check(MotionPolicy.Circle(NavMode.PanZoom,true,true),"Ctrl+Shift or a held pan button still permits ball-circle zoom");
Check(!MotionPolicy.Circle(NavMode.Orbit,true,true),"Held orbit circles remain deliberate rotation");
Check(MotionPolicy.Circle(NavMode.Orbit,true,false)&&!MotionPolicy.Circle(NavMode.PanZoom,false,true),"Free-ball circle zoom respects its saved enable switch");
Check(MotionPolicy.WhilePaused(NavMode.Orbit,true,false)==NavMode.Idle,"A click can still restore normal pointing");
Check(MotionPolicy.ResumeAfterClick(true,NavMode.Orbit,false,true),"Ball-only orbit resumes on the next trackball movement after a click");
Check(!MotionPolicy.ResumeAfterClick(true,NavMode.Orbit,false,false)&&!MotionPolicy.ResumeAfterClick(true,NavMode.Idle,false,true)&&!MotionPolicy.ResumeAfterClick(true,NavMode.Orbit,true,true),"Click pause resumes only on 3D ball movement without a held mode");
Check(MotionPolicy.WhilePaused(NavMode.Orbit,true,true)==NavMode.Orbit&&MotionPolicy.WhilePaused(NavMode.Snap,true,true)==NavMode.Snap,"Held commands work after returning to the pointer");


var panAnchor=CameraMath.Move(offCenter,NavMode.PanZoom,70,-25,120);
Check(Vector2.Distance(ProjectPivot(offCenter),ProjectPivot(panAnchor))<.0001,"Simultaneous pan/zoom keeps the moving cursor pivot stable");
Vector2 OrthoPivot(CameraPose pose){var b=Vector3.Normalize(pose.Eye-pose.Target);var r=Vector3.Normalize(Vector3.Cross(pose.Up,b));var u=Vector3.Normalize(Vector3.Cross(b,r));var offset=pose.Pivot-pose.Target;return new(Vector3.Dot(offset,r)/pose.OrthoHeight,Vector3.Dot(offset,u)/pose.OrthoHeight);}
var offOrtho=offCenter with{Orthographic=true};
var orthoZoom=CameraMath.Move(offOrtho,NavMode.Zoom,0,-30,120);
Check(Vector2.Distance(OrthoPivot(offOrtho),OrthoPivot(orthoZoom))<.0001,"Orthographic zoom keeps an off-center cursor pivot in place");
Check(Math.Abs(Vector3.Distance(offOrtho.Eye,offOrtho.Target)-Vector3.Distance(orthoZoom.Eye,orthoZoom.Target))<.0001,"Orthographic zoom preserves camera depth");
var closeUp=c;for(int i=0;i<1000;i++)closeUp=CameraMath.Move(closeUp,NavMode.Zoom,0,-40,120);
Check(closeUp.Valid&&Vector3.Distance(closeUp.Eye,closeUp.Target)>=.00009f,"Repeated close-up zoom cannot collapse the camera or disable navigation");

Console.WriteLine(count+" regression checks passed.");
