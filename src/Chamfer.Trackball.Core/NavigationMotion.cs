namespace Chamfer.Trackball.Core;

public readonly record struct NavigationFrame(float Right,float Up,float Forward,float Yaw=0,float Pitch=0){
    public bool Active=>Right!=0||Up!=0||Forward!=0||Yaw!=0||Pitch!=0;
}
// The hold defines a virtual centre. Reversing back into its forgiving centre
// zone stops that command immediately; passing beyond it reverses the command.
public sealed class NavigationMotion {
    NavigationLayer layer;Axis right,up,forward,turn;double vr,vu,vf,vyaw,vpitch,previous=double.NaN;
    struct Axis {
        public double Position;public bool Active;
        public void Add(double delta){
            // Limit speed in Rate. Keeping the full displacement makes an
            // equal return stroke reach centre even after speed has saturated.
            Position += delta;
            if(Active&&Math.Abs(Position)<=3){Position=0;Active=false;}
            else if(!Active&&Math.Abs(Position)>=4)Active=true;
        }
        public readonly double Rate {
            get{if(!Active)return 0;double amount=Math.Clamp((Math.Abs(Position)-3)/37,0,1);return Math.Sign(Position)*Math.Pow(amount,1.25)*1600;}
        }
    }
    public void ChangeLayer(NavigationLayer next){
        if(next==layer)return;
        // Each layer owns its height command. Keep the horizontal and forward
        // travel axes active when starting or ending a look.
        up=turn=default;
        vu=vyaw=vpitch=0;
        layer=next;
    }
    const float ScrollToMotion=.25f;
    // Heading follows the owner's observed twist direction in the viewport.
    static float TwistHeading(float scroll)=>-scroll*ScrollToMotion;
    public static NavigationFrame Direct(float dx,float dy,float scroll,NavigationLayer layer,bool ballTwist=true)=>layer switch{
        NavigationLayer.Look=>new(0,scroll*ScrollToMotion,0,dx,-dy),
        NavigationLayer.Height=>new(dx,-dy,0,ballTwist?TwistHeading(scroll):scroll*ScrollToMotion),
        _=>new(dx,0,-dy,TwistHeading(scroll))
    };
    public bool Centered=>!right.Active&&!up.Active&&!forward.Active&&!turn.Active;
    static float Advance(ref double velocity,double command,double dt){
        if(command==0){velocity=0;return 0;}
        const double tau=.018;double decay=Math.Exp(-dt/tau);
        double travel=command*dt+(velocity-command)*tau*(1-decay);
        velocity=command+(velocity-command)*decay;return (float)travel;
    }
    public NavigationFrame Step(float dx,float dy,float scroll,double milliseconds,bool finish=false,bool coupledLook=false,bool ballTwist=false,NavigationLayer control=NavigationLayer.Travel){
        if(!float.IsFinite(dx)||!float.IsFinite(dy)||!float.IsFinite(scroll)||!double.IsFinite(milliseconds))throw new ArgumentException("Invalid navigation motion.");
        if(finish){Reset();return default;}
        double dt=double.IsNaN(previous)?1.0/125:(milliseconds-previous)/1000;
        if(dt<=0||dt>.1){Reset();dt=1.0/125;}
        previous=milliseconds;
        ChangeLayer(control);
        if(control==NavigationLayer.Look){
            // A full notch chooses the previous one-notch climb speed. Any
            // opposite report selects the other direction instead of centring.
            if(scroll!=0){
                double position=Math.Sign(scroll)*(120*ScrollToMotion);
                if(up.Position!=position)vu=0;
                up=new Axis{Position=position,Active=true};
            }
            return new(Advance(ref vr,right.Rate,dt),Advance(ref vu,up.Rate,dt),
                Advance(ref vf,forward.Rate,dt),dx,-dy);
        }

        right.Add(dx);
        if(control==NavigationLayer.Height){
            up.Add(-dy);
            turn.Add(ballTwist?TwistHeading(scroll):scroll*ScrollToMotion);
        }else{
            forward.Add(-dy);
            if(ballTwist)turn.Add(TwistHeading(scroll));
            else up.Add(scroll*ScrollToMotion);
        }

        bool walking=control==NavigationLayer.Travel;
        double steer=walking&&!ballTwist&&forward.Active?Math.Clamp((Math.Abs(forward.Position)-3)/5,0,1):0;
        // Twist owns a separate, centred heading command. Turning and ball
        // travel can continue independently and either can return to neutral.
        double yawRate=walking&&!ballTwist?right.Rate*(coupledLook?1:steer):turn.Rate;
        float yaw=Advance(ref vyaw,yawRate,dt);
        float pitch=Advance(ref vpitch,walking&&coupledLook&&!ballTwist?forward.Rate:0,dt);
        return new(Advance(ref vr,right.Rate*(1-steer),dt),Advance(ref vu,up.Rate,dt),Advance(ref vf,forward.Rate,dt),yaw,pitch);
    }
    public void Reset(){layer=NavigationLayer.Travel;right=up=forward=turn=default;vr=vu=vf=vyaw=vpitch=0;previous=double.NaN;}
}
