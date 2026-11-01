using System.Numerics;
using Chamfer.Trackball.Core;

static class TinyKnobChecks{
 public static void Run(Action<bool,string> check){
  var start=new CameraPose(new(0,0,10),Vector3.Zero,Vector3.UnitY,Vector3.Zero,.8f,10,800,800,false,Vector3.UnitY);
  EstimatedFaceSnap Fresh(){var input=new EstimatedFaceSnap();input.Reset(start);return input;}
  foreach(int sign in new[]{1,-1}){
   var one=Fresh();one.Step(start,sign*8,0,0,.2f,1,"x",false,1000);
   var pose=one.Step(start,0,0,0,.2f,1,"x",false,1032);
   check(one.QuarterTurns==1&&one.LastDirection==sign,"A single tiny "+(sign>0?"clockwise":"anticlockwise")+" packet triggers without requiring four movement frames");
   check(Vector3.Dot(Vector3.Normalize(pose.Eye-pose.Target),Vector3.UnitZ)>.999f&&Math.Abs(Vector3.Dot(pose.Up,Vector3.UnitY))<.001f&&Math.Abs(Vector3.Distance(pose.Eye,pose.Target)-10)<.001f,"A tiny nudge changes only face orientation by one exact quarter turn");
  }
  var quantized=Fresh();
  for(int n=0;n<4;n++)quantized.Step(start,2,n%2==0?1:-1,0,.2f,1,"x",false,1000+n*16);
  check(quantized.QuarterTurns==1,"A tiny knob turn tolerates alternating one-count cross-axis quantization");
  var drift=Fresh();drift.Step(start,8,2,0,.2f,1,"x",false,1000);drift.Step(start,0,0,0,.2f,1,"x",false,1032);
  check(drift.QuarterTurns==1,"A tiny knob turn can include two cross-axis counts instead of requiring a perfect line");
  var slow=Fresh();for(int n=0;n<8;n++)slow.Step(start,1,0,0,.2f,1,"x",false,1000+n*16);
  check(slow.QuarterTurns==1,"Eight one-count reports trigger the first turn without discarding the beginning");
  var reverse=Fresh();reverse.Step(start,8,0,0,.2f,1,"x",false,1000);reverse.Step(start,0,0,0,.2f,1,"x",false,1032);
  var reversed=reverse.Step(start,-8,0,0,.2f,1,"x",false,1080);
  check(reverse.QuarterTurns==2&&reverse.LastDirection==-1&&Vector3.Dot(reversed.Up,Vector3.UnitY)>.999f,"A tiny reverse immediately turns back without old clockwise evidence blocking it");
  for(int n=0;n<40;n++)reverse.Step(start,-8,0,0,.2f,1,"x",false,1096+n*16);
  check(reverse.QuarterTurns==2,"A continuing tiny stroke stays latched to one output");
  reverse.Step(start,-8,0,0,.2f,1,"x",false,2100);
  check(reverse.QuarterTurns==3,"A short pause rearms a tiny same-direction nudge");

  var afterFaces=Fresh();var face=start;
  for(int n=0;n<25;n++)face=afterFaces.Step(start,0,40,0,.2f,1,"x",false,1000+n*16);
  var faceDirection=Vector3.Normalize(face.Eye-face.Target);var faceUp=face.Up;int before=afterFaces.QuarterTurns;
  for(int n=0;n<8;n++)face=afterFaces.Step(start,1,0,0,.2f,1,"x",false,1400+n*16);
  check(afterFaces.QuarterTurns==before+1&&afterFaces.EstimatedTwist,"A complete eight-count knob nudge is reconsidered after face rolls in the same Alt hold");
  check(Vector3.Dot(Vector3.Normalize(face.Eye-face.Target),faceDirection)>.999f&&Math.Abs(Vector3.Dot(face.Up,faceUp))<.001f,"Reconsidering a tiny nudge keeps the selected cube face");
  for(int n=0;n<12;n++)face=afterFaces.Step(start,0,40,0,.2f,1,"x",false,1544+n*16);
  check(!afterFaces.EstimatedTwist&&Vector3.Dot(Vector3.Normalize(face.Eye-face.Target),faceDirection)<.5f,"Directional up/down face selection resumes after a tiny orientation turn");

  var noise=Fresh();for(int n=0;n<80;n++)noise.Step(start,n%2==0?1:-1,0,0,.2f,1,"x",false,1000+n*16);
  noise.Step(start,0,0,0,.2f,1,"x",false,3000);
  check(noise.QuarterTurns==0&&!noise.Pending,"Alternating one-count sensor noise does not become a tiny turn");
  var weak=Fresh();weak.Step(start,4,0,0,.2f,1,"x",false,1000);weak.Step(start,0,0,0,.2f,1,"x",false,1800);
  check(weak.QuarterTurns==0&&!weak.Pending,"A stopped sub-trigger micro movement resolves without a phantom turn");
  var zero=Fresh();zero.Step(start,8,0,0,.2f,0,"x",false,1000);zero.Step(start,0,0,0,.2f,0,"x",false,1032);
  check(zero.QuarterTurns==0,"Zero saved Twist speed still disables the tiny-turn output");
  var verticalAxis=Fresh();verticalAxis.Step(start,0,8,0,.2f,1,"y",false,1000);verticalAxis.Step(start,0,0,0,.2f,1,"y",false,1032);
  check(verticalAxis.QuarterTurns==1,"The saved vertical knob axis also accepts a tiny one-packet turn");
  var inverted=Fresh();inverted.Step(start,8,0,0,.2f,1,"x",true,1000);inverted.Step(start,0,0,0,.2f,1,"x",true,1032);
  check(inverted.QuarterTurns==1&&inverted.LastDirection==-1,"Reverse Twist still controls the tiny-turn direction");

  var model=TwistCalibrationChecks.Model();model.QuarterCounts=32000;
  var shortCw=new[]{new TwistSample(4,2,0,1000),new TwistSample(4,2,0,1016)};
  check(model.DecideNudge(shortCw).Twist,"An older long calibration recognizes tiny bursts by shape rather than recorded speed");
  check(!model.DecideNudge(new[]{new TwistSample(8,0,0,1000)}).Twist,"Learned normal horizontal face rolls remain distinct from a learned diagonal knob turn");
  var calibrated=Fresh();calibrated.Step(start,4,2,0,.2f,1,"x",false,1000,model);calibrated.Step(start,4,2,0,.2f,1,"x",false,1016,model);calibrated.Step(start,0,0,0,.2f,1,"x",false,1048,model);
  check(calibrated.QuarterTurns==1,"Tiny short bursts also work through saved calibration in the actual estimator");
  calibrated.Step(start,-4,-2,0,.2f,1,"x",false,1080,model);calibrated.Step(start,-4,-2,0,.2f,1,"x",false,1096,model);
  check(calibrated.QuarterTurns==2&&calibrated.LastDirection==-1,"A calibrated tiny reverse produces the opposite quarter turn");
  var overlap=TwistCalibrationModel.Train(TwistCalibrationChecks.Knob(1).Concat(TwistCalibrationChecks.Knob(-1)).ToArray(),TwistCalibrationChecks.Knob(1),TwistCalibrationChecks.Knob(-1));
  check(!overlap.DecideNudge(shortCw).Twist,"Identical learned face and knob shapes still report uncertainty for tiny turns");
 }
}
