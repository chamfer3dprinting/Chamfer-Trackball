namespace Chamfer.Trackball.Core;

// Explicit held intent makes a one-axis knob turn useful without claiming
// independent three-axis sensing from the mouse's X/Y reports.
public static class KnobTwist{
 public static float CameraDelta(float x,float y,string axis,bool reverse,float gain){
  if(!float.IsFinite(x)||!float.IsFinite(y)||!float.IsFinite(gain))throw new ArgumentException("Invalid twist movement.");
  if(axis is not "x" and not "y")throw new ArgumentException("Twist axis must be X or Y.",nameof(axis));
  float counts=axis=="y"?y:x;
  // Positive selected-axis movement is the initial clockwise convention.
  // Reverse lets the owner calibrate it to their physical knob direction.
  return Math.Clamp(counts*(reverse?.2f:-.2f)*Math.Clamp(gain,0,4),-1000,1000);
 }
}

public sealed class KnobSteps{
 public const float QuarterDelta=MathF.PI/2/.005f;
 // A nudge is a trigger, not a proportional 90-degree physical ball rotation.
 public const float NudgeCounts=8;
 public const float NudgeThreshold=NudgeCounts*.2f;
 public const int NudgePauseMs=220;
 float remainder,nudge;int nudgeDirection;long? lastNudge;bool nudgeMode;
 public bool Nudged=>nudgeMode&&nudgeDirection!=0;
 public float Progress=>nudgeMode?Math.Clamp(MathF.Abs(nudge)/NudgeThreshold,0,1):MathF.Abs(remainder)/QuarterDelta;
 public void Reset(){remainder=nudge=0;nudgeDirection=0;lastNudge=null;nudgeMode=false;}
 public int AddNudge(float delta,long now){
  if(!float.IsFinite(delta)){Reset();throw new ArgumentException("Invalid twist movement.");}
  nudgeMode=true;if(delta==0)return 0;
  if(lastNudge is long previous&&(now<previous||now-previous>=NudgePauseMs)){nudge=0;nudgeDirection=0;}
  lastNudge=now;int sign=Math.Sign(delta);
  if(nudgeDirection==sign){nudge=0;return 0;}
  nudge=Math.Sign(nudge)==sign?nudge+Math.Clamp(delta,-1000,1000):Math.Clamp(delta,-1000,1000);
  if(MathF.Abs(nudge)+.00001f<NudgeThreshold)return 0;
  nudge=0;nudgeDirection=sign;return sign;
 }
 public int Add(float delta){
  nudgeMode=false;
  if(!float.IsFinite(delta)){Reset();throw new ArgumentException("Invalid twist movement.");}
  remainder+=Math.Clamp(delta,-1000,1000);int steps=(int)(remainder/QuarterDelta);
  remainder-=steps*QuarterDelta;return steps;
 }
}
