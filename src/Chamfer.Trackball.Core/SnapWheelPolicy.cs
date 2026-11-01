namespace Chamfer.Trackball.Core;

public static class SnapWheelPolicy{
 // The owner's confirmed native M512 layout reports the left wheel as vertical
 // and the right wheel as horizontal. Do not change device correction flags.
 public static bool Captures(NavMode mode,int message)=>(message is 0x20a or 0x20e)&&mode!=NavMode.Idle&&(mode!=NavMode.Snap||message==0x20a);
}
