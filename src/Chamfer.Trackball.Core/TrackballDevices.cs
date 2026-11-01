using System.Text.Json.Nodes;
namespace Chamfer.Trackball.Core;

public sealed record TrackballDevice(string Id,string Name,bool SwapButtons=false,bool SwapWheels=false);
public static class TrackballDevices{
 public const int Maximum=16;
 static string Identity(string value){
  if(string.IsNullOrWhiteSpace(value)||value.Length>4095||value.Any(char.IsControl))throw new InvalidDataException("Invalid trackball identity.");
  return value;
 }
 public static TrackballDevice[] Read(JsonObject config){
  if(config["trackballs"] is not JsonArray list||list.Count>Maximum)throw new InvalidDataException("Use a valid list of up to 16 trackballs.");
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var result=new List<TrackballDevice>();
  foreach(var node in list){
   if(node is not JsonObject row)throw new InvalidDataException("Invalid saved trackball.");
   var id=Identity(ConfigStore.S(row,"id"));var name=ConfigStore.S(row,"name").Trim();
   if(name.Length is <1 or >60||name.Any(char.IsControl)||!seen.Add(id))throw new InvalidDataException("Use a unique trackball identity and a name with 1–60 characters.");
   result.Add(new(id,name,ConfigStore.B(row,"swapButtons"),ConfigStore.B(row,"swapWheels")));
  }
  return result.ToArray();
 }
 public static void Normalize(JsonObject config){
  config["trackballs"]??=new JsonArray();var list=Read(config);
  var selected=ConfigStore.S(config,"selectedDevice");
  if(selected.Length>0&&!list.Any(d=>d.Id.Equals(selected,StringComparison.OrdinalIgnoreCase))){
   Identity(selected);if(list.Length>=Maximum)throw new InvalidDataException("You can remember up to 16 trackballs.");
   config["trackballs"]!.AsArray().Add(new JsonObject{["id"]=selected,["name"]="Trackball "+(list.Length+1),["swapButtons"]=ConfigStore.B(config,"trackballSwapButtons"),["swapWheels"]=ConfigStore.B(config,"trackballSwapWheels")});
  }
 }
 static JsonObject Row(JsonObject config,string id)=>config["trackballs"]!.AsArray().OfType<JsonObject>().FirstOrDefault(d=>ConfigStore.S(d,"id").Equals(id,StringComparison.OrdinalIgnoreCase))??throw new InvalidDataException("Identify this trackball first.");
 public static void Remember(JsonObject config,string id){
  Identity(id);Normalize(config);
  if(!Read(config).Any(d=>d.Id.Equals(id,StringComparison.OrdinalIgnoreCase))){
   var list=config["trackballs"]!.AsArray();if(list.Count>=Maximum)throw new InvalidDataException("You can remember up to 16 trackballs.");
   list.Add(new JsonObject{["id"]=id,["name"]="Trackball "+(list.Count+1),["swapButtons"]=false,["swapWheels"]=false});
  }
  Select(config,id);
 }
 public static void Select(JsonObject config,string id){
  var row=Row(config,id);config["selectedDevice"]=ConfigStore.S(row,"id");
  config["trackballSwapButtons"]=ConfigStore.B(row,"swapButtons");config["trackballSwapWheels"]=ConfigStore.B(row,"swapWheels");
 }
 public static void SaveCorrections(JsonObject config){
  var id=ConfigStore.S(config,"selectedDevice");if(id.Length==0)return;
  var row=Row(config,id);row["swapButtons"]=ConfigStore.B(config,"trackballSwapButtons");row["swapWheels"]=ConfigStore.B(config,"trackballSwapWheels");
 }
 public static void Rename(JsonObject config,string id,string name){
  name=name.Trim();if(name.Length is <1 or >60||name.Any(char.IsControl))throw new InvalidDataException("Use a trackball name with 1–60 characters.");
  Row(config,id)["name"]=name;
 }
 public static void ResetCorrections(JsonObject config){
  foreach(var row in config["trackballs"]!.AsArray().OfType<JsonObject>()){row["swapButtons"]=false;row["swapWheels"]=false;}
 }
}

// Only explicitly identified devices can take over navigation. Ordinary mice
// never join this list just because they emit HID movement.
public sealed class TrackballRoster{
 readonly object gate=new();Dictionary<string,TrackballDevice> devices=new(StringComparer.OrdinalIgnoreCase);
 HashSet<string> present=new(StringComparer.OrdinalIgnoreCase);string preferred="",active="";
 public string ActiveId{get{lock(gate)return active;}}
 public int Count{get{lock(gate)return devices.Count;}}
 public TrackballDevice? Find(string id){lock(gate)return devices.GetValueOrDefault(id);}
 public bool Configure(IEnumerable<TrackballDevice> entries,string selected){lock(gate){
  var next=entries.ToDictionary(d=>d.Id,StringComparer.OrdinalIgnoreCase);string before=active;
  bool changedPreference=!preferred.Equals(selected,StringComparison.OrdinalIgnoreCase);preferred=selected;devices=next;
  if(changedPreference||!devices.ContainsKey(active)){
   bool connectedPreference=present.Contains(preferred)&&devices.ContainsKey(preferred);
   active=connectedPreference?preferred:devices.ContainsKey(active)&&present.Contains(active)?active:devices.Keys.FirstOrDefault(present.Contains)??(devices.ContainsKey(preferred)?preferred:devices.Keys.FirstOrDefault()??"");
  }
  return !before.Equals(active,StringComparison.OrdinalIgnoreCase);
 }}
 public bool Observe(string id,bool canSwitch){lock(gate){
  if(!devices.ContainsKey(id))return false;present.Add(id);
  if(!canSwitch||active.Equals(id,StringComparison.OrdinalIgnoreCase))return false;
  active=devices[id].Id;return true;
 }}
 public bool UpdatePresence(IEnumerable<string> ids){lock(gate){
  present=new(ids,StringComparer.OrdinalIgnoreCase);string before=active;
  if(!present.Contains(active))active=present.Contains(preferred)&&devices.ContainsKey(preferred)?preferred:devices.Keys.FirstOrDefault(present.Contains)??preferred;
  return !before.Equals(active,StringComparison.OrdinalIgnoreCase);
 }}
 public (TrackballDevice Device,bool Connected,bool Active,bool Editing)[] Snapshot(){lock(gate)
  return devices.Values.Select(d=>(d,present.Contains(d.Id),active.Equals(d.Id,StringComparison.OrdinalIgnoreCase),preferred.Equals(d.Id,StringComparison.OrdinalIgnoreCase))).ToArray();
 }
}
