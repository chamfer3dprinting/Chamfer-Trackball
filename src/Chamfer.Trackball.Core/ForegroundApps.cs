using System.Text.Json.Nodes;
namespace Chamfer.Trackball.Core;

// A browser changes its title when switching tabs without changing its HWND.
// Probe titles at 20 Hz; avoid repeating process enumeration on each input frame.
public sealed class ForegroundApps{
    public const int RefreshMs=50;
    nint lastWindow;long nextProbe;bool observed;
    public bool ProbeDue(nint window,long now){
        if(observed&&window==lastWindow&&now<nextProbe)return false;
        observed=true;lastWindow=window;nextProbe=now+RefreshMs;return true;
    }
    public void Invalidate()=>observed=false;
    public static JsonObject? Match(JsonArray profiles,string exe,string title)=>
        profiles.OfType<JsonObject>().FirstOrDefault(p=>ConfigStore.B(p,"enabled")&&
            (p["exe"]?.AsArray().Any(x=>Executable(exe,x?.GetValue<string>()??""))==true||
             p["title"]?.AsArray().Any(x=>title.Contains(x?.GetValue<string>()??"\0",StringComparison.OrdinalIgnoreCase))==true));
    public static bool Same(JsonObject? before,JsonObject? after)=>
        before==null&&after==null||before!=null&&after!=null&&
        ConfigStore.S(before,"id")==ConfigStore.S(after,"id");
    // Executable patterns support only literal characters and '*'. Avoid the
    // regex engine on the input/UI thread: even a cold simple match can exceed
    // a short regex timeout, and repeated stars can cause backtracking.
    static bool Executable(string exe,string pattern){
        if(pattern.Length==0||pattern.Length>=200)return false;
        if(!pattern.Contains('*'))return exe.Equals(pattern,StringComparison.OrdinalIgnoreCase);
        var parts=pattern.Split('*');
        if(!exe.StartsWith(parts[0],StringComparison.OrdinalIgnoreCase)||
           !exe.EndsWith(parts[^1],StringComparison.OrdinalIgnoreCase))return false;
        int offset=parts[0].Length,limit=exe.Length-parts[^1].Length;
        if(offset>limit)return false;
        for(int i=1;i<parts.Length-1;i++){
            int index=exe.IndexOf(parts[i],offset,limit-offset,StringComparison.OrdinalIgnoreCase);
            if(index<0)return false;
            offset=index+parts[i].Length;
        }
        return true;
    }
}
