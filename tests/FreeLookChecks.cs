using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
internal static class FreeLookChecks {
    static CameraPose Pose(Vector3 worldUp,float pitch=0,float roll=0,bool orthographic=false){
        var back=worldUp==Vector3.UnitY?Vector3.UnitZ:Vector3.UnitY;var right=Vector3.Normalize(Vector3.Cross(worldUp,back));
        var q=Quaternion.CreateFromAxisAngle(right,pitch);back=Vector3.Transform(back,q);var up=Vector3.Transform(worldUp,q);up=Vector3.Transform(up,Quaternion.CreateFromAxisAngle(back,roll));
        return new(back*200,Vector3.Zero,up,new(12,-8,20),.8f,200,1200,800,orthographic,worldUp,new(12,-8,20));
    }
    static Vector3 Forward(CameraPose p)=>Vector3.Normalize(p.Target-p.Eye);
    static float Elevation(CameraPose p)=>MathF.Asin(Math.Clamp(Vector3.Dot(Forward(p),p.WorldUpAxis),-1,1));
    static float RollCos(CameraPose p){var f=Forward(p);var upright=Vector3.Normalize(p.WorldUpAxis-f*Vector3.Dot(f,p.WorldUpAxis));return Vector3.Dot(Vector3.Normalize(p.Up),upright);}
    public static void Run(Action<bool,string> check){
        foreach(var axis in new[]{Vector3.UnitY,Vector3.UnitZ})foreach(float pitch in new[]{-.8f,0,.8f})foreach(float roll in new[]{0,.6f})foreach(bool ortho in new[]{false,true}){
            var start=Pose(axis,pitch,roll,ortho);var right=CameraMath.Look(start,20,0,.5f);var left=CameraMath.Look(start,-20,0,.5f);
            var horizontalRight=Vector3.Normalize(Vector3.Cross(Forward(start),axis));
            check(Vector3.Dot(Forward(right),horizontalRight)>0&&Vector3.Dot(Forward(left),horizontalRight)<0,"Free look turns right/left on world axis "+axis);
            var up=CameraMath.Look(start,0,-20,.5f);var down=CameraMath.Look(start,0,20,.5f);
            check(Elevation(up)>Elevation(start)&&Elevation(down)<Elevation(start),"Ball forward/back aims up/down independently of camera translation");
            check(right.Eye==start.Eye&&left.Eye==start.Eye&&up.Eye==start.Eye&&down.Eye==start.Eye,"Free look keeps the camera eye stationary");
            check(Math.Abs(Vector3.Distance(up.Eye,up.Target)-Vector3.Distance(start.Eye,start.Target))<.00005f&&Math.Abs(RollCos(up)-RollCos(start))<.00001f,"Free look preserves lens distance and existing roll");
            check(Math.Abs(Vector3.Distance(right.Pivot,right.Eye)-Vector3.Distance(start.Pivot,start.Eye))<.00005f&&Math.Abs(Vector3.Distance(right.ZoomPivot!.Value,right.Eye)-Vector3.Distance(start.ZoomPivot!.Value,start.Eye))<.00005f,"Camera anchors turn about the eye with the view");
            var one=CameraMath.Look(start,20,-12,.5f);var many=start;for(int i=0;i<10;i++)many=CameraMath.Look(many,2,-1.2f,.5f);
            check(Vector3.Distance(Forward(one),Forward(many))<.00001f&&Vector3.Distance(one.Up,many.Up)<.00001f,"Free-look diagonal orientation is independent of report batching");
            var inverse=CameraMath.Look(one,-20,12,.5f);check(Vector3.Distance(Forward(inverse),Forward(start))<.00001f,"Reversing a free-look stroke restores the heading");
            check(CameraMath.Look(start,20,20,0)==start,"Zero look sensitivity leaves the camera unchanged");
        }
        foreach(float pitch in new[]{-MathF.PI/2,MathF.PI/2}){
            var start=Pose(Vector3.UnitY,pitch);var pose=start;
            for(int i=0;i<100;i++)pose=CameraMath.Look(pose,3,pitch>0?100:-100,.5f);
            check(pose.Valid&&Math.Abs(Elevation(pose))<=MathF.PI/2-.0099f,"Free look remains valid near top/bottom poles and cannot flip");
            check(pose.Eye==start.Eye,"Pole aiming does not move the camera");
        }
        var root=Pose(Vector3.UnitY);float low=Vector3.Distance(Forward(root),Forward(CameraMath.Look(root,12,-12,MotionPolicy.Gain(10)))),high=Vector3.Distance(Forward(root),Forward(CameraMath.Look(root,12,-12,MotionPolicy.Gain(38))));
        check(high>low*10,"Saved 3D Orbit speed controls look yaw and pitch");
        bool invalid=false;try{CameraMath.Look(root,float.NaN,0);}catch(ArgumentException){invalid=true;}check(invalid,"Invalid look input cannot corrupt the camera");
        var motion=new NavigationMotion();var coupled=motion.Step(12,-12,60,1000,coupledLook:true);
        check(coupled.Forward>0&&coupled.Up>0&&coupled.Yaw>0&&coupled.Pitch>0,"A single held navigation chord sets travel, height and free-look rates together");
        var continued=motion.Step(0,0,0,1008,coupledLook:true);
        check(continued.Forward>0&&continued.Yaw>0&&continued.Pitch>0,"Look and travel continue without an extra key or more ball reports");
        var centred=motion.Step(-10,10,-60,1016,coupledLook:true);check(!centred.Active&&motion.Centered,"Returning near centre stops movement and looking together");
        motion.Reset();var side=motion.Step(12,0,0,2000,coupledLook:true);check(side.Right>0&&side.Yaw>0&&side.Pitch==0,"A sideways single-hold command moves and looks toward that side");
        motion.Reset();var previous=motion.Step(12,0,0,2000);check(previous.Right>0&&previous.Yaw==0&&previous.Pitch==0,"Turning the option off retains the previous pure side travel");
        motion.Reset();motion.Step(12,-12,0,3000,coupledLook:true);check(!motion.Step(0,0,0,3008,true,coupledLook:true).Active&&!motion.Step(0,0,0,3016,coupledLook:true).Active,"Releasing navigation clears both look and travel commands");
        var moved=CameraMath.Fly(root,coupled with{Up=0},.4f,.4f,true,true);
        check(moved.Eye!=root.Eye&&Forward(moved).X>0&&Forward(moved).Y>0&&Math.Abs(moved.Eye.Y-root.Eye.Y)<.0001f,"Coupled navigation moves while looking up/right and retains level-flight height");
        check(CameraMath.Fly(root,coupled,0,0,true,true)==root,"Zero speed stops both camera aiming and translation");
        var file=Path.Combine(Path.GetTempPath(),"chamfer-free-look-"+Guid.NewGuid().ToString("N")+".json");
        try{
            var store=new ConfigStore(file);store.Select(1);var cfg=store.Snapshot();var normal=cfg["programs"]![0]!.DeepClone();
            check(!ConfigStore.B(cfg,"navigationFreeLook"),"Look and travel together is opt-in");
            store.SavePreset("Before free look");cfg["navigationFreeLook"]=true;store.Save(cfg);store.SavePreset("Backtick free look");
            var reopened=new ConfigStore(file);check(ConfigStore.B(reopened.Snapshot(),"navigationFreeLook")&&JsonNode.DeepEquals(normal,reopened.Snapshot()["programs"]![0]),"Free-look preference persists without changing Normal mouse");
            reopened.ApplyPreset(0);check(!ConfigStore.B(reopened.Snapshot(),"navigationFreeLook"),"Before-free-look preset restores the previous movement");
            reopened.ApplyPreset(1);check(ConfigStore.B(reopened.Snapshot(),"navigationFreeLook"),"Free-look preset restores coupled navigation");
            var legacy=reopened.Snapshot();legacy["presets"]![0]!["settings"]!["feel"]!.AsObject().Remove("navigationFreeLook");File.WriteAllText(file,legacy.ToJsonString());
            var oldPreset=new ConfigStore(file);oldPreset.ApplyPreset(0);check(!ConfigStore.B(oldPreset.Snapshot(),"navigationFreeLook"),"An old preset without a free-look flag restores previous travel");
        }finally{foreach(var p in new[]{file,file+".previous",file+".writing"})if(File.Exists(p))File.Delete(p);}
        ExportTrace();
    }
    static object View(CameraPose p)=>new{eye=new[]{p.Eye.X,p.Eye.Y,p.Eye.Z},target=new[]{p.Target.X,p.Target.Y,p.Target.Z},up=new[]{p.Up.X,p.Up.Y,p.Up.Z},pivot=new[]{p.Pivot.X,p.Pivot.Y,p.Pivot.Z},zoomPivot=new[]{p.ZoomPivot!.Value.X,p.ZoomPivot.Value.Y,p.ZoomPivot.Value.Z},worldUp=new[]{p.WorldUpAxis.X,p.WorldUpAxis.Y,p.WorldUpAxis.Z},verticalFov=p.VerticalFov,orthoHeight=p.OrthoHeight,width=p.Width,height=p.Height,orthographic=p.Orthographic};
    static void ExportTrace(){
        var cases=new List<object>();
        foreach(var sample in new[]{("look-travel-right",12f,0f,0f,false),("look-travel-left",-12f,0f,0f,false),("look-travel-up",0f,-12f,0f,false),("look-travel-down",0f,12f,0f,false),("look-travel-diagonal",12f,-12f,0f,false),("look-reverse-diagonal",-12f,12f,0f,false),("level-look-travel",0f,-12f,0f,true),("level-look-height",0f,0f,60f,true)}){
            var start=Pose(Vector3.UnitY);var pose=start;var rate=new NavigationMotion();var poses=new List<object>();
            for(int i=0;i<32;i++){
                var travel=rate.Step(i==0?sample.Item2:0,i==0?sample.Item3:0,i==0?sample.Item4:0,1000+i*8,coupledLook:true);
                pose=CameraMath.Fly(pose,travel,MotionPolicy.Gain(38),MotionPolicy.Gain(38),sample.Item5,true);poses.Add(View(pose));
            }
            cases.Add(new{label=sample.Item1,mode="Navigate",freeLook=true,coupledLook=true,lookOnly=false,levelFlight=sample.Item5,heightTravel=sample.Item4!=0,lookX=sample.Item2,lookY=sample.Item3,steers=sample.Item2!=0,turnSign=Math.Sign(sample.Item2),start=View(start),poses});
        }
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-free-look-trace.json"),JsonSerializer.Serialize(new{cases}));
    }
}
