using System.Net.WebSockets;
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
    const string AppUrl="https://chamfer-trackball.invalid/";
    readonly WebView2 web=new(){Dock=DockStyle.Fill};
    readonly NotifyIcon tray=new(){Icon=Brand.Icon,Text="Chamfer Trackball",Visible=true};
    readonly ConfigStore store;readonly InputDriver input;readonly NativeServer native;readonly Controller controller;
    readonly string settingsPath;readonly GuideForm guide;readonly AdminService admin;
    readonly System.Windows.Forms.Timer updateTimer=new(){Interval=900000};
    bool quit;readonly bool smoke;JsonObject update=new(){["channel"]=BuildInfo.Edition,["stage"]="idle",["version"]=BuildInfo.Version,["history"]=new JsonArray(),["notes"]=new JsonArray("Hold Shift to orbit, Ctrl+Shift to pan, Ctrl to zoom and Alt for six-face snapping. Release the keys for normal pointing.")};
    string updateUrl="",updateHash="";
    readonly HttpClient http=new(){Timeout=TimeSpan.FromSeconds(30)};
    public MainForm(bool smoke=false,bool startTray=false){
        this.smoke=smoke;
        Text="Chamfer Trackball"+(BuildInfo.Admin?" Admin":"");Icon=Brand.Icon;ClientSize=new(1106,754);MinimumSize=new(920,640);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(20,23,27);
        settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ChamferTrackball","settings.json");
        store=new(settingsPath);native=new();if(native.Error.Length>0){native.Dispose();throw new InvalidOperationException(native.Error);}input=new();controller=new(store,input,native);guide=new(controller,store);admin=new(Path.GetDirectoryName(settingsPath)!);
        Controls.Add(web);http.DefaultRequestHeaders.UserAgent.ParseAdd("ChamferTrackball/"+BuildInfo.Version);
        var menu=new ContextMenuStrip();menu.Items.Add("Open",null,(_,_)=>{Show();WindowState=FormWindowState.Normal;Activate();});menu.Items.Add("Show control guide",null,(_,_)=>{var c=store.Snapshot();c["modePanel"]=true;store.Save(c);});menu.Items.Add("Quit",null,(_,_)=>Exit());tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>{Show();Activate();};
        Shown+=async(_,_)=>{await Initialize();if(startTray)Hide();};
        updateTimer.Tick+=async(_,_)=>{if(!smoke&&ConfigStore.B(store.Snapshot(),"autoUpdateCheck",true))await CheckUpdates();};updateTimer.Start();
        FormClosing+=(_,e)=>{if(!quit){e.Cancel=true;Hide();return;}updateTimer.Dispose();guide.Dispose();admin.Dispose();controller.Dispose();input.Dispose();native.Dispose();tray.Dispose();http.Dispose();};
    }
    async Task Initialize(){
        try{
            // Keep the trusted embedded settings UI separate from remote websites.
            var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Path.GetDirectoryName(settingsPath)!,"RebuiltWebView"));
            await web.EnsureCoreWebView2Async(env);
            web.CoreWebView2.Settings.AreDevToolsEnabled=false;
            web.CoreWebView2.Settings.IsStatusBarEnabled=false;
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
            web.CoreWebView2.NavigationStarting+=(_,e)=>{if(e.Uri!=AppUrl)e.Cancel=true;};
            web.CoreWebView2.NewWindowRequested+=(_,e)=>{e.Handled=true;if(Uri.TryCreate(e.Uri,UriKind.Absolute,out var u)&&u.Scheme=="https")Process.Start(new ProcessStartInfo(u.AbsoluteUri){UseShellExecute=true});};
            web.CoreWebView2.WebMessageReceived+=HandleMessage;
            string[] names=["ctState","ctSave","ctStatus","ctResetAll","ctProgram","ctProgramDelete","ctPresetSave","ctPresetApply","ctPresetRename","ctPresetDelete","ctLog","ctQuit","ctHide","ctStartup","ctPadRect","ctRow","ctTitleBar","ctOpenMouseSettings","ctOpenSettingsFile","ctTestMacro","ctUpdState","ctUpdCheck","ctUpdInstall","ctInstallIntegration","ctAddProfile","ctIdentify","ctCancelIdentify","ctDeviceState","adminState","adminRefresh","adminConnect","adminClearToken","adminRelease","adminRequestVersion","adminWithdraw"];
            var js="(()=>{const pending=new Map();let seq=0;chrome.webview.addEventListener('message',e=>{const m=e.data;const p=pending.get(m.id);if(!p)return;pending.delete(m.id);m.error?p[1](new Error(m.error)):p[0](m.result);});for(const n of "+JsonSerializer.Serialize(names)+")window[n]=(...args)=>new Promise((ok,no)=>{const id=++seq;pending.set(id,[ok,no]);chrome.webview.postMessage({id,name:n,args});});})();";
            await web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(js);
            using var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("ChamferTrackball.ui.html")??throw new IOException("Interface resource is missing.");
            using var reader=new StreamReader(s);var html=Encoding.UTF8.GetBytes((await reader.ReadToEndAsync()).Replace("{{MARK}}",Brand.DataUrl));
            web.CoreWebView2.AddWebResourceRequestedFilter(AppUrl+"*",CoreWebView2WebResourceContext.All);
            web.CoreWebView2.WebResourceRequested+=(_,e)=>{
                if(e.Request.Uri==AppUrl)e.Response=env.CreateWebResourceResponse(new MemoryStream(html,false),200,"OK","Content-Type: text/html; charset=utf-8");
            };
            if(smoke)web.CoreWebView2.NavigationCompleted+=async(_,e)=>{if(e.IsSuccess&&web.Source?.AbsoluteUri==AppUrl)await SmokeTest();};
            web.CoreWebView2.Navigate(AppUrl);
            if(!smoke&&ConfigStore.B(store.Snapshot(),"autoUpdateCheck",true))_ = CheckUpdates();
        }catch(Exception ex){if(smoke){File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-ui-smoke.error"),ex.ToString());Console.Error.WriteLine(ex);Environment.Exit(1);}MessageBox.Show("Cannot open the interface. Ensure Microsoft Edge WebView2 Runtime is installed. "+ex.Message);Exit();}
    }
    async void HandleMessage(object? sender,CoreWebView2WebMessageReceivedEventArgs e){
        if(smoke)File.AppendAllText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-ui-smoke.bridge"),e.Source+" "+ConfigStore.S(JsonNode.Parse(e.WebMessageAsJson),"name")+"\\n");
        if(e.Source!=AppUrl||e.WebMessageAsJson.Length>1024*1024)return;
        long id=0;
        try{
            var message=JsonNode.Parse(e.WebMessageAsJson)!.AsObject();id=message["id"]!.GetValue<long>();var args=message["args"]!.AsArray();object? result=null;
            switch(ConfigStore.S(message,"name")){
                case "ctState":result=new{version=BuildInfo.Version,edition=BuildInfo.Edition,publicVersion=BuildInfo.PublicVersion,build=BuildInfo.Revision,source=BuildInfo.Source,notes=new[]{new{version=BuildInfo.Version,notes=new[]{"Original Chamfer logo and floating control guide restored.","Identify the trackball once: corrections affect that device only.","Separate Admin build updates and explicit public release controls.","Default pointer movement with held Shift orbit, Ctrl+Shift pan, Ctrl zoom and Alt face snapping.","Normal mouse reset and queued saving preserve your settings.","Updates now advance the application version; click Update to download, install and restart."}}},config=store.Snapshot(),defaults=ConfigStore.Defaults(),programDefaults=ConfigStore.Defaults()["programs"],settingsPath,startup=StartupEnabled(),pointerAccel=false,justUpdated=false};break;
                case "ctSave":store.Save(args[0]!.AsObject());result=store.Snapshot();break;
                case "ctStatus":result=controller.Status();break;
                case "ctResetAll":result=store.Reset();break;
                case "ctProgram":store.Select(args[0]!.GetValue<int>());result=true;break;
                case "ctProgramDelete":result=store.DeleteProgram(args[0]!.GetValue<int>());break;
                case "ctPresetSave":result=store.SavePreset(args[0]!.GetValue<string>(),args.Count>1&&args[1]!.GetValue<bool>());break;
                case "ctPresetApply":result=store.ApplyPreset(args[0]!.GetValue<int>());break;
                case "ctPresetRename":result=store.RenamePreset(args[0]!.GetValue<int>(),args[1]!.GetValue<string>());break;
                case "ctPresetDelete":result=store.DeletePreset(args[0]!.GetValue<int>());break;
                case "ctLog":result=controller.LogAfter(args[0]?.GetValue<long>()??0);break;
                case "ctPadRect":controller.Pad((int)args[0]!.GetValue<double>(),(int)args[1]!.GetValue<double>(),(int)args[2]!.GetValue<double>(),(int)args[3]!.GetValue<double>(),args[4]!.GetValue<bool>(),web.PointToScreen(Point.Empty));result=true;break;
                case "ctTitleBar":case "ctRow":result=true;break;
                case "ctHide":Hide();result=true;break;
                case "ctQuit":BeginInvoke((Action)Exit);result=true;break;
                case "ctStartup":result=SetStartup(args[0]!.GetValue<bool>());break;
                case "ctOpenMouseSettings":Process.Start(new ProcessStartInfo("ms-settings:mousetouchpad"){UseShellExecute=true});result=true;break;
                case "ctOpenSettingsFile":Process.Start(new ProcessStartInfo("notepad.exe"){ArgumentList={settingsPath},UseShellExecute=false});result=true;break;
                case "ctTestMacro":controller.PlayMacro(args[0]!.AsObject());result=true;break;
                case "ctIdentify":input.Identify();result=true;break;
                case "ctCancelIdentify":input.CancelIdentify();result=true;break;
                case "ctDeviceState":result=new{ready=input.Ready,status=input.DeviceStatus};break;
                case "adminState":await admin.Poll();result=admin.State();break;
                case "adminRefresh":await admin.Refresh();result=admin.State();break;
                case "adminConnect":await admin.Connect(args[0]!.GetValue<string>());result=admin.State();break;
                case "adminClearToken":admin.Clear();result=true;break;
                case "adminRequestVersion":await admin.RequestPublicVersion(args[0]!.GetValue<string>());result=admin.State();break;
                case "adminRelease":await admin.Release(args[0]!.GetValue<string>());result=true;break;
                case "adminWithdraw":await admin.Withdraw(args[0]!.GetValue<string>());result=true;break;
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
        if(ConfigStore.S(update,"stage") is "checking" or "downloading" or "restarting")return;
        update["stage"]="checking";
        try{
            var releases=new JsonArray();
            for(int page=1;page<=100;page++){
                using var response=await http.GetAsync("https://api.github.com/repos/chamfer3dprinting/Chamfer-Trackball/releases?per_page=100&page="+page);response.EnsureSuccessStatusCode();
                var batch=JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
                foreach(var release in batch)releases.Add(release!.DeepClone());
                if(batch.Count<100)break;
                if(page==100)throw new IOException("Too many releases to inspect safely.");
            }
            var latest=ReleasePolicy.Latest(releases,BuildInfo.Admin,long.TryParse(BuildInfo.Revision,out var build)?build:0,BuildInfo.Version);
            update["stage"]="latest";updateUrl="";updateHash="";update.Remove("latest");update.Remove("error");
            if(latest!=null){
                var asset=latest["assets"]?.AsArray().FirstOrDefault(x=>ConfigStore.S(x,"name")==BuildInfo.Asset);
                var url=ConfigStore.S(asset,"browser_download_url");var digest=ConfigStore.S(asset,"digest");
                if(url.StartsWith("https://github.com/chamfer3dprinting/Chamfer-Trackball/releases/download/",StringComparison.Ordinal)&&digest.StartsWith("sha256:")&&digest.Length==71){
                    updateUrl=url;updateHash=digest[7..];update["stage"]="available";
                    update["latest"]=ReleasePolicy.VersionText(ConfigStore.S(latest,"tag_name"),BuildInfo.Admin);
                    update["notes"]=new JsonArray(ConfigStore.S(latest,"body"));
                }else throw new IOException("The new release is missing its verified executable.");
            }
        }catch(Exception ex){update["stage"]="error";update["error"]=ex.Message;}
    }
    async Task InstallUpdate(){
        if(ConfigStore.S(update,"stage") is "downloading" or "installing" or "restarting")return;
        if(updateUrl.Length==0)throw new InvalidOperationException("Check for an update first.");
        try{
        update["stage"]="downloading";update["percent"]=0;
        var bytes=await http.GetByteArrayAsync(updateUrl);
        if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(updateHash,StringComparison.OrdinalIgnoreCase))throw new IOException("The download did not match GitHub's SHA-256 digest.");
        var target=Path.Combine(Path.GetTempPath(),"ChamferTrackball-update-"+Guid.NewGuid().ToString("N")+".exe");await File.WriteAllBytesAsync(target,bytes);
        var p=new ProcessStartInfo(target){UseShellExecute=false};p.ArgumentList.Add("--install-update");p.ArgumentList.Add(target);p.ArgumentList.Add(Environment.ProcessPath!);p.ArgumentList.Add(Environment.ProcessId.ToString());
        Process.Start(p);update["stage"]="restarting";Exit();
        }catch(Exception ex){update["stage"]="error";update["error"]=ex.Message;throw;}
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

        // Real WebSocket negotiation from a focused Studio browser tab. Its
        // process ID is not exposed to web JavaScript, unlike native adapters.
        using var browser=new ClientWebSocket();
        browser.Options.SetRequestHeader("Origin","https://studio.chamfer3dprinting.com");
        using var browserTimeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await browser.ConnectAsync(new Uri("ws://127.0.0.1:47613"),browserTimeout.Token);
        async Task BrowserSend(JsonObject value)=>await browser.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(value.ToJsonString())),WebSocketMessageType.Text,true,browserTimeout.Token);
        async Task<JsonObject> BrowserRead(string type){
            var bytes=new byte[16384];
            for(int tries=0;tries<12;tries++){int length=0;WebSocketReceiveResult received;
                do{received=await browser.ReceiveAsync(new ArraySegment<byte>(bytes,length,bytes.Length-length),browserTimeout.Token);length+=received.Count;}while(!received.EndOfMessage);
                var message=JsonNode.Parse(bytes.AsSpan(0,length))!.AsObject();if(ConfigStore.S(message,"t")==type)return message;
            }throw new Exception("Missing browser camera message: "+type);
        }
        await BrowserSend(new(){["t"]="hello",["app"]="chamfer3d",["v"]=1});
        var offer=await BrowserRead("ready");if(ConfigStore.I(offer,"v")!=2)throw new Exception("Studio protocol upgrade missing.");
        await BrowserSend(new(){["t"]="hello",["app"]="chamfer3d",["v"]=2});
        NativeServer.Client? studio=null;
        for(int tries=0;tries<50;tries++){studio=native.Find("chamfer3d",Environment.ProcessId);if(studio?.Version==2)break;await Task.Delay(10);}
        if(studio?.Version!=2||!native.Supports(studio,NavMode.Snap)||!native.Supports(studio,NavMode.Navigate))throw new Exception("Focused Studio browser did not support snap/navigation.");
        foreach(var command in new[]{NavMode.Snap,NavMode.Navigate,NavMode.PanZoom,NavMode.Zoom}){
            native.Begin(studio,command,240,120);var request=await BrowserRead("begin");
            var browserScene=(JsonObject)scene.DeepClone();browserScene["session"]=ConfigStore.S(request,"session");
            await BrowserSend(browserScene);
            for(int tries=0;tries<50&&studio.Pose==null;tries++)await Task.Delay(10);
            if(studio.Pose==null||!native.Move(studio,command,30,20,120,command==NavMode.Navigate,1))throw new Exception("Studio camera command did not run: "+command);
            if(command==NavMode.Snap&&studio.Pose!.WorldUp!=System.Numerics.Vector3.UnitY)throw new Exception("Studio snap did not use the host cube's Y-up convention.");
            var result=await BrowserRead("camera");if(ConfigStore.S(result,"session")!=studio.Session)throw new Exception("Studio camera session mismatch.");
            native.End(studio);await BrowserRead("end");
        }

        // Exercise a natural-sized face turn through the actual Studio socket,
        // including the serialized camera Up vector and guide feedback.
        native.Begin(studio,NavMode.Snap,240,120);var turnRequest=await BrowserRead("begin");
        var turnScene=(JsonObject)scene.DeepClone();turnScene["session"]=ConfigStore.S(turnRequest,"session");
        turnScene["eye"]=new JsonArray(0,0,10);turnScene["target"]=new JsonArray(0,0,0);turnScene["pivot"]=new JsonArray(0,0,0);
        await BrowserSend(turnScene);
        for(int tries=0;tries<50&&studio.Pose==null;tries++)await Task.Delay(10);
        if(studio.Pose==null||ConfigStore.S(native.SnapState(studio),"state")!="ready")throw new Exception("Face-turn ready feedback missing.");
        var turnStart=studio.Pose!;var turnPrevious=new System.Numerics.Vector2(180,0);
        for(int sample=1;sample<=80;sample++){
            float angle=MathF.Tau*sample/80;var point=new System.Numerics.Vector2(180*MathF.Cos(angle),180*MathF.Sin(angle));var delta=point-turnPrevious;turnPrevious=point;
            if(!native.Move(studio,NavMode.Snap,delta.X,delta.Y,0,false,.2f))throw new Exception("Studio face turn was not delivered.");
        }
        var turnState=native.SnapState(studio);
        if(ConfigStore.I(turnState,"turns")!=1||ConfigStore.I(turnState,"direction")!=1||ConfigStore.S(turnState,"message")!="Clockwise 90°")throw new Exception("Face-turn feedback did not match the recognized clockwise gesture.");
        var wantedUp=CameraMath.AlignFace(turnStart,System.Numerics.Vector3.UnitZ,-1).Up;bool sawTurnCamera=false;
        for(int tries=0;tries<100;tries++){
            var result=await BrowserRead("camera");var up=result["up"]!;
            var receivedUp=new System.Numerics.Vector3(up[0]!.GetValue<float>(),up[1]!.GetValue<float>(),up[2]!.GetValue<float>());
            if(System.Numerics.Vector3.Distance(receivedUp,wantedUp)<.0001f){sawTurnCamera=true;break;}
        }
        if(!sawTurnCamera||System.Numerics.Vector3.Dot(System.Numerics.Vector3.Normalize(studio.Pose!.Eye-studio.Pose.Target),System.Numerics.Vector3.UnitZ)<.999f)throw new Exception("Studio face turn changed the selected face or lost camera orientation.");
        native.End(studio);await BrowserRead("end");

        await BrowserSend(new(){["t"]="focus",["on"]=false});
        for(int tries=0;tries<50&&native.Find("chamfer3d",Environment.ProcessId)!=null;tries++)await Task.Delay(10);
        if(native.Find("chamfer3d",Environment.ProcessId)!=null||native.AllowsNavigation("chamfer3d",Environment.ProcessId))throw new Exception("Unfocused Studio browser retained navigation or mouse emulation.");
    }
    async Task SmokeTest(){
        try{
            if(!System.Text.RegularExpressions.Regex.IsMatch(BuildInfo.Version,@"^\d+\.\d+\.\d+$")||(!BuildInfo.Admin&&BuildInfo.Version!=BuildInfo.PublicVersion))throw new Exception("Edition version metadata mismatch.");
            input.VerifyRecovery();
            await NativeSmokeTest();
            await ReleaseSmoke.Run();
            if(BuildInfo.Admin){var state=admin.State();if(state==null)throw new Exception("Admin state unavailable.");}
            else{bool denied=false;try{admin.State();}catch(UnauthorizedAccessException){denied=true;}if(!denied)throw new Exception("Public edition accepted an Admin command.");}
            using(var image=new Bitmap(guide.Width,guide.Height)){guide.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-guide-smoke.png"));}
            await Task.Delay(1200);
            store.IdentifyDevice("ui-identified-trackball");
            var check=await web.CoreWebView2.ExecuteScriptAsync("(async()=>{const state=await ctState();if(!state.version||state.version.split('.').length!==3)throw Error('version');if(!document.querySelector('.brand img').src.startsWith('data:image/png;base64,')||document.querySelector('.brand img').naturalWidth<64)throw Error('logo missing');if(state.edition==='admin'&&document.querySelector('#navAdmin').hidden)throw Error('admin missing');if(state.edition==='admin'){goTo('admin');if(!document.querySelector('#publicVersion')||!document.querySelector('#releasePublic')||document.querySelector('#releasePublic').getBoundingClientRect().width===0)throw Error('custom release control missing');}renderNotes();if(document.querySelector('#sideVer').textContent!=='v'+state.version||document.querySelector('#abVer').textContent.includes('build'))throw Error('version label uses internal build');goTo('home');renderCheat();updateFaceTurn({snapTurn:{message:'Curve detected — keep curling'}});if(!document.querySelector('#homeFaceTurn').textContent.includes('Curve detected')||!document.querySelector('#controlFaceTurn').textContent.includes('Curve detected'))throw Error('face curve feedback missing');updateFaceTurn({snapTurn:{message:'Clockwise 90°'}});if(!document.querySelector('#homeFaceTurn').textContent.includes('Clockwise 90°'))throw Error('face turn feedback missing');updateFaceTurn({});updateKeyboard({keys:[91,18],appState:'cad',mode:'Navigate'});if(document.querySelectorAll('#liveKeyboard .down').length!==3)throw Error('physical Windows/Alt keyboard indicators missing');updateKeyboard({keys:[],appState:'cad',mode:'Idle'});if(document.querySelector('#liveKeyboard .down'))throw Error('keyboard release stuck');goTo('controls');renderBinds();if(!document.querySelector('[data-hold=snapKeys]')||!document.querySelector('[data-hold=navigateKeys]'))throw Error('missing held keys');if(!document.querySelector('[data-preset=ballnav]')||!document.querySelector('[data-k=circleZoom]')||!document.querySelector('[data-hold=zoomKeys]'))throw Error('missing ball navigation controls');cfg.combos.push({buttons:[],bind:{action:'keys',keys:'Ctrl+S'}});await doSave();const saved=await ctState();if(saved.config.selectedDevice!=='ui-identified-trackball')throw Error('settings save erased trackball identity');if(!document.querySelector('#homeIdentifyTrackball')||!document.querySelector('#homeDeviceSetup'))throw Error('Home identification controls missing');if(!saved.config.combos.some(c=>c.buttons.length===0&&c.bind.keys==='Ctrl+S'))throw Error('draft not saved');cfg=await ctPresetSave('UI preset',false);renderPrograms();if(cfg.presets.length!==1||!document.querySelector('#presetSelect').textContent.includes('UI preset'))throw Error('preset save or selector missing');const originalPointer=cfg.pointerSpeed;cfg.pointerSpeed=18;await doSave();cfg=await ctPresetApply(0);if(cfg.pointerSpeed!==originalPointer||cfg.selectedDevice!=='ui-identified-trackball')throw Error('preset apply lost speed or identity');cfg=await ctPresetRename(0,'Renamed UI preset');if(cfg.presets[0].name!=='Renamed UI preset')throw Error('preset rename failed');cfg=await ctPresetDelete(0);if(cfg.presets.length)throw Error('preset delete failed');await ctProgram(0);cfg=(await ctState()).config;cfg.pointerSpeed=6;cfg.glide=12;cfg.smooth=false;await doSave();await new Promise(r=>setTimeout(r,140));let normalSpeeds=await ctStatus();if(Math.abs(normalSpeeds.pointerGain-.5)>.00001)throw Error('Normal mouse DPI did not reach input engine');await ctProgram(1);cfg=(await ctState()).config;cfg.orbitSens=1;cfg.panSens=25;cfg.zoomSens=0;await doSave();await new Promise(r=>setTimeout(r,140));const cadSpeeds=await ctStatus();if(Math.abs(cadSpeeds.orbitGain-.01)>.00001||Math.abs(cadSpeeds.panGain-.09540955)>.00001||cadSpeeds.zoomGain!==0)throw Error('3D speed sliders did not reach input engine');await ctProgram(0);cfg=(await ctState()).config;await new Promise(r=>setTimeout(r,140));normalSpeeds=await ctStatus();if(cfg.pointerSpeed!==6||cfg.glide!==12||cfg.smooth||Math.abs(normalSpeeds.pointerGain-.5)>.00001)throw Error('Normal mouse motion settings changed after program switch');await ctProgram(1);cfg=(await ctState()).config;renderAll();await new Promise(r=>setTimeout(r,700));let mutations=0;const observer=new MutationObserver(rows=>mutations+=rows.length);for(const id of ['footApp','stMode','stDevice','stApp','integrationState','padMode'])observer.observe(document.getElementById(id),{childList:true,characterData:true,subtree:true});await new Promise(r=>setTimeout(r,600));observer.disconnect();if(mutations>2)throw Error('Idle status labels are rebuilding: '+mutations);return true;})().then(()=>window.__smokeResult={ok:true}).catch(e=>window.__smokeResult={ok:false,error:String(e.stack||e)})");
            // ExecuteScriptAsync returns a Promise object for async expressions. Confirm
            // persisted state from C# after the bridge has completed, then inspect DOM.
            string outcome="null";
            for(int tries=0;tries<50;tries++){await Task.Delay(100);outcome=await web.CoreWebView2.ExecuteScriptAsync("JSON.stringify(window.__smokeResult||null)");if(outcome!="\"null\"")break;}
            var diagnostic=await web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({url:location.href,ctState:typeof window.ctState,cfg:typeof cfg,toast:document.querySelector('#toast')?.textContent,body:document.body.innerText.slice(0,1800),result:window.__smokeResult})");
            using(var screenshot=File.Create(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-ui-smoke.png")))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,screenshot);
            var ready=await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-hold=snapKeys]')!==null&&document.querySelector('[data-hold=navigateKeys]')!==null&&document.querySelector('[data-install-integration=blender]')!==null");
            var passed=await web.CoreWebView2.ExecuteScriptAsync("window.__smokeResult?.ok===true");
            if(passed!="true"||ready!="true"||store.Snapshot()["programs"]?[0]?["combos"]?.AsArray().Any(c=>ConfigStore.S(c?["bind"],"keys")=="Ctrl+S")!=true)throw new Exception("UI bridge/persistence smoke test failed: "+check+" "+ready+" "+outcome+" "+diagnostic);
            var output=Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-ui-smoke.png");
            using(var image=File.Create(output))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-ui-smoke.ok"),"Version, logo, edition, Home identification, trackball identity across UI saves, floating guide, held controls, input reconnect/registration recovery, Studio snap/navigation negotiation, quiet idle status, integration installer, saved presets, Normal mouse DPI/glide, fine 3D gains across program switches and saved combination verified.");
            Exit();
        }catch(Exception ex){File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")??Path.GetTempPath(),"chamfer-ui-smoke.error"),ex.ToString());Console.Error.WriteLine(ex);Environment.Exit(1);}
    }
    void Exit(){quit=true;Close();}
}
