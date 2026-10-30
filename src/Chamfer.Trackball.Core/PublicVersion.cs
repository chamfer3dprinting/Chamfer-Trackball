using System.Text.RegularExpressions;
namespace Chamfer.Trackball.Core;
public static class PublicVersion {
 public static string Normalize(string raw){
  string value=raw.Trim();if(value.StartsWith("v",StringComparison.OrdinalIgnoreCase))value=value[1..];
  if(!Regex.IsMatch(value,@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$")||!Version.TryParse(value,out var v)||v.Major>65534||v.Minor>65534||v.Build>65534)throw new ArgumentException("Enter a version like 1.2.3 (each number from 0 to 65534).");
  return value;
 }
}
