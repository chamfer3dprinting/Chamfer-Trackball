using System.Reflection;
namespace Chamfer.Trackball.App;
internal static class BuildInfo{
#if ADMIN
 public const bool Admin=true;
 public const string Edition="admin",Version="1.5.5",Asset="ChamferTrackball-Admin.exe";
#else
 public const bool Admin=false;
 public const string Edition="public",Version="1.0.1",Asset="ChamferTrackball.exe";
#endif
 public static string Revision=>Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a=>a.Key=="BuildNumber")?.Value??"0";
 public static string Source=>Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a=>a.Key=="SourceRevision")?.Value??"local";
}
