using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
static class TrackballDeviceChecks{
 public static void Run(Action<bool,string> check){
  string folder=Path.Combine(Path.GetTempPath(),"trackball-device-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  string path=Path.Combine(folder,"settings.json");
  try{
   var legacy=ConfigStore.Defaults();legacy["configVersion"]=29;legacy["selectedDevice"]="m512";legacy["trackballSwapButtons"]=true;
   File.WriteAllText(path,legacy.ToJsonString());var store=new ConfigStore(path);var before=store.Snapshot();
   check(TrackballDevices.Read(before).Single() is {Id:"m512",SwapButtons:true,SwapWheels:false},"Upgrade remembers the existing trackball and its native corrections");
   check(ConfigStore.I(before,"configVersion")==30&&ConfigStore.I(JsonNode.Parse(File.ReadAllText(path)),"configVersion")==30,"Device-list upgrade is persisted once");
   var programs=before["programs"]!.ToJsonString();var presets=before["presets"]!.ToJsonString();
   store.IdentifyDevice("slimblade");var second=store.Snapshot();
   check(TrackballDevices.Read(second).Length==2&&TrackballDevices.Read(second).Any(d=>d.Id=="m512"),"Identifying a second trackball retains the M512");
   check(!ConfigStore.B(second,"trackballSwapButtons")&&!ConfigStore.B(second,"trackballSwapWheels"),"A new trackball keeps native clicks and wheel axes");
   check(second["programs"]!.ToJsonString()==programs&&second["presets"]!.ToJsonString()==presets,"Adding a trackball preserves program controls, speeds and presets");
   var stale=(JsonObject)before.DeepClone();stale["trackballSwapButtons"]=false;stale["trackballs"]=new JsonArray();store.Save(stale);
   var saved=store.Snapshot();
   check(TrackballDevices.Read(saved).Length==2&&ConfigStore.S(saved,"selectedDevice")=="slimblade","A stale settings page cannot erase a remembered trackball or change selection");
   check(!ConfigStore.B(saved,"trackballSwapButtons")&&TrackballDevices.Read(saved).Single(d=>d.Id=="m512").SwapButtons,"A stale page cannot copy the previous device corrections to the new one");
   store.SelectDevice("m512");var a=store.Snapshot();
   check(ConfigStore.B(a,"trackballSwapButtons"),"Selecting the M512 restores its own correction");
   a["trackballSwapWheels"]=true;store.Save(a);store.SelectDevice("slimblade");store.RenameDevice("slimblade","SlimBlade Pro");
   var reopened=new ConfigStore(path);var all=TrackballDevices.Read(reopened.Snapshot());
   check(all.Length==2&&all.Single(d=>d.Id=="slimblade").Name=="SlimBlade Pro","Both trackballs and their names survive restart");
   check(all.Single(d=>d.Id=="m512") is {SwapButtons:true,SwapWheels:true}&&all.Single(d=>d.Id=="slimblade") is {SwapButtons:false,SwapWheels:false},"Click and wheel corrections remain separate after restart");
   reopened.SavePreset("Devices stay separate");reopened.SelectDevice("m512");reopened.ApplyPreset(0);
   check(TrackballDevices.Read(reopened.Snapshot()).Length==2&&ConfigStore.S(reopened.Snapshot(),"selectedDevice")=="m512","Applying a program preset keeps the remembered devices and preferred trackball");
   var reset=reopened.Reset();check(TrackballDevices.Read(reset).Length==2&&TrackballDevices.Read(reset).All(d=>!d.SwapButtons&&!d.SwapWheels),"Reset restores native corrections while keeping both identified trackballs");
   bool denied=false;try{reopened.SelectDevice("ordinary-mouse");}catch(InvalidDataException){denied=true;}
   check(denied&&TrackballDevices.Read(reopened.Snapshot()).Length==2,"An unidentified ordinary mouse cannot become a remembered trackball");
   denied=false;try{reopened.RenameDevice("m512","\n");}catch(InvalidDataException){denied=true;}
   check(denied,"Invalid trackball names cannot damage the device list");
  }finally{Directory.Delete(folder,true);}
  var roster=new TrackballRoster();var entries=new[]{new TrackballDevice("m512","M512"),new TrackballDevice("slimblade","SlimBlade Pro",true,false)};
  roster.Configure(entries,"m512");roster.UpdatePresence(new[]{"m512","slimblade","ordinary-mouse"});
  check(roster.ActiveId=="m512"&&roster.Snapshot().Count(d=>d.Connected)==2,"Both connected trackballs are recognized without treating ordinary mice as trackballs");
  check(roster.Observe("slimblade",true)&&roster.ActiveId=="slimblade","Physical input switches to the second remembered trackball");
  check(!roster.Observe("ordinary-mouse",true)&&roster.ActiveId=="slimblade","Ordinary mouse movement cannot steal the active trackball");
  check(!roster.Observe("m512",false)&&roster.ActiveId=="slimblade","A held button or drag prevents another trackball from stealing the gesture");
  check(roster.Observe("M512",true)&&roster.ActiveId=="m512","The other trackball resumes after release, with case-insensitive identity");
  roster.Observe("slimblade",true);roster.Configure(entries,"m512");
  check(roster.ActiveId=="slimblade","Unrelated settings saves do not repeatedly reset the physical active trackball");
  roster.UpdatePresence(new[]{"m512"});check(roster.ActiveId=="m512","Unplugging the active trackball falls back to the connected remembered one");
  roster.UpdatePresence(Array.Empty<string>());check(roster.Snapshot().All(d=>!d.Connected),"Disconnecting both retains their identities without claiming they are connected");
  roster.UpdatePresence(new[]{"slimblade"});check(roster.ActiveId=="slimblade"&&roster.Snapshot().Single(d=>d.Active).Connected,"A remembered trackball reconnects without another identification");
  check(roster.Find("SLIMBLADE")?.SwapButtons==true&&roster.Find("m512")?.SwapButtons==false,"Runtime correction lookup stays per device");
  check(MotionPolicy.FrameIntervalMs==8,"Navigation requests one normal Windows timer tick instead of a quantized 16 ms interval");
  foreach(var mode in new[]{NavMode.Orbit,NavMode.PanZoom,NavMode.Zoom}){
   var c=new CameraPose(new(0,0,20),System.Numerics.Vector3.Zero,System.Numerics.Vector3.UnitY,System.Numerics.Vector3.Zero,.8f,10,1200,800,false);
   var frame=new MotionFrames();float totalX=0,totalY=0;
   for(int i=0;i<80;i++){frame.Move(.125f,-.0625f);if(i%8==7){var sample=frame.Peek();totalX+=sample.X;totalY+=sample.Y;frame.Reset();}}
   var split=c;for(int i=0;i<10;i++)split=CameraMath.Move(split,mode,1,-.5f);
   var combined=CameraMath.Move(c,mode,totalX,totalY);
   check(System.Numerics.Vector3.Distance(split.Eye,combined.Eye)<.02f,"Faster scheduling preserves movement distance for "+mode);
  }
 }
}
