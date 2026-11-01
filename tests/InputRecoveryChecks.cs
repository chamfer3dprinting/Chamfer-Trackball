using Chamfer.Trackball.Core;
static class InputRecoveryChecks{
 public static void Run(Action<bool,string> check){
  var recovery=new InputRecovery();
  check(!recovery.Observe(true,1000)&&!recovery.Observe(true,1100)&&!recovery.Observe(true,1200),"A transient input fault waits for stable watchdog ticks before resuming");
  check(recovery.Observe(true,1250),"A healthy idle device recovers without requiring another physical movement");
  recovery.Reset();
  check(!recovery.Observe(true,1000)&&!recovery.Observe(true,1200),"A renewed UI stall restarts the recovery grace period");
  check(!recovery.Observe(true,1300)&&!recovery.Observe(true,1400)&&recovery.Observe(true,1450),"Recovery resumes after the new healthy interval");
  recovery.Reset();recovery.Observe(true,1000);recovery.Observe(false,1100);
  check(!recovery.Observe(true,1200)&&!recovery.Observe(true,1300)&&!recovery.Observe(true,1400)&&recovery.Observe(true,1450),"Lost device, hook or registration eligibility resets recovery");
  recovery.Reset();
  check(!recovery.Observe(false,1000)&&!recovery.Observe(false,5000),"A permanent input fault or unavailable device never resumes automatically");
  recovery.Reset();recovery.Observe(true,1000);recovery.Observe(true,1100);
  check(!recovery.Observe(true,900)&&!recovery.Observe(true,1000)&&!recovery.Observe(true,1100)&&recovery.Observe(true,1150),"A backwards clock restarts the healthy interval safely");
  recovery.Reset();check(!recovery.Observe(true,0)&&!recovery.Observe(true,100)&&!recovery.Observe(true,200)&&recovery.Observe(true,250),"Recovery also handles an initial zero timestamp");
 }
}
