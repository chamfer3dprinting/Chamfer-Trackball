using System.Numerics;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
int count=0;void Check(bool pass,string name){count++;if(!pass)throw new Exception("FAILED: "+name);Console.WriteLine("PASS "+name);}
var defaults=ConfigStore.Defaults();
Check(ConfigStore.I(defaults,"activeProgram")==0&&ModeResolver.Resolve(defaults,new HashSet<int>{16})==NavMode.Idle,"Factory startup is an ordinary mouse");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{17})==NavMode.Idle,"Normal mouse keeps Ctrl as an ordinary modifier");
ConfigStore.LoadProgram(defaults,1);
Check(ConfigStore.S(defaults,"ball")=="pointer"&&ModeResolver.Resolve(defaults,new HashSet<int>())==NavMode.Idle,"The 3D program points normally until a navigation key is held");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{17})==NavMode.Zoom,"Ctrl and roll selects Zoom in the 3D program");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{17,67})==NavMode.Idle&&ModeResolver.Resolve(defaults,new HashSet<int>{17,86})==NavMode.Idle,"Ctrl+C and Ctrl+V remain ordinary keyboard shortcuts");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{16})==NavMode.Orbit,"Shift orbits");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{16,17})==NavMode.PanZoom,"Ctrl+Shift wins over Shift");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{18})==NavMode.Snap,"Alt snaps");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{91})==NavMode.Navigate,"Windows navigates");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{91,18})==NavMode.Navigate,"Alt modifies navigation height");
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
var dir=Vector3.Normalize(snap.Eye-snap.Target);var components=new[]{Math.Abs(dir.X),Math.Abs(dir.Y),Math.Abs(dir.Z)}.Where(x=>x>.001).ToArray();
Check(components.Length==1&&Math.Abs(components[0]-1)<.001,"Snap aligns to one cube face without an edge or corner");
bool IsFace(CameraPose view){
 var direction=Vector3.Normalize(view.Eye-view.Target);var values=new[]{Math.Abs(direction.X),Math.Abs(direction.Y),Math.Abs(direction.Z)};
 return view.Valid&&values.Count(value=>value>.001)==1&&values.Max()>.999f;
}
foreach(var (name,axis) in new[]{("Right",Vector3.UnitX),("Left",-Vector3.UnitX),("Back",Vector3.UnitY),("Front",-Vector3.UnitY),("Top",Vector3.UnitZ),("Bottom",-Vector3.UnitZ)}){
 var face=c with{Eye=c.Target+axis*10,Up=Math.Abs(axis.Z)>.9?Vector3.UnitY:Vector3.UnitZ};
 var aligned=CameraMath.Move(face,NavMode.Snap,0,0);
 Check(IsFace(aligned)&&Vector3.Dot(Vector3.Normalize(aligned.Eye-aligned.Target),axis)>.999f,"Snap can reach the "+name+" face");
}
bool faceOnly=true;
for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++){
 if(x==0&&y==0&&z==0)continue;var axis=Vector3.Normalize(new Vector3(x,y,z));
 var initial=c with{Eye=c.Target+axis*10,Up=Math.Abs(axis.Z)>.9?Vector3.UnitY:Vector3.UnitZ};
 foreach(var orthographic in new[]{false,true})faceOnly&=IsFace(CameraMath.Move(initial with{Orthographic=orthographic},NavMode.Snap,0,0));
}
Check(faceOnly,"Diagonal starting views snap only to faces in perspective and orthographic cameras");
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
 store.IdentifyDevice("trackball-device-a");
 s["combos"]=new JsonArray(new JsonObject{["buttons"]=new JsonArray(),["bind"]=new JsonObject{["action"]="keys",["keys"]="Ctrl+S"}});
 store.Save(s);var reopened=new ConfigStore(path).Snapshot();
 Check(reopened["combos"]!.AsArray().Count==1&&reopened["combos"]![0]!["buttons"]!.AsArray().Count==0,"Incomplete combination persists through restart");
 Check(ConfigStore.S(reopened,"selectedDevice")=="trackball-device-a","A stale settings save cannot erase the newly identified trackball");
 store.IdentifyDevice("trackball-device-b");store.Save(reopened);
 Check(ConfigStore.S(store.Snapshot(),"selectedDevice")=="trackball-device-b","An older page cannot restore a previous trackball identity");
 s=reopened;s["combos"]=list.DeepClone();store.Save(s);
 Check(new ConfigStore(path).Snapshot()["combos"]!.AsArray().Count==2,"Complete combinations persist");
 store.Select(0);Check(ConfigStore.S(store.Snapshot(),"orbitKeys")=="none","Normal mouse has no navigation");
 store.Select(1);Check(ConfigStore.S(store.Snapshot(),"orbitKeys")=="shift","Program switch restores held keys");
 var preferences=store.Snapshot();preferences["orbitSens"]=73;preferences["programs"]![1]!["gestures"]!["up"]=new JsonObject{["type"]="keys",["keys"]="F"};
 store.Save(preferences);store.Select(0);store.Select(1);
 Check(ConfigStore.I(store.Snapshot(),"orbitSens")==73,"Navigation speed survives program switches");
 Check(ConfigStore.S(store.Snapshot()["programs"]![1]!["gestures"]!["up"],"keys")=="F","Gesture edits survive saving");

 var programFeel=store.Snapshot();programFeel["pointerSpeed"]=16;programFeel["glide"]=77;programFeel["smooth"]=false;programFeel["panSens"]=68;programFeel["zoomSens"]=28;
 programFeel["snapKeys"]="Alt+S";programFeel["navigateKeys"]="win";store.Save(programFeel);
 store.Select(0);var normalFeel=store.Snapshot();normalFeel["pointerSpeed"]=7;normalFeel["glide"]=9;store.Save(normalFeel);store.Select(1);
 var restoredFeel=new ConfigStore(path).Snapshot();
 Check(ConfigStore.I(restoredFeel,"pointerSpeed")==16&&ConfigStore.I(restoredFeel,"glide")==77&&!ConfigStore.B(restoredFeel,"smooth"),"Pointer speed and glide switch together and survive restart");
 Check(ConfigStore.I(restoredFeel,"panSens")==68&&ConfigStore.I(restoredFeel,"zoomSens")==28&&ConfigStore.S(restoredFeel,"snapKeys")=="Alt+S"&&ConfigStore.S(restoredFeel,"navigateKeys")=="win","Each program retains independent 3D speeds and all held controls");
 store.Select(0);Check(ConfigStore.I(store.Snapshot(),"pointerSpeed")==7&&ConfigStore.I(store.Snapshot(),"glide")==9,"Normal mouse retains its own pointer and glide choices");
 store.Select(1);

 var dialSettings=store.Snapshot();dialSettings["circleZoom"]=true;dialSettings["circleSize"]=210;
 dialSettings["orbitKeys"]="Alt+Q";dialSettings["panKeys"]="Ctrl+Space";dialSettings["zoomKeys"]="Shift+Z";
 store.Save(dialSettings);store.Select(0);
 Check(!ConfigStore.B(store.Snapshot(),"circleZoom"),"Normal program does not inherit circle zoom");
 store.Select(1);var custom=new ConfigStore(path).Snapshot();
 Check(ConfigStore.B(custom,"circleZoom")&&ConfigStore.I(custom,"circleSize")==210,"Circle settings survive program switches and restart");
 Check(ConfigStore.S(custom,"selectedDevice")=="trackball-device-b","Trackball identity survives program switches and restart");
 Check(ModeResolver.Resolve(custom,new HashSet<int>{18,81})==NavMode.Orbit,"Custom orbit chord is used");
 Check(ModeResolver.Resolve(custom,new HashSet<int>{17,32})==NavMode.PanZoom,"Custom pan chord is used");
 Check(ModeResolver.Resolve(custom,new HashSet<int>{16,90})==NavMode.Zoom,"Custom zoom chord is used");
 custom["trackballSwapButtons"]=true;custom["trackballSwapWheels"]=true;store.Save(custom);
 var reset=store.Reset();
 Check(ConfigStore.S(reset,"selectedDevice")=="trackball-device-b","Restoring normal controls keeps the identified trackball");
 bool invalidIdentity=false;try{store.IdentifyDevice("");}catch(ArgumentException){invalidIdentity=true;}
 Check(invalidIdentity&&ConfigStore.S(store.Snapshot(),"selectedDevice")=="trackball-device-b","Invalid identification cannot replace the saved trackball");
 Check(ConfigStore.I(reset,"activeProgram")==0&&ModeResolver.Resolve(reset,new HashSet<int>{16})==NavMode.Idle,"Reset selects normal mouse without navigation");
 Check(!ConfigStore.B(reset,"trackballSwapButtons")&&!ConfigStore.B(reset,"trackballSwapWheels"),"Reset disables device corrections as well as bindings");Check(ConfigStore.S(reset,"leftWheel")=="normal"&&ConfigStore.S(reset,"rightWheel")=="normal","Reset restores both wheels");
 Check(reset["programs"]![0]!["buttons"]!.AsObject().All(b=>ConfigStore.S(b.Value,"action")=="normal")&&reset["programs"]![1]!["buttons"]!.AsObject().Where(b=>b.Key!="back").All(b=>ConfigStore.S(b.Value,"action")=="normal")&&ConfigStore.S(reset["programs"]![1]!["buttons"]!["back"],"action")=="panToggle","Reset restores normal mouse clicks and the 3D Back toggle");
 var bad=store.Snapshot();bad["orbitKeys"]="ctrl";bool rejected=false;try{store.Save(bad);}catch(InvalidDataException){rejected=true;}Check(rejected,"Saving Ctrl-only navigation is rejected");

 var partial=store.Snapshot();partial["programs"]!.AsArray().Add(new JsonObject{["name"]="New legacy program",["feel"]=new JsonObject{["orbitSens"]=82}});
 store.Save(partial);store.Select(2);var repaired=store.Snapshot();
 Check(ConfigStore.I(repaired,"orbitSens")==82&&ConfigStore.I(repaired,"panSens")==50&&ConfigStore.I(repaired,"pointerSpeed")==10,"An incomplete added program gets its own full motion defaults instead of leaking the previous program");
 store.Select(0);
 var migrated=ConfigStore.Defaults();migrated["configVersion"]=20;migrated["activeProgram"]=1;migrated["programs"]![1]!["orbitKeys"]="ctrl";File.WriteAllText(path,migrated.ToJsonString());var ms=new ConfigStore(path).Snapshot();
 Check(ConfigStore.S(ms,"orbitKeys")=="shift"&&ConfigStore.I(ms,"configVersion")==25,"Legacy config migrates without Ctrl-only orbit");
 Check(ConfigStore.S(ms,"ball")=="pointer"&&ConfigStore.S(ms,"zoomKeys")=="ctrl","Legacy 3D defaults migrate to held navigation and Ctrl zoom");
 foreach(var zoomBinding in new[]{"none","z","Shift+Z"}){
  var old=ConfigStore.Defaults();old["configVersion"]=23;old["activeProgram"]=1;old["selectedDevice"]="retained-trackball";
  old["programs"]![1]!["ball"]="orbit";old["programs"]![1]!["zoomKeys"]=zoomBinding;
  old["programs"]![1]!["orbitKeys"]="Alt+Q";old["programs"]![1]!["panKeys"]="Ctrl+Space";
  File.WriteAllText(path,old.ToJsonString());var upgraded=new ConfigStore(path).Snapshot();
  Check(ConfigStore.S(upgraded,"ball")=="pointer"&&ConfigStore.S(upgraded,"selectedDevice")=="retained-trackball","Held-navigation migration keeps the trackball and restores pointing for "+zoomBinding);
  Check(ConfigStore.S(upgraded,"zoomKeys")==(zoomBinding=="none"?"ctrl":zoomBinding)&&ConfigStore.S(upgraded,"orbitKeys")=="Alt+Q"&&ConfigStore.S(upgraded,"panKeys")=="Ctrl+Space","Migration preserves custom held keys for "+zoomBinding);
 }
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


