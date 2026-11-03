using System.Numerics;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;

static class AltSnapWheelChecks{
 public static void Run(Action<bool,string> check){
  const string slimDevice=@"\\?\HID#VID_047D&PID_80D6&MI_00#fixture";
  check(SlimBladeTwist.Applies(slimDevice,NavMode.Orbit,0x20a)&&SlimBladeTwist.Applies(slimDevice,NavMode.PanZoom,0x20a),"SlimBlade vertical twist is eligible during orbit and pan");
  check(!SlimBladeTwist.Applies(slimDevice,NavMode.Snap,0x20a)&&!SlimBladeTwist.Applies(slimDevice,NavMode.Orbit,0x20e)&&!SlimBladeTwist.Applies("HID#VID_25A7&PID_FA61&",NavMode.Orbit,0x20a),"Snap, horizontal wheel and the M512 bypass the twist trial");
  var dial=new SlimBladeTwist();
  foreach(var (mode,expected) in new[]{(NavMode.Orbit,60f),(NavMode.PanZoom,90f)}){
   dial.Add(120,mode);float first=dial.Step(),total=first;
   for(int i=0;i<12;i++)total+=dial.Step();
   check(first>0&&first<expected&&Math.Abs(total-expected)<.001f&&dial.Step()==0,"SlimBlade twist starts immediately and delivers a bounded smooth "+mode+" step");
   dial.Add(-120,mode);
   check(dial.Step()<0,"Reverse twist reverses "+mode);
   dial.Reset();
   check(dial.Step()==0,"Releasing the held gesture clears the remaining "+mode+" twist");
  }
  var filter=new CameraMotionFilter();
  var firstMotion=filter.Step(100,-50);
  var secondMotion=filter.Step(0,0);
  var thirdMotion=filter.Step(0,0);
  check(firstMotion.X==70&&firstMotion.Y==-35&&secondMotion.X>thirdMotion.X&&thirdMotion.X>0,"Steady 3D responds immediately and distributes a short movement over three frames");
  check(Math.Abs(firstMotion.X+secondMotion.X+thirdMotion.X-100)<.001f&&Math.Abs(firstMotion.Y+secondMotion.Y+thirdMotion.Y+50)<.001f&&filter.Step(0,0)==(0f,0f),"Steady 3D preserves measured input without adding coast");
  filter.Step(100,-50);
  var finish=filter.Step(-10,5,true);
  check(Math.Abs(finish.X-20)<.001f&&Math.Abs(finish.Y+10)<.001f&&filter.Step(0,0)==(0f,0f),"Release delivers buffered counts and stops the filter");
  filter.Step(100,100);
  filter.Reset();
  check(filter.Step(0,0)==(0f,0f)&&!ConfigStore.B(ConfigStore.Defaults(),"steadyCamera"),"A new gesture starts clean and older settings keep their prior motion feel");
  check(!ConfigStore.B(ConfigStore.Defaults(),"slimBladeSmoothTwist"),"SlimBlade twist trial defaults off and can be reverted");

  check(SnapWheelPolicy.Captures(NavMode.Snap,0x20a)&&!SnapWheelPolicy.Captures(NavMode.Snap,0x20e),"Snap captures the confirmed left wheel and leaves the right wheel on its normal path");
  check(!SnapWheelPolicy.Captures(NavMode.Idle,0x20a)&&!SnapWheelPolicy.Captures(NavMode.Idle,0x20e),"Normal mouse retains both native wheels");
  foreach(var mode in new[]{NavMode.Orbit,NavMode.PanZoom,NavMode.Zoom,NavMode.Navigate,NavMode.Roll})
   check(SnapWheelPolicy.Captures(mode,0x20a)&&SnapWheelPolicy.Captures(mode,0x20e),"The existing two-wheel navigation path is preserved for "+mode);
  check(!SnapWheelPolicy.Captures(NavMode.Snap,0x207),"The middle button is not treated as a wheel or new Twist hold");

  foreach(var axis in new[]{Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ}){
   var initial=new CameraPose(axis*10,Vector3.Zero,Math.Abs(axis.Y)>.9f?Vector3.UnitZ:Vector3.UnitY,Vector3.Zero,.8f,10,800,800,false,Vector3.UnitY);
   var faces=new FaceSnap();faces.Reset(initial);var start=faces.Step(initial,0,0,0,1,1000);
   var turned=faces.Step(start,0,0,120,1,1016);
   check(Math.Abs(Vector3.Dot(turned.Up,start.Up))<.001f&&Vector3.Distance(turned.Eye,start.Eye)<.001f&&Vector3.Distance(turned.Target,start.Target)<.001f,"One left-wheel notch turns the "+axis+" face exactly 90 degrees without changing its face or distance");
   var restored=faces.Step(start,0,0,-120,1,1032);
   check(Vector3.Distance(restored.Up,start.Up)<.001f&&Vector3.Distance(restored.Eye,start.Eye)<.001f,"The opposite left-wheel notch restores the "+axis+" face orientation");
  }
  var pose=new CameraPose(new(0,0,10),Vector3.Zero,Vector3.UnitY,Vector3.Zero,.8f,10,800,800,false,Vector3.UnitY);
  var partial=new FaceSnap();partial.Reset(pose);
  var half=partial.Step(pose,0,0,60,1,1000);check(Vector3.Distance(half.Up,pose.Up)<.001f&&partial.TurnCount==0,"A partial smooth-wheel report waits until a complete notch");
  var full=partial.Step(pose,0,0,60,1,1016);check(Math.Abs(Vector3.Dot(full.Up,pose.Up))<.001f&&partial.TurnCount==1,"Two partial wheel reports combine into one exact orientation snap");
  partial.Reset(pose);partial.Step(pose,0,0,60,1,1000);partial.Reset(pose);var fresh=partial.Step(pose,0,0,60,1,1016);
  check(partial.TurnCount==0&&Vector3.Distance(fresh.Up,pose.Up)<.001f,"Releasing Alt discards a partial notch rather than carrying it into a later gesture");
  var zeroGain=new FaceSnap();zeroGain.Reset(pose);var snapped=zeroGain.Step(pose,0,0,120,0,1000);
  check(Math.Abs(Vector3.Dot(snapped.Up,pose.Up))<.001f,"Wheel snaps stay exact quarter turns instead of becoming proportional to 3D speed");
  var four=new FaceSnap();four.Reset(pose);var fourth=pose;for(int n=0;n<4;n++)fourth=four.Step(pose,0,0,120,1,1000+n*16);
  check(Vector3.Distance(fourth.Up,pose.Up)<.001f&&four.TurnCount==4,"Four wheel notches return to the starting orientation");

  var defaults=ConfigStore.Defaults();check(!ConfigStore.B(defaults,"altTwistEstimate"),"Normal mouse does not guess ball twists");
  ConfigStore.LoadProgram(defaults,1);
  check(ConfigStore.S(defaults,"snapKeys")=="alt"&&!ConfigStore.B(defaults,"altTwistEstimate"),"The 3D default uses Alt faces and left-wheel turns with ball-twist guessing off");
  check(ConfigStore.S(defaults["buttons"]?["middle"],"action")=="normal"&&!ConfigStore.B(defaults,"trackballSwapButtons")&&!ConfigStore.B(defaults,"trackballSwapWheels"),"The top-left button and native click/wheel corrections remain unchanged");

  var path=Path.Combine(Path.GetTempPath(),"chamfer-left-wheel-"+Guid.NewGuid().ToString("N")+".json");
  try{
   var old=ConfigStore.Defaults();old["configVersion"]=28;old["activeProgram"]=1;old["selectedDevice"]="retained-left-wheel-device";
   old["programs"]![1]!["altTwistEstimate"]=true;old["programs"]![1]!["feel"]!["orbitSens"]=7;old["programs"]![1]!["twistKeys"]="alt+t";
   var buttons=old["programs"]![1]!["buttons"]!.DeepClone();var presets=old["presets"]!.DeepClone();var other=(JsonObject)old["programs"]![1]!.DeepClone();other["name"]="Other CAD";other["altTwistEstimate"]=true;old["programs"]!.AsArray().Add(other);
   File.WriteAllText(path,old.ToJsonString());var store=new ConfigStore(path);var migrated=store.Snapshot();
   check(ConfigStore.I(migrated,"configVersion")==30&&!ConfigStore.B(migrated,"altTwistEstimate"),"Existing 3D settings migrate from ambiguous guessing to directional faces");
   var saved=JsonNode.Parse(File.ReadAllText(path))!;
   check(ConfigStore.I(saved,"configVersion")==30&&!ConfigStore.B(saved["programs"]?[1],"altTwistEstimate"),"The upgraded default is persisted atomically so it survives restart immediately");
   check(ConfigStore.S(migrated,"twistKeys")=="alt+t"&&ConfigStore.D(migrated,"orbitSens")==7&&JsonNode.DeepEquals(migrated["buttons"],buttons)&&JsonNode.DeepEquals(migrated["presets"],presets)&&ConfigStore.S(migrated,"selectedDevice")=="retained-left-wheel-device","The wheel setup preserves other keys, speed, native buttons, presets and device identity");
   check(ConfigStore.B(migrated["programs"]?[2],"altTwistEstimate"),"The migration does not overwrite a separate custom CAD program");
   migrated["altTwistEstimate"]=true;store.Save(migrated);var reopened=new ConfigStore(path).Snapshot();
   check(ConfigStore.B(reopened,"altTwistEstimate"),"A later explicit choice to enable the experimental option is not overwritten on every restart");
  }finally{foreach(var file in new[]{path,path+".previous",path+".writing"})if(File.Exists(file))File.Delete(file);}
 }
}
