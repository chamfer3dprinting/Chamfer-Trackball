using System.Numerics;
using Chamfer.Trackball.Core;
static class CursorZoomChecks {
 static Vector2 Screen(CameraPose c,Vector3 p){
  var back=Vector3.Normalize(c.Eye-c.Target);var right=Vector3.Normalize(Vector3.Cross(c.Up,back));var up=Vector3.Normalize(Vector3.Cross(back,right));var relative=p-c.Eye;
  float span=c.Orthographic?c.OrthoHeight:2*-Vector3.Dot(relative,back)*MathF.Tan(c.VerticalFov/2);
  return new(Vector3.Dot(relative,right)/span,Vector3.Dot(relative,up)/span);
 }
 public static void Run(Action<bool,string> check){
  var camera=new CameraPose(new(5,3,12),new(1,1,0),Vector3.UnitY,new(-2,0,-1),.8f,14,1200,800,false,Vector3.UnitY,new(4,3,0));
  foreach(bool ortho in new[]{false,true}){
   var c=camera with{Orthographic=ortho};var anchor=c.ZoomPivot!.Value;var screen=Screen(c,anchor);
   foreach(float dy in new[]{-45f,45f}){
    var zoom=CameraMath.Move(c,NavMode.Zoom,0,dy);
    check(Vector2.Distance(Screen(zoom,anchor),screen)<.00001f,$"Cursor point stays fixed during {(ortho?"orthographic":"perspective")} zoom {dy}");
    check(zoom.Pivot==c.Pivot&&zoom.ZoomPivot==c.ZoomPivot,"Cursor zoom preserves the separate orbit pivot");
    check(dy<0?(ortho?zoom.OrthoHeight<c.OrthoHeight:Vector3.Distance(zoom.Eye,zoom.Target)<Vector3.Distance(c.Eye,c.Target)):(ortho?zoom.OrthoHeight>c.OrthoHeight:Vector3.Distance(zoom.Eye,zoom.Target)>Vector3.Distance(c.Eye,c.Target)),"Cursor zoom changes magnification in the requested direction");
    var reversed=CameraMath.Move(zoom,NavMode.Zoom,0,-dy);
    check(Vector3.Distance(reversed.Eye,c.Eye)<.0001f&&Vector3.Distance(reversed.Target,c.Target)<.0001f&&MathF.Abs(reversed.OrthoHeight-c.OrthoHeight)<.0001f,"Reversing cursor zoom returns to the original view");
   }
   var panned=CameraMath.Move(c,NavMode.PanZoom,23,-12);
   var combined=CameraMath.Move(c,NavMode.PanZoom,23,-12,120);
   check(Vector2.Distance(Screen(combined,combined.ZoomPivot!.Value),Screen(panned,panned.ZoomPivot!.Value))<.00001f,"Pan plus wheel zoom keeps the translated cursor point fixed");
   foreach(var mode in new[]{NavMode.Orbit,NavMode.Roll,NavMode.Navigate}){
    var moved=CameraMath.Move(c,mode,20,10,120);
    check(Vector2.Distance(Screen(moved,moved.ZoomPivot!.Value),screen)<.00002f,"Cursor anchor follows the camera basis during "+mode);
   }
   var aligned=CameraMath.AlignFace(c,Vector3.UnitX,1);
   check(Vector2.Distance(Screen(aligned,aligned.ZoomPivot!.Value),screen)<.00002f,"Face alignment preserves the cursor ray");
   var snap=new FaceSnap();snap.Reset(c);var snapped=snap.Step(c,60,0,120,1,1000);
   check(Vector2.Distance(Screen(snapped,snapped.ZoomPivot!.Value),screen)<.00002f,"Directional snap and quarter turn preserve the cursor ray");
   var old=c with{ZoomPivot=null};var explicitOld=old with{ZoomPivot=old.Pivot};
   var legacy=CameraMath.Move(old,NavMode.Zoom,0,30);var same=CameraMath.Move(explicitOld,NavMode.Zoom,0,30);
   check(Vector3.Distance(legacy.Eye,same.Eye)<.00001f&&Vector3.Distance(legacy.Target,same.Target)<.00001f&&legacy.OrthoHeight==same.OrthoHeight,"Older scene providers retain the original pivot behavior");
  }
  check(!(camera with{ZoomPivot=new(float.NaN,0,0)}).Valid,"A non-finite cursor zoom anchor is rejected");
  check(CameraMath.Move(camera with{ZoomPivot=null},NavMode.Orbit,24,17).Eye==CameraMath.Move(camera,NavMode.Orbit,24,17).Eye,"Cursor zoom does not change the orbit path");
 }
}
