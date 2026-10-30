using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
namespace Chamfer.Trackball.App;
internal sealed class AdminService:IDisposable{
 const string Repo="chamfer3dprinting/Chamfer-Trackball";
 readonly HttpClient http;
 readonly string path;string token="";JsonArray releases=[];string error="",message="";bool loaded,busy;
 public AdminService(string settingsDirectory){
  http=new(){Timeout=TimeSpan.FromSeconds(60)};
  path=Path.Combine(settingsDirectory,"admin-auth.dpapi");http.DefaultRequestHeaders.UserAgent.ParseAdd("ChamferTrackball-Admin/"+BuildInfo.Version);
  if(BuildInfo.Admin&&File.Exists(path))try{var raw=ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser);try{token=Encoding.UTF8.GetString(raw);}finally{CryptographicOperations.ZeroMemory(raw);}}catch(CryptographicException){error="Reconnect GitHub to manage releases.";}
 }
 // Used by the executable's isolated smoke test; no credentials are read or saved.
 internal AdminService(HttpClient client,string credential){http=client;path="";token=credential;}
 async Task<JsonArray> ReadReleases(){
  var all=new JsonArray();
  for(int page=1;page<=100;page++){
   var batch=(await Api(HttpMethod.Get,"/releases?per_page=100&page="+page)).AsArray();
   foreach(var r in batch)all.Add(r!.DeepClone());if(batch.Count<100)return all;
  }
  throw new IOException("Too many releases to inspect safely.");
 }
 void RequireAdmin(){if(!BuildInfo.Admin)throw new UnauthorizedAccessException("Admin commands are unavailable in the public app.");}
 async Task<JsonNode> Api(HttpMethod method,string route,JsonObject? body=null,string? credential=null){
  using var request=new HttpRequestMessage(method,"https://api.github.com/repos/"+Repo+route);
  string key=credential??token;if(key.Length>0)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
  request.Headers.Accept.ParseAdd("application/vnd.github+json");request.Headers.Add("X-GitHub-Api-Version","2022-11-28");
  if(body!=null)request.Content=new StringContent(body.ToJsonString(),Encoding.UTF8,"application/json");
  using var response=await http.SendAsync(request);
  if(!response.IsSuccessStatusCode)throw new InvalidOperationException("GitHub returned "+(int)response.StatusCode+". Check your repository Contents read/write permission and token expiry.");
  if(response.StatusCode==System.Net.HttpStatusCode.NoContent)return new JsonObject();
  return JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
 }
 public async Task Connect(string value){
  RequireAdmin();value=value.Trim();if(value.Length is <20 or >512||value.Any(char.IsWhiteSpace))throw new ArgumentException("Paste a GitHub access token.");
  var repo=await Api(HttpMethod.Get,"",credential:value);
  if(!ConfigStore.B(repo["permissions"],"push"))throw new UnauthorizedAccessException("This token cannot manage Chamfer Trackball releases.");
  var bytes=Encoding.UTF8.GetBytes(value);try{var encrypted=ProtectedData.Protect(bytes,null,DataProtectionScope.CurrentUser);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,encrypted);}finally{CryptographicOperations.ZeroMemory(bytes);}
  token=value;error="";message="GitHub connected. Public builds remain unpublished until you release them.";await Refresh();
 }
 public void Clear(){RequireAdmin();token="";if(File.Exists(path))File.Delete(path);loaded=false;releases=[];message="GitHub disconnected. Reconnect before publishing or withdrawing.";error="";}
 public async Task Refresh(){
  RequireAdmin();if(busy)return;busy=true;error="";
  try{
   releases=await ReadReleases();loaded=true;
  }catch(Exception ex){error=ex.Message;}finally{busy=false;}
 }
 public object State(){
  RequireAdmin();var publicRows=releases.Where(r=>ReleasePolicy.PublicTag(ConfigStore.S(r,"tag_name"))&&!ConfigStore.B(r,"draft")&&!ConfigStore.B(r,"prerelease")).ToArray();
  var latest=publicRows.OrderByDescending(r=>new Version(ConfigStore.S(r,"tag_name")[8..])).FirstOrDefault();
  var versions=new List<object>();var seen=new HashSet<string>();
  foreach(var r in releases.Where(r=>ReleasePolicy.CandidateTag(ConfigStore.S(r,"tag_name"))).OrderByDescending(r=>r?["created_at"]?.GetValue<string>()).GroupBy(r=>ConfigStore.S(r,"tag_name").Split("-build.")[0][11..]).Select(g=>g.OrderByDescending(r=>long.Parse(ConfigStore.S(r,"tag_name").Split("-build.")[1])).First())){
   var version=ConfigStore.S(r,"tag_name").Split("-build.")[0][11..];
   var pub=publicRows.FirstOrDefault(p=>ConfigStore.S(p,"tag_name")=="public-v"+version);
   seen.Add(version);versions.Add(new{version,date=ConfigStore.S(r,"created_at").Split('T')[0],@public=pub!=null,latest=pub!=null&&ReferenceEquals(pub,latest),thisApp=false,canShip=token.Length>0&&r?["assets"]?.AsArray().Any(a=>ConfigStore.S(a,"name")=="ChamferTrackball.exe"&&ConfigStore.S(a,"digest").StartsWith("sha256:")&&ConfigStore.S(a,"digest").Length==71)==true,notes=new[]{ConfigStore.S(r,"body")}});
  }
  foreach(var pub in publicRows.Where(p=>!seen.Contains(ConfigStore.S(p,"tag_name")[8..])))
   versions.Add(new{version=ConfigStore.S(pub,"tag_name")[8..],date=ConfigStore.S(pub,"created_at").Split('T')[0],@public=true,latest=ReferenceEquals(pub,latest),thisApp=false,canShip=false,notes=new[]{ConfigStore.S(pub,"body")}});
  return new{connected=token.Length>0,repo=Repo,error,message,loaded,busy=busy?"loading":"",pending="",versions};
 }
 async Task<(byte[] Bytes,string Hash)> VerifiedExecutable(JsonNode release){
  var asset=release["assets"]?.AsArray().FirstOrDefault(a=>ConfigStore.S(a,"name")=="ChamferTrackball.exe")??throw new IOException("Release has no public executable.");
  var id=ConfigStore.D(asset,"id");var digest=ConfigStore.S(asset,"digest");
  using var req=new HttpRequestMessage(HttpMethod.Get,"https://api.github.com/repos/"+Repo+"/releases/assets/"+id.ToString("0",System.Globalization.CultureInfo.InvariantCulture));
  req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);req.Headers.Accept.ParseAdd("application/octet-stream");
  using var response=await http.SendAsync(req);response.EnsureSuccessStatusCode();var bytes=await response.Content.ReadAsByteArrayAsync();
  var hash="sha256:"+Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
  if(bytes.Length<2||bytes[0]!=77||bytes[1]!=90||digest!=hash)throw new IOException("Public executable verification failed.");
  var marker=System.Text.RegularExpressions.Regex.Match(ConfigStore.S(release,"body"),@"(?m)^Verified executable SHA-256: (sha256:[a-f0-9]{64})\r?$");
  if(marker.Success&&marker.Groups[1].Value!=hash)throw new IOException("The draft does not contain its originally verified executable.");
  return (bytes,hash);
 }
 public async Task Release(string version){
  RequireAdmin();if(token.Length==0)throw new UnauthorizedAccessException("Connect your GitHub account first.");if(busy)throw new InvalidOperationException("Another release operation is running.");
  if(!ReleasePolicy.PublicTag("public-v"+version))throw new ArgumentException("Invalid version.");
  busy=true;error="";
  try{
   var all=await ReadReleases();
   var existing=all.FirstOrDefault(r=>ConfigStore.S(r,"tag_name")=="public-v"+version);
   if(existing!=null){
    if(!ConfigStore.B(existing,"draft"))throw new InvalidOperationException("That public version is already released.");
    await VerifiedExecutable(existing);
    await Api(HttpMethod.Patch,"/releases/"+ConfigStore.D(existing,"id").ToString("0",System.Globalization.CultureInfo.InvariantCulture),new(){["draft"]=false,["prerelease"]=false,["make_latest"]="true"});
    message="Public v"+version+" released again with its original executable.";return;
   }
   var candidate=all.Where(r=>ReleasePolicy.CandidateTag(ConfigStore.S(r,"tag_name"))&&ConfigStore.S(r,"tag_name").StartsWith("candidate-v"+version+"-build.")).OrderByDescending(r=>long.Parse(ConfigStore.S(r,"tag_name").Split("-build.")[1])).FirstOrDefault()??throw new InvalidOperationException("No verified candidate found.");
   var (bytes,hash)=await VerifiedExecutable(candidate);
   var draft=await Api(HttpMethod.Post,"/releases",new(){["tag_name"]="public-v"+version,["target_commitish"]=ConfigStore.S(candidate,"target_commitish"),["name"]="Chamfer Trackball v"+version,["body"]=ConfigStore.S(candidate,"body")+"\n\nVerified executable SHA-256: "+hash,["draft"]=true,["prerelease"]=false});
   var releaseId=ConfigStore.D(draft,"id").ToString("0",System.Globalization.CultureInfo.InvariantCulture);
   using var upload=new HttpRequestMessage(HttpMethod.Post,"https://uploads.github.com/repos/"+Repo+"/releases/"+releaseId+"/assets?name=ChamferTrackball.exe");upload.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);upload.Content=new ByteArrayContent(bytes);upload.Content.Headers.ContentType=new MediaTypeHeaderValue("application/octet-stream");
   using var uploaded=await http.SendAsync(upload);uploaded.EnsureSuccessStatusCode();var saved=JsonNode.Parse(await uploaded.Content.ReadAsStringAsync())!;
   if(ConfigStore.S(saved,"digest")!=hash)throw new IOException("Uploaded executable digest mismatch; release remains a draft.");
   await Api(HttpMethod.Patch,"/releases/"+releaseId,new(){["draft"]=false,["prerelease"]=false,["make_latest"]="true"});
   message="Public v"+version+" released.";
  }catch(Exception ex){error=ex.Message;throw;}finally{busy=false;await Refresh();}
 }
 public async Task Withdraw(string version){
  RequireAdmin();if(token.Length==0)throw new UnauthorizedAccessException("Connect GitHub first.");if(!ReleasePolicy.PublicTag("public-v"+version))throw new ArgumentException("Invalid version.");
  if(busy)throw new InvalidOperationException("Another release operation is running.");
  busy=true;error="";
  try{
   var r=await Api(HttpMethod.Get,"/releases/tags/public-v"+version);
   await Api(HttpMethod.Patch,"/releases/"+ConfigStore.D(r,"id").ToString("0",System.Globalization.CultureInfo.InvariantCulture),new(){["draft"]=true});
   message="Public v"+version+" withdrawn to a draft. Its history and executable are retained.";
  }catch(Exception ex){error=ex.Message;throw;}finally{busy=false;await Refresh();}
 }
 public void Dispose()=>http.Dispose();
}
