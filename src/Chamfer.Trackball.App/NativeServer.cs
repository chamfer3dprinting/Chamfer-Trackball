using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
namespace Chamfer.Trackball.App;
internal sealed class NativeServer:IDisposable {
    internal sealed class Client {
        public string Id=Guid.NewGuid().ToString("N"),App="",Session="";
        public int Pid,Version=2;public DateTime Seen=DateTime.UtcNow;
        public bool Focus=true,SnapTwist,EstimateTwist;public float TwistGain=1;public string TwistAxis="x";public bool ReverseTwist;public DateTime Began;public CameraPose? Pose,BasePose;public readonly FaceSnap Snap=new();public readonly EstimatedFaceSnap EstimatedSnap=new();
        public ConcurrentQueue<JsonObject> Out=new();public WebSocket? Socket;
        public readonly SemaphoreSlim OutSignal=new(0,1);
        public object Gate=new();
    }
    readonly TcpListener listener=new(IPAddress.Loopback,47613);
    readonly CancellationTokenSource stop=new();
    readonly ConcurrentDictionary<string,Client> clients=new();
    static readonly JsonSerializerOptions json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    public string Error {get;private set;}="";
    public NativeServer(){try{listener.Start();_ = Accept();}catch(SocketException){Error="Port 47613 is in use. Quit the previous Trackball app, then reopen this version.";}}
    static bool Matches(Client c,string app,int pid)=>c.App==app&&(c.Pid==pid||c.Socket!=null&&c.Pid==0&&app=="chamfer3d")&&DateTime.UtcNow-c.Seen<TimeSpan.FromSeconds(3);
    public Client? Find(string app,int pid)=>clients.Values.FirstOrDefault(c=>Matches(c,app,pid)&&c.Focus);
    public bool AllowsNavigation(string app,int pid){var relevant=clients.Values.Where(c=>Matches(c,app,pid)).ToArray();return relevant.Length==0||relevant.Any(c=>c.Focus);}
    public bool Supports(Client? c,NavMode mode)=>c!=null&&(c.Version>=2||mode is NavMode.Orbit or NavMode.PanZoom or NavMode.Zoom or NavMode.Roll);
    public void Begin(Client c,NavMode mode,int x,int y,bool snapTwist=false,bool estimateTwist=false,float twistGain=1,string twistAxis="x",bool reverseTwist=false){lock(c.Gate){c.SnapTwist=snapTwist;c.EstimateTwist=mode==NavMode.Snap&&estimateTwist;c.TwistGain=twistGain;c.TwistAxis=twistAxis;c.ReverseTwist=reverseTwist;c.EstimatedSnap.Reset();c.Began=DateTime.UtcNow;c.Session=Guid.NewGuid().ToString("N");c.Pose=c.BasePose=null;c.Snap.Reset();Queue(c,new(){["t"]="begin",["session"]=c.Session,["mode"]=mode.ToString(),["x"]=x,["y"]=y});}}
    public bool Move(Client c,NavMode mode,float dx,float dy,float wheel,bool vertical,float gain){
        lock(c.Gate){
            if(c.Version==1){
                Queue(c,new(){["t"]="m",["ox"]=mode==NavMode.Orbit?dx*gain:0,["oy"]=mode==NavMode.Orbit?dy*gain:0,["roll"]=mode==NavMode.Roll?-dx*gain*.005f:mode==NavMode.Orbit?wheel*gain*-.0015f:0,["px"]=mode==NavMode.PanZoom?dx*gain:0,["py"]=mode==NavMode.PanZoom?dy*gain:0,["zoom"]=MotionPolicy.ZoomAmount(mode,dy,wheel)*gain});
                return true;
            }
            if(c.Pose==null)return false;
            if(mode==NavMode.Snap)c.Pose=c.EstimateTwist?c.EstimatedSnap.Step(c.BasePose!,dx,dy,wheel,gain,c.TwistGain,c.TwistAxis,c.ReverseTwist,Environment.TickCount64):c.Snap.Step(c.BasePose!,dx,dy,wheel,gain);
            else c.Pose=CameraMath.Move(c.Pose,mode,dx,dy,wheel,vertical,gain);
            Queue(c,PoseMessage(c));return true;
        }
    }
    public bool PendingSnap(Client? c){if(c==null)return false;lock(c.Gate)return c.EstimateTwist&&c.EstimatedSnap.Pending;}
    public JsonObject SnapState(Client? c){
        if(c==null)return new(){["state"]="unavailable",["message"]="Face turns need a native 3D connection."};
        lock(c.Gate){
            if(c.EstimateTwist){
                var input=c.EstimatedSnap;string message=input.State=="estimating"?"Estimating motion…":input.EstimatedTwist?"Estimated twist · "+(input.LastDirection<0?"anticlockwise":"clockwise")+" · next "+Math.Round(input.Progress*100)+"%":"Faces · roll up/down/left/right";
                if(input.LastTurnAt!=0&&Environment.TickCount64-input.LastTurnAt<1400)message="Estimated "+(input.LastDirection>0?"clockwise":"anticlockwise")+" 90°";
                return new(){["state"]=input.State,["message"]=message,["samples"]=input.Samples,["turns"]=input.QuarterTurns,["direction"]=input.LastDirection};
            }
            var snap=c.Snap;bool recent=snap.TurnCount>0&&Environment.TickCount64-snap.LastTurnAt<1400;
            string state=c.Pose==null?"waiting":recent?"turned":snap.Samples==0?"ready":"rolling";
            string message=state switch{
                "waiting"=>"Waiting for the 3D view…",
                "turned"=>snap.LastTurnDirection>0?"Clockwise 90°":"Counterclockwise 90°",
                "ready"=>"Roll up/down/left/right to switch faces",
                _=>"Switching cube faces"
            };
            return new(){["state"]=state,["message"]=message,["samples"]=snap.Samples,["turns"]=snap.TurnCount,["direction"]=snap.LastTurnDirection};
        }
    }
    static JsonArray V(Vector3 v)=>new(v.X,v.Y,v.Z);
    static JsonObject PoseMessage(Client c){var p=c.Pose!;return new(){["t"]="camera",["session"]=c.Session,["eye"]=V(p.Eye),["target"]=V(p.Target),["up"]=V(p.Up),["orthoHeight"]=p.OrthoHeight,["orthographic"]=p.Orthographic};}
    public void End(Client? c){if(c==null)return;lock(c.Gate){Queue(c,new(){["t"]="end",["session"]=c.Session});c.Pose=c.BasePose=null;c.Session="";c.SnapTwist=c.EstimateTwist=false;}}
    static void Queue(Client c,JsonObject msg){
        lock(c.Gate){
            if(ConfigStore.S(msg,"t")=="camera"){
                var keep=new List<JsonObject>();var session=ConfigStore.S(msg,"session");
                while(c.Out.TryDequeue(out var queued))if(ConfigStore.S(queued,"t")!="camera"||ConfigStore.S(queued,"session")!=session)keep.Add(queued);
                foreach(var queued in keep)c.Out.Enqueue(queued);
            }
            while(c.Out.Count>=64&&c.Out.TryDequeue(out _)){ }
            c.Out.Enqueue(msg);
            if(c.Socket!=null&&c.OutSignal.CurrentCount==0)c.OutSignal.Release();
        }
    }
    void Receive(Client c,JsonObject m){
        c.Seen=DateTime.UtcNow;
        if(ConfigStore.S(m,"t")=="focus"){c.Focus=ConfigStore.B(m,"on");return;}
        if(ConfigStore.S(m,"t")!="scene"||ConfigStore.S(m,"session")!=c.Session||c.Session.Length==0)return;
        try{
            Vector3 vec(string key){var a=m[key]!.AsArray();if(a.Count!=3)throw new FormatException();return new(a[0]!.GetValue<float>(),a[1]!.GetValue<float>(),a[2]!.GetValue<float>());}
            var p=new CameraPose(vec("eye"),vec("target"),vec("up"),vec("pivot"),(float)ConfigStore.D(m,"verticalFov",.785398),(float)ConfigStore.D(m,"orthoHeight",10),ConfigStore.I(m,"width",800),ConfigStore.I(m,"height",600),ConfigStore.B(m,"orthographic"));
            p=p with{WorldUp=m["worldUp"] is JsonArray?vec("worldUp"):c.App=="chamfer3d"?Vector3.UnitY:Vector3.UnitZ};
            if(p.Valid&&CameraPose.Finite(p.WorldUp)&&p.WorldUp.LengthSquared()>1e-12f)lock(c.Gate){
                c.Pose=c.BasePose=p;c.Snap.Reset(p);c.EstimatedSnap.Reset(p);
                if(c.SnapTwist){c.Pose=c.BasePose=c.Snap.Step(p,0,0);Queue(c,PoseMessage(c));}
            }
        }catch(Exception ex)when(ex is FormatException or InvalidOperationException or ArgumentException or NullReferenceException){ }
    }
    async Task Accept(){
        while(!stop.IsCancellationRequested)try{var tcp=await listener.AcceptTcpClientAsync(stop.Token);_ = Handle(tcp);}catch(OperationCanceledException){break;}catch(SocketException)when(stop.IsCancellationRequested){break;}
    }
    static bool OriginAllowed(string? origin)=>string.IsNullOrEmpty(origin)||origin=="null"||origin=="https://studio.chamfer3dprinting.com"||origin=="https://preview.chamfer3dprinting.com";
    async Task Handle(TcpClient tcp){
        using(tcp)try{
            tcp.ReceiveTimeout=1500;tcp.SendTimeout=1500;
            var stream=tcp.GetStream();using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(2000);
            var bytes=new List<byte>();var one=new byte[1];
            while(bytes.Count<16384){if(await stream.ReadAsync(one,timeout.Token)==0)return;bytes.Add(one[0]);if(bytes.Count>=4&&bytes[^4]==13&&bytes[^3]==10&&bytes[^2]==13&&bytes[^1]==10)break;}
            if(bytes.Count>=16384)return;
            var head=Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n");
            var request=head[0].Split(' ');if(request.Length!=3)return;
            var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(var line in head.Skip(1)){int colon=line.IndexOf(':');if(colon>0)headers[line[..colon]]=line[(colon+1)..].Trim();}
            headers.TryGetValue("Origin",out var origin);
            if(!OriginAllowed(origin)){await Reply(stream,403,new{error="Origin denied"},stop.Token);return;}
            if(headers.TryGetValue("Upgrade",out var upgrade)&&upgrade.Equals("websocket",StringComparison.OrdinalIgnoreCase)){
                if(!headers.TryGetValue("Sec-WebSocket-Key",out var key)||key.Length>100)return;
                var accept=Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: "+accept+"\r\n\r\n"),stop.Token);
                timeout.CancelAfter(Timeout.Infinite);
                using var socket=WebSocket.CreateFromStream(stream,true,null,TimeSpan.FromSeconds(1));await Socket(socket);return;
            }
            var uri=new Uri("http://127.0.0.1"+request[1]);var query=uri.Query.TrimStart('?').Split('&',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Split('=',2)).Where(x=>x.Length==2).ToDictionary(x=>x[0],x=>Uri.UnescapeDataString(x[1]));
            if(request[0]=="GET"&&uri.AbsolutePath=="/native/poll"){
                var app=query.GetValueOrDefault("app","");
                if(app.Length is <1 or >40||!app.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_')){await Reply(stream,400,new{error="Invalid app"},stop.Token);return;}
                if(!int.TryParse(query.GetValueOrDefault("pid"),out int pid)||pid<=0){await Reply(stream,400,new{error="Process ID required"},stop.Token);return;}
                var c=clients.Values.FirstOrDefault(c=>c.App==app&&c.Pid==pid&&c.Socket==null)??new Client{App=app,Pid=pid};
                clients[c.Id]=c;c.Seen=DateTime.UtcNow;
                var messages=new List<JsonObject>();lock(c.Gate)while(c.Out.TryDequeue(out var message))messages.Add(message);
                await Reply(stream,200,new{id=c.Id,commands=messages},stop.Token);return;
            }
            if(request[0]=="POST"&&uri.AbsolutePath=="/native/scene"){
                if(!int.TryParse(headers.GetValueOrDefault("Content-Length"),out int length)||length is <1 or >16384){await Reply(stream,400,new{error="Invalid length"},stop.Token);return;}
                var body=new byte[length];await stream.ReadExactlyAsync(body,timeout.Token);var obj=JsonNode.Parse(body)!.AsObject();
                if(!clients.TryGetValue(ConfigStore.S(obj,"id"),out var c)){await Reply(stream,404,new{error="Unknown connection"},stop.Token);return;}
                Receive(c,obj);await Reply(stream,200,new{ok=true},stop.Token);return;
            }
            await Reply(stream,200,new{app="Chamfer Trackball",version=BuildInfo.Version},stop.Token);
        }catch(Exception ex)when(ex is IOException or OperationCanceledException or SocketException or JsonException or ArgumentException or InvalidOperationException){ }
    }
    static async Task Reply(NetworkStream stream,int status,object obj,CancellationToken ct){
        var body=JsonSerializer.SerializeToUtf8Bytes(obj,json);var head=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+" Response\r\nContent-Type: application/json\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n");
        await stream.WriteAsync(head,ct);await stream.WriteAsync(body,ct);
    }
    async Task Socket(WebSocket socket){
        var c=new Client{Socket=socket};var buffer=new byte[16384];
        using var quit=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        var sender=Task.Run(async()=>{
            while(!quit.IsCancellationRequested&&socket.State==WebSocketState.Open){
                bool signaled=await c.OutSignal.WaitAsync(TimeSpan.FromSeconds(1),quit.Token);
                c.Seen=DateTime.UtcNow;
                if(!signaled)continue;
                while(socket.State==WebSocketState.Open){
                    JsonObject? item=null;lock(c.Gate){if(c.Out.TryDequeue(out var next))item=next;}
                    if(item==null)break;
                    var bytes=Encoding.UTF8.GetBytes(item.ToJsonString());await socket.SendAsync(bytes,WebSocketMessageType.Text,true,quit.Token);
                }
            }
        },quit.Token);
        try{
            while(socket.State==WebSocketState.Open){
                int count=0;WebSocketReceiveResult got;
                do{got=await socket.ReceiveAsync(new ArraySegment<byte>(buffer,count,buffer.Length-count),quit.Token);count+=got.Count;if(count>=buffer.Length&&!got.EndOfMessage)throw new IOException("Message too large.");}while(!got.EndOfMessage&&got.MessageType!=WebSocketMessageType.Close);
                if(got.MessageType==WebSocketMessageType.Close)break;
                var m=JsonNode.Parse(buffer.AsSpan(0,count))!.AsObject();
                if(ConfigStore.S(m,"t")=="hello"){
                    c.App=ConfigStore.S(m,"app");c.Version=ConfigStore.I(m,"v",1);c.Pid=ConfigStore.I(m,"pid");clients[c.Id]=c;
                    if(c.App=="chamfer3d"&&c.Version==1)Queue(c,new(){["t"]="ready",["v"]=2});
                }else Receive(c,m);
            }
        }catch(Exception ex)when(ex is WebSocketException or OperationCanceledException or IOException or JsonException or InvalidOperationException){ }
        finally{quit.Cancel();clients.TryRemove(c.Id,out _);try{await sender;}catch(OperationCanceledException){ }catch(WebSocketException){ }}
    }
    public void Dispose(){stop.Cancel();listener.Stop();foreach(var c in clients.Values)c.Socket?.Abort();stop.Dispose();}
}
