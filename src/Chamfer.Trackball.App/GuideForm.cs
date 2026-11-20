using System.Drawing.Drawing2D;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
namespace Chamfer.Trackball.App;
internal sealed class GuideForm:Form{
 readonly PivotForm pivot=new();
 readonly Controller controller;readonly ConfigStore store;readonly System.Windows.Forms.Timer timer=new(){Interval=100};
 readonly Image logo=Brand.Image();
 bool compact,dragging,showGuide=true;
 Point drag;
 JsonObject state=new();
 string last="";
 long guideConfigRevision=-1,nextStateAt;
 float ballX,ballY,dialLift,turnAngle,turnRemaining;
 public GuideForm(Controller controller,ConfigStore store){
  this.controller=controller;this.store=store;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;
  DoubleBuffered=true;AutoScaleMode=AutoScaleMode.None;ClientSize=new(330,466);BackColor=Color.FromArgb(39,43,46);ForeColor=Color.FromArgb(220,225,230);
  Icon=Brand.Icon;Text="Chamfer Trackball guide";StartPosition=FormStartPosition.Manual;
  var area=Screen.PrimaryScreen!.WorkingArea;Location=new(area.Right-Width-18,area.Bottom-Height-18);
  var c=store.Snapshot();int x=ConfigStore.I(c,"guideX",Location.X),y=ConfigStore.I(c,"guideY",Location.Y);
  if(Screen.AllScreens.Any(s=>s.WorkingArea.IntersectsWith(new Rectangle(x,y,Width,Height))))Location=new(x,y);
  timer.Tick+=(_,_)=>UpdateGuide();
  timer.Start();
 }
 void UpdateGuide(){
  long now=Environment.TickCount64,revision=store.Revision;
  if(revision!=guideConfigRevision){
   showGuide=ConfigStore.B(store.Snapshot(),"modePanel",true);
   guideConfigRevision=revision;
  }
  bool changed=false;
  if(now>=nextStateAt||controller.Mode!=ConfigStore.S(state,"mode")){
   state=controller.GuideState();nextStateAt=now+100;
   if(showGuide){string current=state.ToJsonString();changed=current!=last;last=current;}
  }
  Win32.GetCursorPos(out var cursor);
  pivot.UpdateMarker(ConfigStore.B(state,"pivotMarker"),
    ConfigStore.B(state,"snapPivot")?new(ConfigStore.I(state,"pivotX"),ConfigStore.I(state,"pivotY")):new(cursor.X,cursor.Y));
  var motion=controller.ReadGuideMotion();
  if(!showGuide){if(Visible)Hide();timer.Interval=100;return;}
  if(!Visible)Show();
  bool animated=!compact&&UpdateMotion(motion);
  int interval=animated?25:100;
  if(timer.Interval!=interval)timer.Interval=interval;
  if(changed||animated)Invalidate();
 }

 bool UpdateMotion(MotionFrame motion){
  bool wasActive=ballX!=0||ballY!=0||dialLift!=0||turnRemaining!=0;
  static float Offset(float previous,float delta,float limit){
   float next=Math.Clamp(previous*.68f+delta,-limit,limit);
   return Math.Abs(next)<.3f?0:next;
  }
  ballX=Offset(ballX,motion.X*.22f,22);
  ballY=Offset(ballY,motion.Y*.22f,22);
  dialLift=Offset(dialLift,motion.Wheel*.2f,24);
  turnRemaining=Math.Clamp(turnRemaining+motion.Wheel*.75f,-180,180);
  float step=Math.Abs(turnRemaining)<.5f?turnRemaining:turnRemaining*.44f;
  turnAngle=(turnAngle+step)%360;
  turnRemaining-=step;
  return wasActive||motion.HasPrimary;
 }

