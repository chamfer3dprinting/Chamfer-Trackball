using System.Reflection;
namespace Chamfer.Trackball.App;
internal static class BuildInfo{
#if ADMIN
 public const bool Admin=true;
 public const string Edition="admin",Version="1.5.5",Asset="ChamferTrackball-Admin.exe";
#else
 public const bool Admin=false;
 public const string Edition="public",Asset="ChamferTrackball.exe";
 public static string Version=>Assembly.GetExecutingAssembly().GetName().Version!.ToString(3);
#endif
 public static string PublicVersion=>Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a=>a.Key=="PublicCandidateVersion")?.Value??"1.0.1";
 public static string Revision=>Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a=>a.Key=="BuildNumber")?.Value??"0";
 public static string Source=>Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a=>a.Key=="SourceRevision")?.Value??"local";
}