Check(ModeResolver.Resolve(defaults,new HashSet<int>{91,68})==NavMode.Idle,"Windows+D is not a navigation chord");
Check(ModeResolver.Resolve(defaults,new HashSet<int>{192})==NavMode.Idle,"Backtick is available for ordinary typing");
var mouseDefaults=ConfigStore.Defaults();
Check(ModeResolver.Resolve(mouseDefaults,new HashSet<int>{91})==NavMode.Idle,"Normal mouse does not reserve Windows");
Check(mouseDefaults["combos"]!.AsArray().Any(n=>n?["buttons"]?.ToJsonString()=="[3,4]"&&ConfigStore.S(n?["bind"],"action")=="lockRotate"),"Confirmed btn3+btn4 combination is a default without swapping clicks");
Check(PointerPolicy.Gain(1)==1f/32&&PointerPolicy.Gain(10)==1&&PointerPolicy.Gain(20)==3.5f,"Pointer gain matches every displayed DPI endpoint and neutral speed");
Check(!PointerPolicy.Capture(10,false)&&PointerPolicy.Capture(1,false)&&PointerPolicy.Capture(10,true),"Neutral pointing can pass through; speed and glide request device routing");
foreach(int step in new[]{1,6,10,15,20}){
 var pointerTest=new PointerMotion();int x=0,y=0;
 for(int sample=0;sample<320;sample++){var output=pointerTest.Move(1,-2,PointerPolicy.Gain(step));x+=output.X;y+=output.Y;}
 Check(x==(int)MathF.Round(320*PointerPolicy.Gain(step))&&y==(int)MathF.Round(-640*PointerPolicy.Gain(step)),"Pointer speed "+step+" changes actual counts without losing subpixels");
}
var pointerGlide=new PointerMotion();pointerGlide.Move(16,0,1);
Check(pointerGlide.Tick(100,.1f,true,1)==(0,0),"Immediate pointer delivery is never repeated by the frame timer");
var pointerTail=pointerGlide.Tick(116,.1f,true,1);
Check(pointerTail.X>0&&pointerTail.Y==0,"Pointer glide emits a real decaying tail after the ball stops");
pointerGlide.Reset();Check(pointerGlide.Tick(132,1,true,1)==(0,0),"Click, app change and navigation transition can stop pointer glide");
pointerGlide.Move(16,0,1);pointerGlide.Tick(148,1,false,1);
Check(pointerGlide.Tick(164,1,false,1)==(0,0),"Disabling soft stop stops pointer momentum immediately");
float TailDistance(float seconds){
 var motion=new PointerMotion();motion.Move(16,0,1);motion.Tick(100,seconds,true,1);float distance=0;
 for(long now=116;now<5100;now+=16)distance+=motion.Tick(now,seconds,true,1).X;
 return distance;
}
Check(TailDistance(1)>TailDistance(.1f)*5,"Increasing Glide visibly increases pointer coasting distance");
var studioView=new CameraPose(new(0,0,10),Vector3.Zero,Vector3.UnitY,Vector3.Zero,.8f,10,1200,800,false,Vector3.UnitY);
var studioRight=CameraMath.AlignFace(studioView,Vector3.UnitX);
Check(Vector3.Dot(studioRight.Up,Vector3.UnitY)>.999f&&Vector3.Dot(Vector3.Normalize(studioRight.Eye-studioRight.Target),Vector3.UnitX)>.999f,"Studio Right face uses its Y-up cube convention");
var studioTop=CameraMath.AlignFace(studioView,Vector3.UnitY);var studioBottom=CameraMath.AlignFace(studioView,-Vector3.UnitY);
Check(studioTop.Valid&&studioBottom.Valid&&Vector3.Dot(studioTop.Up,-Vector3.UnitZ)>.999f&&Vector3.Dot(studioBottom.Up,Vector3.UnitZ)>.999f,"Studio Top and Bottom have upright opposite-face orientations");
var stepSnap=new FaceSnap();stepSnap.Reset(studioTop);var seenFaces=new HashSet<string>();var reachedBottom=false;
for(int sample=0;sample<40;sample++){
 var face=stepSnap.Step(studioTop,0,5);var direction=Vector3.Normalize(face.Eye-face.Target);seenFaces.Add(CameraMath.NearestFace(direction).ToString());
 reachedBottom|=Vector3.Dot(direction,-Vector3.UnitY)>.999f;
 Check(IsFace(face),"Continuous face snap remains aligned at sample "+sample);
}
Check(reachedBottom&&seenFaces.Count>=3,"Continued rolling from Top passes through an adjacent face and reaches Bottom");
var turnSnap=new FaceSnap();turnSnap.Reset(studioRight);
var quarterTurn=turnSnap.Step(studioRight,0,0,120);var fullTurn=quarterTurn;
for(int turn=0;turn<3;turn++)fullTurn=turnSnap.Step(studioRight,0,0,120);
Check(Vector3.Dot(Vector3.Normalize(quarterTurn.Eye-quarterTurn.Target),Vector3.UnitX)>.999f&&Math.Abs(Vector3.Dot(quarterTurn.Up,studioRight.Up))<.0001,"Alt wheel turns a face by 90 degrees without changing the face");
Check(Vector3.Distance(fullTurn.Up,studioRight.Up)<.0001,"Four face turns return to the upright view");
var jitterSnap=new FaceSnap();jitterSnap.Reset(studioView with{Eye=new(7.05f,0,7),Up=Vector3.UnitY});
var stableFace=jitterSnap.Step(studioView,0,0);bool stable=true;
for(int sample=0;sample<100;sample++){var face=jitterSnap.Step(studioView,sample%2==0?.1f:-.1f,0);stable&=Vector3.Dot(Vector3.Normalize(stableFace.Eye-stableFace.Target),Vector3.Normalize(face.Eye-face.Target))>.999f;}
Check(stable,"Face-boundary noise does not flicker between adjacent views");
var snappedOff=CameraMath.AlignFace(offCenter with{WorldUp=Vector3.UnitY},Vector3.UnitX,1);
Check(Vector2.Distance(ProjectPivot(offCenter),ProjectPivot(snappedOff))<.0001,"Upright face turns preserve an off-center cursor pivot");


