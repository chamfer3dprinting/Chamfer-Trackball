using System.Numerics;
namespace Chamfer.Trackball.Core;

// Recognizes a deliberate closed roll path. This derives a gesture from X/Y;
// it does not measure a third physical rotation axis.
public sealed class CircleZoom {
    const float SharpTurnLimit=1.05f;
    const int MaxSamples=192;
    readonly List<Vector2> points=[];
    readonly List<float> turns=[];
    Vector2 position,sampled,previous,straightAnchor;float straightTravel;
    long lastTime;bool hasHeading;
    public bool Active {get;private set;}
    public void Reset(){points.Clear();turns.Clear();position=sampled=previous=Vector2.Zero;lastTime=0;straightTravel=0;straightAnchor=Vector2.Zero;hasHeading=Active=false;}
    public (bool Consumed,float Wheel) Add(float dx,float dy,long now,float maxDiameter=160){
        if(!float.IsFinite(dx)||!float.IsFinite(dy)||!float.IsFinite(maxDiameter)){Reset();return (false,0);}
        var delta=new Vector2(dx,dy);
        if(delta.LengthSquared()==0){
            if(lastTime!=0&&(now<lastTime||now-lastTime>700))Reset();
            return (Active,0);
        }
        if(lastTime!=0&&(now<lastTime||now-lastTime>700))Reset();
        lastTime=now;
        float diameter=Math.Clamp(maxDiameter,60,400);
        if(delta.Length()>diameter){Reset();return (false,0);}
        if(Active){
            var heading=Vector2.Normalize(delta);
            if(straightTravel==0||Vector2.Dot(heading,straightAnchor)<.995f){straightAnchor=heading;straightTravel=delta.Length();}
            else straightTravel+=delta.Length();
            if(straightTravel>=Math.Max(18,diameter*.18f)){Reset();return (false,0);}
        }
        position+=delta;
        float wheel=0;bool consumed=Active;
        float step=Math.Clamp(diameter/60f,3,8);
        while(Vector2.Distance(position,sampled)>=step){
            var direction=Vector2.Normalize(position-sampled);sampled+=direction*step;
            float turn=hasHeading?MathF.Atan2(previous.X*direction.Y-previous.Y*direction.X,Vector2.Dot(previous,direction)):0;
            previous=direction;hasHeading=true;
            points.Add(sampled);turns.Add(turn);if(points.Count>MaxSamples){points.RemoveAt(0);turns.RemoveAt(0);}
            if(Active){
                // A sharp kink, reversal or sustained straight roll returns control
                // immediately; no queued orbit motion is replayed after zoom.
                if(MathF.Abs(turn)>SharpTurnLimit){Reset();return (false,0);}
                if(HasStraightTail(Math.Max(30,diameter*.3f))){Reset();return (false,0);}
                wheel+=turn*(480/(2*MathF.PI));consumed=true;
            }else if(IsCircle(diameter,out var accumulatedTurn)){
                Active=true;straightTravel=0;consumed=true;
                // Apply the winding collected before recognition so a first circle
                // produces a visible zoom instead of only a tiny final-step change.
                wheel+=accumulatedTurn*(480/(2*MathF.PI));
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
    bool IsCircle(float maxDiameter,out float accumulatedTurn){
        accumulatedTurn=0;
        // Check short, imperfect loop suffixes using precomputed turns and prefix sums.
        int count=points.Count;if(count<10)return false;
        int end=count-1;
        Span<float> lengthPrefix=stackalloc float[count];
        Span<float> turnPrefix=stackalloc float[count];
        Span<float> absoluteTurnPrefix=stackalloc float[count];
        Span<int> sharpTurnPrefix=stackalloc int[count];
        lengthPrefix[0]=turnPrefix[0]=absoluteTurnPrefix[0]=0;
        sharpTurnPrefix[0]=0;
        for(int i=1;i<count;i++){
            float a=turns[i];
            lengthPrefix[i]=lengthPrefix[i-1]+Vector2.Distance(points[i],points[i-1]);
            turnPrefix[i]=turnPrefix[i-1]+a;
            absoluteTurnPrefix[i]=absoluteTurnPrefix[i-1]+MathF.Abs(a);
            sharpTurnPrefix[i]=sharpTurnPrefix[i-1]+(MathF.Abs(a)>SharpTurnLimit?1:0);
        }
        Vector2 lo=points[end],hi=points[end];
        for(int start=end;start>=0;start--){
            lo=Vector2.Min(lo,points[start]);hi=Vector2.Max(hi,points[start]);
            if(start>count-10)continue;
            float length=lengthPrefix[end]-lengthPrefix[start];
            float turn=turnPrefix[end]-turnPrefix[start+1];
            float absoluteTurn=absoluteTurnPrefix[end]-absoluteTurnPrefix[start+1];
            bool sharp=sharpTurnPrefix[end]!=sharpTurnPrefix[start+1];
            var extent=hi-lo;float widest=Math.Max(extent.X,extent.Y),narrowest=Math.Min(extent.X,extent.Y);
            var first=points[start];
            if(!sharp&&length>=40&&MathF.Abs(turn)>=4.8f&&MathF.Abs(turn)/Math.Max(absoluteTurn,.001f)>=.78f
                &&widest<=maxDiameter&&narrowest>=8&&narrowest/widest>=.35f
                &&Vector2.Distance(first,points[end])/length<=.38f){
                accumulatedTurn=turn;return true;
            }
        }
        return false;
    }
}
public static class BallNavigation {
    public static NavMode ToggleOrbitPan(NavMode current,NavMode ball)=> (current==NavMode.Idle?ball:current)==NavMode.PanZoom?NavMode.Orbit:NavMode.PanZoom;
}
