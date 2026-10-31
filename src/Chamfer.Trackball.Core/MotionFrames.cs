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
 public void ClearPrimary(){x=y=wheel=0;}
 public void ClearDial()=>dial=0;
 public void Reset(){ClearPrimary();ClearDial();}
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
 public static (float X,float Y,float Wheel) Scale(NavMode mode,float x,float y,float wheel,float orbit,float pan,float zoom){
  float movement=mode==NavMode.Zoom?zoom:mode is NavMode.Orbit or NavMode.Roll or NavMode.Snap?orbit:pan;
  float scroll=mode is NavMode.Orbit or NavMode.Navigate?orbit:zoom;
  return (x*movement,y*movement,wheel*scroll);
 }
 public static float ZoomAmount(NavMode mode,float dy,float wheel)=>mode==NavMode.Zoom?-dy*.008f+wheel*.0015f:mode is NavMode.PanZoom or NavMode.Roll?wheel*.0015f:0;
}
