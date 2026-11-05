using Chamfer.Trackball.Core;

internal static class NativeDeliveryChecks
{
    internal static void Run(Action<bool,string> check)
    {
        const string slim="HID#VID_047D&PID_80D6&TEST";
        foreach(var mode in Enum.GetValues<NavMode>())
            check(NativeDelivery.Direct(slim,"chamfer3d",2,mode,true,false)==(mode is NavMode.Orbit or NavMode.PanZoom or NavMode.Zoom or NavMode.Navigate),"Immediate native delivery is limited to continuous camera modes: "+mode);
        check(!NativeDelivery.Direct("M512","chamfer3d",2,NavMode.Orbit,true,false),"M512 timer response is preserved");
        check(!NativeDelivery.Direct(slim,"blender",2,NavMode.Orbit,true,false),"Other integrations keep their existing timer response");
        check(!NativeDelivery.Direct(slim,"chamfer3d",1,NavMode.Orbit,true,false),"Legacy Studio remains compatible");
        check(!NativeDelivery.Direct(slim,"chamfer3d",2,NavMode.Orbit,false,false),"Steady motion off retains the glide path");
        check(!NativeDelivery.Direct(slim,"chamfer3d",2,NavMode.Orbit,true,true),"Optional cruise remains on its rate clock");
        check(!NativeDelivery.Circle(slim,NavMode.PanZoom,true,true),"Held SlimBlade panning cannot become circle zoom");
        check(NativeDelivery.Circle("M512",NavMode.PanZoom,true,true),"M512 circle zoom remains available");
        check(NativeDelivery.Circle(slim,NavMode.Orbit,true,false),"Optional free-ball circle zoom is preserved");
        check(!NativeDelivery.Circle(slim,NavMode.PanZoom,false,false),"Circle zoom off stays off");
        check(NativeDelivery.DisplayResponse(6)==6&&NativeDelivery.DisplayResponse(24)==24,"Saved short response reaches Studio without a hard-coded delay");
        check(NativeDelivery.DisplayResponse(0)==6&&NativeDelivery.DisplayResponse(40)==30&&NativeDelivery.DisplayResponse(float.NaN)==12,"Display response is bounded and finite");
        check(SlimBladeTwist.IsDevice(slim.ToLowerInvariant())&&!SlimBladeTwist.IsDevice("HID#VID_047D&PID_80D7&TEST"),"Immediate device recognition is precise and case insensitive");
    }
}
