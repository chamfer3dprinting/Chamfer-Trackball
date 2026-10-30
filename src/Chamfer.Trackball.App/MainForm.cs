using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;
namespace Chamfer.Trackball.App;
internal sealed class MainForm:Form{
    readonly WebView2 web=new(){Dock=DockStyle.Fill};
    readonly NotifyIcon tray=new(){Icon=SystemIcons.Application,Text="Chamfer Trackball",Visible=true};
    readonly ConfigStore store;readonly InputDriver input;readonly NativeServer native;readonly Controller controller;
    readonly string settingsPath;
    bool quit;readonly bool smoke;JsonObject update=new(){["channel"]="public",["stage"]="latest",["version"]=ConfigStore.Version,["history"]=new JsonArray(),["notes"]=new JsonArray("Editable source rebuild: Shift orbit, Ctrl+Shift pan/zoom, Alt snap and backtick navigation with native integrations.")};
    string updateUrl="",updateHash="";
    readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(30)};
    public MainForm(bool smoke=false,bool startTray=false){
        this.smoke=smoke;
        Text="Chamfer Trackball";ClientSize=new(1106,754);MinimumSize=new(920,640);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(20,23,27);
        settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ChamferTrackball","settings.json");
        store=new(settingsPath);native=new();if(native.Error.Length>0){native.Dispose();throw new InvalidOperationException(native.Error);}input=new();controller=new(store,input,native);
        Controls.Add(web);http.DefaultRequestHeaders.UserAgent.ParseAdd("ChamferTrackball/"+ConfigStore.Version);
        var menu=new ContextMenuStrip();menu.Items.Add("Open",null,(_,_)=>{Show();WindowState=FormWindowState.Normal;Activate();});menu.Items.Add("Quit",null,(_,_)=>Exit());tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>{Show();Activate();};
        Shown+=async(_,_)=>{await Initialize();if(startTray)Hide();};
        FormClosing+=(_,e)=>{if(!quit){e.Cancel=true;Hide();return;}controller.Dispose();input.Dispose();native.Dispose();tray.Dispose();http.Dispose();};
    }
    async Task Initialize(){
        try{
            // Keep the trusted embedded settings UI separate from remote websites.
            var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Path.GetDirectoryName(settingsPath)!,"RebuiltWebView"));
            await web.EnsureCoreWebView2Async(env);
            web.CoreWebView2.Settings.AreDevToolsEnabled=false;
            web.CoreWebView2.Settings.IsStatusBarEnabled=false;
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
            web.CoreWebView2.NavigationStarting+=(_,e)=>{if(e.Uri!="about:blank")e.Cancel=true;};
            web.CoreWebView2.NewWindowRequested+=(_,e)=>{e.Handled=true;if(Uri.TryCreate(e.Uri,UriKind.Absolute,out var u)&&u.Scheme=="https")Process.Start(new ProcessStartInfo(u.AbsoluteUri){UseShellExecute=true});};
            web.CoreWebView2.WebMessageReceived+=HandleMessage;
            string[] names=["ctState","ctSave","ctStatus","ctResetAll","ctProgram","ctLog","ctQuit","ctHide","ctStartup","ctPadRect","ctRow","ctTitleBar","ctOpenMouseSettings","ctOpenSettingsFile","ctTestMacro","ctUpdState","ctUpdCheck","ctUpdInstall","ctInstallIntegration","ctAddProfile"];
            var js="(()=>{const pending=new Map();let seq=0;chrome.webview.addEventListener('message',e=>{const m=e.data;const p=pending.get(m.id);if(!p)return;pending.delete(m.id);m.error?p[1](new Error(m.error)):p[0](m.result);});for(const n of "+JsonSerializer.Serialize(names)+")window[n]=(...args)=>new Promise((ok,no)=>{const id=++seq;pending.set(id,[ok,no]);chrome.webview.postMessage({id,name:n,args});});})();";
            await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(js);
            using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("ChamferTrackball.ui.html")??throw new IOException("Interface resource is missing.");
            using var reader=new StreamReader(s);
            if(smoke)web.CoreWebView2.NavigationCompleted+=async(_,_)=>await SmokeTest();
            web.NavigateToString(await reader.ReadToEndAsync());
        }catch(Exception ex){if(smoke){File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-ui-smoke.error"),ex.ToString());Console.Error.WriteLine(ex);Environment.Exit(1);}MessageBox.Show("Cannot open the interface. Ensure Microsoft Edge WebView2 Runtime is installed. "+ex.Message);Exit();}
    }
    async void HandleMessage(object? sender,CoreWebView2WebMessageReceivedEventArgs e){
        if(e.Source!="about:blank"||e.WebMessageAsJson.Length>1024*1024)return;
        long id=0;
        try{
            var message=JsonNode.Parse(e.WebMessageAsJson)!.AsObject();id=message["id"]!.GetValue<long>();var args=message["args"]!.AsArray();object? result=null;
            switch(ConfigStore.S(message,"name")){
                case "ctState":result=new{version=ConfigStore.Version,edition="public",config=store.Snapshot(),defaults=ConfigStore.Defaults(),programDefaults=ConfigStore.Defaults()["programs"],settingsPath,startup=StartupEnabled(),pointerAccel=false,justUpdated=false};break;
                case "ctSave":store.Save(args[0]!.AsObject());result=store.Snapshot();break;
                case "ctStatus":result=controller.Status();break;
                case "ctResetAll":result=store.Reset();break;
                case "ctProgram":store.Select(args[0]!.GetValue<int>());result=true;break;
                case "ctLog":result=controller.LogAfter(args[0]?.GetValue<long>()??0);break;
                case "ctPadRect":controller.Pad((int)args[0]!.GetValue<double>(),(int)args[1]!.GetValue<double>(),(int)args[2]!.GetValue<double>(),(int)args[3]!.GetValue<double>(),args[4]!.GetValue<bool>(),web.PointToScreen(Point.Empty));result=true;break;
                case "ctTitleBar":case "ctRow":result=true;break;
                case "ctHide":Hide();result=true;break;
                case "ctQuit":BeginInvoke((Action)Exit);result=true;break;
                case "ctStartup":result=SetStartup(args[0]!.GetValue<bool>());break;
                case "ctOpenMouseSettings":Process.Start(new ProcessStartInfo("ms-settings:mousetouchpad"){UseShellExecute=true});result=true;break;
                case "ctOpenSettingsFile":Process.Start(new ProcessStartInfo("notepad.exe"){ArgumentList={settingsPath},UseShellExecute=false});result=true;break;
                case "ctTestMacro":controller.PlayMacro(args[0]!.AsObject());result=true;break;
                case "ctUpdState":result=update.DeepClone();break;
                case "ctUpdCheck":await CheckUpdates();result=update.DeepClone();break;
                case "ctUpdInstall":await InstallUpdate();result=true;break;
                case "ctInstallIntegration":result=IntegrationInstaller.Install(args[0]!.GetValue<string>());break;
                case "ctAddProfile":
                    var c=store.Snapshot();var app=args[0]!.AsObject();var name=ConfigStore.S(app,"name").Trim();var exe=ConfigStore.S(app,"exe").Trim();
                    if(name.Length is <1 or >60||exe.Length is <1 or >100||exe.Contains('\\')||exe.Contains('/'))throw new ArgumentException("Enter an app name and executable filename, for example ExampleCAD.exe.");
                    c["profiles"]!.AsArray().Add(new JsonObject{["id"]="custom-"+Guid.NewGuid().ToString("N"),["name"]=name,["enabled"]=true,["exe"]=new JsonArray(exe),["title"]=new JsonArray(),["orbit"]=new JsonObject{["buttons"]=new JsonArray("middle"),["mods"]=new JsonArray("shift")},["pan"]=new JsonObject{["buttons"]=new JsonArray("middle"),["mods"]=new JsonArray()},["fit"]="F",["note"]="Custom mouse emulation. Edit Orbit, Pan and Fit to match this app. Cursor pivot/snap/navigation require a native adapter.",["custom"]=true});
                    store.Save(c);result=store.Snapshot();break;
                default:throw new ArgumentException("Unknown app command.");
            }
            web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{id,result}));
        }catch(Exception ex){
            controller.Log(ex.Message,"err");web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new{id,error=ex.Message}));
        }
    }
    bool StartupEnabled(){using var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");return k?.GetValue("ChamferTrackball")!=null;}
    bool SetStartup(bool enabled){using var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(enabled)k.SetValue("ChamferTrackball","\""+Environment.ProcessPath+"\" --tray");else k.DeleteValue("ChamferTrackball",false);return enabled;}
    async Task CheckUpdates(){
        update["stage"]="checking";
        try{
            using var response=await http.GetAsync("https://api.github.com/repos/chamfer3dprinting/Chamfer-Trackball/releases?per_page=20");response.EnsureSuccessStatusCode();
            var releases=JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
            var latest=releases.Where(r=>!ConfigStore.B(r,"draft")).Select(r=>(Node:r,Version:System.Version.TryParse(ConfigStore.S(r,"tag_name").TrimStart('v'),out var v)?v:null)).Where(r=>r.Version!=null).OrderByDescending(r=>r.Version).FirstOrDefault();
            update["stage"]="latest";
            var version=latest.Version;
            if(latest.Node!=null&&version!=null&&version>new Version(ConfigStore.Version)){
                var asset=latest.Node["assets"]?.AsArray().FirstOrDefault(x=>ConfigStore.S(x,"name")=="ChamferTrackball.exe");
                var url=ConfigStore.S(asset,"browser_download_url");var digest=ConfigStore.S(asset,"digest");
                if(url.StartsWith("https://github.com/chamfer3dprinting/Chamfer-Trackball/releases/download/",StringComparison.Ordinal)&&digest.StartsWith("sha256:")&&digest.Length==71){
                    updateUrl=url;updateHash=digest[7..];update["stage"]="available";update["latest"]=version.ToString();update["notes"]=new JsonArray(ConfigStore.S(latest.Node,"body"));
                }
            }
        }catch(Exception ex){update["stage"]="error";update["error"]=ex.Message;}
    }
    async Task InstallUpdate(){
        if(updateUrl.Length==0)throw new InvalidOperationException("Check for an update first.");
        update["stage"]="downloading";update["percent"]=0;
        var bytes=await http.GetByteArrayAsync(updateUrl);
        if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(updateHash,StringComparison.OrdinalIgnoreCase))throw new IOException("The download did not match GitHub's SHA-256 digest.");
        var target=Path.Combine(Path.GetTempPath(),"ChamferTrackball-update-"+Guid.NewGuid().ToString("N")+".exe");await File.WriteAllBytesAsync(target,bytes);
        var p=new ProcessStartInfo(target){UseShellExecute=false};p.ArgumentList.Add("--install-update");p.ArgumentList.Add(target);p.ArgumentList.Add(Environment.ProcessPath!);p.ArgumentList.Add(Environment.ProcessId.ToString());
        Process.Start(p);update["stage"]="restarting";Exit();
    }
    async Task NativeSmokeTest(){
        using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(3)};
        var first=JsonNode.Parse(await client.GetStringAsync("http://127.0.0.1:47613/native/poll?app=blender&pid="+Environment.ProcessId))!;
        var connection=native.Find("blender",Environment.ProcessId)??throw new Exception("Native adapter registration failed.");
        native.Begin(connection,NavMode.Orbit,240,120);
        var begin=JsonNode.Parse(await client.GetStringAsync("http://127.0.0.1:47613/native/poll?app=blender&pid="+Environment.ProcessId))!;
        if(begin["commands"]!.AsArray().All(x=>ConfigStore.S(x,"t")!="begin"))throw new Exception("Native begin request missing.");
        var scene=new JsonObject{["id"]=first["id"]!.GetValue<string>(),["t"]="scene",["session"]=connection.Session,["eye"]=new JsonArray(4,3,10),["target"]=new JsonArray(1,1,0),["up"]=new JsonArray(0,1,0),["pivot"]=new JsonArray(1,1,0),["verticalFov"]=.8,["orthoHeight"]=10,["width"]=1200,["height"]=800};
        using(var response=await client.PostAsync("http://127.0.0.1:47613/native/scene",new StringContent(scene.ToJsonString(),Encoding.UTF8,"application/json")))response.EnsureSuccessStatusCode();
        if(connection.Pose==null||!native.Move(connection,NavMode.Orbit,20,10,60,false,1))throw new Exception("Native camera/pivot handshake failed.");
        var pose=JsonNode.Parse(await client.GetStringAsync("http://127.0.0.1:47613/native/poll?app=blender&pid="+Environment.ProcessId))!;
        if(!pose["commands"]!.AsArray().Any(x=>ConfigStore.S(x,"t")=="camera"))throw new Exception("Native camera response missing.");
        native.End(connection);
        using(var response=await client.PostAsync("http://127.0.0.1:47613/native/scene",new StringContent(scene.ToJsonString(),Encoding.UTF8,"application/json")))response.EnsureSuccessStatusCode();
        if(connection.Pose!=null)throw new Exception("An ended gesture accepted a stale camera reply.");
        using var forbidden=new HttpRequestMessage(HttpMethod.Get,"http://127.0.0.1:47613/native/poll?app=blender&pid="+Environment.ProcessId);
        forbidden.Headers.Add("Origin","https://untrusted.example");
        using var denied=await client.SendAsync(forbidden);
        if(denied.StatusCode!=System.Net.HttpStatusCode.Forbidden)throw new Exception("Native origin restriction failed.");
    }
    async Task SmokeTest(){
        try{
            await NativeSmokeTest();
            await Task.Delay(1200);
            var check=await web.CoreWebView2.ExecuteScriptAsync("(async()=>{const state=await ctState();if(state.version!=='1.5.6')throw Error('version');goTo('controls');renderBinds();if(!document.querySelector('[data-hold=snapKeys]')||!document.querySelector('[data-hold=navigateKeys]'))throw Error('missing held keys');cfg.combos.push({buttons:[],bind:{action:'keys',keys:'Ctrl+S'}});await doSave();const saved=await ctState();if(!saved.config.combos.some(c=>c.buttons.length===0&&c.bind.keys==='Ctrl+S'))throw Error('draft not saved');renderAll();return true;})()");
            // ExecuteScriptAsync returns a Promise object for async expressions. Confirm
            // persisted state from C# after the bridge has completed, then inspect DOM.
            await Task.Delay(500);
            var ready=await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-hold=snapKeys]')!==null&&document.querySelector('[data-hold=navigateKeys]')!==null&&document.querySelector('[data-install-integration=blender]')!==null");
            if(ready!="true"||store.Snapshot()["combos"]?.AsArray().Any(c=>ConfigStore.S(c?["bind"],"keys")=="Ctrl+S")!=true)throw new Exception("UI bridge/persistence smoke test failed: "+check+" "+ready);
            var output=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-ui-smoke.png");
            using(var image=File.Create(output))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-ui-smoke.ok"),"Version, held key controls, integration installer and saved combination verified.");
            Exit();
        }catch(Exception ex){File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-ui-smoke.error"),ex.ToString());Console.Error.WriteLine(ex);Environment.Exit(1);}
    }
    void Exit(){quit=true;Close();}
}
