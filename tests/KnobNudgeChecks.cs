using System.Numerics;
using Chamfer.Trackball.Core;

static class KnobNudgeChecks{
 public static void Run(Action<bool,string> check){
  var steps=new KnobSteps();
  float small=KnobTwist.CameraDelta(15,0,"x",false,1);
  check(steps.AddNudge(small,1000)==0&&Math.Abs(steps.Progress-.5f)<.001,"Half a short nudge shows trigger progress without rotating");
  check(steps.AddNudge(small,1016)==-1&&steps.Progress==0,"Thirty raw counts trigger one complete clockwise 90-degree snap instead of requiring a full physical turn");
  int continued=0;for(int n=0;n<100;n++)continued+=Math.Abs(steps.AddNudge(-20,1032+n*16));
  check(continued==0,"Continuing the same knob stroke cannot emit repeated quarter turns");
  check(steps.AddNudge(2,2640)==0&&steps.AddNudge(-2,2656)==0,"A small reverse wobble followed by the original direction does not retrigger");
  check(steps.AddNudge(6,2672)==1,"A deliberate opposite nudge immediately triggers one anticlockwise quarter turn");
  check(steps.AddNudge(20,2688)==0,"Continuing the opposite stroke stays latched");
  check(steps.AddNudge(6,2688+KnobSteps.NudgePauseMs)==1,"A brief stop rearms another nudge without releasing the held key");
  steps.Reset();check(steps.AddNudge(-1000,1000)==-1&&steps.AddNudge(-1000,1016)==0,"A large packet still means exactly one snap per stroke");
  steps.Reset();steps.AddNudge(-3,1000);steps.AddNudge(0,1300);
  check(steps.AddNudge(-3,1310)==0&&steps.AddNudge(-3,1326)==-1,"Idle timer frames cannot trigger motion and separated half-strokes do not combine");
  steps.Reset();int jitter=0;for(int n=0;n<200;n++)jitter+=Math.Abs(steps.AddNudge(n%2==0?.2f:-.2f,1000+n*16));
  check(jitter==0,"Alternating sensor jitter cannot accumulate into a snap");
  steps.Reset();check(steps.AddNudge(KnobTwist.CameraDelta(100,0,"x",false,0),1000)==0,"Zero Twist sensitivity still stops nudge-triggered rotation");
  steps.Reset();check(steps.AddNudge(KnobTwist.CameraDelta(30,0,"x",true,1),1000)==1,"Reverse twist still reverses a short nudge");
  steps.Reset();steps.AddNudge(-6,1000);steps.Reset();check(steps.AddNudge(-6,1016)==-1,"Releasing and reholding Twist clears the gesture latch");

  var start=new CameraPose(new(0,0,10),Vector3.Zero,Vector3.UnitY,Vector3.Zero,.8f,10,800,800,false,Vector3.UnitY);
  var estimated=new EstimatedFaceSnap();estimated.Reset(start);var pose=start;
  for(int n=0;n<8;n++)pose=estimated.Step(start,8,0,0,.2f,1,"x",false,1000+n*16);
  check(estimated.QuarterTurns==1&&estimated.LastDirection==1&&estimated.LastEstimatedTurn,"A short recognized Alt-only knob nudge triggers an exact clockwise quarter turn");
  check(Vector3.Dot(Vector3.Normalize(pose.Eye-pose.Target),Vector3.UnitZ)>.999f&&Math.Abs(Vector3.Dot(pose.Up,Vector3.UnitY))<.001&&Math.Abs(Vector3.Distance(pose.Eye,pose.Target)-10)<.001,"The short trigger keeps the cube face and distance while changing orientation by exactly 90 degrees");
  for(int n=8;n<40;n++)pose=estimated.Step(start,8,0,0,.2f,1,"x",false,1000+n*16);
  check(estimated.QuarterTurns==1,"Alt-only estimation also emits only one quarter turn for a continuous stroke");
  for(int n=0;n<8;n++)pose=estimated.Step(start,-8,0,0,.2f,1,"x",false,1700+n*16);
  check(estimated.QuarterTurns==2&&estimated.LastDirection==-1&&Vector3.Dot(pose.Up,Vector3.UnitY)>.999f,"A short anticlockwise nudge returns the face orientation by exactly one step");

  var model=TwistCalibrationChecks.Model();model.QuarterCounts=32000;model.Validate();
  var calibrated=new KnobSteps();float projected=model.NudgeDelta(model.AxisX*30,model.AxisY*30,0,1,false);
  check(calibrated.AddNudge(projected,1000)==-1,"An old calibration with a long recorded turn distance still responds to a small physical nudge");
  check(model.NudgeDelta(20,10,0,.5f,false)==model.NudgeDelta(20,10,0,1,false)*.5f&&model.NudgeDelta(20,10,0,0,false)==0,"Calibrated nudges retain the saved Twist sensitivity");
  check(model.NudgeDelta(20,10,0,1,true)==-model.NudgeDelta(20,10,0,1,false),"Calibrated nudge direction inversion is preserved");

  var session=new TwistCalibrationSession();session.Open();
  void Record(string kind,TwistSample[] values,long now){session.Record(kind,now);foreach(var s in values)session.Tick(s.X,s.Y,s.Wheel,now+600+s.Time-1000);session.Tick(0,0,0,now+5600);}
  Record("faces",TwistCalibrationChecks.Faces(),1000);Record("clockwise",TwistCalibrationChecks.Knob(1),7000);Record("anticlockwise",TwistCalibrationChecks.Knob(-1),13000);
  session.Test();for(int n=0;n<8;n++)session.Tick(8,4,0,20000+n*16);
  check(session.State(20200)["testTurns"]!.GetValue<int>()==1,"The calibration preview uses the same short-nudge trigger as the real 3D view");
 }
}
