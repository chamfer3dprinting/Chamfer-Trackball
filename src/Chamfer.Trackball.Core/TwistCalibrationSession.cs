using System.Numerics;
using System.Text.Json.Nodes;
namespace Chamfer.Trackball.Core;

public sealed class TwistCalibrationSession{
 readonly Dictionary<string,List<TwistSample>> samples=new(){["faces"]=[],["clockwise"]=[],["anticlockwise"]=[]};
 readonly EstimatedFaceSnap testInput=new();readonly CameraPose testStart=new(new(0,0,10),Vector3.Zero,Vector3.UnitY,Vector3.Zero,.8f,10,800,800,false,Vector3.UnitY);
 long begins,ends;string phase="";bool active,testing;int signedTurns,lastTurns;string error="";
 public TwistCalibrationModel? Model{get;private set;}
 public bool Recording{get;private set;}
 public bool Receiving=>Recording||testing;
 public bool Active=>active;
 public void Open(){Cancel();active=true;}
 public void Record(string kind,long now){
  if(!active||!samples.ContainsKey(kind))throw new InvalidOperationException("Open calibration and choose a movement.");
  samples[kind].Clear();phase=kind;Model=null;error="";testing=false;Recording=true;begins=now+600;ends=begins+5000;
 }
 public void Tick(float x,float y,float wheel,long now){
  if(Recording){
   if(now>=ends){Recording=false;phase="review";Build();return;}
   if(now>=begins&&samples[phase].Count<512)samples[phase].Add(new(x,y,wheel,now));
  }else if(testing&&Model!=null){
   testInput.Step(testStart,x,y,wheel,1,1,"x",false,now,Model);
   if(testInput.QuarterTurns>lastTurns){signedTurns+=(testInput.QuarterTurns-lastTurns)*testInput.LastDirection;lastTurns=testInput.QuarterTurns;}
  }
 }
 void Build(){
  if(samples.Values.Any(list=>list.Count==0))return;
  try{Model=TwistCalibrationModel.Train(samples["faces"],samples["clockwise"],samples["anticlockwise"]);error="";}
  catch(InvalidDataException ex){Model=null;error=ex.Message;}
 }
 public void Test(){
  if(Model==null)throw new InvalidOperationException("Record all three samples before testing.");
  Recording=false;testing=true;signedTurns=lastTurns=0;testInput.Reset(testStart);phase="test";error="";
 }
 public JsonObject State(long now){
  var counts=new JsonObject();foreach(var item in samples)counts[item.Key]=item.Value.Count(s=>s.Moving);
  return new(){["active"]=active,["recording"]=Recording,["phase"]=phase,["remainingMs"]=Recording?Math.Max(0,ends-now):0,["starting"]=Recording&&now<begins,
   ["progress"]=Recording?Math.Clamp((now-begins)/5000d,0,1):0,["samples"]=counts,["hasModel"]=Model!=null,["quality"]=Model?.Quality??"",["source"]=Model?.Source??"",
   ["summary"]=Model==null?"":Model.Source=="wheel"?"A scroll signal was detected during the knob turns.":"Your X/Y roll patterns were learned. Similar motions can still overlap.",
   ["error"]=error,["testing"]=testing,["testIntent"]=testing?(Model?.Source=="wheel"&&testInput.EstimatedTwist?"Twist 90°":SnapLabels.For(testInput.State)):"",["testTurns"]=signedTurns,["testProgress"]=Math.Round(testInput.Progress*100)};
 }
 public void Cancel(){active=testing=Recording=false;phase="";Model=null;error="";foreach(var list in samples.Values)list.Clear();testInput.Reset();signedTurns=lastTurns=0;}
}
