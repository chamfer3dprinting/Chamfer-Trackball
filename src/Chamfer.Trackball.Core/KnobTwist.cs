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
 float remainder;
 public float Progress=>MathF.Abs(remainder)/QuarterDelta;
 public void Reset()=>remainder=0;
 public int Add(float delta){
  if(!float.IsFinite(delta)){Reset();throw new ArgumentException("Invalid twist movement.");}
  remainder+=Math.Clamp(delta,-1000,1000);int steps=(int)(remainder/QuarterDelta);
  remainder-=steps*QuarterDelta;return steps;
 }
}
