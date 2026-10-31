using System.Numerics;
namespace Chamfer.Trackball.Core;
public sealed record CameraPose(Vector3 Eye,Vector3 Target,Vector3 Up,Vector3 Pivot,float VerticalFov,float OrthoHeight,int Width,int Height,bool Orthographic,Vector3 WorldUp=default){
    public Vector3 WorldUpAxis=>WorldUp.LengthSquared()>1e-12f?Vector3.Normalize(WorldUp):Vector3.UnitZ;
    public bool Valid=>Finite(Eye)&&Finite(Target)&&Finite(Up)&&Finite(Pivot)&&Vector3.DistanceSquared(Eye,Target)>1e-12f&&Up.LengthSquared()>1e-12f&&Vector3.Cross(Target-Eye,Up).LengthSquared()>1e-12f&&Height>0&&Width>0&&float.IsFinite(VerticalFov)&&VerticalFov>0&&VerticalFov<3.13f&&float.IsFinite(OrthoHeight)&&OrthoHeight>0;
    public static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
public static class CameraMath {
    static readonly Vector3[] FaceDirections=[Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ];
    static Vector3 Unit(Vector3 x)=>Vector3.Normalize(x);
    public static CameraPose Move(CameraPose c,NavMode mode,float dx,float dy,float wheel=0,bool vertical=false,float gain=1){
        if(!c.Valid||!float.IsFinite(dx)||!float.IsFinite(dy)||!float.IsFinite(wheel))throw new ArgumentException("Invalid camera or motion.");
        dx=Math.Clamp(dx,-1000,1000)*gain;dy=Math.Clamp(dy,-1000,1000)*gain;wheel=Math.Clamp(wheel,-1200,1200)*gain;
        var back=Unit(c.Eye-c.Target);var right=Unit(Vector3.Cross(c.Up,back));var up=Unit(Vector3.Cross(back,right));
        var depth=MathF.Max(1e-5f,MathF.Abs(Vector3.Dot(c.Eye-c.Pivot,back)));
        var pixel=(c.Orthographic?c.OrthoHeight:2*depth*MathF.Tan(c.VerticalFov/2))/c.Height;
        if(mode==NavMode.Roll){
            var twist=Quaternion.CreateFromAxisAngle(back,-dx*.005f);
            var pose=c with{Eye=c.Pivot+Vector3.Transform(c.Eye-c.Pivot,twist),Target=c.Pivot+Vector3.Transform(c.Target-c.Pivot,twist),Up=Unit(Vector3.Transform(up,twist))};
            return Zoom(pose,wheel*.0015f);
        }
        if(mode==NavMode.Orbit){
            var q=mode==NavMode.Roll?Quaternion.Identity:Quaternion.Normalize(Quaternion.CreateFromAxisAngle(up,-dx*.005f)*Quaternion.CreateFromAxisAngle(right,-dy*.005f));
            q=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(back,-wheel*.0015f)*q);
            return c with{Eye=c.Pivot+Vector3.Transform(c.Eye-c.Pivot,q),Target=c.Pivot+Vector3.Transform(c.Target-c.Pivot,q),Up=Unit(Vector3.Transform(up,q))};
        }
        if(mode==NavMode.Snap){
            var proposed=Move(c,NavMode.Orbit,dx*4,dy*4,0);
            return AlignFace(proposed,NearestFace(Unit(proposed.Eye-proposed.Target)),(int)MathF.Round(wheel/120));
        }
        if(mode==NavMode.Navigate){
            var step=(-right*dx+(vertical?up:back)*dy)*pixel;
            var e=c.Eye+step;var t=c.Target+step;
            if(wheel!=0){var q=Quaternion.CreateFromAxisAngle(c.WorldUpAxis,-wheel*.0015f);t=e+Vector3.Transform(t-e,q);up=Vector3.Transform(up,q);}
            return c with{Eye=e,Target=t,Up=up,Pivot=c.Pivot+step};
        }
        if(mode==NavMode.PanZoom){
            var move=(-right*dx+up*dy)*pixel;
            c=c with{Eye=c.Eye+move,Target=c.Target+move,Pivot=c.Pivot+move};return Zoom(c,wheel*.0015f);
        }
        return mode==NavMode.Zoom?Zoom(c,-dy*.008f+wheel*.0015f):c;
    }
    public static Vector3 NearestFace(Vector3 direction)=>FaceDirections.MaxBy(face=>Vector3.Dot(face,direction));
    public static CameraPose AlignFace(CameraPose c,Vector3 face,int quarterTurns=0){
        var back=Unit(c.Eye-c.Target);var right=Unit(Vector3.Cross(c.Up,back));var up=Unit(Vector3.Cross(back,right));
        var worldUp=c.WorldUpAxis;
        // Studio is Y-up; Blender and the fallback protocol are Z-up.
        var poleUp=MathF.Abs(worldUp.Z)>.9f?Vector3.UnitY:-Vector3.UnitZ;
        var reference=MathF.Abs(Vector3.Dot(face,worldUp))>.98f?poleUp*(Vector3.Dot(face,worldUp)>0?1:-1):worldUp;
        var wantedUp=Unit(reference-face*Vector3.Dot(reference,face));
        wantedUp=Unit(Vector3.Transform(wantedUp,Quaternion.CreateFromAxisAngle(face,-(quarterTurns%4)*MathF.PI/2)));
        Quaternion Basis(Vector3 b,Vector3 u){
            var r=Unit(Vector3.Cross(u,b));u=Unit(Vector3.Cross(b,r));
            return Quaternion.CreateFromRotationMatrix(new Matrix4x4(r.X,r.Y,r.Z,0,u.X,u.Y,u.Z,0,b.X,b.Y,b.Z,0,0,0,0,1));
        }
        var q=Quaternion.Normalize(Basis(face,wantedUp)*Quaternion.Conjugate(Basis(back,up)));
        return c with{Eye=c.Pivot+Vector3.Transform(c.Eye-c.Pivot,q),Target=c.Pivot+Vector3.Transform(c.Target-c.Pivot,q),Up=wantedUp};
    }
    static CameraPose Zoom(CameraPose c,float amount){
        var factor=MathF.Exp(Math.Clamp(-amount,-2,2));
        if(c.Orthographic){
            float height=Math.Clamp(c.OrthoHeight*factor,1e-6f,1e12f);factor=height/c.OrthoHeight;
            var back=Unit(c.Eye-c.Target);var offset=c.Target-c.Pivot;
            var move=(offset-back*Vector3.Dot(offset,back))*(factor-1);
            return c with{Eye=c.Eye+move,Target=c.Target+move,OrthoHeight=height};
        }
        // Keep view-space separation representable after prolonged close-up zoom.
        float magnitude=MathF.Max(c.Eye.Length(),MathF.Max(c.Target.Length(),c.Pivot.Length()));
        float distance=Vector3.Distance(c.Eye,c.Target),minimum=MathF.Max(1e-4f,magnitude*1e-6f);
        factor=Math.Clamp(factor,minimum/distance,MathF.Max(minimum/distance,1e12f/distance));
        return c with{Eye=c.Pivot+(c.Eye-c.Pivot)*factor,Target=c.Pivot+(c.Target-c.Pivot)*factor};
    }
}

