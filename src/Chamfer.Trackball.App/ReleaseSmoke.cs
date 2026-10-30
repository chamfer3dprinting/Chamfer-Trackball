using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
namespace Chamfer.Trackball.App;

// Runs only from --smoke-test, against an in-memory HTTP handler. No external
// requests, saved credentials, or public GitHub releases are involved.
internal static class ReleaseSmoke{
 static void Check(bool pass,string message){if(!pass)throw new Exception("Release smoke: "+message);}
 static async Task Denied(Func<Task> operation){bool denied=false;try{await operation();}catch{denied=true;}Check(denied,"operation should be rejected");}
 static AdminService Service(MemoryGitHub api,string credential="smoke-test-credential")=>new(new HttpClient(api){Timeout=TimeSpan.FromSeconds(10)},credential);
 public static async Task Run(){
  using(var api=new MemoryGitHub())using(var service=Service(api)){
   if(!BuildInfo.Admin){await Denied(()=>service.Release("1.0.1"));await Denied(()=>service.RequestPublicVersion("9.8.7"));Check(api.Requests==0,"public app contacted release API");Console.WriteLine("Public app rejects release operations.");return;}
  }
  using(var api=new MemoryGitHub())using(var service=Service(api,"")){
   await Denied(()=>service.Release("1.0.1"));await Denied(()=>service.RequestPublicVersion("9.8.7"));Check(api.Requests==0,"missing credential contacted release API");
  }
  using(var api=new MemoryGitHub())using(var service=Service(api)){
   await service.Refresh();
   var state=JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(service.State()))!;
   Check(state["versions"]!.AsArray().Any(v=>ConfigStore.S(v,"version")=="1.0.1"&&ConfigStore.B(v,"canShip")),"candidate on later API page is missing from Admin");
   await service.Release("1.0.1");
   Check(api.Published&&api.Uploads==1&&api.Drafts==1&&api.Downloads==1,"release did not verify, upload and publish exactly once");
   var release=ReleasePolicy.Latest(api.Releases,false,0,"1.0.0");
   Check(ConfigStore.S(release,"tag_name")=="public-v1.0.1","public updater cannot see the promoted release");
   Check(ReleasePolicy.Latest(api.Releases,false,0,"1.0.1")==null,"public updater reinstalls current version");
   await Denied(()=>service.Release("1.0.1"));Check(api.Uploads==1,"repeated publish replaced an existing executable");
   await service.Withdraw("1.0.1");Check(!api.Published,"withdraw did not return release to draft");
   Check(ReleasePolicy.Latest(api.Releases,false,0,"1.0.0")==null,"withdrawn release is offered by public updater");
   await service.Release("1.0.1");Check(api.Published&&api.Uploads==1,"re-release replaced the original executable");
  }
  using(var api=new MemoryGitHub{CorruptDownload=true})using(var service=Service(api)){
   await Denied(()=>service.Release("1.0.1"));Check(api.Drafts==0&&!api.Published,"corrupt candidate reached draft/publish");
  }
  using(var api=new MemoryGitHub{CorruptUpload=true})using(var service=Service(api)){
   await Denied(()=>service.Release("1.0.1"));Check(api.Drafts==1&&!api.Published,"bad upload digest was published");
   await Denied(()=>service.Release("1.0.1"));Check(!api.Published&&api.Uploads==1,"retry published a failed draft without verifying its original binary");
  }
  using(var api=new MemoryGitHub{Forbidden=true})using(var service=Service(api)){
   await Denied(()=>service.Release("1.0.1"));Check(api.Drafts==0&&!api.Published,"permission failure created a release");
  }
  using(var api=new MemoryGitHub())using(var service=Service(api)){
   await service.RequestPublicVersion(" v9.8.7 ");
   Check(ConfigStore.S(api.CustomRequest,"version")=="9.8.7"&&ConfigStore.S(api.CustomRequest,"sourceRevision")==BuildInfo.Source&&api.RequestWrites==1,"custom version request was not pinned and queued");
   var queued=JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(service.State()))!;
   Check(ConfigStore.S(queued,"pending")=="release 9.8.7","queued version is not shown in Admin");
   await Denied(()=>service.RequestPublicVersion("8.7.6"));Check(api.RequestWrites==1,"a pending release was replaced");
   api.CustomStatus=new(){["requestId"]=ConfigStore.S(api.CustomRequest,"requestId"),["state"]="error",["message"]="Test build failed"};
   await service.Refresh();var failed=JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(service.State()))!;
   Check(ConfigStore.S(failed,"pending")==""&&ConfigStore.S(failed,"error")=="Test build failed","build failure is not shown in Admin");
   await service.RequestPublicVersion("8.7.6");Check(api.RequestWrites==2,"failed release cannot be retried with another chosen version");
   api.Releases.Add(new JsonObject{["tag_name"]="public-v8.7.6",["draft"]=false,["prerelease"]=false,["assets"]=new JsonArray()});
   await service.Refresh();var completed=JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(service.State()))!;
   Check(ConfigStore.S(completed,"pending")==""&&ConfigStore.S(completed,"message").Contains("released"),"published custom version does not clear progress");
  }
  Console.WriteLine("Custom public version queue, pinned source, pending state, errors and completion passed.");
  Console.WriteLine("Admin release smoke passed: authenticated publish, pagination, verified upload, public update discovery, duplicate prevention, withdrawal/re-release and failure handling.");
 }
 sealed class MemoryGitHub:HttpMessageHandler{
  public readonly byte[] Exe=Encoding.ASCII.GetBytes("MZ-verified-public-candidate");
  public readonly JsonArray Releases=[];
  public JsonObject? CustomRequest,CustomStatus;public int RequestWrites;
  public int Requests,Uploads,Drafts,Downloads;public bool CorruptDownload,CorruptUpload,Forbidden;
  string Digest=>"sha256:"+Convert.ToHexString(SHA256.HashData(Exe)).ToLowerInvariant();
  JsonObject? Public=>Releases.OfType<JsonObject>().FirstOrDefault(r=>ConfigStore.S(r,"tag_name")=="public-v1.0.1");
  public bool Published=>Public!=null&&!ConfigStore.B(Public,"draft");
  public MemoryGitHub(){
   for(int i=0;i<100;i++)Releases.Add(new JsonObject{["id"]=i+1,["tag_name"]="v0.0."+i,["draft"]=false,["prerelease"]=true});
   Releases.Add(new JsonObject{["id"]=400,["tag_name"]="candidate-v1.0.1-build.99",["created_at"]="2026-10-30T12:00:00Z",["target_commitish"]=new string('a',40),["body"]="Verified candidate",["draft"]=true,["prerelease"]=false,["assets"]=new JsonArray(new JsonObject{["id"]=42,["name"]="ChamferTrackball.exe",["digest"]=Digest})});
  }
  static HttpResponseMessage Json(JsonNode value)=>new(HttpStatusCode.OK){Content=new StringContent(value.ToJsonString(),Encoding.UTF8,"application/json")};
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){
   Requests++;Check(request.Headers.Authorization?.Parameter=="smoke-test-credential","missing authenticated request");
   var uri=request.RequestUri!;string path=uri.AbsolutePath;string method=request.Method.Method;
   if(Forbidden)return new(HttpStatusCode.Forbidden){Content=new StringContent("permission denied")};
   if(method=="GET"&&path.Contains("/contents/.github/custom-public-")){
    var value=path.EndsWith("custom-public-request.json")?CustomRequest:CustomStatus;
    return value==null?new HttpResponseMessage(HttpStatusCode.NotFound){Content=new StringContent("{}")}:Json(new JsonObject{["sha"]="file-sha",["content"]=Convert.ToBase64String(Encoding.UTF8.GetBytes(value.ToJsonString()))});
   }
   if(method=="PUT"&&path.EndsWith("/contents/.github/custom-public-request.json")){
    var body=JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!;
    CustomRequest=JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(ConfigStore.S(body,"content"))))!.AsObject();
    Check(ConfigStore.S(CustomRequest,"action")=="release"&&ConfigStore.S(CustomRequest,"sourceRevision")==BuildInfo.Source,"custom request has wrong action/source");
    Check(!CustomRequest.ToJsonString().Contains("credential"),"credential was copied into repository request");
    RequestWrites++;return Json(new JsonObject{["content"]=new JsonObject{["sha"]="saved-sha"}});
   }
   if(method=="GET"&&path.EndsWith("/releases")){
    int page=uri.Query.Contains("page=2")?2:1;
    return Json(new JsonArray(Releases.Skip((page-1)*100).Take(100).Select(r=>r!.DeepClone()).ToArray()));
   }
   if(method=="GET"&&(path.EndsWith("/releases/assets/42")||path.EndsWith("/releases/assets/502")) ){
    Downloads++;return new(HttpStatusCode.OK){Content=new ByteArrayContent(CorruptDownload?Encoding.ASCII.GetBytes("MZ-corrupt"):Exe)};
   }
   if(method=="GET"&&path.EndsWith("/releases/tags/public-v1.0.1")){
    Check(Public!=null,"unknown public release");return Json(Public!.DeepClone());
   }
   if(method=="POST"&&path.EndsWith("/releases")){
    var body=JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
    Check(ConfigStore.B(body,"draft")&&!ConfigStore.B(body,"prerelease"),"public release must start as a non-preview draft");
    Check(ConfigStore.S(body,"tag_name")=="public-v1.0.1"&&ConfigStore.S(body,"target_commitish")==new string('a',40),"wrong candidate version/source");
    body["id"]=501;body["assets"]=new JsonArray();Releases.Add(body);Drafts++;return Json(body.DeepClone());
   }
   if(method=="POST"&&uri.Host=="uploads.github.com"&&path.EndsWith("/releases/501/assets")){
    var bytes=await request.Content!.ReadAsByteArrayAsync(cancellationToken);
    Check(bytes.SequenceEqual(Exe)&&uri.Query=="?name=ChamferTrackball.exe","uploaded candidate was changed");
    Uploads++;var asset=new JsonObject{["id"]=502,["name"]="ChamferTrackball.exe",["digest"]=CorruptUpload?"sha256:"+new string('0',64):Digest};
    Public!["assets"]!.AsArray().Add(asset.DeepClone());return Json(asset);
   }
   if(method=="PATCH"&&path.EndsWith("/releases/501")){
    var body=JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken))!.AsObject();
    if(!ConfigStore.B(body,"draft",true))Check(ConfigStore.S(Public?["assets"]?[0],"digest")==Digest,"publishing without a verified asset");
    foreach(var item in body)Public![item.Key]=item.Value?.DeepClone();return Json(Public!.DeepClone());
   }
   throw new Exception("Unexpected release test request: "+method+" "+path);
  }
 }
}
