using System.Numerics;
namespace Chamfer.Trackball.Core;

// Recognizes a deliberate closed roll path. This derives a gesture from X/Y;
// it does not measure a third physical rotation axis.
public sealed class CircleZoom {
    readonly List<Vector2> points=[];
    Vector2 position,sampled,previous,straightAnchor;float straightTravel;
    long lastTime;bool hasHeading;
    public bool Active {get;private set;}
    public void Reset(){points.Clear();position=sampled=previous=Vector2.Zero;lastTime=0;straightTravel=0;straightAnchor=Vector2.Zero;hasHeading=Active=false;}
    public (bool Consumed,float Wheel) Add(float dx,float dy,long now,float maxDiameter=160){
        if(!float.IsFinite(dx)||!float.IsFinite(dy)||!float.IsFinite(maxDiameter)){Reset();return (false,0);}
        if(lastTime!=0&&(now<lastTime||now-lastTime>300))Reset();
        lastTime=now;
        var delta=new Vector2(dx,dy);if(delta.LengthSquared()==0)return (Active,0);
        if(delta.Length()>Math.Clamp(maxDiameter,60,400)){Reset();return (false,0);}
        if(Active){
            var heading=Vector2.Normalize(delta);
            if(straightTravel==0||Vector2.Dot(heading,straightAnchor)<.995f){straightAnchor=heading;straightTravel=delta.Length();}
            else straightTravel+=delta.Length();
            if(straightTravel>=Math.Max(18,maxDiameter*.18f)){Reset();return (false,0);}
        }
        position+=delta;
        float wheel=0;bool consumed=Active;
        const float step=6;
        while(Vector2.Distance(position,sampled)>=step){
            var direction=Vector2.Normalize(position-sampled);sampled+=direction*step;
            float turn=hasHeading?MathF.Atan2(previous.X*direction.Y-previous.Y*direction.X,Vector2.Dot(previous,direction)):0;
            previous=direction;hasHeading=true;
            points.Add(sampled);if(points.Count>96)points.RemoveAt(0);
            if(Active){
                // A sharp kink, reversal or sustained straight roll returns control
                // immediately; no queued orbit motion is replayed after zoom.
                if(MathF.Abs(turn)>1.05f){Reset();return (false,0);}
                if(HasStraightTail(Math.Max(30,maxDiameter*.3f))){Reset();return (false,0);}
                wheel+=turn*(480/(2*MathF.PI));consumed=true;
            }else if(IsCircle(Math.Clamp(maxDiameter,60,400))){
                Active=true;straightTravel=0;consumed=true;
                wheel+=turn*(480/(2*MathF.PI));
            }
        }
        return (consumed,wheel);
    }
    bool HasStraightTail(float minimumLength){
        int end=points.Count-1,start=end;float length=0;
        while(start>0&&length<minimumLength){length+=Vector2.Distance(points[start],points[start-1]);start--;}
        if(length<minimumLength)return false;
        var axis=points[end]-points[start];float distance=axis.Length();if(distance/length<.985f)return false;
        for(int i=start+1;i<end;i++){var v=points[i]-points[start];if(MathF.Abs(axis.X*v.Y-axis.Y*v.X)/distance>2)return false;}
        return true;
    }
    bool IsCircle(float maxDiameter){
        // Inspect recent suffixes so a straight approach to a circle cannot prevent
        // recognition. Require roughly four fifths of a turn, consistent winding,
        // a small bounding box and an almost closed, two-dimensional path.
        for(int start=Math.Max(0,points.Count-96);start<=points.Count-12;start++){
            var first=points[start];Vector2 lo=first,hi=first,prior=Vector2.Zero;
            float length=0,turn=0,absoluteTurn=0;bool heading=false,sharp=false;
            for(int i=start+1;i<points.Count;i++){
                var segment=points[i]-points[i-1];float size=segment.Length();if(size<.01f)continue;
                var direction=segment/size;length+=size;lo=Vector2.Min(lo,points[i]);hi=Vector2.Max(hi,points[i]);
                if(heading){float a=MathF.Atan2(prior.X*direction.Y-prior.Y*direction.X,Vector2.Dot(prior,direction));turn+=a;absoluteTurn+=MathF.Abs(a);if(MathF.Abs(a)>1.05f)sharp=true;}
                prior=direction;heading=true;
            }
            var extent=hi-lo;float widest=Math.Max(extent.X,extent.Y),narrowest=Math.Min(extent.X,extent.Y);
            if(!sharp&&length>=60&&MathF.Abs(turn)>=5.0f&&MathF.Abs(turn)/Math.Max(absoluteTurn,.001f)>=.90f
                &&widest<=maxDiameter&&narrowest>=12&&narrowest/widest>=.5f
                &&Vector2.Distance(first,points[^1])/length<=.23f)return true;
        }
        return false;
    }
}
public static class BallNavigation {
    public static NavMode ToggleOrbitPan(NavMode current,NavMode ball)=> (current==NavMode.Idle?ball:current)==NavMode.PanZoom?NavMode.Orbit:NavMode.PanZoom;
}
