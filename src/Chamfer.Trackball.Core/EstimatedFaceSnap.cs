using System.Numerics;
namespace Chamfer.Trackball.Core;

// Experimental intent estimation from X/Y geometry. It cannot measure an
// independent twist axis; uncertain strokes are delivered as directional snaps.
public sealed class EstimatedFaceSnap{
 readonly Queue<TwistSample> recent=new(8);readonly FaceSnap faces=new();readonly KnobSteps turns=new();readonly List<(float X,float Y,long Time)> pending=new(16);
 CameraPose? pose;long began,lastMotion,faceDecisionAt,lastWheel;float along,cross,alongEnergy,crossEnergy,travel;int stage;bool turnedInBurst; // 0 new, 1 observing, 2 faces, 3 estimated twist
 public bool Pending=>stage==1;
 public bool EstimatedTwist=>stage==3;
 public int QuarterTurns{get;private set;}
 public long LastTurnAt{get;private set;}
 public int LastDirection{get;private set;}
 public bool LastEstimatedTurn{get;private set;}
 public long Samples{get;private set;}
 public float Progress=>turns.Progress;
 public string State=>stage switch{1=>"estimating",3=>"twist",_=>"faces"};
 public void Reset(CameraPose? initial=null){
  faces.Reset(initial);pose=initial==null?null:faces.Step(initial,0,0);turns.Reset();pending.Clear();stage=0;began=lastMotion=faceDecisionAt=lastWheel=0;recent.Clear();
  along=cross=alongEnergy=crossEnergy=travel=0;QuarterTurns=0;LastDirection=0;LastTurnAt=0;LastEstimatedTurn=false;Samples=0;
 }
 void Start(long now){
  stage=1;turnedInBurst=false;began=now;recent.Clear();along=cross=alongEnergy=crossEnergy=travel=0;pending.Clear();turns.Reset();faces.Reset(pose);
 }
 CameraPose Face(CameraPose start,float x,float y,float wheel,float gain,long now){
  int before=faces.TurnCount;pose=faces.Step(pose??start,x,y,wheel,gain,now);
  if(faces.TurnCount!=before){QuarterTurns+=faces.TurnCount-before;LastDirection=faces.LastTurnDirection;LastTurnAt=now;LastEstimatedTurn=false;}
  return pose;
 }
 void Twist(CameraPose start,float x,float y,float gain,string axis,bool reverse,long now,TwistCalibrationModel? calibration=null,float wheel=0){
  float delta=calibration?.CameraDelta(x,y,wheel,gain,reverse)??KnobTwist.CameraDelta(x,y,axis,reverse,gain);if(delta!=0)LastDirection=-Math.Sign(delta);int steps=turns.Add(delta);
  if(steps==0)return;
  pose=CameraMath.Move(pose??start,NavMode.Roll,(steps%4)*KnobSteps.QuarterDelta,0);
  turnedInBurst=true;LastEstimatedTurn=true;QuarterTurns+=Math.Abs(steps);LastDirection=-Math.Sign(steps);LastTurnAt=now;
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
  if(motion){recent.Enqueue(new(x,y,0,now));while(recent.Count>8)recent.Dequeue();}
  if(stage==3&&wheel!=0){stage=2;faceDecisionAt=now;turns.Reset();pending.Clear();faces.Reset(pose);return Face(start,x,y,wheel,faceGain,now);}
  if(stage==3){
   if(motion){along+=a;cross+=c;alongEnergy+=a*a;crossEnergy+=c*c;travel+=MathF.Sqrt(a*a+c*c);}
   bool uncertain=motion&&(calibration!=null?recent.Count>=4&&now-recent.Peek().Time>=64&&!calibration.Decide(recent.ToArray()).Twist:MathF.Abs(c)>MathF.Abs(a)*.5f&&MathF.Abs(c)>8||travel>160&&(crossEnergy>alongEnergy*.012f||MathF.Abs(cross)>MathF.Abs(along)*.07f));
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
   bool retry=motion&&wheel==0&&now-faceDecisionAt>=160&&MathF.Abs(a)>=4&&MathF.Abs(c)<=Math.Max(2,MathF.Abs(a)*(calibration!=null?.5f:.08f));
   if(!retry)return Face(start,x,y,wheel,faceGain,now);
   Start(now);
  }
  if(motion){
   pending.Add((x,y,now));along+=a;cross+=c;alongEnergy+=a*a;crossEnergy+=c*c;travel+=MathF.Sqrt(a*a+c*c);
  }
  if(stage==0)return wheel==0?pose:Face(start,0,0,wheel,faceGain,now);
  bool clearlyDirectional=wheel!=0||pending.Count>=3&&(calibration!=null?MathF.Abs(cross)>Math.Max(8,MathF.Abs(along)*.75f):MathF.Abs(cross)>Math.Max(8,MathF.Abs(along)*.12f)||crossEnergy>alongEnergy*.025f);
  bool ready=now-began>=112&&pending.Count>=4||now-began>=160||pending.Count>=16;
  if(!clearlyDirectional&&!ready)return pose;
  bool knob=!clearlyDirectional&&(calibration!=null?calibration.Decide(pending.Select(p=>new TwistSample(p.X,p.Y,0,p.Time)).ToArray()).Twist:travel>=60&&MathF.Abs(along)/Math.Max(travel,.001f)>=.97f
      &&MathF.Abs(cross)<=Math.Max(2,MathF.Abs(along)*.045f)&&crossEnergy<=alongEnergy*.0065f);
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
