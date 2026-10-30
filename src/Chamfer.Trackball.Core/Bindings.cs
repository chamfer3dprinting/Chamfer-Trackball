namespace Chamfer.Trackball.Core;
public enum NavMode { Idle,Orbit,PanZoom,Zoom,Snap,Navigate,Roll,Gestures }
public sealed record KeyChord(int[] Keys,string Canonical) {
    public bool IsCtrlAlone=>Keys.Length==1&&Keys[0]==17;
    public int Weight=>Keys.Length;
    public bool Matches(IReadOnlySet<int> held) {
        if(!Keys.All(held.Contains))return false;
        if(held.Any(k=>k is not 16 and not 17 and not 18&&!Keys.Contains(k)))return false;
        // Exact modifiers: Ctrl+Shift must never accidentally activate Shift orbit.
        foreach(var m in new[]{16,17,18})if(held.Contains(m)!=Keys.Contains(m))return false;
        return true;
    }
    public static int VirtualKey(string raw){
        var s=raw.Trim().ToLowerInvariant();
        if(s.Length==1){if(s[0]=='`')return 192;if(char.IsAsciiLetterOrDigit(s[0]))return char.ToUpperInvariant(s[0]);}
        if(s.StartsWith("num")&&int.TryParse(s[3..],out var num)&&num is >=0 and <=9)return 96+num;
        if(s.StartsWith("f")&&int.TryParse(s[1..],out var f)&&f is >=1 and <=24)return 111+f;
        return s switch{"ctrl" or "control"=>17,"shift"=>16,"alt"=>18,"backtick" or "grave"=>192,"space"=>32,"tab"=>9,"enter" or "return"=>13,"escape" or "esc"=>27,"backspace"=>8,"delete"=>46,"insert"=>45,"home"=>36,"end"=>35,"pageup"=>33,"pagedown"=>34,"left"=>37,"up"=>38,"right"=>39,"down"=>40,"plus"=>187,"minus"=>189,"equals"=>187,"comma"=>188,"period"=>190,"slash"=>191,"backslash"=>220,"semicolon"=>186,"quote"=>222,"["=>219,"]"=>221,"win" or "windows"=>91,"capslock"=>20,"printscreen"=>44,"pause"=>19,"menu"=>93,"volumeup"=>175,"volumedown"=>174,"mute"=>173,"playpause"=>179,"nexttrack"=>176,"prevtrack"=>177,_=>throw new FormatException("Unsupported key: "+raw)};
    }
    public static KeyChord Parse(string s){
        var keys=s.Split('+',StringSplitOptions.RemoveEmptyEntries).Select(VirtualKey).Distinct().Order().ToArray();
        if(keys.Length==0||keys.Length>4)throw new FormatException("Choose one key with optional Ctrl, Shift or Alt.");
        if(keys.Count(k=>k is not 16 and not 17 and not 18)>1)throw new FormatException("Use at most one non-modifier key.");
        return new(keys,string.Join("+",keys));
    }
}
public static class ModeResolver {
    public static NavMode Resolve(System.Text.Json.Nodes.JsonObject config,IReadOnlySet<int> keys){
        var options=new[]{("panKeys",NavMode.PanZoom),("navigateKeys",NavMode.Navigate),("snapKeys",NavMode.Snap),("zoomKeys",NavMode.Zoom),("orbitKeys",NavMode.Orbit)};
        foreach(var (name,mode) in options){
            var spec=ConfigStore.S(config,name,"none");
            if(spec is "" or "none")continue;
            try{
                var chord=KeyChord.Parse(spec);
                // Alt is an intentional navigation modifier while backtick is held.
                if(mode==NavMode.Navigate&&keys.Contains(18)&&!chord.Keys.Contains(18)){
                    var withoutAlt=new HashSet<int>(keys);withoutAlt.Remove(18);
                    if(chord.Matches(withoutAlt))return mode;
                }
                if(!chord.IsCtrlAlone&&chord.Matches(keys))return mode;
            }catch(FormatException){ }
        }
        return NavMode.Idle;
    }
}
public sealed class ComboTracker {
    private readonly HashSet<int> held=[];
    private string fired="";
    public void Clear(){held.Clear();fired="";}
    public System.Text.Json.Nodes.JsonObject? Change(int button,bool down,System.Text.Json.Nodes.JsonArray? combos){
        if(down)held.Add(button);else held.Remove(button);
        if(held.Count<2){fired="";return null;}
        if(!down||combos==null)return null;
        foreach(var node in combos){
            if(node?["buttons"] is not System.Text.Json.Nodes.JsonArray buttons)continue;
            var ids=buttons.Where(b=>b!=null).Select(b=>b!.GetValue<int>()).Distinct().Order().ToArray();
            if(ids.Length<2||!ids.All(held.Contains))continue;
            var token=string.Join(",",ids);if(token==fired)continue;
            fired=token;return node?["bind"]?.DeepClone().AsObject();
        }
        return null;
    }
}
