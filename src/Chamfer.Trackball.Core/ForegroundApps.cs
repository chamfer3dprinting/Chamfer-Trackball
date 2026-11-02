using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
    static bool Executable(string exe,string pattern)=>pattern.Length>0&&pattern.Length<200&&
        Regex.IsMatch(exe,"^"+Regex.Escape(pattern).Replace("\\*",".*")+"$",
            RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(10));
}
