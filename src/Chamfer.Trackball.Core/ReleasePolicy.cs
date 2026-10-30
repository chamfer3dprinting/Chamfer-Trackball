using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
namespace Chamfer.Trackball.Core;
public static class ReleasePolicy{
 public static bool PublicTag(string tag)=>Regex.IsMatch(tag,@"^public-v\d+\.\d+\.\d+$");
 public static bool CandidateTag(string tag)=>Regex.IsMatch(tag,@"^candidate-v\d+\.\d+\.\d+-build\.\d+$");
 public static long AdminBuild(string tag)=>Regex.Match(tag,@"^admin-v\d+\.\d+\.\d+-build\.(\d+)$") is var m&&m.Success&&long.TryParse(m.Groups[1].Value,out var n)?n:-1;
 public static JsonNode? Latest(JsonArray releases,bool admin,long currentBuild,string currentVersion){
  if(admin)return releases.Where(r=>!ConfigStore.B(r,"draft")&&AdminBuild(ConfigStore.S(r,"tag_name"))>currentBuild).OrderByDescending(r=>AdminBuild(ConfigStore.S(r,"tag_name"))).FirstOrDefault();
  return releases.Where(r=>!ConfigStore.B(r,"draft")&&!ConfigStore.B(r,"prerelease")&&PublicTag(ConfigStore.S(r,"tag_name"))&&Version.TryParse(ConfigStore.S(r,"tag_name")[8..],out var v)&&v>new Version(currentVersion)).OrderByDescending(r=>new Version(ConfigStore.S(r,"tag_name")[8..])).FirstOrDefault();
 }
}
