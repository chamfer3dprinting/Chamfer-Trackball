using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
static class ForegroundAppChecks{
 public static void Run(Action<bool,string> check){
  var cfg=ConfigStore.Defaults();var profiles=cfg["profiles"]!.AsArray();
  var browser=(nint)501;var watcher=new ForegroundApps();
  check(watcher.ProbeDue(browser,1000),"Foreground title is inspected on the first browser observation");
  check(!watcher.ProbeDue(browser,1008)&&!watcher.ProbeDue(browser,1049),"Navigation frames do not repeatedly inspect browser titles");
  check(watcher.ProbeDue(browser,1050),"A title-only tab switch is checked again while the browser HWND is unchanged");
  check(watcher.ProbeDue((nint)502,1051),"Changing windows refreshes the app immediately without waiting for the title interval");
  watcher.Invalidate();check(watcher.ProbeDue((nint)502,1052),"A configuration change invalidates cached foreground detection");
  var normal=ForegroundApps.Match(profiles,"chrome.exe","Video - YouTube - Google Chrome");
  var studio=ForegroundApps.Match(profiles,"chrome.exe","Chamfer Studio - Google Chrome");
  check(normal==null&&ConfigStore.S(studio,"id")=="chamfer3d","Switching the same browser from a normal tab to Studio enables its CAD profile");
  check(ForegroundApps.Match(profiles,"chrome.exe","Video - YouTube - Google Chrome")==null,"Leaving the Studio tab removes the CAD profile instead of capturing navigation in an ordinary tab");
  var renamed=ForegroundApps.Match(profiles,"chrome.exe","Part 2 * - CHAMFER STUDIO - Google Chrome");
  check(ForegroundApps.Same(studio,renamed),"Project title and dirty-marker changes do not restart an active Studio gesture");
  check(ForegroundApps.Same(null,null)&&!ForegroundApps.Same(null,studio)&&!ForegroundApps.Same(studio,null),"Entering and leaving a supported app changes the foreground context exactly once");
  check(ConfigStore.S(ForegroundApps.Match(profiles,"Chamfer3D Studio.exe","Untitled"),"id")=="chamfer3d","The installed Studio executable keeps matching independently of its title");
  var custom=new JsonArray(
   new JsonObject{["id"]="off",["enabled"]=false,["exe"]=new JsonArray("chrome.exe")},
   new JsonObject{["id"]="literal",["enabled"]=true,["exe"]=new JsonArray("custom.cad*.exe")},
   new JsonObject{["id"]="title",["enabled"]=true,["title"]=new JsonArray("My CAD")});
  check(ForegroundApps.Match(custom,"chrome.exe","Ordinary document")==null,"Disabled app profiles stay disabled during title refresh");
  check(ConfigStore.S(ForegroundApps.Match(custom,"CUSTOM.CAD2026.EXE",""),"id")=="literal","Custom executable wildcard and case rules are preserved");
  check(ForegroundApps.Match(custom,"customXcad2026.exe","")==null,"Executable dots are matched literally rather than as regular-expression wildcards");
  check(ConfigStore.S(ForegroundApps.Match(custom,"browser.exe","Design - my cad"),"id")=="title","Added browser CAD profiles respond to title-only changes too");
  check(!ForegroundApps.Same(studio,new JsonObject{["id"]="other"}),"Switching between different CAD profiles releases the previous navigation context");
  bool MatchExecutable(string value,string pattern)=>ForegroundApps.Match(new JsonArray(new JsonObject{
   ["id"]="pattern",["enabled"]=true,["exe"]=new JsonArray(pattern)}),value,"")!=null;
  check(MatchExecutable("STUDIO.EXE","studio.exe")&&!MatchExecutable("mystudio.exe","studio.exe"),"Exact executable matching stays case-insensitive and anchored");
  check(MatchExecutable("custom.part.2026.exe","custom*.exe")&&MatchExecutable("custom.exe","custom*.exe"),"A star matches both an empty and a longer filename segment");
  check(MatchExecutable("Custom.part.2026.exe","*PART*2026.EXE")&&!MatchExecutable("custom.part.2026.exe.tmp","*part*2026.exe"),"Multiple wildcard segments preserve ordering and suffix anchoring");
  check(!MatchExecutable("a","a*a")&&!MatchExecutable("part.exe","*part*part.exe"),"Wildcard prefix and suffix cannot consume overlapping characters");
  check(MatchExecutable("custom.part.exe","custom***part**.exe")&&!MatchExecutable("custom.part.exe","custom*missing*.exe"),"Consecutive stars and missing middle segments remain deterministic");
  check(MatchExecutable("cad+[x](1)?.exe","cad+[x](1)?.exe")&&!MatchExecutable("cadx1.exe","cad+[x](1)?.exe"),"Regex punctuation stays literal in executable patterns");
  check(!MatchExecutable("studio.exe","")&&!MatchExecutable(new string('a',200),new string('a',200))&&MatchExecutable(new string('a',199),new string('a',199)),"Empty and oversized patterns keep the existing bounds");
  var ambiguousPattern=string.Concat(Enumerable.Repeat("a*",60))+"z.exe";
  check(!MatchExecutable(new string('a',4000)+"y.exe",ambiguousPattern)&&MatchExecutable(new string('a',4000)+"z.exe",ambiguousPattern),"Adversarial wildcard input resolves without regex backtracking or timeout");
  ConfigStore.LoadProgram(cfg,1);
  check(ConfigStore.S(cfg,"orbitKeys")=="shift"&&ConfigStore.S(cfg,"panKeys")=="alt"&&ConfigStore.S(cfg,"zoomKeys")=="ctrl"&&ConfigStore.S(cfg,"snapKeys")=="none","Foreground detection never rewrites the saved held controls");
 }
}