var presetPath=Path.Combine(Path.GetTempPath(),"chamfer-presets-"+Guid.NewGuid().ToString("N")+".json");
try{
 var library=new ConfigStore(presetPath);library.IdentifyDevice("preset-trackball");library.Select(1);
 var setup=library.Snapshot();var staleLibrary=(JsonObject)setup.DeepClone();
 setup["pointerSpeed"]=7;setup["orbitSens"]=62;setup["panSens"]=71;setup["zoomSens"]=39;setup["glide"]=64;setup["smooth"]=false;
 setup["circleSize"]=90;setup["leftWheel"]="keys";setup["leftWheelUpKeys"]="Ctrl+S";
 setup["combos"]=new JsonArray(new JsonObject{["buttons"]=new JsonArray(1,2),["bind"]=new JsonObject{["action"]="keys",["keys"]="Alt+Q"}});
 setup["programs"]![1]!["gestures"]!["up"]=new JsonObject{["type"]="keys",["keys"]="F"};
 library.Save(setup);var savedPreset=library.SavePreset(" Fine CAD ");
 Check(savedPreset["presets"]!.AsArray().Count==1&&ConfigStore.S(savedPreset["presets"]![0],"name")=="Fine CAD","A named preset saves with a trimmed name");
 var presetSettings=savedPreset["presets"]![0]!["settings"]!;
 Check(ConfigStore.I(presetSettings,"pointerSpeed")==7&&ConfigStore.I(presetSettings["feel"],"glide")==64&&!ConfigStore.B(presetSettings["feel"],"smooth"),"Presets save pointer speed, glide and motion feel");
 Check(ConfigStore.S(presetSettings,"navigateKeys")=="win"&&ConfigStore.S(presetSettings,"leftWheelUpKeys")=="Ctrl+S"&&ConfigStore.S(presetSettings["gestures"]!["up"],"keys")=="F","Presets save navigation keys, wheels and gestures");
 Check(!presetSettings.AsObject().ContainsKey("selectedDevice")&&!presetSettings.AsObject().ContainsKey("profiles")&&!presetSettings.AsObject().ContainsKey("theme"),"A program preset excludes device identity and unrelated app settings");
 library.Save(staleLibrary);Check(library.Snapshot()["presets"]!.AsArray().Count==1,"An older settings page cannot erase a newly saved preset");
 var reopenedLibrary=new ConfigStore(presetPath);reopenedLibrary.Select(0);
 var beforeApply=reopenedLibrary.Snapshot();var otherBefore=beforeApply["programs"]![1]!.ToJsonString();
 var applied=reopenedLibrary.ApplyPreset(0);
 Check(ConfigStore.S(applied["programs"]![0],"name")=="Normal mouse"&&applied["programs"]![1]!.ToJsonString()==otherBefore,"Applying a preset preserves program names and the other program's settings");
 Check(ConfigStore.I(applied,"pointerSpeed")==7&&ConfigStore.I(applied,"orbitSens")==62&&ConfigStore.I(applied,"panSens")==71&&ConfigStore.I(applied,"zoomSens")==39&&ConfigStore.I(applied,"glide")==64,"Applied presets reach every actual speed and glide field");
 Check(ConfigStore.S(applied,"selectedDevice")=="preset-trackball"&&applied["combos"]![0]!["buttons"]!.ToJsonString()=="[1,2]","Applying a preset keeps the identified trackball and restores combinations");
 var edited=reopenedLibrary.Snapshot();edited["pointerSpeed"]=18;reopenedLibrary.Save(edited);
 Check(ConfigStore.I(reopenedLibrary.Snapshot()["presets"]![0]!["settings"],"pointerSpeed")==7,"Editing a program does not change the saved preset");
 bool duplicateRejected=false;try{reopenedLibrary.SavePreset("fine cad");}catch(InvalidDataException){duplicateRejected=true;}
 Check(duplicateRejected&&reopenedLibrary.Snapshot()["presets"]!.AsArray().Count==1,"An existing preset needs explicit replacement");
 reopenedLibrary.SavePreset("fine cad",true);
 Check(ConfigStore.I(reopenedLibrary.Snapshot()["presets"]![0]!["settings"],"pointerSpeed")==18,"Explicit replacement updates the existing preset");
 reopenedLibrary.RenamePreset(0,"Detail view");
 Check(ConfigStore.S(new ConfigStore(presetPath).Snapshot()["presets"]![0],"name")=="Detail view","Renamed presets survive restart");
 bool invalidName=false;try{reopenedLibrary.SavePreset(" ");}catch(InvalidDataException){invalidName=true;}
 Check(invalidName,"Blank preset names cannot overwrite the library");
 var resetLibrary=reopenedLibrary.Reset();
 Check(resetLibrary["presets"]!.AsArray().Count==1&&ConfigStore.S(resetLibrary["presets"]![0],"name")=="Detail view","Resetting controls preserves personal presets");
 reopenedLibrary.Select(1);var referenced=reopenedLibrary.Snapshot();
 var third=(JsonObject)referenced["programs"]![1]!.DeepClone();third["name"]="Third";third["pointerSpeed"]=17;referenced["programs"]!.AsArray().Add(third);
 referenced["buttons"]!["back"]=new JsonObject{["action"]="program",["program"]=2};
 referenced["modeButtons"]=new JsonObject{["middle"]=new JsonObject{["action"]="program",["program"]=1}};
 referenced["combos"]=new JsonArray(new JsonObject{["buttons"]=new JsonArray(1,2),["bind"]=new JsonObject{["action"]="program",["program"]=2}});
 referenced["programs"]![1]!["gestures"]!["up"]=new JsonObject{["type"]="program",["program"]=1};
 referenced["wheelPrograms"]=new JsonArray(0,1,2,2);reopenedLibrary.Save(referenced);reopenedLibrary.SavePreset("Program targets");
 var remaining=reopenedLibrary.DeleteProgram(1);
 Check(ConfigStore.S(remaining["programs"]![1],"name")=="Third"&&ConfigStore.I(remaining,"pointerSpeed")==17,"Deleting a program loads all settings from the remaining program");
 var targets=remaining["presets"]![1]!["settings"]!;
 Check(ConfigStore.I(targets["buttons"]!["back"],"program")==1&&ConfigStore.I(targets["modeButtons"]!["middle"],"program")==0&&ConfigStore.I(targets["combos"]![0]!["bind"],"program")==1&&ConfigStore.I(targets["gestures"]!["up"],"program")==0&&targets["wheelPrograms"]!.ToJsonString()=="[0,0,1,1]","Program deletion repairs every saved preset's button, mode, combination, gesture and wheel target");
 reopenedLibrary.ApplyPreset(1);
 Check(ConfigStore.S(reopenedLibrary.Snapshot()["programs"]![1],"name")=="Third"&&ConfigStore.I(reopenedLibrary.Snapshot()["buttons"]!["back"],"program")==1,"A preset still applies correctly after one of its target programs was deleted");
 var beforeDelete=reopenedLibrary.Snapshot();reopenedLibrary.DeletePreset(0);
 Check(reopenedLibrary.Snapshot()["presets"]!.AsArray().Count==1&&reopenedLibrary.Snapshot()["programs"]!.ToJsonString()==beforeDelete["programs"]!.ToJsonString(),"Deleting a saved preset keeps the applied program settings");
 bool invalidIndex=false;try{reopenedLibrary.ApplyPreset(99);}catch(InvalidDataException){invalidIndex=true;}
 Check(invalidIndex,"An invalid preset index cannot change the program");
 var tooMany=ConfigStore.Defaults();for(int index=0;index<65;index++)tooMany["presets"]!.AsArray().Add(new JsonObject{["name"]="Preset "+index,["settings"]=tooMany["programs"]![0]!.DeepClone()});
 bool overflow=false;try{ConfigStore.Validate(tooMany);}catch(InvalidDataException){overflow=true;}
 Check(overflow,"Preset libraries have a bounded size");
}finally{
 foreach(var suffix in new[]{"",".previous",".writing"})if(File.Exists(presetPath+suffix))File.Delete(presetPath+suffix);
}

Console.WriteLine(count+" regression checks passed.");
