using System.Numerics;
using System.Text.Json;
using Chamfer.Trackball.Core;
static class CameraOrientationChecks {
 static Vector3[] Faces=[Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ];
 static Vector2 Screen(CameraPose c,Vector3 p){var back=Vector3.Normalize(c.Eye-c.Target);var right=Vector3.Normalize(Vector3.Cross(c.Up,back));var up=Vector3.Cross(back,right);var v=p-c.Eye;float depth=-Vector3.Dot(v,back),h=2*depth*MathF.Tan(c.VerticalFov/2);return new(Vector3.Dot(v,right)/h,Vector3.Dot(v,up)/h);}
 static object Pose(CameraPose p)=>new{eye=new[]{p.Eye.X,p.Eye.Y,p.Eye.Z},target=new[]{p.Target.X,p.Target.Y,p.Target.Z},up=new[]{p.Up.X,p.Up.Y,p.Up.Z},pivot=new[]{p.Pivot.X,p.Pivot.Y,p.Pivot.Z},zoomPivot=new[]{p.Pivot.X,p.Pivot.Y,p.Pivot.Z},orthoHeight=p.OrthoHeight,orthographic=p.Orthographic};
 public static void Run(Action<bool,string> check){
  var root=new CameraPose(new(0,0,200),Vector3.Zero,Vector3.UnitY,new(15,-10,40),MathF.PI/4,160,1200,900,false,Vector3.UnitY,new(15,-10,40));
  foreach(var face in Faces)foreach(int quarter in new[]{0,1,2,3}){
   var start=CameraMath.AlignFace(root,face,quarter);var once=CameraMath.Move(start,NavMode.Orbit,100,60);var chunks=start;
   for(int i=0;i<10;i++)chunks=CameraMath.Move(chunks,NavMode.Orbit,10,6);
   check(Vector3.Distance(once.Eye,chunks.Eye)<.003&&Vector3.Distance(once.Up,chunks.Up)<.00002,"One diagonal roll has the same orientation in one or ten reports: "+face+" / "+quarter);
   var reversed=CameraMath.Move(once,NavMode.Orbit,-100,-60);
   check(Vector3.Distance(reversed.Eye,start.Eye)<.003&&Vector3.Distance(reversed.Up,start.Up)<.00002,"Reversing a diagonal roll restores its exact starting orientation: "+face+" / "+quarter);
   check(Vector2.Distance(Screen(start,start.Pivot),Screen(once,start.Pivot))<.00001,"A diagonal orbit keeps its off-centre screen pivot: "+face+" / "+quarter);
   foreach(var direction in new[]{new Vector2(50,0),new Vector2(-50,0),new Vector2(0,50),new Vector2(0,-50)}){
    var snap=new FaceSnap();snap.Reset(start);var next=start;
    for(int i=0;i<4;i++)next=snap.Step(start,direction.X,direction.Y,0,1,1000+i*200);
    check(Vector3.Distance(next.Eye,start.Eye)<.005&&Vector3.Distance(next.Up,start.Up)<.00003,"Four adjacent face changes preserve starting orientation: "+face+" / "+quarter+" / "+direction);
   }
  }
  var centre=root with{Pivot=Vector3.Zero,ZoomPivot=Vector3.Zero};
  var rightView=CameraMath.Move(centre,NavMode.Orbit,4,0);var downView=CameraMath.Move(centre,NavMode.Orbit,0,4);var marker=new Vector3(0,0,20);
  check(Screen(rightView,marker).X>Screen(centre,marker).X&&Screen(downView,marker).Y<Screen(centre,marker).Y,"Screen-right/down rolls move the visible model right/down, with camera-relative directions");
  var cases=new List<object>();
  foreach(var item in new[]{(Name:"diagonal",Mode:NavMode.Orbit),(Name:"pan",Mode:NavMode.PanZoom),(Name:"zoom",Mode:NavMode.Zoom)}){
   var pose=root;var poses=new List<object>();
   for(int i=0;i<60;i++){pose=CameraMath.Move(pose,item.Mode,item.Mode==NavMode.Zoom?0:2,item.Mode==NavMode.Zoom?-1:1);poses.Add(Pose(pose));}
   cases.Add(new{label=item.Name,mode=item.Mode.ToString(),start=Pose(root),poses});
  }
  var snapper=new FaceSnap();snapper.Reset(root);var snaps=new List<object>();var current=root;
  for(int i=0;i<8;i++){current=snapper.Step(root,0,50,0,1,1000+i*200);snaps.Add(Pose(current));}
  for(int i=0;i<4;i++){current=snapper.Step(root,0,0,120,1,3000+i*200);snaps.Add(Pose(current));}
  cases.Add(new{label="faces-and-quarter-turns",mode="Snap",start=Pose(root),poses=snaps});
  string path=Path.Combine(Path.GetTempPath(),"chamfer-orientation-trace.json");File.WriteAllText(path,JsonSerializer.Serialize(new{cases}));
  Console.WriteLine("Native camera orientation replay written for disposable Studio browser verification.");
 }
}
