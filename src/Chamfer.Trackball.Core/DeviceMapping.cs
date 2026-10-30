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
public sealed class MouseGate{
 readonly List<(MouseEdge Edge,long Time)> pending=[];
 public int Count=>pending.Count;
 public void Block(MouseEdge edge,long now)=>pending.Add((edge,now));
 public bool Take(MouseEdge raw){
  var i=pending.FindIndex(p=>p.Edge==raw);if(i<0)return false;pending.RemoveAt(i);return true;
 }
 public bool Expired(long now)=>pending.Any(p=>now-p.Time>150);
 public MouseEdge[] Drain(){var r=pending.Select(p=>p.Edge).ToArray();pending.Clear();return r;}
}
