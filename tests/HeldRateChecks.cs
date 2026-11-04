using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
static class HeldRateChecks {
 public static void Run(Action<bool,string> check) {
  var rate=new HeldRateMotion();
  var first=rate.Step(12,-12,0,1000,NavMode.Orbit);
  check(first.X>0&&first.X<1&&first.Y<0,"A small held roll starts a gentle bounded cruise");
  var next=rate.Step(0,0,0,1016,NavMode.Orbit);
  check(next.X>first.X&&next.Y<0,"Cruise continues without more ball input only inside its active hold");
  var stopped=rate.Step(0,0,0,1032,NavMode.Orbit,true);
  check(!stopped.HasPrimary&&!rate.Step(0,0,0,1048,NavMode.Orbit).HasPrimary,"Release stops immediately without replaying stored travel");
  rate.Reset();rate.Step(3,0,0,2000,NavMode.Orbit);
  check(!rate.Step(0,0,0,2016,NavMode.Orbit).HasPrimary,"The cruise centre has a dead zone for tiny accidental movement");
  rate.Reset();rate.Step(120,0,0,3000,NavMode.Orbit);
  float full=rate.Step(0,0,0,3016,NavMode.Orbit).X;
  rate.Reset();rate.Step(12,0,0,3000,NavMode.Orbit);
  check(full>rate.Step(0,0,0,3016,NavMode.Orbit).X*20,"Displacement provides a fine centre and a bounded faster outer range");
  rate.Reset();rate.Step(120,0,0,4000,NavMode.Orbit);
  rate.Step(-240,0,0,4016,NavMode.Orbit);
  check(rate.Step(0,0,0,4064,NavMode.Orbit).X<0,"Rolling back through centre reverses cruise smoothly");
  rate.Reset();rate.Step(40,30,120,5000,NavMode.PanZoom);
  check(!rate.Step(0,0,0,5300,NavMode.PanZoom).HasPrimary,"A stalled input loop discards cruise instead of catching up");
  rate.Step(40,30,120,5400,NavMode.PanZoom);rate.Reset();
  check(!rate.Step(0,0,0,5416,NavMode.PanZoom).HasPrimary,"Focus, device and gesture resets clear every cruise axis");
  double Travel(int interval){var r=new HeldRateMotion();r.Step(60,0,0,1000,NavMode.Orbit);double sum=0;for(int t=interval;t<=1000;t+=interval)sum+=r.Step(0,0,0,1000+t,NavMode.Orbit).X;return sum;}
  check(Math.Abs(Travel(10)-Travel(20))<.02,"Cruise velocity is integrated over elapsed time independent of tick cadence");
  var path=Path.Combine(Path.GetTempPath(),"chamfer-cruise-"+Guid.NewGuid().ToString("N")+".json");
  try{
   var store=new ConfigStore(path);store.Select(1);var cfg=store.Snapshot();var original=cfg["programs"]![0]!.DeepClone();
   cfg["spaceMouseCruise"]=true;store.Save(cfg);store.SavePreset("Cruise test");
   var reopen=new ConfigStore(path);
   check(ConfigStore.B(reopen.Snapshot(),"spaceMouseCruise")&&JsonNode.DeepEquals(original,reopen.Snapshot()["programs"]![0]),"Cruise persists in its program while Normal mouse remains unchanged");
   reopen.Select(0);reopen.ApplyPreset(0);
   check(ConfigStore.B(reopen.Snapshot(),"spaceMouseCruise"),"Saved presets retain the cruise response");
  }finally{foreach(var p in new[]{path,path+".previous",path+".writing"})if(File.Exists(p))File.Delete(p);}
 }
}
