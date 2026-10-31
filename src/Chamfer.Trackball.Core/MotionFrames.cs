namespace Chamfer.Trackball.Core;

public readonly record struct MotionFrame(float X,float Y,float Wheel,float Dial){
 public bool HasPrimary=>X!=0||Y!=0||Wheel!=0;
 public bool HasDial=>Dial!=0;
}
public sealed class MotionFrames{
 float x,y,wheel,dial;
 static float Add(float current,float value){if(!float.IsFinite(value))throw new ArgumentException("Invalid sensor movement.");return Math.Clamp(current+value,-16000,16000);}
 public MotionFrame Peek()=>new(x,y,wheel,dial);
 public void Move(float dx,float dy){x=Add(x,dx);y=Add(y,dy);}
 public void Wheel(float delta)=>wheel=Add(wheel,delta);
 public void Dial(float delta)=>dial=Add(dial,delta);
 public void ClearMove()=>x=y=0;
 public void ClearPrimary(){x=y=wheel=0;}
 public void ClearDial()=>dial=0;
 public void Reset(){ClearPrimary();ClearDial();}
}
public sealed class MotionGlide{
 float velocityX,velocityY;long lastTime;
 public bool Active=>velocityX*velocityX+velocityY*velocityY>=.0025f;
 public (float X,float Y) Step(float x,float y,long now,float seconds,bool enabled){
  if(!enabled){Reset();return (x,y);}
  if(!float.IsFinite(x)||!float.IsFinite(y)||!float.IsFinite(seconds)){Reset();return (0,0);}
  float dt=lastTime==0?.016f:Math.Clamp((now-lastTime)/1000f,.001f,.05f);lastTime=now;
  if(x!=0||y!=0){velocityX=x/dt;velocityY=y/dt;return (x,y);}
  if(!Active)return (0,0);
  float tau=Math.Clamp(seconds,.05f,1.5f),decay=MathF.Exp(-dt/tau),travel=tau*(1-decay);
  float outX=velocityX*travel,outY=velocityY*travel;
  velocityX*=decay;velocityY*=decay;
  if(!Active)velocityX=velocityY=0;
  return (outX,outY);
 }
 public void Reset(){velocityX=velocityY=0;lastTime=0;}
}
public sealed class FractionalInput{
 float x,y,wheel;
 static int Take(ref float remainder,float value){remainder+=value;int whole=(int)MathF.Round(remainder);remainder-=whole;return whole;}
 public (int X,int Y,int Wheel) Add(float dx,float dy,float delta)=>(Take(ref x,dx),Take(ref y,dy),Take(ref wheel,delta));
 public void Reset()=>x=y=wheel=0;
}
public static class MotionPolicy{
 public static float Gain(double slider)=>MathF.Pow(2,((float)Math.Clamp(slider,0,100)-50)/20);
 public static bool Circle(NavMode mode,bool enabled,bool held)=>enabled&&(mode==NavMode.PanZoom||mode==NavMode.Orbit&&!held);
 public static NavMode WhilePaused(NavMode desired,bool paused,bool held)=>paused&&!held?NavMode.Idle:desired;
 public static bool ResumeAfterClick(bool stoppedByClick,NavMode desired,bool heldNavigation,bool ballMoved)=>stoppedByClick&&ballMoved&&!heldNavigation&&desired!=NavMode.Idle;
 public static (float X,float Y,float Wheel) Scale(NavMode mode,float x,float y,float wheel,float orbit,float pan,float zoom){
  float movement=mode==NavMode.Zoom?zoom:mode is NavMode.Orbit or NavMode.Roll or NavMode.Snap?orbit:pan;
  float scroll=mode is NavMode.Orbit or NavMode.Navigate?orbit:zoom;
  return (x*movement,y*movement,wheel*scroll);
 }
 public static float ZoomAmount(NavMode mode,float dy,float wheel)=>mode==NavMode.Zoom?-dy*.008f+wheel*.0015f:mode is NavMode.PanZoom or NavMode.Roll?wheel*.0015f:0;
}

public static class PointerPolicy{
 static readonly float[] Multipliers=[0,1f/32,1f/16,1f/8,1f/4,3f/8,1f/2,5f/8,3f/4,7f/8,1,1.25f,1.5f,1.75f,2,2.25f,2.5f,2.75f,3,3.25f,3.5f];
 public static float Gain(int step)=>Multipliers[Math.Clamp(step,1,20)];
 public static bool Capture(int step,bool smooth)=>step!=10||smooth;
}
public sealed class PointerMotion{
 readonly MotionFrames samples=new();readonly MotionGlide glide=new();readonly FractionalInput fractions=new();
 public bool Active=>glide.Active||samples.Peek().HasPrimary;
 public (int X,int Y) Move(int x,int y,float gain){
  samples.Move(x,y);var whole=fractions.Add(x*gain,y*gain,0);return (whole.X,whole.Y);
 }
 public (int X,int Y) Tick(long now,float seconds,bool smooth,float gain){
  var frame=samples.Peek();samples.ClearMove();
  var tail=glide.Step(frame.X,frame.Y,now,seconds,smooth);
  // Real samples were already delivered immediately. Only emit the decay tail.
  if(frame.HasPrimary)return (0,0);
  var whole=fractions.Add(tail.X*gain,tail.Y*gain,0);return (whole.X,whole.Y);
 }
 public void Reset(){samples.Reset();glide.Reset();fractions.Reset();}
}
