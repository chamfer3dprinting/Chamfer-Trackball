namespace Chamfer.Trackball.Core;

public readonly record struct NavigationFrame(float Right,float Up,float Forward,float Yaw=0,float Pitch=0){
    public bool Active=>Right!=0||Up!=0||Forward!=0||Yaw!=0||Pitch!=0;
}
// The hold defines a virtual centre. Reversing back into its forgiving centre
// zone stops that command immediately; passing beyond it reverses the command.
public sealed class NavigationMotion {
    Axis right,up,forward;double vr,vu,vf,vyaw,vpitch,previous=double.NaN;
    struct Axis {
        public double Position;public bool Active;
        public void Add(double delta){
            Position=Math.Clamp(Position+delta,-40,40);
            if(Active&&Math.Abs(Position)<=3){Position=0;Active=false;}
            else if(!Active&&Math.Abs(Position)>=4)Active=true;
        }
        public readonly double Rate {
            get{if(!Active)return 0;double amount=Math.Clamp((Math.Abs(Position)-3)/37,0,1);return Math.Sign(Position)*Math.Pow(amount,1.25)*1600;}
        }
    }
    public bool Centered=>!right.Active&&!up.Active&&!forward.Active;
    static float Advance(ref double velocity,double command,double dt){
        if(command==0){velocity=0;return 0;}
        const double tau=.018;double decay=Math.Exp(-dt/tau);
        double travel=command*dt+(velocity-command)*tau*(1-decay);
        velocity=command+(velocity-command)*decay;return (float)travel;
    }
    public NavigationFrame Step(float dx,float dy,float scroll,double milliseconds,bool finish=false,bool coupledLook=false){
        if(!float.IsFinite(dx)||!float.IsFinite(dy)||!float.IsFinite(scroll)||!double.IsFinite(milliseconds))throw new ArgumentException("Invalid navigation motion.");
        if(finish){Reset();return default;}
        double dt=double.IsNaN(previous)?1.0/125:(milliseconds-previous)/1000;
        if(dt<=0||dt>.1){Reset();dt=1.0/125;}
        previous=milliseconds;
        right.Add(dx);up.Add(scroll/4);forward.Add(-dy);
        double steer=forward.Active?Math.Clamp((Math.Abs(forward.Position)-3)/5,0,1):0;
        return new(Advance(ref vr,right.Rate*(1-steer),dt),Advance(ref vu,up.Rate,dt),Advance(ref vf,forward.Rate,dt),Advance(ref vyaw,right.Rate*(coupledLook?1:steer),dt),Advance(ref vpitch,coupledLook?forward.Rate:0,dt));
    }
    public void Reset(){right=up=forward=default;vr=vu=vf=vyaw=vpitch=0;previous=double.NaN;}
}
