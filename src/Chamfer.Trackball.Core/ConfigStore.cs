using System.Text.Json;
using System.Text.Json.Nodes;
namespace Chamfer.Trackball.Core;

public sealed class ConfigStore {
    private static readonly string[] ProgramFields=["buttons","leftWheel","rightWheel","modeButtons","combos","wheelPrograms","orbitKeys","panKeys","zoomKeys","snapKeys","navigateKeys","circleZoom","circleSize","gestures","pointerSpeed","ball","feel","leftWheelUpKeys","leftWheelDownKeys","rightWheelUpKeys","rightWheelDownKeys"];
    private readonly object gate=new();
    private readonly string path;
    private JsonObject config;
    public long Revision {get;private set;}
    public static JsonObject Defaults() {
        using var stream=typeof(ConfigStore).Assembly.GetManifestResourceStream("Chamfer.Trackball.Core.defaults.json")!;
        return JsonNode.Parse(stream)!.AsObject();
    }
    public ConfigStore(string path) {
        this.path=path; config=Defaults();
        if(File.Exists(path)){
            // Do not silently replace an unreadable configuration.
            config=JsonNode.Parse(File.ReadAllText(path))?.AsObject()??throw new InvalidDataException("Settings must be a JSON object.");
        }
        var oldVersion=I(config,"configVersion",0); Normalize(config);
        if(oldVersion<21){
            foreach(var node in config["programs"]!.AsArray()){
                var p=node!.AsObject();
                if(S(p,"name").Contains("3D",StringComparison.OrdinalIgnoreCase)){
                    p["snapKeys"]="alt";p["navigateKeys"]="backtick";
                    if(S(p,"orbitKeys")=="ctrl")p["orbitKeys"]="shift";
                }
            }
            LoadProgram(config,I(config,"activeProgram",0));config["configVersion"]=22;
        }
        Validate(config);
    }
    public JsonObject Snapshot(){lock(gate)return (JsonObject)config.DeepClone();}
    public void Save(JsonObject incoming) {
        lock(gate){
            var next=(JsonObject)incoming.DeepClone();Normalize(next);
            var i=Math.Clamp(I(next,"activeProgram",0),0,next["programs"]!.AsArray().Count-1);
            next["activeProgram"]=i;
            var p=next["programs"]![i]!.AsObject();
            foreach(var key in ProgramFields) if(key!="gestures"&&next.ContainsKey(key))p[key]=next[key]?.DeepClone();
            p["feel"]??=new JsonObject();
            foreach(var key in new[]{"orbitSens","panSens","zoomSens","invertOrbitX","invertOrbitY","invertPan","invertZoom","preciseSlow","clickSlop","smooth","glide"})if(next.ContainsKey(key))p["feel"]![key]=next[key]?.DeepClone();
            next["feel"]=p["feel"]!.DeepClone();
            Validate(next);Write(next);config=next;Revision++;
        }
    }
    public void Select(int index){lock(gate){var n=(JsonObject)config.DeepClone();LoadProgram(n,index);Write(n);config=n;Revision++;}}
    public JsonObject Reset(){lock(gate){var n=Defaults();foreach(var k in new[]{"theme","macros","welcomeDone","trayHintShown","selectedDevice"})if(config[k]!=null)n[k]=config[k]!.DeepClone();Write(n);config=n;Revision++;return Snapshot();}}
    private void Write(JsonObject n){
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var text=n.ToJsonString(new JsonSerializerOptions{WriteIndented=true});
        var tmp=path+".writing";File.WriteAllText(tmp,text);
        if(File.Exists(path))File.Copy(path,path+".previous",true);
        File.Move(tmp,path,true);
    }
    public static void LoadProgram(JsonObject c,int index){
        var ps=c["programs"]!.AsArray();index=Math.Clamp(index,0,ps.Count-1);c["activeProgram"]=index;
        var p=ps[index]!.AsObject();
        foreach(var k in ProgramFields)c[k]=p[k]?.DeepClone();
        if(p["feel"] is JsonObject feel)foreach(var x in feel)c[x.Key]=x.Value?.DeepClone();
    }
    public static void Normalize(JsonObject c){
        var d=Defaults();
        foreach(var x in d)if(!c.ContainsKey(x.Key))c[x.Key]=x.Value?.DeepClone();
        if(c["programs"] is not JsonArray ps||ps.Count==0)throw new InvalidDataException("At least one program is required.");
        foreach(var node in ps){
            var p=node?.AsObject()??throw new InvalidDataException("Invalid program.");
            foreach(var k in ProgramFields)if(!p.ContainsKey(k))p[k]=k is "snapKeys" or "navigateKeys"?JsonValue.Create("none"):d["programs"]![0]![k]?.DeepClone();
        }
        c["activeProgram"]=Math.Clamp(I(c,"activeProgram",0),0,ps.Count-1);c["configVersion"]=22;
    }
    public static void Validate(JsonObject c){
        foreach(var node in c["programs"]!.AsArray()){
            var p=node!.AsObject();var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var key in new[]{"orbitKeys","panKeys","zoomKeys","snapKeys","navigateKeys"}){
                var chord=S(p,key,"none");if(chord=="none"||chord=="")continue;
                var binding=KeyChord.Parse(chord);
                if(binding.IsCtrlAlone)throw new InvalidDataException("Ctrl alone stays a normal keyboard key. Choose another navigation binding.");
                if(!used.Add(binding.Canonical))throw new InvalidDataException("Two navigation modes cannot use the same held keys. Pan and zoom share the Pan + zoom mode.");
            }
            if(p["combos"] is JsonArray combos && combos.Count>64)throw new InvalidDataException("A program supports up to 64 combinations.");
        }
    }
    public static string S(JsonNode? n,string key,string fallback="")=>n?[key]?.GetValueKind()==JsonValueKind.String?n[key]!.GetValue<string>():fallback;
    public static int I(JsonNode? n,string key,int fallback=0)=>n?[key] is JsonValue v&&v.TryGetValue<int>(out var a)?a:fallback;
    public static double D(JsonNode? n,string key,double fallback=0)=>n?[key] is JsonValue v&&v.TryGetValue<double>(out var a)&&double.IsFinite(a)?a:fallback;
    public static bool B(JsonNode? n,string key,bool fallback=false)=>n?[key] is JsonValue v&&v.TryGetValue<bool>(out var a)?a:fallback;
}