public sealed class FaceSnap{
 CameraPose? virtualPose,lastAligned;Vector3 face;float wheelRemainder,turnRemainder;int quarterTurns;
 readonly FaceTurnGesture ballTurn=new();readonly Queue<CameraPose> headingHistory=new(4);bool turning;long lastMotion;
 public long Samples{get;private set;}
 public int TurnCount{get;private set;}
 public int LastTurnDirection{get;private set;}
 public long LastTurnAt{get;private set;}
 public bool Turning=>ballTurn.Curving;
 public int Orientation=>quarterTurns;
 public void Reset(CameraPose? pose=null){
  virtualPose=lastAligned=pose;face=pose==null?Vector3.Zero:CameraMath.NearestFace(Vector3.Normalize(pose.Eye-pose.Target));
  quarterTurns=0;wheelRemainder=turnRemainder=0;turning=false;ballTurn.Reset();headingHistory.Clear();lastMotion=0;Samples=0;TurnCount=LastTurnDirection=0;LastTurnAt=0;
  if(pose!=null&&Vector3.Dot(Vector3.Normalize(pose.Eye-pose.Target),face)>.999f){
   var canonical=CameraMath.AlignFace(pose,face).Up;
   var seen=Vector3.Normalize(pose.Up-face*Vector3.Dot(pose.Up,face));
   float angle=MathF.Atan2(Vector3.Dot(face,Vector3.Cross(canonical,seen)),Vector3.Dot(canonical,seen));
   quarterTurns=(int)MathF.Round(-angle/(MathF.PI/2));
  }
 }
 public CameraPose Step(CameraPose start,float x,float y,float wheel=0,float gain=1,long now=0){
  virtualPose??=start;if(face==Vector3.Zero)face=CameraMath.NearestFace(Vector3.Normalize(start.Eye-start.Target));
  long time=now==0?Environment.TickCount64:now;
  if(lastMotion!=0&&(time<lastMotion||time-lastMotion>100))headingHistory.Clear();
  if(x!=0||y!=0)lastMotion=time;
  var before=lastAligned??CameraMath.AlignFace(virtualPose,face,quarterTurns);
  if(x!=0||y!=0)Samples++;
  bool wasActive=ballTurn.Active;
  var dial=ballTurn.Add(x,y,time);
  bool candidate=ballTurn.Curving;
  if(candidate){
   // Freeze the current face while the ball curls, rather than navigating
   // through other faces before a deliberate dial gesture is recognized.
   if(!turning){
    // The first vector cannot reveal curvature. Keep the preceding aligned
    // face so a fast first segment cannot become the turn's new face.
    virtualPose=headingHistory.TryPeek(out var preceding)?preceding:before;headingHistory.Clear();face=CameraMath.NearestFace(Vector3.Normalize(virtualPose.Eye-virtualPose.Target));
    var canonical=CameraMath.AlignFace(virtualPose,face).Up;
    float angle=MathF.Atan2(Vector3.Dot(face,Vector3.Cross(canonical,virtualPose.Up)),Vector3.Dot(canonical,virtualPose.Up));
    quarterTurns=(int)MathF.Round(-angle/(MathF.PI/2));turning=true;
   }
   if(dial.Consumed){
    if(!wasActive){int turn=Math.Sign(dial.Wheel);quarterTurns-=turn;turnRemainder=0;ReportTurn(turn,time);}
    else{
     turnRemainder+=dial.Wheel;int turns=(int)(turnRemainder/480);
     quarterTurns-=turns;turnRemainder-=turns*480;if(turns!=0)ReportTurn(turns,time);
    }
   }
  }else{
   if(turning){virtualPose=lastAligned??virtualPose;turning=false;turnRemainder=0;}
   virtualPose=CameraMath.Move(virtualPose,NavMode.Orbit,x*4*gain,y*4*gain);
   var direction=Vector3.Normalize(virtualPose.Eye-virtualPose.Target);var next=CameraMath.NearestFace(direction);
   if(Vector3.Dot(next,direction)>Vector3.Dot(face,direction)+.08f){face=next;quarterTurns=0;}
  }
  wheelRemainder+=wheel;int wheelTurns=(int)(wheelRemainder/120);wheelRemainder-=wheelTurns*120;
  quarterTurns=(quarterTurns+wheelTurns)%4;
  lastAligned=CameraMath.AlignFace(virtualPose,face,quarterTurns);
  if(wheelTurns!=0){virtualPose=lastAligned;headingHistory.Clear();ballTurn.Reset();turning=false;turnRemainder=0;ReportTurn(-wheelTurns,time);}
  else if((x!=0||y!=0)&&!candidate){headingHistory.Enqueue(before);while(headingHistory.Count>4)headingHistory.Dequeue();}
  return lastAligned;
 }
 void ReportTurn(int turns,long now){if(turns==0)return;TurnCount+=Math.Abs(turns);LastTurnDirection=Math.Sign(turns);LastTurnAt=now;}

}