 internal void VerifyMotionAnimation(){
  UpdateMotion(new(20,-20,120,0));
  float clockwise=turnAngle;
  if(ballX<=0||ballY>=0||dialLift<=0||clockwise<=0)
   throw new InvalidOperationException("The floating guide did not show ball movement and a right twist.");
  UpdateMotion(new(-60,60,-240,0));
  if(ballX>=0||ballY<=0||dialLift>=0||turnAngle>=clockwise)
   throw new InvalidOperationException("The floating guide did not animate the reverse direction.");
  ballX=ballY=dialLift=turnAngle=turnRemaining=0;
 }

 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ClientSize=new((int)(330*DeviceDpi/96f),(int)((compact?70:466)*DeviceDpi/96f));var a=Screen.FromPoint(Location).WorkingArea;Location=new(Math.Clamp(Left,a.Left,Math.Max(a.Left,a.Right-Width)),Math.Clamp(Top,a.Top,Math.Max(a.Top,a.Bottom-Height)));}
 protected override bool ShowWithoutActivation=>true;
 protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
 protected override void OnPaint(PaintEventArgs e){
  base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  float scale=DeviceDpi/96f;g.ScaleTransform(scale,scale);
  using var font=new Font("Segoe UI",9);using var bold=new Font("Segoe UI",10,FontStyle.Bold);using var tiny=new Font("Segoe UI",8);
  using var text=new SolidBrush(ForeColor);using var muted=new SolidBrush(Color.FromArgb(156,173,186));using var accent=new SolidBrush(Color.FromArgb(53,198,220));
  using var line=new Pen(Color.FromArgb(85,97,105),2);
  g.DrawImage(logo,new Rectangle(9,6,25,25));g.DrawString("Chamfer Trackball",bold,text,40,9);
  g.DrawString(compact?"+":"−",bold,text,282,7);g.DrawString("×",bold,text,309,7);
  if(compact){g.DrawString(controller.Mode=="Idle"?"Mouse":controller.Mode,bold,accent,12,40);return;}
  var s=state.Count==0?controller.GuideState():state;
  var states=s["buttons"]!.AsArray();
  string[] names=["middle","back","left","right"],defaults=["Middle key","Back","Left click","Right click"];
  for(int i=0;i<4;i++){
   float x=i%2==0?10:168,y=40+(i/2)*27;
   bool down=states[i]!.GetValue<bool>();var a=ConfigStore.S(s["bindings"]?[names[i]],"action","normal");
   using var pen=new Pen(down?Color.FromArgb(53,198,220):Color.FromArgb(140,146,151),1);
   g.DrawRectangle(pen,x,y,39,21);g.DrawString("btn"+(i+1),font,down?accent:text,x+4,y+2);
   string label=a=="normal"?defaults[i]:a switch{"keys"=>ConfigStore.S(s["bindings"]?[names[i]],"keys"),"faceSnap"=>"Snap face","panToggle"=>"Orbit / pan","lock3d"=>"3D on/off",_=>a};
   g.DrawString(label,tiny,text,new RectangleF(x+47,y+3,109,21));
  }
  var layer=ConfigStore.S(s,"navigationLayer");bool looking=layer=="Look",height=layer=="Height";var mode=ConfigStore.S(s,"mode");bool drone=mode=="Navigate"&&ConfigStore.B(s,"navigationActive");bool threeAxes=ConfigStore.B(s,"threeAxisControls");bool freeLook=drone&&!threeAxes&&ConfigStore.B(s,"freeLook");var title=mode switch{"Idle"=>"Mouse","PanZoom"=>"Pan + zoom","Navigate"=>looking?"Look around":height?"Raise / lower":drone&&threeAxes?"Camera walk":freeLook?"Look + travel":drone?(ConfigStore.B(s,"levelFlight")?"Level flight":"Drone navigation"):"Navigate","Snap"=>ConfigStore.S(s,"snapLabel","Snap faces"),"Roll"=>"Twist",_=>mode};
  var measure=g.MeasureString(title,bold);g.DrawString(title,bold,mode=="Idle"?text:accent,(330-measure.Width)/2,99);
  string program="Program: "+ConfigStore.S(s,"program");var pm=g.MeasureString(program,font);g.DrawString(program,font,muted,(330-pm.Width)/2,123);
  bool reverseTwist=ConfigStore.B(s,"reverseTwist");
  bool heldTwist=ConfigStore.B(s,"continuousTwist");
  var (ballAction,wheelAction,rotary,detail)=mode switch{
   "Orbit" when heldTwist=>("Orbit","Roll view",true,"Twist the other way to reverse the roll"),
   "Orbit"=>("Orbit","Roll view",true,"The pivot follows the cursor"),
   "PanZoom" when heldTwist=>("Pan","Zoom",false,"Twist the other way to reverse zoom"),
   "PanZoom"=>("Pan","Zoom",false,"Zoom follows the cursor"),
   "Zoom"=>("Zoom","Zoom",false,"Zoom follows the cursor"),
   "Snap"=>("Snap faces","Turn 90°",true,"The pivot follows the cursor"),
   "Roll"=>("Turn view","Turn view",true,"The pivot follows the cursor"),
   "Navigate" when looking=>("Look around","Height",false,reverseTwist?"Down climbs · up descends":"Up climbs · down descends"),
   "Navigate" when height=>("Change height","Turn",true,"Roll forward to climb · back to descend"),
   "Navigate" when threeAxes=>("Travel","Turn",true,reverseTwist?
    "Down turns right · up turns left":"Down turns left · up turns right"),
   "Navigate"=>(freeLook?"Travel + look":"Travel","Height",false,"Up climbs · down descends"),
   _=>("Point","Scroll",false,"")
  };
  DrawMotion(g,line,accent,text,rotary);
  bool twist=ConfigStore.B(s,"twistInput");
  using var centred=new StringFormat{Alignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter};
  g.DrawString("Ball · "+ballAction,font,text,new RectangleF(10,240,147,20),centred);
  g.DrawString((twist?"Twist":"Wheel")+" · "+wheelAction,font,text,new RectangleF(173,240,147,20),centred);
  g.DrawString(detail,tiny,muted,new RectangleF(10,272,310,20),centred);
  g.DrawLine(line,12,301,318,301);
  var keys=new HashSet<int>(s["keys"]!.AsArray().Select(key=>key!.GetValue<int>()));
  int shortcutRow=0;
  foreach(var item in s["shortcuts"]!.AsArray()){
   string chord=ConfigStore.S(item,"chord","none");if(chord is "" or "none")continue;
   bool held=ConfigStore.B(item,"held");try{held|=KeyChord.Parse(chord).Matches(keys);}catch(FormatException){}
   float y=307+shortcutRow++*16;string label=chord.Replace("win","Windows",StringComparison.OrdinalIgnoreCase).Replace("+"," + ");
   g.DrawString(label,tiny,held?accent:muted,new RectangleF(13,y,156,18));
   g.DrawString(ConfigStore.S(item,"label"),tiny,held?accent:text,180,y);
  }
  string footer=mode=="Idle"&&twist?"Ball twist scrolls up / down":heldTwist?(mode=="Orbit"?"Release the orbit key to stop rolling":"Release a pan/zoom key to stop zooming"):
   drone&&looking?"Opposite twist reverses · release look to stop":
   drone&&threeAxes&&!height?"Opposite twist reverses · release to stop turning":
   drone?"Roll back to centre or release to stop":ConfigStore.B(s,"knobActive")?"Turn: "+ConfigStore.D(s,"knobDegrees").ToString("0")+"° · next "+ConfigStore.D(s,"knobProgress").ToString("0")+"%":mode=="Snap"?ConfigStore.S(s["snapTurn"],"message","Roll up/down/left/right to switch faces"):"Physical keyboard · held keys light up";
  g.DrawString(footer,tiny,mode is "Snap" or "Roll"?accent:muted,new RectangleF(13,448,304,18));

 }
 void DrawMotion(Graphics g,Pen line,Brush accent,Brush text,bool rotary){
  var ball=new RectangleF(42,146,82,82);
  using var shade=new LinearGradientBrush(ball,Color.FromArgb(55,68,76),Color.FromArgb(12,16,18),70f);
  g.FillEllipse(shade,ball);g.DrawEllipse(line,ball);
  g.DrawEllipse(line,62,146,42,82);g.DrawEllipse(line,42,167,82,40);
  const float ballCenterX=83,centerY=187,dialCenterX=248;
  using var signal=new Pen(accent,2.5f){EndCap=LineCap.Round};
  if(ballX!=0||ballY!=0)g.DrawLine(signal,ballCenterX,centerY,ballCenterX+ballX,centerY+ballY);
  g.FillEllipse(ballX!=0||ballY!=0?accent:text,ballCenterX+ballX-4,centerY+ballY-4,8,8);

  var dial=new RectangleF(207,146,82,82);
  using var baseFill=new SolidBrush(Color.FromArgb(19,25,29));
  g.FillEllipse(baseFill,dial);g.DrawEllipse(line,dial);
  if(rotary){
   float sweep=dialLift<0?-68:68;
   if(dialLift!=0)g.DrawArc(signal,213,152,70,70,turnAngle-90-sweep,sweep);
   float radians=(turnAngle-90)*MathF.PI/180;
   float x=dialCenterX+35*MathF.Cos(radians),y=centerY+35*MathF.Sin(radians);
   g.FillEllipse(dialLift==0?text:accent,x-4,y-4,8,8);
  }else{
   g.DrawLine(line,dialCenterX,centerY-25,dialCenterX,centerY+25);
   if(dialLift!=0)g.DrawLine(signal,dialCenterX,centerY,dialCenterX,centerY-dialLift);
   g.FillEllipse(dialLift==0?text:accent,dialCenterX-4,centerY-dialLift-4,8,8);
  }
 }

 protected override void OnMouseDown(MouseEventArgs e){
  base.OnMouseDown(e);float scale=DeviceDpi/96f;float x=e.X/scale,y=e.Y/scale;
  if(y<34&&x>302){var c=store.Snapshot();c["modePanel"]=false;store.Save(c);Hide();return;}
  if(y<34&&x>275){compact=!compact;ClientSize=new((int)(330*scale),(int)((compact?70:466)*scale));Invalidate();return;}
  if(y<34){dragging=true;drag=e.Location;Capture=true;}
 }
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)Location=new(Left+e.X-drag.X,Top+e.Y-drag.Y);}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!dragging)return;dragging=false;Capture=false;var c=store.Snapshot();c["guideX"]=Left;c["guideY"]=Top;store.Save(c);}
 protected override void Dispose(bool disposing){if(disposing){timer.Dispose();pivot.Dispose();logo.Dispose();}base.Dispose(disposing);}
}
