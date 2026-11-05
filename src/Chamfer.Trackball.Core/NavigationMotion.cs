namespace Chamfer.Trackball.Core;

public readonly record struct NavigationFrame(float Right,float Up,float Forward){
    public bool Active=>Right!=0||Up!=0||Forward!=0;
}
// A held navigation key turns the ball into a bounded three-axis velocity
// control. Stop/reset clears the command as well as the response immediately.
public sealed class NavigationMotion {
    double right,up,forward,vr,vu,vf,previous=double.NaN;
    static double Target(double value){
        double amount=Math.Clamp((Math.Abs(value)-1)/39,0,1);
        return Math.Sign(value)*Math.Pow(amount,1.25)*1600;
    }
    static float Advance(ref double velocity,double command,double dt){
        const double tau=.018;double decay=Math.Exp(-dt/tau);
        double travel=command*dt+(velocity-command)*tau*(1-decay);
        velocity=command+(velocity-command)*decay;
        return (float)travel;
    }
    public NavigationFrame Step(float dx,float dy,float scroll,double milliseconds,bool finish=false){
        if(!float.IsFinite(dx)||!float.IsFinite(dy)||!float.IsFinite(scroll)||!double.IsFinite(milliseconds))throw new ArgumentException("Invalid navigation motion.");
        if(finish){Reset();return default;}
        double dt=double.IsNaN(previous)?1.0/125:(milliseconds-previous)/1000;
        if(dt<=0||dt>.1){Reset();dt=1.0/125;}
        previous=milliseconds;
        right=Math.Clamp(right+dx,-40,40);up=Math.Clamp(up+scroll/4,-40,40);forward=Math.Clamp(forward-dy,-40,40);
        return new(Advance(ref vr,Target(right),dt),Advance(ref vu,Target(up),dt),Advance(ref vf,Target(forward),dt));
    }
    public void Reset(){right=up=forward=vr=vu=vf=0;previous=double.NaN;}
}
