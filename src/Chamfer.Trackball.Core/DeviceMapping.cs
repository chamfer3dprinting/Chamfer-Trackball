namespace Chamfer.Trackball.Core;

// Windows mouse messages are retained here so the mapping policy is testable
// without installing hooks or changing any system mouse setting.
public readonly record struct MouseEdge(int Message,int Data=0){
 public bool Down=>Message is 0x201 or 0x204 or 0x207 or 0x20b;
 public bool Up=>Message is 0x202 or 0x205 or 0x208 or 0x20c;
 public bool Wheel=>Message is 0x20a or 0x20e;
 public int Button=>Message switch{0x201 or 0x202=>1,0x204 or 0x205=>2,0x207 or 0x208=>3,0x20b or 0x20c=>3+Data,_=>0};
 public MouseEdge Release()=>this with{Message=Message+1};
}
public sealed class DeviceMapping{
 readonly Dictionary<(string,int),MouseEdge> held=[];
 public int HeldCount=>held.Count;
 public MouseEdge Map(string device,string selected,MouseEdge edge,bool swapButtons,bool swapWheels){
  var key=(device,edge.Button);
  if(edge.Up&&held.Remove(key,out var down))return down.Release();
  bool target=selected.Length>0&&device.Equals(selected,StringComparison.OrdinalIgnoreCase);
  var result=edge;
  if(target&&swapButtons)result=edge with{Message=edge.Message switch{0x201=>0x204,0x202=>0x205,0x204=>0x201,0x205=>0x202,_=>edge.Message}};
  if(target&&swapWheels&&edge.Wheel)result=edge with{Message=edge.Message==0x20a?0x20e:0x20a};
  if(edge.Down){if(held.TryGetValue(key,out var previous))return previous;held[key]=result;}
  return result;
 }
 public MouseEdge[] ReleaseAll(){var r=held.Values.Select(x=>x.Release()).Distinct().ToArray();held.Clear();return r;}
}
// Raw Input and low-level hooks may arrive in either order. The app synchronizes
// access between its input hook thread and the UI thread.
// Keep both observations, including hooks that passed through, so a stale raw
// event can never become a duplicate click when routing is enabled later.
public readonly record struct MouseMatch(string Device,MouseEdge Edge,bool Blocked);
public sealed class MouseGate{
 readonly List<(MouseEdge Edge,long Time,bool Blocked)> hooks=[];
 readonly List<(string Device,MouseEdge Edge,long Time)> raw=[];
 public int Count=>hooks.Count(p=>p.Blocked);
 public MouseMatch? Hook(MouseEdge edge,long now,bool blocked){
  Prune(now);
  var i=raw.FindIndex(p=>p.Edge==edge);
  if(i>=0){var item=raw[i];raw.RemoveAt(i);return new(item.Device,edge,blocked);}
  hooks.Add((edge,now,blocked));return null;
 }
 public MouseMatch? Raw(string device,MouseEdge edge,long now){
  Prune(now);
  var i=hooks.FindIndex(p=>p.Edge==edge);
  if(i>=0){var item=hooks[i];hooks.RemoveAt(i);return new(device,edge,item.Blocked);}
  raw.Add((device,edge,now));return null;
 }
 void Prune(long now){
  raw.RemoveAll(p=>now-p.Time>150);
  hooks.RemoveAll(p=>!p.Blocked&&now-p.Time>150);
 }
 public bool Expired(long now){Prune(now);return hooks.Any(p=>p.Blocked&&now-p.Time>150);}
 public MouseEdge[] Drain(){var r=hooks.Where(p=>p.Blocked).Select(p=>p.Edge).ToArray();hooks.Clear();raw.Clear();return r;}
}
// An idle ball is healthy. Fail open only when legacy movement continues for
// half a second without any corresponding Raw Input movement.
public sealed class MotionHealth{
 long? waitingSince;long lastBlocked;
 public void Block(long now){waitingSince??=now;lastBlocked=now;}
 public void Raw()=>waitingSince=null;
 public bool Expired(long now)=>waitingSince is long since&&lastBlocked-since>500&&now-lastBlocked<150;
 public void Reset(){waitingSince=null;lastBlocked=0;}
}
