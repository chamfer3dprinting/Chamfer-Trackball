using System.Text.Json;
using System.Text.Json.Nodes;
namespace Chamfer.Trackball.Core;

public sealed class ConfigStore {
    private static readonly string[] ProgramFields=["buttons","leftWheel","rightWheel","modeButtons","combos","wheelPrograms","orbitKeys","panKeys","zoomKeys","snapKeys","twistKeys","altTwistEstimate","navigateKeys","circleZoom","circleSize","gestures","pointerSpeed","ball","feel","leftWheelUpKeys","leftWheelDownKeys","rightWheelUpKeys","rightWheelDownKeys"];
    private readonly object gate=new();
    private readonly string path;
    private JsonObject config;
    public long Revision {get;private set;}
    private static JsonObject? nodeProgram(JsonObject c,string name)=>c["programs"]!.AsArray().Select(n=>n?.AsObject()).FirstOrDefault(p=>p!=null&&S(p,"name").Contains(name,StringComparison.OrdinalIgnoreCase));
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
        if(oldVersion<23){
            var navigation=nodeProgram(config,"3D");
            if(navigation!=null){
                navigation["ball"]="orbit";navigation["circleZoom"]=true;
                var buttons=navigation["buttons"] as JsonObject??new JsonObject();
                buttons["back"]=new JsonObject{["action"]="panToggle",["tap"]="click"};
                navigation["buttons"]=buttons;
            }
            LoadProgram(config,I(config,"activeProgram",0));config["configVersion"]=23;
        }
        if(oldVersion<24){
            var navigation=nodeProgram(config,"3D");
            if(navigation!=null){
                navigation["ball"]="pointer";
                if(S(navigation,"zoomKeys","none") is "" or "none")navigation["zoomKeys"]="ctrl";
            }
            LoadProgram(config,I(config,"activeProgram",0));config["configVersion"]=24;
        }
        if(oldVersion<25){
            var navigation=nodeProgram(config,"3D");
            if(navigation!=null&&S(navigation,"navigateKeys")=="backtick")navigation["navigateKeys"]="win";
            LoadProgram(config,I(config,"activeProgram",0));config["configVersion"]=25;
        }
        if(oldVersion<26){
            var navigation=nodeProgram(config,"3D");
            if(navigation!=null&&(S(navigation,"twistKeys","none") is "" or "none")&&ModeResolver.Resolve(navigation,new HashSet<int>{16,18})==NavMode.Idle)navigation["twistKeys"]="alt+shift";
            LoadProgram(config,I(config,"activeProgram",0));config["configVersion"]=26;
        }
        if(oldVersion<27){
            var navigation=nodeProgram(config,"3D");
            if(navigation!=null&&S(navigation,"snapKeys").Equals("alt",StringComparison.OrdinalIgnoreCase)){
                navigation["altTwistEstimate"]=true;
                if(S(navigation,"twistKeys")=="alt+shift")navigation["twistKeys"]="none";
            }
            LoadProgram(config,I(config,"activeProgram",0));config["configVersion"]=27;
        }
        Validate(config);
    }
    public JsonObject Snapshot(){lock(gate)return (JsonObject)config.DeepClone();}
    public void Save(JsonObject incoming) {
        lock(gate){
            var next=(JsonObject)incoming.DeepClone();Normalize(next);
            // Device identity comes from Raw Input, not a possibly stale settings page.
            next["selectedDevice"]=config["selectedDevice"]?.DeepClone()??JsonValue.Create("");
            // The named library has dedicated mutations; an older page cannot erase it.
            next["presets"]=config["presets"]!.DeepClone();
            var i=Math.Clamp(I(next,"activeProgram",0),0,next["programs"]!.AsArray().Count-1);
            next["activeProgram"]=i;
            var p=next["programs"]![i]!.AsObject();
            foreach(var key in ProgramFields) if(key!="gestures"&&next.ContainsKey(key))p[key]=next[key]?.DeepClone();
            p["feel"]??=new JsonObject();
            foreach(var key in new[]{"orbitSens","panSens","zoomSens","twistSens","twistAxis","invertTwist","invertOrbitX","invertOrbitY","invertPan","invertZoom","preciseSlow","clickSlop","smooth","glide"})if(next.ContainsKey(key))p["feel"]![key]=next[key]?.DeepClone();
            next["feel"]=p["feel"]!.DeepClone();
            Validate(next);Write(next);config=next;Revision++;
        }
    }
    public void IdentifyDevice(string device){
        if(string.IsNullOrWhiteSpace(device)||device.Length>4095)throw new ArgumentException("Invalid trackball identity.",nameof(device));
        lock(gate){
            var next=(JsonObject)config.DeepClone();next["selectedDevice"]=device;
            Write(next);config=next;Revision++;
        }
    }
    public void Select(int index){lock(gate){var n=(JsonObject)config.DeepClone();LoadProgram(n,index);Write(n);config=n;Revision++;}}
    public JsonObject Reset(){lock(gate){var n=Defaults();foreach(var k in new[]{"theme","macros","welcomeDone","trayHintShown","selectedDevice","presets"})if(config[k]!=null)n[k]=config[k]!.DeepClone();Write(n);config=n;Revision++;return Snapshot();}}


    static void ReindexProgramReferences(JsonObject settings,int removed){
        int Fix(int value)=>value>removed?value-1:value==removed?0:value;
        var bindings=(settings["buttons"] as JsonObject??new()).Select(p=>p.Value)
            .Concat((settings["modeButtons"] as JsonObject??new()).Select(p=>p.Value))
            .Concat((settings["combos"] as JsonArray??new()).Select(p=>p?["bind"]));
        foreach(var binding in bindings)if(binding is JsonObject b&&S(b,"action")=="program")b["program"]=Fix(I(b,"program"));
        foreach(var entry in settings["gestures"] as JsonObject??new())if(entry.Value is JsonObject g&&S(g,"type")=="program")g["program"]=Fix(I(g,"program"));
        if(settings["wheelPrograms"] is JsonArray wheels)for(int i=0;i<wheels.Count;i++)wheels[i]=Fix(wheels[i]?.GetValue<int>()??0);
    }
    public JsonObject DeleteProgram(int index){lock(gate){
        var n=(JsonObject)config.DeepClone();var list=n["programs"]!.AsArray();
        if(list.Count<2||index<0||index>=list.Count)throw new InvalidDataException("Keep at least one valid program.");
        int active=I(n,"activeProgram");list.RemoveAt(index);
        foreach(var item in list)ReindexProgramReferences(item!.AsObject(),index);
        foreach(var preset in n["presets"]!.AsArray())ReindexProgramReferences(preset!["settings"]!.AsObject(),index);
        active=active>index?active-1:active==index?Math.Min(index,list.Count-1):active;
        LoadProgram(n,active);return PersistPresetChange(n);
    }}
    static string PresetName(string name){
        name=name.Trim();if(name.Length is <1 or >60||name.Any(char.IsControl))throw new InvalidDataException("Use a preset name with 1–60 characters.");
        return name;
    }
    static JsonObject PresetAt(JsonObject n,int index){
        var list=n["presets"]!.AsArray();if(index<0||index>=list.Count)throw new InvalidDataException("That preset no longer exists.");
        return list[index]!.AsObject();
    }
    JsonObject PersistPresetChange(JsonObject next){Validate(next);Write(next);config=next;Revision++;return Snapshot();}
    public JsonObject SavePreset(string name,bool replace=false){lock(gate){
        name=PresetName(name);var n=(JsonObject)config.DeepClone();var list=n["presets"]!.AsArray();
        var existing=list.Select((p,i)=>(p,i)).FirstOrDefault(item=>string.Equals(S(item.p,"name"),name,StringComparison.OrdinalIgnoreCase));
        bool found=existing.p!=null;
        if(found&&!replace)throw new InvalidDataException("A preset with that name already exists.");
        if(!found&&list.Count>=64)throw new InvalidDataException("You can save up to 64 presets.");
        var current=n["programs"]![I(n,"activeProgram")]!.AsObject();var settings=new JsonObject();
        foreach(var field in ProgramFields)settings[field]=current[field]?.DeepClone();
        settings["defaultKind"]=JsonValue.Create(S(current,"defaultKind",S(current,"name")));
        var preset=new JsonObject{["name"]=name,["settings"]=settings};
        if(found)list[existing.i]=preset;else list.Add(preset);
        return PersistPresetChange(n);
    }}
    public JsonObject ApplyPreset(int index){lock(gate){
        var n=(JsonObject)config.DeepClone();var settings=PresetAt(n,index)["settings"]!.AsObject();
        int active=I(n,"activeProgram");var current=n["programs"]![active]!.AsObject();
        foreach(var field in ProgramFields)if(settings.ContainsKey(field)){
            if(field=="feel"&&settings[field] is JsonObject feel){
                current["feel"]??=new JsonObject();foreach(var entry in feel)current["feel"]![entry.Key]=entry.Value?.DeepClone();
            }else current[field]=settings[field]?.DeepClone();
        }
        if(settings["defaultKind"]!=null)current["defaultKind"]=settings["defaultKind"]!.DeepClone();
        LoadProgram(n,active);return PersistPresetChange(n);
    }}
    public JsonObject RenamePreset(int index,string name){lock(gate){
        name=PresetName(name);var n=(JsonObject)config.DeepClone();var item=PresetAt(n,index);
        if(n["presets"]!.AsArray().Where((_,i)=>i!=index).Any(p=>string.Equals(S(p,"name"),name,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("A preset with that name already exists.");
        item["name"]=name;return PersistPresetChange(n);
    }}
    public JsonObject DeletePreset(int index){lock(gate){
        var n=(JsonObject)config.DeepClone();PresetAt(n,index);n["presets"]!.AsArray().RemoveAt(index);return PersistPresetChange(n);
    }}
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
            foreach(var k in ProgramFields)if(!p.ContainsKey(k))p[k]=k is "snapKeys" or "twistKeys" or "navigateKeys"?JsonValue.Create("none"):d["programs"]![0]![k]?.DeepClone();
            if(p["feel"] is not JsonObject)p["feel"]=d["programs"]![0]!["feel"]!.DeepClone();
            foreach(var entry in d["programs"]![0]!["feel"]!.AsObject())if(!p["feel"]!.AsObject().ContainsKey(entry.Key))p["feel"]![entry.Key]=entry.Value?.DeepClone();
        }
        c["activeProgram"]=Math.Clamp(I(c,"activeProgram",0),0,ps.Count-1);c["configVersion"]=27;
    }
    public static void Validate(JsonObject c){
        var programs=c["programs"]!.AsArray().Select(n=>n!.AsObject()).ToList();
        if(c["presets"] is not JsonArray presets||presets.Count>64)throw new InvalidDataException("Use a valid library of up to 64 presets.");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var preset in presets){
            var name=PresetName(S(preset,"name"));if(!names.Add(name))throw new InvalidDataException("Preset names must be unique.");
            if(preset?["settings"] is not JsonObject settings)throw new InvalidDataException("A preset must contain program settings.");
            programs.Add(settings);
        }
        foreach(var p in programs){
            var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var key in new[]{"orbitKeys","panKeys","zoomKeys","snapKeys","twistKeys","navigateKeys"}){
                var chord=S(p,key,"none");if(chord=="none"||chord=="")continue;
                var binding=KeyChord.Parse(chord);
                if(binding.IsCtrlAlone&&key!="zoomKeys")throw new InvalidDataException("Ctrl by itself is available for Zoom. Choose another key for this navigation command.");
                if(!used.Add(binding.Canonical))throw new InvalidDataException("Two navigation modes cannot use the same held keys. Pan and zoom share the Pan + zoom mode.");
            }
            if(S(p["feel"],"twistAxis","x") is not "x" and not "y")throw new InvalidDataException("Twist axis must be X or Y.");
            if(p["combos"] is JsonArray combos && combos.Count>64)throw new InvalidDataException("A program supports up to 64 combinations.");
        }
    }
    public static string S(JsonNode? n,string key,string fallback="")=>n?[key]?.GetValueKind()==JsonValueKind.String?n[key]!.GetValue<string>():fallback;
    public static int I(JsonNode? n,string key,int fallback=0)=>n?[key] is JsonValue v&&v.TryGetValue<int>(out var a)?a:fallback;
    public static double D(JsonNode? n,string key,double fallback=0)=>n?[key] is JsonValue v&&v.TryGetValue<double>(out var a)&&double.IsFinite(a)?a:fallback;
    public static bool B(JsonNode? n,string key,bool fallback=false)=>n?[key] is JsonValue v&&v.TryGetValue<bool>(out var a)?a:fallback;
}
