using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
static class CameraResponseChecks{
 public static void Run(Action<bool,string> check){
  var filter=new CameraMotionFilter();
  var first=filter.Step(100,-50,120,1000);
  check(first.X>0&&first.X<70&&first.Y<0&&first.Wheel>0&&first.Wheel<120,"Time-based response eases the first movement and twist together without waiting for another input");
  var sum=first;var last=first;
  for(int i=1;i<30;i++){last=filter.Step(0,0,0,1000+i*16.0);sum=new(sum.X+last.X,sum.Y+last.Y,sum.Wheel+last.Wheel,0);}
  check(Math.Abs(sum.X-100)<.001&&Math.Abs(sum.Y+50)<.001&&Math.Abs(sum.Wheel-120)<.001&&!last.HasPrimary,"Measured ball and twist travel is conserved and the short response stops at rest");
  filter.Reset();first=filter.Step(100,-50,120,1000);var finish=filter.Step(-10,5,-20,1008,finish:true);
  check(Math.Abs(first.X+finish.X-90)<.001&&Math.Abs(first.Y+finish.Y+45)<.001&&Math.Abs(first.Wheel+finish.Wheel-100)<.001&&!filter.Step(0,0,0,1016).HasPrimary,"Release drains each measured axis once and clears the response");
  filter.Step(50,50,120,1032);filter.Reset();
  check(!filter.Step(0,0,0,1048).HasPrimary,"Focus/device/gesture reset cannot leak a previous response");
  filter.Step(50,50,120,1064);
  check(!filter.Step(0,0,0,1600).HasPrimary,"A UI pause discards stale response instead of producing a catch-up camera jump");
  filter.Step(50,50,120,1616);
  check(!filter.Step(0,0,0,1600).HasPrimary,"A backwards clock restarts the response without stale motion");
  var uniform=new CameraMotionFilter();var previous=new Queue<float>();double now=1000,squaredNew=0,squaredOld=0;int count=0;
  int[] intervals=[8,24,12,20,16,9,23,15,17];
  for(int i=0;i<600;i++){
   int dt=intervals[i%intervals.Length];now+=dt;float movement=dt;
   var output=uniform.Step(movement,0,0,now);previous.Enqueue(movement);if(previous.Count>3)previous.Dequeue();
   var points=previous.ToArray();double old=points[^1]*.70+(points.Length>1?points[^2]*.22:0)+(points.Length>2?points[^3]*.08:0);
   if(i>60){squaredNew+=Math.Pow(output.X/dt-1,2);squaredOld+=Math.Pow(old/dt-1,2);count++;}
  }
  double rmsNew=Math.Sqrt(squaredNew/count),rmsOld=Math.Sqrt(squaredOld/count);
  check(rmsOld>.1&&rmsNew<rmsOld*.05,"Uneven timer intervals preserve steady camera speed instead of changing a three-tick filter's effective speed");
  Console.WriteLine($"Response cadence replay: previous RMS speed variation {rmsOld:P1}; elapsed-time response {rmsNew:P2}.");
  foreach(float response in new[]{6f,18f,40f}){
   var trace=new CameraMotionFilter();double x=0,y=0,w=0,inputX=0,inputY=0,inputW=0;now=1000;
   for(int i=0;i<90;i++){int dt=intervals[i%intervals.Length];now+=dt;float dx=i<50?(i%2==0?14:0):-3,dy=i<60?-2:4,dw=i%9==0?120:0;
    var v=trace.Step(dx,dy,dw,now,response);x+=v.X;y+=v.Y;w+=v.Wheel;inputX+=dx;inputY+=dy;inputW+=dw;}
   var rest=trace.Step(0,0,0,now+1,response,true);x+=rest.X;y+=rest.Y;w+=rest.Wheel;
   check(Math.Abs(x-inputX)<.002&&Math.Abs(y-inputY)<.002&&Math.Abs(w-inputW)<.002,"Jittered and reversing motion keeps all measured travel at response "+response+" ms");
  }
  var quick=new CameraMotionFilter().Step(100,0,0,1000,6);
  var smooth=new CameraMotionFilter().Step(100,0,0,1000,40);
  check(quick.X>smooth.X*2,"The response control changes easing while leaving the final sensitivity unchanged");
  var original=ConfigStore.Defaults();
  check(!ConfigStore.B(original,"steadyCamera")&&ConfigStore.D(original,"cameraResponseMs")==18,"The optional response keeps old setups off and uses an 18 ms default");
  var path=Path.Combine(Path.GetTempPath(),"chamfer-response-"+Guid.NewGuid().ToString("N")+".json");
  try{
   var store=new ConfigStore(path);var settings=store.Snapshot();var programs=settings["programs"]!.DeepClone();var presets=settings["presets"]!.DeepClone();
   settings["steadyCamera"]=true;settings["cameraResponseMs"]=32;store.Save(settings);var reopened=new ConfigStore(path).Snapshot();
   check(ConfigStore.B(reopened,"steadyCamera")&&ConfigStore.D(reopened,"cameraResponseMs")==32&&JsonNode.DeepEquals(programs,reopened["programs"])&&JsonNode.DeepEquals(presets,reopened["presets"]),"Response tuning persists without changing program speeds, controls or presets");
  }finally{foreach(var file in new[]{path,path+".previous",path+".writing"})if(File.Exists(file))File.Delete(file);}
  var dial=new SlimBladeTwist();dial.Add(120,NavMode.PanZoom);
  check(dial.Take()==90&&dial.Step()==0,"The shared response takes the reduced SlimBlade twist once without double smoothing");
  bool rejected=false;try{filter.Step(float.NaN,0,0,2000);}catch(ArgumentException){rejected=true;}
  check(rejected,"Invalid motion cannot poison the response state");
 }
}
