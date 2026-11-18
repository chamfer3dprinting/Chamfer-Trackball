using System.Numerics;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
internal static class NavigationLayerChecks {
    static CameraPose Pose()=>new(new(0,0,200),Vector3.Zero,Vector3.UnitY,new(12,-8,20),.8f,200,1200,800,false,Vector3.UnitY,new(12,-8,20));
    static HashSet<int> Keys(params int[] keys)=>new(keys);
    public static void Run(Action<bool,string> check){
        var cfg=ConfigStore.Defaults();ConfigStore.LoadProgram(cfg,1);
        check(ModeResolver.Resolve(cfg,Keys(192))==NavMode.Navigate&&NavigationBindings.Resolve(cfg,Keys(192))==NavigationLayer.Travel,"Backtick alone retains camera travel");
        check(ModeResolver.Resolve(cfg,Keys(192,49))==NavMode.Navigate&&NavigationBindings.Resolve(cfg,Keys(192,49))==NavigationLayer.Look,"Backtick plus 1 selects look without ending Navigate");
        check(ModeResolver.Resolve(cfg,Keys(192,17))==NavMode.Navigate&&NavigationBindings.Resolve(cfg,Keys(192,17))==NavigationLayer.Height,"Backtick plus Ctrl selects camera height");
        check(ModeResolver.Resolve(cfg,Keys(49))==NavMode.Idle&&ModeResolver.Resolve(cfg,Keys(17))==NavMode.Zoom,"The extra key alone types and Ctrl alone retains Zoom");
        check(ModeResolver.Resolve(cfg,Keys(192,49,65))==NavMode.Idle&&ModeResolver.Resolve(cfg,Keys(192,17,49))==NavMode.Idle,"Unassigned extra keys cannot ambiguously activate a navigation layer");
        check(ModeResolver.Resolve(cfg,Keys(16))==NavMode.Orbit&&ModeResolver.Resolve(cfg,Keys(18))==NavMode.PanZoom,"Shift rotation and Alt translation remain independent");
        check(NavigationBindings.Label(cfg,NavigationLayer.Look)=="backtick+1"&&NavigationBindings.Chord(cfg,NavigationLayer.Look)!.Matches(Keys(192,49)),"The keyboard guide combines the base hold and extra ordinary key");
        ConfigStore.LoadProgram(cfg,0);
        check(ModeResolver.Resolve(cfg,Keys(192,49))==NavMode.Idle&&ModeResolver.Resolve(cfg,Keys(192,17))==NavMode.Idle,"Normal mouse leaves both extra holds alone");
        ConfigStore.LoadProgram(cfg,1);cfg["navigateKeys"]="space";cfg["navigateLookKeys"]="q";cfg["navigateHeightKeys"]="shift";
        ConfigStore.Validate(cfg); // root values are applied to the program by Save below
        check(NavigationBindings.Resolve(cfg,Keys(32,81))==NavigationLayer.Look&&NavigationBindings.Resolve(cfg,Keys(32,16))==NavigationLayer.Height&&NavigationBindings.Resolve(cfg,Keys(192,49))==null,"Custom base/look/height bindings use their own keys");
        cfg["navigateLookKeys"]="space";bool overlap=false;try{NavigationBindings.Chord(cfg,NavigationLayer.Look);}catch(FormatException){overlap=true;}
        check(overlap,"A look extra cannot repeat the base Camera walk key");

        var rate=new NavigationMotion();var start=Pose();
        var look=rate.Step(12,-9,120,1000,ballTwist:true,control:NavigationLayer.Look);
        var view=CameraMath.Fly(start,look,.3f,.2f,true,true);
        check(look.Right==0&&look.Up==0&&look.Forward==0&&look.Yaw==12&&look.Pitch==9,"Look maps ball axes to heading and pitch without starting travel or using twist");
        check(view.Eye==start.Eye&&(view.Target-view.Eye).X>0&&(view.Target-view.Eye).Y>0,"Looking right/up turns about the camera eye");
        check(Math.Abs(Vector3.Distance(view.Eye,view.Target)-Vector3.Distance(start.Eye,start.Target))<.0001f,"Look retains camera lens distance");
        check(!rate.Step(0,0,0,1008,ballTwist:true,control:NavigationLayer.Look).Active,"No new look movement means no continued unwanted turning");
        var opposite=CameraMath.Fly(start,NavigationMotion.Direct(-12,9,0,NavigationLayer.Look),.3f,.2f,true,true);
        check((opposite.Target-opposite.Eye).X<0&&(opposite.Target-opposite.Eye).Y<0,"Looking left/down follows the opposite ball directions");
        rate.Reset();var walking=rate.Step(0,-12,0,2000,ballTwist:true);
        var aimed=rate.Step(15,-20,0,2008,ballTwist:true,control:NavigationLayer.Look);
        var after=rate.Step(0,0,0,2016,ballTwist:true);
        check(walking.Forward>0&&aimed.Forward>0&&aimed.Yaw>0&&aimed.Pitch>0&&after.Forward>0&&after.Pitch==0&&after.Yaw==0,"Travel continues during look and releasing the extra key keeps the saved travel command");
        var stopped=rate.Step(0,12,0,2024,ballTwist:true);
        check(!stopped.Active&&rate.Centered,"A look stroke cannot move the travel centre needed to stop");
        rate.Reset();rate.Step(0,0,120,3000,ballTwist:true);var aimAfterTwist=rate.Step(0,0,0,3008,ballTwist:true,control:NavigationLayer.Look);
        check(!aimAfterTwist.Active&&!rate.Step(0,0,0,3016,ballTwist:true).Active,"Entering and leaving look clears an old knob-turn rate");
        rate.Reset();var up=rate.Step(0,-12,0,4000,ballTwist:true,control:NavigationLayer.Height);
        var holdUp=rate.Step(0,0,0,4008,ballTwist:true,control:NavigationLayer.Height);
        check(up.Up>0&&up.Forward==0&&up.Pitch==0&&holdUp.Up>up.Up,"Height hold raises the camera continuously without pitching or starting forward travel");
        check(!rate.Step(0,12,0,4016,ballTwist:true,control:NavigationLayer.Height).Active&&rate.Centered,"Returning the height stroke to centre stops elevation");
        rate.Reset();var down=rate.Step(0,12,0,4100,ballTwist:true,control:NavigationLayer.Height);
        check(down.Up<0&&down.Forward==0&&down.Yaw==0,"Backward height roll lowers the camera");
        check(!rate.Step(0,0,0,4108,ballTwist:true).Active,"Releasing the height modifier clears elevation immediately");
        rate.Reset();rate.Step(0,-12,0,5000,ballTwist:true);var raiseWalking=rate.Step(8,-12,120,5008,ballTwist:true,control:NavigationLayer.Height);
        check(raiseWalking.Up>0&&raiseWalking.Forward>0&&raiseWalking.Right>0&&raiseWalking.Yaw>0,"Height can combine with already active travel, sideways movement and knob heading");
        var returned=rate.Step(0,0,0,5016,ballTwist:true);
        check(returned.Up==0&&returned.Forward>0,"Leaving Height stops vertical movement while retaining the prior travel command");
        check(!rate.Step(0,0,0,5024,true,ballTwist:true).Active&&!rate.Step(0,0,0,5032,ballTwist:true).Active,"Releasing Camera walk clears every layer and latched command");
        rate.Reset();rate.Step(0,-12,0,6000,ballTwist:true,control:NavigationLayer.Height);
        check(!rate.Step(0,0,0,6400,ballTwist:true,control:NavigationLayer.Height).Active,"A timer pause cannot replay stale elevation");
        foreach(var gain in new[]{0f,MotionPolicy.Gain(1),MotionPolicy.Gain(38),MotionPolicy.Gain(100)}){
            var h=CameraMath.Fly(start,new(0,10,0),gain,.3f,true);
            var l=CameraMath.Fly(start,new(0,0,0,12,9),.3f,gain,true,true);
            check(gain==0?h==start&&l==start:h.Eye.Y>start.Eye.Y&&(l.Target-l.Eye).Y>0,"Look and height honor sensitivity "+gain);
        }
        var tilted=CameraMath.Look(start,10,-80,.5f);var raised=CameraMath.Fly(tilted,new(0,10,0),.3f,0,true);
        check(raised.Eye.Y>tilted.Eye.Y&&Math.Abs(raised.Eye.X-tilted.Eye.X)<.0001f&&Math.Abs(raised.Eye.Z-tilted.Eye.Z)<.0001f&&Vector3.Distance(raised.Target-raised.Eye,tilted.Target-tilted.Eye)<.0001f,"Height follows world up and keeps the tilted camera direction");
        var direct=NavigationMotion.Direct(5,-7,120,NavigationLayer.Height);
        check(direct==new NavigationFrame(5,7,0,30)&&NavigationMotion.Direct(5,-7,120,NavigationLayer.Look)==new NavigationFrame(0,0,0,5,7),"Non-continuous navigation supports the same height/look layers");
        var previousAmount=0f;
        foreach(int speed in new[]{1,10,27,38,50,75,100}){
            float amount=Vector3.Distance(start.Eye,CameraMath.Fly(start,new(0,10,0),MotionPolicy.Gain(speed),0,true).Eye);
            check(amount>previousAmount,"Height speed increases monotonically at "+speed);previousAmount=amount;
        }
        var file=Path.Combine(Path.GetTempPath(),"chamfer-nav-layers-"+Guid.NewGuid().ToString("N")+".json");
        try{
            var store=new ConfigStore(file);store.Select(1);cfg=store.Snapshot();var normal=cfg["programs"]![0]!.DeepClone();
            store.SavePreset("Default walk");cfg["navigateLookKeys"]="q";cfg["navigateHeightKeys"]="shift";store.Save(cfg);store.SavePreset("Custom walk");
            var opened=new ConfigStore(file);check(ConfigStore.S(opened.Snapshot(),"navigateLookKeys")=="q"&&ConfigStore.S(opened.Snapshot(),"navigateHeightKeys")=="shift","Both extra navigation holds survive saving and restarting");
            opened.ApplyPreset(0);check(ConfigStore.S(opened.Snapshot(),"navigateLookKeys")=="1"&&ConfigStore.S(opened.Snapshot(),"navigateHeightKeys")=="ctrl","Applying a preset restores the default look/height holds");
            opened.ApplyPreset(1);check(ConfigStore.S(opened.Snapshot(),"navigateLookKeys")=="q"&&JsonNode.DeepEquals(normal,opened.Snapshot()["programs"]![0]),"Custom hold presets preserve Normal mouse");
            cfg=opened.Snapshot();cfg["navigateLookKeys"]="backtick";bool rejected=false;try{opened.Save(cfg);}catch(FormatException){rejected=true;}
            check(rejected&&ConfigStore.S(opened.Snapshot(),"navigateLookKeys")=="q","A rejected overlapping hold cannot corrupt saved controls");
            cfg=opened.Snapshot();cfg["navigateKeys"]="q";cfg["navigateLookKeys"]="ctrl";cfg["navigateHeightKeys"]="shift";cfg["zoomKeys"]="ctrl+q";rejected=false;
            try{opened.Save(cfg);}catch(InvalidDataException){rejected=true;}
            check(rejected,"Combined walk holds cannot override another configured navigation chord");
            cfg=opened.Snapshot();foreach(var program in cfg["programs"]!.AsArray()){program!.AsObject().Remove("navigateLookKeys");program.AsObject().Remove("navigateHeightKeys");}
            foreach(var preset in cfg["presets"]!.AsArray()){preset!["settings"]!.AsObject().Remove("navigateLookKeys");preset["settings"]!.AsObject().Remove("navigateHeightKeys");}
            File.WriteAllText(file,cfg.ToJsonString());var legacy=new ConfigStore(file);cfg=legacy.Snapshot();
            check(ConfigStore.S(cfg["programs"]![1],"navigateLookKeys")=="none"&&ConfigStore.S(cfg["programs"]![1],"navigateHeightKeys")=="none","Existing programs do not silently acquire new keys");
            legacy.ApplyPreset(0);check(ConfigStore.S(legacy.Snapshot(),"navigateLookKeys")=="none"&&ConfigStore.S(legacy.Snapshot(),"navigateHeightKeys")=="none","An older saved preset restores the previous walk without extra holds");
        }finally{foreach(var path in new[]{file,file+".previous",file+".writing"})if(File.Exists(path))File.Delete(path);}
    }
}
