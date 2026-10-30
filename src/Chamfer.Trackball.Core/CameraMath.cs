using System.Numerics;
namespace Chamfer.Trackball.Core;
public sealed record CameraPose(Vector3 Eye,Vector3 Target,Vector3 Up,Vector3 Pivot,float VerticalFov,float OrthoHeight,int Width,int Height,bool Orthographic){
    public bool Valid=>Finite(Eye)&&Finite(Target)&&Finite(Up)&&Finite(Pivot)&&Vector3.DistanceSquared(Eye,Target)>1e-12f&&Up.LengthSquared()>1e-12f&&Vector3.Cross(Target-Eye,Up).LengthSquared()>1e-12f&&Height>0&&Width>0&&float.IsFinite(VerticalFov)&&VerticalFov>0&&VerticalFov<3.13f&&float.IsFinite(OrthoHeight)&&OrthoHeight>0;
    public static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
}
public static class CameraMath {
    static Vector3 Unit(Vector3 x)=>Vector3.Normalize(x);
    public static CameraPose Move(CameraPose c,NavMode mode,float dx,float dy,float wheel=0,bool vertical=false,float gain=1){
        if(!c.Valid||!float.IsFinite(dx)||!float.IsFinite(dy)||!float.IsFinite(wheel))throw new ArgumentException("Invalid camera or motion.");
        dx=Math.Clamp(dx,-1000,1000)*gain;dy=Math.Clamp(dy,-1000,1000)*gain;wheel=Math.Clamp(wheel,-1200,1200)*gain;
        var back=Unit(c.Eye-c.Target);var right=Unit(Vector3.Cross(c.Up,back));var up=Unit(Vector3.Cross(back,right));
        var depth=MathF.Max(1e-5f,MathF.Abs(Vector3.Dot(c.Eye-c.Pivot,back)));
        var pixel=(c.Orthographic?c.OrthoHeight:2*depth*MathF.Tan(c.VerticalFov/2))/c.Height;
        if(mode is NavMode.Orbit or NavMode.Roll){
            var q=mode==NavMode.Roll?Quaternion.Identity:Quaternion.Normalize(Quaternion.CreateFromAxisAngle(up,-dx*.005f)*Quaternion.CreateFromAxisAngle(right,-dy*.005f));
            q=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(back,-wheel*.0015f)*q);
            return c with{Eye=c.Pivot+Vector3.Transform(c.Eye-c.Pivot,q),Target=c.Pivot+Vector3.Transform(c.Target-c.Pivot,q),Up=Unit(Vector3.Transform(up,q))};
        }
        if(mode==NavMode.Snap){
            var proposed=Move(c,NavMode.Orbit,dx*4,dy*4,0);
            var dir=Unit(proposed.Eye-c.Pivot);var best=Vector3.UnitZ;float score=-2;
            for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++)for(int z=-1;z<=1;z++){
                if(x==0&&y==0&&z==0)continue;var d=Unit(new Vector3(x,y,z));var dot=Vector3.Dot(d,dir);
                if(dot>score){score=dot;best=d;}
            }
            var distance=Vector3.Distance(c.Eye,c.Pivot);
            var worldUp=MathF.Abs(Vector3.Dot(best,Vector3.UnitZ))>.98f?Vector3.UnitY:Vector3.UnitZ;
            return c with{Eye=c.Pivot+best*distance,Target=c.Pivot,Up=Unit(worldUp-best*Vector3.Dot(worldUp,best))};
        }
        if(mode==NavMode.Navigate){
            var step=(-right*dx+(vertical?up:back)*dy)*pixel;
            var e=c.Eye+step;var t=c.Target+step;
            if(wheel!=0){var q=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-wheel*.0015f);t=e+Vector3.Transform(t-e,q);up=Vector3.Transform(up,q);}
            return c with{Eye=e,Target=t,Up=up,Pivot=c.Pivot+step};
        }
        if(mode==NavMode.PanZoom){
            var move=(-right*dx+up*dy)*pixel;
            c=c with{Eye=c.Eye+move,Target=c.Target+move};return Zoom(c,wheel*.0015f);
        }
        return mode==NavMode.Zoom?Zoom(c,-dy*.008f+wheel*.0015f):c;
    }
    static CameraPose Zoom(CameraPose c,float amount){
        var factor=MathF.Exp(Math.Clamp(-amount,-2,2));
        if(c.Orthographic)return c with{OrthoHeight=Math.Clamp(c.OrthoHeight*factor,1e-6f,1e12f)};
        return c with{Eye=c.Pivot+(c.Eye-c.Pivot)*factor,Target=c.Pivot+(c.Target-c.Pivot)*factor};
    }
}
