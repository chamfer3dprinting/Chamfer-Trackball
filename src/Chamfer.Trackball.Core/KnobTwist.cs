namespace Chamfer.Trackball.Core;

// Explicit held intent makes a one-axis knob turn useful without claiming
// independent three-axis sensing from the mouse's X/Y reports.
public static class KnobTwist{
 public static float CameraDelta(float x,float y,string axis,bool reverse,float gain){
  if(!float.IsFinite(x)||!float.IsFinite(y)||!float.IsFinite(gain))throw new ArgumentException("Invalid twist movement.");
  if(axis is not "x" and not "y")throw new ArgumentException("Twist axis must be X or Y.",nameof(axis));
  float counts=axis=="y"?y:x;
  // The owner's clockwise probe projects onto positive X; camera roll uses
  // the opposite sign so the model turns clockwise on screen.
  return Math.Clamp(counts*(reverse?.2f:-.2f)*Math.Clamp(gain,0,4),-1000,1000);
 }
}
