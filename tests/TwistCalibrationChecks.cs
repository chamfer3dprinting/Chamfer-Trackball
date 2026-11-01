using System.Numerics;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;

static class TwistCalibrationChecks{
 // Synthetic labelled fixtures only. No owner's motion recordings belong in source.
 public static TwistSample[] Faces()=>Enumerable.Range(0,128).Select(i=>{
  int direction=i/32;float jitter=i%4-1.5f;
  return new TwistSample(direction==0?14:direction==1?-14:jitter,direction==2?14:direction==3?-14:jitter,0,1000+i*16);
 }).ToArray();
 public static TwistSample[] Knob(int sign)=>Enumerable.Range(0,128).Select(i=>new TwistSample(sign*(30+i%3),sign*(15+i%3*.5f),0,1000+i*16)).ToArray();
 public static TwistCalibrationModel Model()=>TwistCalibrationModel.Train(Faces(),Knob(1),Knob(-1));
 public static void Run(Action<bool,string> check){
  var model=Model();
  check(model.Source=="xy"&&model.AxisX>.85f&&model.AxisY>.4f,"Calibration learns a diagonal knob axis without changing device coordinates");
  check(model.Decide(Knob(1).Take(8).ToArray()).Twist&&model.Decide(Knob(1).Take(8).ToArray()).Direction==1,"Learned clockwise motion is recognized");
  check(model.Decide(Knob(-1).Take(8).ToArray()).Twist&&model.Decide(Knob(-1).Take(8).ToArray()).Direction==-1,"Learned anticlockwise motion is recognized");
  check(!model.Decide(Faces().Take(8).ToArray()).Twist&&!model.Decide(Faces().Skip(64).Take(8).ToArray()).Twist,"Learned horizontal and vertical face rolls stay directional");
  check(model.Separation>=.65f,"Distinct synthetic calibration samples report useful separation");
  var restored=TwistCalibrationModel.FromNode(model.ToNode())!;
  check(restored.Decide(Knob(1).Take(8).ToArray()).Twist&&Math.Abs(restored.QuarterCounts-model.QuarterCounts)<.001,"Serialized calibration preserves its learned decision and turn distance");
  check(Math.Abs(model.CameraDelta(model.AxisX*model.QuarterCounts,model.AxisY*model.QuarterCounts,0,1,false)+KnobSteps.QuarterDelta)<.001,"Learned comfortable movement maps to one clockwise quarter turn");
  check(Math.Abs(model.CameraDelta(model.AxisX*model.QuarterCounts,model.AxisY*model.QuarterCounts,0,.25f,false)+KnobSteps.QuarterDelta*.25f)<.001&&model.CameraDelta(10,5,0,0,false)==0,"Twist speed and zero sensitivity scale calibrated motion");
  check(model.CameraDelta(10,5,0,1,false)==-model.CameraDelta(10,5,0,1,true),"Direction inversion remains effective after calibration");
  var overlapFaces=Knob(1).Concat(Knob(-1)).ToArray();var overlap=TwistCalibrationModel.Train(overlapFaces,Knob(1),Knob(-1));
  check(overlap.Quality=="Overlapping patterns"&&!overlap.Decide(Knob(1).Take(8).ToArray()).Twist,"Identical face and twist signals report overlap and fall back to faces");

  var cwWheel=Enumerable.Range(0,6).Select(i=>new TwistSample(0,0,-120,1000+i*250)).ToArray();
  var ccwWheel=Enumerable.Range(0,6).Select(i=>new TwistSample(0,0,120,1000+i*250)).ToArray();
  var wheel=TwistCalibrationModel.Train(Faces(),cwWheel,ccwWheel);
  check(wheel.Source=="wheel"&&wheel.WheelSign==-1&&wheel.QuarterCounts==120&&wheel.Quality=="Scroll signal","Separate opposite scroll signals are learned without inventing an X/Y twist axis");
  var start=new CameraPose(new(0,0,10),Vector3.Zero,Vector3.UnitY,Vector3.Zero,.8f,10,800,800,false,Vector3.UnitY);
  var input=new EstimatedFaceSnap();input.Reset(start);var pose=input.Step(start,0,0,-120,1,1,"x",false,2000,wheel);
  check(input.QuarterTurns==1&&input.LastDirection==1&&Vector3.Dot(Vector3.Normalize(pose.Eye-pose.Target),Vector3.UnitZ)>.999f&&Math.Abs(Vector3.Dot(pose.Up,Vector3.UnitY))<.001,"A calibrated twist scroll signal makes an exact face-orientation turn without zooming or changing its face");
  check(Math.Abs(Vector3.Distance(pose.Eye,pose.Target)-10)<.001,"Calibrated wheel twist preserves camera distance");
  for(int n=0;n<12;n++)pose=input.Step(start,0,40,0,1,1,"x",false,2300+n*16,wheel);
  check(input.State=="faces"&&Vector3.Dot(Vector3.Normalize(pose.Eye-pose.Target),Vector3.UnitZ)<.5f,"Directional rolls still select a neighboring face after a calibrated wheel turn");
  var singleWheel=TwistCalibrationModel.Train(Faces(),cwWheel.Take(1).ToArray(),ccwWheel.Take(1).ToArray());
  check(singleWheel.Source=="wheel","A distinct hardware wheel report does not require eight synthetic motion frames");

  bool Reject(Action action){try{action();return false;}catch(Exception ex)when(ex is InvalidDataException or ArgumentException){return true;}}
  check(Reject(()=>TwistCalibrationModel.Train([],Knob(1),Knob(-1))),"Missing normal-roll data is rejected");
  check(Reject(()=>TwistCalibrationModel.Train(Faces(),Knob(1),Knob(1))),"Two same-direction knob recordings are rejected");
  check(Reject(()=>TwistCalibrationModel.Train(Faces(),[new(float.NaN,0,0,1)],Knob(-1))),"Nonfinite recorded motion cannot create a calibration");
  var bad=model.ToNode();bad["axisX"]=99;check(Reject(()=>TwistCalibrationModel.FromNode(bad)),"Out-of-range learned axes are rejected");
  bad=model.ToNode();bad["patterns"]=null;check(Reject(()=>TwistCalibrationModel.FromNode(bad)),"Null learned pattern arrays are rejected safely");
  bad=model.ToNode();bad["quarterCounts"]=0;check(Reject(()=>TwistCalibrationModel.FromNode(bad)),"Zero calibrated turn distance is rejected");

  var session=new TwistCalibrationSession();session.Open();session.Record("faces",1000);session.Tick(999,999,0,1100);
  check(session.Recording&&session.State(1100)["samples"]!["faces"]!.GetValue<int>()==0,"Recording ignores the warm-up movement caused by clicking Start");
  void Record(string kind,TwistSample[] values,long now){session.Record(kind,now);foreach(var item in values)session.Tick(item.X,item.Y,item.Wheel,now+600+item.Time-1000);session.Tick(0,0,0,now+5600);}
  Record("faces",Faces(),2000);Record("clockwise",Knob(1),8000);
  check(session.Model==null&&!session.Recording,"Two recordings cannot be applied as a complete calibration");
  Record("anticlockwise",Knob(-1),14000);
  check(session.Model!=null&&session.Active&&!session.Receiving&&session.State(20000)["hasModel"]!.GetValue<bool>(),"All three bounded recordings produce a reviewable model");
  session.Test();check(session.Receiving&&session.State(20000)["testing"]!.GetValue<bool>(),"The learned result can be tested before saving");
  for(int n=0;n<150;n++)session.Tick(31,15.5f,0,21000+n*16);
  check(session.State(24000)["testTurns"]!.GetValue<int>()>0,"Preview reports clockwise quarter turns from the learned pattern");
  session.Record("clockwise",25000);check(session.Model==null&&session.Recording,"Re-recording invalidates the previous uncommitted result");
  session.Cancel();check(!session.Active&&!session.Receiving&&session.Model==null,"Cancel discards only the unsaved calibration session");
  session.Open();session.Record("faces",100);session.Tick(0,0,0,5700);
  check(!session.Recording&&session.State(5700)["samples"]!["faces"]!.GetValue<int>()==0,"An empty recording ends on time and cannot claim movement");

  var path=Path.Combine(Path.GetTempPath(),"chamfer-calibration-"+Guid.NewGuid().ToString("N")+".json");
  try{
   var store=new ConfigStore(path);store.IdentifyDevice("test-trackball");store.Select(1);var before=store.Snapshot();var stale=(JsonObject)before.DeepClone();
   var applied=store.SetTwistCalibration(model);
   check(applied["twistCalibration"]!=null&&applied["programs"]![1]!["twistCalibration"]!=null,"Apply saves the learned model into the active program");
   check(JsonNode.DeepEquals(before["buttons"],applied["buttons"])&&JsonNode.DeepEquals(before["feel"],applied["feel"])&&ConfigStore.S(applied,"selectedDevice")=="test-trackball"&&!ConfigStore.B(applied,"trackballSwapButtons")&&!ConfigStore.B(applied,"trackballSwapWheels"),"Calibration preserves native clicks, wheel axes, device identity and motion settings");
   stale["pointerSpeed"]=7;store.Save(stale);
   check(store.Snapshot()["twistCalibration"]!=null&&ConfigStore.I(store.Snapshot(),"pointerSpeed")==7,"A stale general settings save cannot erase a newly applied calibration");
   store.SavePreset("Calibrated");store.Select(0);
   check(store.Snapshot()["twistCalibration"]==null,"Calibration is isolated from the Normal mouse program");
   store.Select(1);var reopened=new ConfigStore(path);
   check(reopened.Snapshot()["twistCalibration"]!=null,"Calibration survives restart and program switching");
   reopened.SetTwistCalibration(null);var cleared=reopened.Snapshot();
   check(cleared["twistCalibration"]==null&&cleared["programs"]![1]!["twistCalibration"]==null,"Clear removes the program calibration");
   reopened.ApplyPreset(0);check(reopened.Snapshot()["twistCalibration"]!=null,"Named presets restore their saved twist calibration");
   check(!reopened.Snapshot().ToJsonString().Contains("\"time\""),"Persisted settings contain the learned model rather than raw motion recordings");
  }finally{foreach(var file in new[]{path,path+".previous",path+".writing"})if(File.Exists(file))File.Delete(file);}
 }
}
