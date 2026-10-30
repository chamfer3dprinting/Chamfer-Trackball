using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace Chamfer.Trackball.Core;
public static class ReleasePolicy{
 public static bool PublicTag(string tag)=>Regex.IsMatch(tag,@"^public-v\d+\.\d+\.\d+$");
 public static bool AdminTag(string tag)=>Regex.IsMatch(tag,@"^admin-v\d+\.\d+\.\d+$");
 public static bool CandidateTag(string tag)=>Regex.IsMatch(tag,@"^candidate-v\d+\.\d+\.\d+-build\.\d+$");
 // Retained for older clients and the single migration release. New clients
 // select updates by application version and ignore internal build revisions.
 public static long AdminBuild(string tag)=>Regex.Match(tag,@"^admin-v\d+\.\d+\.\d+-build\.(\d+)$") is var m&&m.Success&&long.TryParse(m.Groups[1].Value,out var n)?n:-1;
 public static string VersionText(string tag,bool admin)=> (admin?AdminTag(tag):PublicTag(tag))?tag[(admin?7:8)..]:"";
 public static JsonNode? Latest(JsonArray releases,bool admin,long currentBuild,string currentVersion){
  if(!Version.TryParse(currentVersion,out var current))return null;
  return releases.Where(r=>!ConfigStore.B(r,"draft")&&(admin||!ConfigStore.B(r,"prerelease"))
    &&Version.TryParse(VersionText(ConfigStore.S(r,"tag_name"),admin),out var next)&&next>current)
    .OrderByDescending(r=>new Version(VersionText(ConfigStore.S(r,"tag_name"),admin))).FirstOrDefault();
 }
}
