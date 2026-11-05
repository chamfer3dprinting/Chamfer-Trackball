using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
internal static class NavigationChecks {
    static CameraPose Pose()=>new(new(0,0,200),Vector3.Zero,Vector3.UnitY,Vector3.Zero,.8f,200,1200,800,false,Vector3.UnitY,Vector3.Zero);
    public static void Run(Action<bool,string> check){
        var rate=new NavigationMotion();rate.Reset();var ball=rate.Step(0,-12,0,900);check(ball.Forward>0&&ball.Up==0&&ball.Right==0,"Ball forward controls depth without changing height");
        rate.Reset();var scroll=rate.Step(0,0,60,950);check(scroll.Up>0&&scroll.Forward==0&&scroll.Right==0,"Scroll up controls height without changing depth");
        rate.Reset();var a=rate.Step(12,-8,60,1000);var b=rate.Step(0,0,0,1008);
        check(a.Yaw>0&&a.Up>0&&a.Forward>0&&b.Forward>a.Forward,"Drone controls combine steering/up/forward and continue without new movement");
        check(!rate.Step(0,0,0,1016,true).Active&&!rate.Step(0,0,0,1024).Active,"Navigation key release immediately clears every velocity");
        rate.Reset();check(!rate.Step(1,1,0,2000).Active,"One-count sensor noise does not launch the drone");
        rate.Reset();rate.Step(12,0,0,3000);var reverse=rate.Step(-30,8,-120,3008);reverse=rate.Step(0,0,0,3096);
        check(reverse.Yaw<0&&reverse.Up<0&&reverse.Forward<0,"Rolling back and reverse scrolling command left steering/down/back");
        check(!rate.Step(0,0,0,3400).Active,"A paused loop discards stale navigation instead of catching up");
        rate.Reset();rate.Step(12,8,60,4000);rate.Reset();check(!rate.Step(0,0,0,4008).Active,"Focus/device/mode reset clears drone motion");
        double Travel(int dt){var r=new NavigationMotion();r.Step(12,0,0,1000);double x=0;for(int t=dt;t<=1000;t+=dt)x+=r.Step(0,0,0,1000+t).Right;return x;}
        check(Math.Abs(Travel(8)-Travel(20))<.01,"Drone travel is integrated over elapsed time");
        rate.Reset();var strafe=rate.Step(12,0,0,4500);check(strafe.Right>0&&strafe.Yaw==0&&strafe.Forward==0,"A straight side roll strafes without turning");
        rate.Reset();var turnRight=rate.Step(12,-12,0,4600);rate.Reset();var turnLeft=rate.Step(-12,-12,0,4700);
        check(turnRight.Forward>0&&turnRight.Yaw>0&&turnRight.Right==0&&turnLeft.Forward>0&&turnLeft.Yaw<0,"Forward diagonals steer in the requested direction");
        var start=Pose();var turnedRight=CameraMath.Fly(start,turnRight,MotionPolicy.Gain(27),MotionPolicy.Gain(28));var turnedLeft=CameraMath.Fly(start,turnLeft,MotionPolicy.Gain(27),MotionPolicy.Gain(28));
        check((turnedRight.Target-turnedRight.Eye).X>0&&(turnedLeft.Target-turnedLeft.Eye).X<0,"Diagonal right/left turns change the camera heading correctly");
        check(Math.Abs(Vector3.Distance(turnedRight.Eye,turnedRight.Target)-Vector3.Distance(start.Eye,start.Target))<.0001f,"Turning retains the camera lens distance");
        check(CameraMath.Fly(start,turnRight,0,0)==start,"Zero translation and turning sensitivity stops every navigation component");
        var move=CameraMath.Fly(start,new(12,8,15),MotionPolicy.Gain(27));
        check(move.Eye.X>start.Eye.X&&move.Eye.Y>start.Eye.Y&&move.Eye.Z<start.Eye.Z,"Ball directions follow the view and positive twist moves forward");
        check(Vector3.Distance(move.Target-move.Eye,start.Target-start.Eye)<.0001f&&move.Up==start.Up,"Drone translation preserves orientation and camera distance");
        check(Vector3.Distance(move.Pivot-start.Pivot,move.Eye-start.Eye)<.0001f&&Vector3.Distance(move.ZoomPivot!.Value-start.ZoomPivot!.Value,move.Eye-start.Eye)<.0001f,"Camera anchors move with the drone");
        var rolled=start with{Up=Vector3.UnitX};var rmove=CameraMath.Fly(rolled,new(0,10,0));
        check(rmove.Eye.X>rolled.Eye.X&&Math.Abs(rmove.Eye.Y-rolled.Eye.Y)<.0001f,"Up/down follows a rolled view rather than a fixed world axis");
        var lookRight=start with{Eye=new(200,0,0)};var forward=CameraMath.Fly(lookRight,new(0,0,10));
        check(forward.Eye.X<lookRight.Eye.X&&Math.Abs(forward.Eye.Z-lookRight.Eye.Z)<.0001f,"Forward follows camera heading");
        var fine=CameraMath.Fly(start,new(10,0,0),MotionPolicy.Gain(10));var fast=CameraMath.Fly(start,new(10,0,0),MotionPolicy.Gain(40));
        check(Vector3.Distance(start.Eye,fast.Eye)>Vector3.Distance(start.Eye,fine.Eye)*10,"Saved 3D speed controls drone travel");
        rate.Reset();var travel=start;for(int i=0;i<126;i++)travel=CameraMath.Fly(travel,rate.Step(i==0?12:0,0,0,5000+i*8),MotionPolicy.Gain(27));
        check(travel.Eye.X>start.Eye.X+5&&travel.Eye.X<start.Eye.X+40,"A small roll produces useful bounded movement at the owner's sensitivity");

        var file=Path.Combine(Path.GetTempPath(),"chamfer-navigation-"+Guid.NewGuid().ToString("N")+".json");
        try{
            var store=new ConfigStore(file);check(!ConfigStore.B(store.Snapshot(),"navigationCruise"),"Normal mouse leaves continuous navigation off");
            store.Select(1);var cfg=store.Snapshot();check(ConfigStore.B(cfg,"navigationCruise")&&!ConfigStore.B(cfg,"spaceMouseCruise"),"3D default enables only navigation continuity, not orbit/pan/zoom cruise");
            var original=cfg["programs"]![0]!.DeepClone();cfg["navigationCruise"]=false;store.Save(cfg);store.SavePreset("Direct navigation");cfg=store.Snapshot();cfg["navigationCruise"]=true;store.Save(cfg);store.SavePreset("Drone navigation");
            cfg=store.Snapshot();cfg["configVersion"]=30;store.Save(cfg);check(ConfigStore.I(JsonNode.Parse(File.ReadAllText(file)),"configVersion")==31,"An older interface save cannot downgrade the navigation schema");
            var reopened=new ConfigStore(file);check(ConfigStore.B(reopened.Snapshot(),"navigationCruise")&&JsonNode.DeepEquals(original,reopened.Snapshot()["programs"]![0]),"Navigation choice persists without modifying Normal mouse");
            reopened.ApplyPreset(0);check(!ConfigStore.B(reopened.Snapshot(),"navigationCruise"),"Saved preset restores direct navigation");
            reopened.ApplyPreset(1);check(ConfigStore.B(reopened.Snapshot(),"navigationCruise"),"Saved preset restores drone navigation");
            var old=reopened.Snapshot();old["configVersion"]=30;old.Remove("navigationCruise");foreach(var p in old["programs"]!.AsArray())p!["feel"]!.AsObject().Remove("navigationCruise");File.WriteAllText(file,old.ToJsonString());
            var migrated=new ConfigStore(file).Snapshot();check(ConfigStore.I(migrated,"configVersion")==31&&ConfigStore.B(migrated,"navigationCruise")&&!ConfigStore.B(migrated["programs"]![0]!["feel"],"navigationCruise"),"Existing 3D settings gain navigation without changing Normal mouse");
        }finally{foreach(var p in new[]{file,file+".previous",file+".writing"})if(File.Exists(p))File.Delete(p);}
        ExportTrace();
    }
    static object View(CameraPose p)=>new{eye=new[]{p.Eye.X,p.Eye.Y,p.Eye.Z},target=new[]{p.Target.X,p.Target.Y,p.Target.Z},up=new[]{p.Up.X,p.Up.Y,p.Up.Z},pivot=new[]{p.Pivot.X,p.Pivot.Y,p.Pivot.Z},zoomPivot=new[]{p.Pivot.X,p.Pivot.Y,p.Pivot.Z},verticalFov=p.VerticalFov,orthoHeight=p.OrthoHeight,width=p.Width,height=p.Height,orthographic=p.Orthographic};
    static void ExportTrace(){
        var cases=new List<object>();
        foreach(var sample in new[]{("drone-right",12f,0f,0f),("drone-up",0f,0f,60f),("drone-forward",0f,-12f,0f),("drone-combined",12f,-8f,60f),("drone-turn-right",12f,-12f,0f),("drone-turn-left",-12f,-12f,0f)}){
            var start=Pose();var pose=start;var rate=new NavigationMotion();var poses=new List<object>();
            for(int i=0;i<32;i++){var motion=rate.Step(i==0?sample.Item2:0,i==0?sample.Item3:0,i==0?sample.Item4:0,1000+i*8);pose=CameraMath.Fly(pose,motion,MotionPolicy.Gain(27),MotionPolicy.Gain(28));poses.Add(View(pose));}
            cases.Add(new{label=sample.Item1,mode="Navigate",steers=sample.Item2!=0&&sample.Item3!=0,turnSign=Math.Sign(sample.Item2),start=View(start),poses});
        }
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-navigation-trace.json"),JsonSerializer.Serialize(new{cases}));
    }
}
