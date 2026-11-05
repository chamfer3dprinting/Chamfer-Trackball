using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
internal static class ThreeAxisChecks {
    static CameraPose Pose(bool ortho=false)=>new(new(0,0,200),Vector3.Zero,Vector3.UnitY,new(12,-8,20),.8f,200,1200,800,ortho,Vector3.UnitY,new(12,-8,20));
    static Vector3 Forward(CameraPose p)=>Vector3.Normalize(p.Target-p.Eye);
    static Vector2 Screen(CameraPose p,Vector3 point){
        var back=Vector3.Normalize(p.Eye-p.Target);var right=Vector3.Normalize(Vector3.Cross(p.Up,back));var up=Vector3.Normalize(Vector3.Cross(back,right));var d=point-p.Eye;
        float scale=p.Orthographic?p.OrthoHeight:2*-Vector3.Dot(d,back)*MathF.Tan(p.VerticalFov/2);
        return new(Vector3.Dot(d,right)/scale,Vector3.Dot(d,up)/scale);
    }
    public static void RunMotion(Action<bool,string> check){
        foreach(bool ortho in new[]{false,true}){
            var start=Pose(ortho);var screen=Screen(start,start.Pivot);
            foreach(var input in new[]{(12f,0f,0f),(0f,12f,0f),(0f,0f,60f),(12f,8f,60f)}){
                var scaled=MotionPolicy.Scale(NavMode.Orbit,input.Item1,input.Item2,input.Item3,.4f,.3f,.2f,.5f);
                var rotated=CameraMath.Move(start,NavMode.Orbit,scaled.X,scaled.Y,scaled.Wheel);
                check(rotated!=start&&Vector2.Distance(Screen(rotated,start.Pivot),screen)<.00001f,"Every rotation axis stays anchored at the off-centre cursor");
                check(Math.Abs(Vector3.Distance(rotated.Eye,rotated.Pivot)-Vector3.Distance(start.Eye,start.Pivot))<.0001f,"Rotation changes orientation without changing cursor-pivot distance");
            }
            var twistOnly=MotionPolicy.Scale(NavMode.Orbit,12,12,60,0,1,1,.4f);
            var roll=CameraMath.Move(start,NavMode.Orbit,twistOnly.X,twistOnly.Y,twistOnly.Wheel);
            check(twistOnly.X==0&&twistOnly.Y==0&&roll.Up!=start.Up,"Twist speed still rolls the view when ball rotation speed is zero");
            var stop=MotionPolicy.Scale(NavMode.Orbit,12,12,60,0,1,1,0);
            check(CameraMath.Move(start,NavMode.Orbit,stop.X,stop.Y,stop.Wheel)==start,"Zero ball and twist rotation speed stops all rotation axes");
            var panX=CameraMath.Move(start,NavMode.PanZoom,12,0);var panY=CameraMath.Move(start,NavMode.PanZoom,0,12);
            check(panX.Eye.X<start.Eye.X&&panX.Eye.Y==start.Eye.Y&&panX.Up==start.Up&&Vector3.Distance(Forward(panX),Forward(start))<.00001f,"Rightward ball translation moves the visible object right without rotating");
            check(panY.Eye.Y>start.Eye.Y&&panY.Eye.X==start.Eye.X&&panY.Up==start.Up,"Downward ball translation moves the visible object down without rotating");
            var zoom=CameraMath.Move(start,NavMode.PanZoom,0,0,60);
            check(Vector2.Distance(Screen(zoom,start.Pivot),screen)<.00001f&&Vector3.Distance(Forward(zoom),Forward(start))<.00001f,"Translation twist zooms at the cursor without rotating");
            check(ortho?zoom.OrthoHeight<start.OrthoHeight:Vector3.Distance(zoom.Eye,zoom.Target)<Vector3.Distance(start.Eye,start.Target),"Positive translation twist moves in; reverse twist moves out");
            foreach(var normal in new[]{Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ}){
                var snapped=CameraMath.AlignFace(start,normal);
                check(Vector3.Distance(Vector3.Normalize(snapped.Eye-snapped.Target),normal)<.00001f&&snapped.Pivot==start.Pivot&&Vector2.Distance(Screen(snapped,start.Pivot),screen)<.00001f,"Each flat face snap rotates about the cursor point and keeps it fixed");
            }
        }
        foreach(var axis in new[]{(12f,0f),( -12f,0f),(0f,-12f),(0f,12f),(12f,-12f),(-12f,12f)}){
            var rate=new NavigationMotion();var a=rate.Step(axis.Item1,axis.Item2,0,1000,ballTwist:true);var next=rate.Step(0,0,0,1008,ballTwist:true);
            check(a.Yaw==0&&a.Pitch==0&&a.Up==0&&Math.Sign(a.Right)==Math.Sign(axis.Item1)&&Math.Sign(a.Forward)==-Math.Sign(axis.Item2),"Walk ball directions translate independently without pitching or steering");
            check(next.Active,"Walk ball movement continues while only Backtick stays held");
            var centre=rate.Step(-axis.Item1,-axis.Item2,0,1016,ballTwist:true);
            check(!centre.Active&&rate.Centered,"Returning the walk ball to centre stops its translation");
        }
        foreach(float wheel in new[]{-60f,60f}){
            var rate=new NavigationMotion();var frame=rate.Step(0,0,wheel,1000,ballTwist:true);
            check(frame.Up==0&&frame.Forward==0&&frame.Right==0&&Math.Sign(frame.Yaw)==Math.Sign(wheel)&&frame.Pitch==0,"Walk knob twist controls yaw instead of height or translation");
            var start=Pose();var turn=CameraMath.Fly(start,frame,.4f,.4f,true);
            check(turn.Eye==start.Eye&&Forward(turn).X*wheel>0&&Math.Abs(Forward(turn).Y)<.00001f,"Twist-only walk turns right/left in place without bobbing or pitching");
            var centre=rate.Step(0,0,-wheel,1008,ballTwist:true);check(!centre.Active&&rate.Centered,"Reverse twist back to centre stops turning while Backtick stays held");
        }
        var combined=new NavigationMotion();var f=combined.Step(12,-12,60,2000,ballTwist:true);var view=CameraMath.Fly(Pose(),f,.4f,.4f,true);
        check(view.Eye.X>0&&view.Eye.Z<200&&view.Eye.Y==0&&Forward(view).X>0,"Ball travel and knob turning combine continuously and simultaneously");
        check(CameraMath.Fly(Pose(),f,0,0,true)==Pose(),"Zero walk translation and twist sensitivity stops the combined motion");
        check(!combined.Step(0,0,0,2008,finish:true,ballTwist:true).Active&&combined.Centered,"Backtick release clears all three walk commands");
        var low=CameraMath.Move(Pose(),NavMode.Orbit,0,0,60*MotionPolicy.Gain(10));var high=CameraMath.Move(Pose(),NavMode.Orbit,0,0,60*MotionPolicy.Gain(38));
        check(Vector3.Distance(high.Up,Pose().Up)>Vector3.Distance(low.Up,Pose().Up)*10,"Twist speed has a useful range for third-axis rotation");
    }
    public static void Run(Action<bool,string> check){
        RunMotion(check);
        var defaults=ConfigStore.Defaults();check(!ConfigStore.B(defaults,"threeAxisControls"),"Normal mouse keeps its ordinary inputs");
        ConfigStore.LoadProgram(defaults,1);
        check(ConfigStore.B(defaults,"threeAxisControls")&&ModeResolver.Resolve(defaults,new HashSet<int>{16})==NavMode.Orbit&&ModeResolver.Resolve(defaults,new HashSet<int>{18})==NavMode.PanZoom&&ModeResolver.Resolve(defaults,new HashSet<int>{192})==NavMode.Navigate,"Shift rotation, Alt translation and Backtick walk match the owner's diagrams");
        check(ConfigStore.S(defaults,"snapKeys")=="none"&&ConfigStore.S(defaults["buttons"]?["back"],"action")=="faceSnap","Face snapping defaults to the top-right button rather than Alt");
        var file=Path.Combine(Path.GetTempPath(),"chamfer-three-axis-"+Guid.NewGuid().ToString("N")+".json");
        try{
            var store=new ConfigStore(file);store.Select(1);var cfg=store.Snapshot();var normal=cfg["programs"]![0]!.DeepClone();
            cfg["threeAxisControls"]=false;store.Save(cfg);store.SavePreset("Before three-axis controls");cfg=store.Snapshot();cfg["threeAxisControls"]=true;store.Save(cfg);store.SavePreset("Diagram controls");
            var reopened=new ConfigStore(file);check(ConfigStore.B(reopened.Snapshot(),"threeAxisControls")&&JsonNode.DeepEquals(normal,reopened.Snapshot()["programs"]![0]),"Three-axis choice persists per program without altering Normal mouse");
            reopened.ApplyPreset(0);check(!ConfigStore.B(reopened.Snapshot(),"threeAxisControls"),"Rollback preset restores the previous navigation mapping");
            reopened.ApplyPreset(1);check(ConfigStore.B(reopened.Snapshot(),"threeAxisControls"),"Saved diagram preset restores the three independent input axes");
            var old=reopened.Snapshot();old["presets"]![0]!["settings"]!["feel"]!.AsObject().Remove("threeAxisControls");File.WriteAllText(file,old.ToJsonString());
            var legacy=new ConfigStore(file);legacy.ApplyPreset(0);check(!ConfigStore.B(legacy.Snapshot(),"threeAxisControls"),"Older saved presets keep their previous mapping until explicitly changed");
        }finally{foreach(var p in new[]{file,file+".previous",file+".writing"})if(File.Exists(p))File.Delete(p);}
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-three-axis-trace.json"),Export());
    }
    static object View(CameraPose p)=>new{eye=new[]{p.Eye.X,p.Eye.Y,p.Eye.Z},target=new[]{p.Target.X,p.Target.Y,p.Target.Z},up=new[]{p.Up.X,p.Up.Y,p.Up.Z},pivot=new[]{p.Pivot.X,p.Pivot.Y,p.Pivot.Z},zoomPivot=new[]{p.ZoomPivot!.Value.X,p.ZoomPivot.Value.Y,p.ZoomPivot.Value.Z},worldUp=new[]{p.WorldUpAxis.X,p.WorldUpAxis.Y,p.WorldUpAxis.Z},verticalFov=p.VerticalFov,orthoHeight=p.OrthoHeight,width=p.Width,height=p.Height,orthographic=p.Orthographic};
    public static string Export(){
        var cases=new List<object>();
        foreach(var input in new[]{("rotation-pitch",NavMode.Orbit,0f,8f,0f),("rotation-yaw",NavMode.Orbit,12f,0f,0f),("rotation-roll",NavMode.Orbit,0f,0f,60f),("rotation-combined",NavMode.Orbit,12f,8f,60f),("translation-right",NavMode.PanZoom,12f,0f,0f),("translation-down",NavMode.PanZoom,0f,8f,0f),("translation-depth",NavMode.PanZoom,0f,0f,60f),("translation-combined",NavMode.PanZoom,12f,8f,60f),("walk-forward",NavMode.Navigate,0f,-12f,0f),("walk-back",NavMode.Navigate,0f,12f,0f),("walk-right",NavMode.Navigate,12f,0f,0f),("walk-left",NavMode.Navigate,-12f,0f,0f),("walk-twist-right",NavMode.Navigate,0f,0f,60f),("walk-twist-left",NavMode.Navigate,0f,0f,-60f),("walk-combined-right",NavMode.Navigate,12f,-12f,60f),("walk-combined-left",NavMode.Navigate,-12f,12f,-60f)}){
            var start=Pose();var pose=start;var rate=new NavigationMotion();var poses=new List<object>();
            for(int i=0;i<32;i++){
                if(input.Item2==NavMode.Navigate)pose=CameraMath.Fly(pose,rate.Step(i==0?input.Item3:0,i==0?input.Item4:0,i==0?input.Item5:0,1000+i*8,ballTwist:true),MotionPolicy.Gain(38),MotionPolicy.Gain(37),true);
                else{var scaled=MotionPolicy.Scale(input.Item2,input.Item3/4,input.Item4/4,input.Item5/4,MotionPolicy.Gain(38),MotionPolicy.Gain(38),MotionPolicy.Gain(38),MotionPolicy.Gain(37));pose=CameraMath.Move(pose,input.Item2,scaled.X,scaled.Y,scaled.Wheel);}
                poses.Add(View(pose));
            }
            cases.Add(new{label=input.Item1,mode=input.Item2.ToString(),threeAxis=true,levelFlight=input.Item2==NavMode.Navigate,heightTravel=false,lookOnly=input.Item2==NavMode.Navigate&&input.Item3==0&&input.Item4==0,steers=input.Item2==NavMode.Navigate&&input.Item5!=0,turnSign=Math.Sign(input.Item5),start=View(start),poses});
        }
        int faceIndex=0;
        foreach(var face in new[]{Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ}){
            var start=Pose(true);var snapped=CameraMath.AlignFace(start,face);cases.Add(new{label="button-snap-"+faceIndex++,mode="Snap",threeAxis=true,start=View(start),poses=new[]{View(snapped)}});
        }
        return JsonSerializer.Serialize(new{cases});
    }
}
