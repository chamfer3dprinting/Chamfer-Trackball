using System.Numerics;
namespace Chamfer.Trackball.Core;

// Experimental intent estimation from X/Y geometry. It cannot measure an
// independent twist axis; uncertain strokes are delivered as directional snaps.
public sealed class EstimatedFaceSnap{
 readonly Queue<TwistSample> recent=new(8);readonly FaceSnap faces=new();readonly KnobSteps turns=new();readonly List<(float X,float Y,long Time)> pending=new(16);
 CameraPose? pose;long began,lastMotion,faceDecisionAt,lastWheel;float along,cross;int stage;bool turnedInBurst; // 0 new, 1 observing, 2 faces, 3 estimated twist
 public bool Pending=>stage==1;
 public bool EstimatedTwist=>stage==3;
 public int QuarterTurns{get;private set;}
 public long LastTurnAt{get;private set;}
 public int LastDirection{get;private set;}
 public bool LastEstimatedTurn{get;private set;}
 public long Samples{get;private set;}
 public float Progress=>turns.Progress;
 public bool Nudged=>turns.Nudged;
 public string State=>stage switch{1=>"estimating",3=>"twist",_=>"faces"};
 public void Reset(CameraPose? initial=null){
  faces.Reset(initial);pose=initial==null?null:faces.Step(initial,0,0);turns.Reset();pending.Clear();stage=0;began=lastMotion=faceDecisionAt=lastWheel=0;recent.Clear();
  along=cross=0;QuarterTurns=0;LastDirection=0;LastTurnAt=0;LastEstimatedTurn=false;Samples=0;
 }
 void Start(long now){
  stage=1;turnedInBurst=false;began=now;recent.Clear();along=cross=0;pending.Clear();turns.Reset();faces.Reset(pose);
 }
 CameraPose Face(CameraPose start,float x,float y,float wheel,float gain,long now){
  int before=faces.TurnCount;pose=faces.Step(pose??start,x,y,wheel,gain,now);
  if(faces.TurnCount!=before){QuarterTurns+=faces.TurnCount-before;LastDirection=faces.LastTurnDirection;LastTurnAt=now;LastEstimatedTurn=false;}
  return pose;
 }
 void Twist(CameraPose start,float x,float y,float gain,string axis,bool reverse,long now,TwistCalibrationModel? calibration=null,float wheel=0){
  float delta=calibration?.NudgeDelta(x,y,wheel,gain,reverse)??KnobTwist.CameraDelta(x,y,axis,reverse,gain);if(delta!=0)LastDirection=-Math.Sign(delta);int steps=turns.AddNudge(delta,now);
  if(steps==0)return;
  pose=CameraMath.Move(pose??start,NavMode.Roll,(steps%4)*KnobSteps.QuarterDelta,0);
  turnedInBurst=true;LastEstimatedTurn=true;QuarterTurns+=Math.Abs(steps);LastDirection=-Math.Sign(steps);LastTurnAt=now;
 }
 static float Along(TwistSample s,string axis,TwistCalibrationModel? calibration)=>calibration?.Project(s.X,s.Y,0)??(axis=="y"?s.Y:s.X);
 static float Cross(TwistSample s,string axis,TwistCalibrationModel? calibration)=>calibration!=null?-s.X*calibration.AxisY+s.Y*calibration.AxisX:axis=="y"?s.X:s.Y;
 static bool NudgeCandidate(IReadOnlyList<TwistSample> samples,string axis,TwistCalibrationModel? calibration){
  float a=0,c=0,aa=0,cc=0,distance=0;int moving=0;
  foreach(var s in samples){float x=Along(s,axis,calibration),y=Cross(s,axis,calibration);a+=x;c+=y;aa+=x*x;cc+=y*y;distance+=MathF.Sqrt(x*x+y*y);if(x!=0||y!=0)moving++;}
  if(MathF.Abs(a)+.0001f<KnobSteps.NudgeCounts)return false;
  if(calibration!=null)return calibration.DecideNudge(samples).Twist;
  // Quantized tiny strokes can include a count or two across the selected
  // axis. Judge their net path instead of requiring an almost perfect line.
  return MathF.Abs(a)/Math.Max(distance,.001f)>=.85f&&MathF.Abs(c)<=Math.Max(2,MathF.Abs(a)*.08f)&&cc<=Math.Max(moving,aa*.08f);
 }
 public CameraPose Step(CameraPose start,float x,float y,float wheel,float faceGain,float twistGain,string axis,bool reverse,long now,TwistCalibrationModel? calibration=null){
  if(!float.IsFinite(x)||!float.IsFinite(y)||!float.IsFinite(wheel))throw new ArgumentException("Invalid snap input.");
  pose??=faces.Step(start,0,0,0,faceGain,now);
  bool motion=x!=0||y!=0;
  if(calibration?.Source=="wheel"){
   if(motion||wheel!=0)Samples++;
   if(wheel!=0){if(stage!=3){turns.Reset();faces.Reset(pose);}stage=3;lastWheel=now;Twist(start,0,0,twistGain,axis,reverse,now,calibration,wheel);return pose;}
   if(motion){
    if(stage==3&&now-lastWheel<80&&MathF.Abs(x)+MathF.Abs(y)<12)return pose;
    if(stage!=2){faces.Reset(pose);turns.Reset();}stage=2;return Face(start,x,y,0,faceGain,now);
   }
   return pose;
  }
  if(motion){
   if(lastMotion==0||now<lastMotion||now-lastMotion>(stage==3?700:250))Start(now);
   lastMotion=now;Samples++;
  }
  float a=calibration!=null?calibration.Project(x,y,0):axis=="y"?y:x,c=calibration!=null?-x*calibration.AxisY+y*calibration.AxisX:axis=="y"?x:y;
  if(motion){
   // Do not mix the previous clockwise stroke into a deliberate reverse
   // nudge on the selected axis; its cancelled net path would look uncertain.
   if(stage==3&&recent.Count>0&&MathF.Abs(a)>=.5f&&MathF.Abs(c)<=Math.Max(1,MathF.Abs(a)*.5f)){
    var previous=recent.Last();if(Math.Sign(Along(previous,axis,calibration))!=Math.Sign(a)){recent.Clear();along=cross=0;}
   }
   recent.Enqueue(new(x,y,0,now));while(recent.Count>8)recent.Dequeue();
  }
  if(stage==3&&wheel!=0){stage=2;faceDecisionAt=now;turns.Reset();pending.Clear();faces.Reset(pose);return Face(start,x,y,wheel,faceGain,now);}
  if(stage==3){
   var window=recent.ToArray();float windowTravel=window.Sum(s=>MathF.Abs(Along(s,axis,calibration)));
   bool uncertain=motion&&(calibration!=null?windowTravel>=KnobSteps.NudgeCounts&&recent.Count>=2&&now-recent.Peek().Time>=32&&!calibration.DecideNudge(window).Twist:MathF.Abs(c)>Math.Max(2,MathF.Abs(a)*.5f)||windowTravel>=16&&!NudgeCandidate(window,axis,null));
   if(uncertain){
    stage=2;faceDecisionAt=now;turns.Reset();faces.Reset(pose);
    if(!turnedInBurst)foreach(var item in pending)Face(start,item.X,item.Y,0,faceGain,item.Time);
    pending.Clear();
   }else{
    if(motion&&!turnedInBurst){pending.Add((x,y,now));if(pending.Count>32)pending.RemoveAt(0);}
    Twist(start,x,y,twistGain,axis,reverse,now,calibration);if(turnedInBurst)pending.Clear();
    return pose;
   }
  }
  if(stage==2){
   // A face decision is local to the recent motion, not the entire Alt hold.
   // Reconsider a later clean knob candidate without requiring a pause or key release.
   TwistSample[]? retry=null;
   if(motion&&wheel==0&&now-faceDecisionAt>=80&&MathF.Abs(a)>=1){
    var history=recent.ToArray();
    for(int first=0;first<history.Length;first++){var candidate=history.Skip(first).ToArray();if(now-candidate[0].Time<=160&&NudgeCandidate(candidate,axis,calibration)){retry=candidate;break;}}
   }
   if(retry==null)return Face(start,x,y,wheel,faceGain,now);
   Start(retry[0].Time);
   // The complete tiny burst may have arrived while faces were selected.
   // Keep that evidence; restarting with only the last one-count packet loses it.
   foreach(var item in retry)recent.Enqueue(item);
   for(int n=0;n<retry.Length-1;n++){
    var item=retry[n];pending.Add((item.X,item.Y,item.Time));float ra=Along(item,axis,calibration),rc=Cross(item,axis,calibration);along+=ra;cross+=rc;
   }
  }
  if(motion){
   pending.Add((x,y,now));along+=a;cross+=c;
  }
  if(stage==0)return wheel==0?pose:Face(start,0,0,wheel,faceGain,now);
  bool clearlyDirectional=wheel!=0||pending.Count>=3&&MathF.Abs(cross)>Math.Max(4,MathF.Abs(along)*(calibration!=null?.75f:.5f));
  var observation=pending.Select(p=>new TwistSample(p.X,p.Y,0,p.Time)).ToArray();
  bool candidateKnob=NudgeCandidate(observation,axis,calibration);
  bool ready=candidateKnob&&(now-began>=48||now-lastMotion>=32)||now-began>=112&&pending.Count>=4||now-began>=160||pending.Count>=16;
  if(!clearlyDirectional&&!ready)return pose;
  // A slow slight nudge may arrive as many tiny reports. Wait for its small
  // trigger distance while motion continues, rather than rejecting it at 160 ms.
  if(!clearlyDirectional&&MathF.Abs(along)<KnobSteps.NudgeCounts&&now-began<640&&pending.Count<48&&now-lastMotion<80)return pose;
  bool knob=!clearlyDirectional&&candidateKnob;
  stage=knob?3:2;if(!knob)faceDecisionAt=now;
  foreach(var item in pending){
   if(knob)Twist(start,item.X,item.Y,twistGain,axis,reverse,item.Time,calibration);
   else Face(start,item.X,item.Y,0,faceGain,item.Time);
  }
  if(!knob||turnedInBurst)pending.Clear();
  if(wheel!=0)Face(start,0,0,wheel,faceGain,now);
  return pose;
 }
}

public static class SnapLabels{
 public static string For(string state)=>state switch{
  "twist"=>"Estimated twist 90°",
  "estimating"=>"Reading Alt motion",
  "waiting"=>"Waiting for 3D view",
  "unavailable"=>"Snap connection needed",
  _=>"Snap faces"
 };
}
