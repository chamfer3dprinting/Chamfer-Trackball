using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
internal static class LevelFlightChecks {
    static CameraPose Pose(Vector3 worldUp,float pitch,float roll,bool orthographic=false){
        var back=worldUp==Vector3.UnitY?Vector3.UnitZ:Vector3.UnitY;var right=Vector3.Normalize(Vector3.Cross(worldUp,back));
        var tilt=Quaternion.CreateFromAxisAngle(right,pitch);back=Vector3.Transform(back,tilt);var up=Vector3.Transform(worldUp,tilt);
        up=Vector3.Transform(up,Quaternion.CreateFromAxisAngle(back,roll));
        return new(back*200,Vector3.Zero,up,Vector3.Zero,.8f,200,1200,800,orthographic,worldUp,Vector3.Zero);
    }
    public static void Run(Action<bool,string> check){
        foreach(var worldUp in new[]{Vector3.UnitY,Vector3.UnitZ})foreach(bool ortho in new[]{false,true})foreach(float pitch in new[]{-MathF.PI/2,-.7f,0,.7f,MathF.PI/2})foreach(float roll in new[]{0,.6f}){
            var start=Pose(worldUp,pitch,roll,ortho);
            var forward=CameraMath.Fly(start,new(0,0,12),.4f,1,true);var reverse=CameraMath.Fly(start,new(0,0,-12),.4f,1,true);
            check(forward.Valid&&reverse.Valid&&Math.Abs(Vector3.Dot(forward.Eye-start.Eye,worldUp))<.00003f&&Math.Abs(Vector3.Dot(reverse.Eye-start.Eye,worldUp))<.00003f,"Level forward/reverse retain height for world up "+worldUp+" pitch "+pitch+" roll "+roll+" ortho "+ortho);
            check(Vector3.Distance(forward.Eye-start.Eye,-(reverse.Eye-start.Eye))<.00003f,"Level reverse follows the opposite horizontal travel");
            var height=CameraMath.Fly(start,new(0,12,0),.4f,1,true);var step=height.Eye-start.Eye;
            check(Vector3.Dot(step,worldUp)>0&&(step-worldUp*Vector3.Dot(step,worldUp)).Length()<.00003f,"Level scroll changes only world height");
            var side=CameraMath.Fly(start,new(12,0,0),.4f,1,true);
            check(Math.Abs(Vector3.Dot(side.Eye-start.Eye,worldUp))<.00003f,"Level strafe stays horizontal in rolled views");
            var turn=CameraMath.Fly(start,new(0,0,12,9),.4f,.4f,true);
            var before=Vector3.Normalize(start.Target-start.Eye);var after=Vector3.Normalize(turn.Target-turn.Eye);
            check(turn.Valid&&Math.Abs(Vector3.Dot(turn.Eye-start.Eye,worldUp))<.00003f&&Math.Abs(Vector3.Dot(before,worldUp)-Vector3.Dot(after,worldUp))<.00001f,"Level steering retains height and look tilt at poles and pitched views");
            check(Math.Abs(Vector3.Distance(turn.Eye,turn.Target)-Vector3.Distance(start.Eye,start.Target))<.00005f,"Level steering preserves lens distance");
            check(CameraMath.Fly(start,new(12,8,12,9),0,0,true)==start,"Level flight keeps zero sensitivity stationary");
            if(Math.Abs(pitch)>.1f){
                var direct=CameraMath.Fly(start,new(0,0,12),.4f,1,false);
                check(Math.Abs(Vector3.Dot(direct.Eye-start.Eye,worldUp))>.001f,"Turning trial off restores camera-direction travel");
            }
        }
        var file=Path.Combine(Path.GetTempPath(),"chamfer-level-flight-"+Guid.NewGuid().ToString("N")+".json");
        try{
            var store=new ConfigStore(file);store.Select(1);var cfg=store.Snapshot();var normal=cfg["programs"]![0]!.DeepClone();
            check(!ConfigStore.B(cfg,"navigationLevelFlight"),"Level flight is an opt-in trial");
            store.SavePreset("Before level flight");cfg["navigationLevelFlight"]=true;store.Save(cfg);store.SavePreset("Level flight trial");
            var reopened=new ConfigStore(file);check(ConfigStore.B(reopened.Snapshot(),"navigationLevelFlight")&&JsonNode.DeepEquals(normal,reopened.Snapshot()["programs"]![0]),"Trial persists in its program without changing Normal mouse");
            reopened.ApplyPreset(0);check(!ConfigStore.B(reopened.Snapshot(),"navigationLevelFlight"),"Before-trial preset restores view-direction navigation");
            reopened.ApplyPreset(1);check(ConfigStore.B(reopened.Snapshot(),"navigationLevelFlight"),"Trial preset restores level navigation");
            var legacy=reopened.Snapshot();legacy["presets"]![0]!["settings"]!["feel"]!.AsObject().Remove("navigationLevelFlight");File.WriteAllText(file,legacy.ToJsonString());
            var oldPreset=new ConfigStore(file);oldPreset.ApplyPreset(0);check(!ConfigStore.B(oldPreset.Snapshot(),"navigationLevelFlight"),"A legacy preset missing the new flag restores original navigation");
            check(ConfigStore.S(oldPreset.Snapshot(),"navigateKeys")=="backtick"&&!ConfigStore.B(oldPreset.Snapshot(),"spaceMouseCruise"),"Level presets retain Backtick and keep general cruise off");
        }finally{foreach(var p in new[]{file,file+".previous",file+".writing"})if(File.Exists(p))File.Delete(p);}
        ExportTrace();
    }
    static object View(CameraPose p)=>new{eye=new[]{p.Eye.X,p.Eye.Y,p.Eye.Z},target=new[]{p.Target.X,p.Target.Y,p.Target.Z},up=new[]{p.Up.X,p.Up.Y,p.Up.Z},pivot=new[]{p.Pivot.X,p.Pivot.Y,p.Pivot.Z},zoomPivot=new[]{p.Pivot.X,p.Pivot.Y,p.Pivot.Z},worldUp=new[]{p.WorldUpAxis.X,p.WorldUpAxis.Y,p.WorldUpAxis.Z},verticalFov=p.VerticalFov,orthoHeight=p.OrthoHeight,width=p.Width,height=p.Height,orthographic=p.Orthographic};
    static void ExportTrace(){
        var cases=new List<object>();
        foreach(var sample in new[]{("level-look-down",-.7f,0f,0f,-12f,0f),("level-look-up",.7f,0f,0f,-12f,0f),("level-looking-straight-down",-MathF.PI/2,0f,0f,-12f,0f),("level-rolled-height",-.7f,.6f,0f,0f,60f),("level-turn-right",-.7f,0f,12f,-12f,0f),("level-reverse-left",.7f,.6f,-12f,12f,0f)}){
            var start=Pose(Vector3.UnitY,sample.Item2,sample.Item3);var pose=start;var rate=new NavigationMotion();var poses=new List<object>();
            for(int i=0;i<32;i++){var motion=rate.Step(i==0?sample.Item4:0,i==0?sample.Item5:0,i==0?sample.Item6:0,1000+i*8);pose=CameraMath.Fly(pose,motion,MotionPolicy.Gain(38),MotionPolicy.Gain(38),true);poses.Add(View(pose));}
            cases.Add(new{label=sample.Item1,mode="Navigate",levelFlight=true,steers=sample.Item4!=0&&sample.Item5!=0,turnSign=Math.Sign(sample.Item4),heightTravel=sample.Item6!=0,start=View(start),poses});
        }
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-level-flight-trace.json"),JsonSerializer.Serialize(new{cases}));
    }
}
