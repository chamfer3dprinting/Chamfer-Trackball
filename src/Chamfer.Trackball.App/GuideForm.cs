using System.Drawing.Drawing2D;
using System.Text.Json.Nodes;
using Chamfer.Trackball.Core;
namespace Chamfer.Trackball.App;
internal sealed class GuideForm:Form{
 readonly Controller controller;readonly ConfigStore store;readonly System.Windows.Forms.Timer timer=new(){Interval=100};
 readonly Image logo=Brand.Image();bool compact,dragging;Point drag;string last="";long guideConfigRevision=-1;bool showGuide=true;
 public GuideForm(Controller controller,ConfigStore store){
  this.controller=controller;this.store=store;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;
  DoubleBuffered=true;AutoScaleMode=AutoScaleMode.None;ClientSize=new(330,430);BackColor=Color.FromArgb(39,43,46);ForeColor=Color.FromArgb(220,225,230);
  Icon=Brand.Icon;Text="Chamfer Trackball guide";StartPosition=FormStartPosition.Manual;
  var area=Screen.PrimaryScreen!.WorkingArea;Location=new(area.Right-Width-18,area.Bottom-Height-18);
  var c=store.Snapshot();int x=ConfigStore.I(c,"guideX",Location.X),y=ConfigStore.I(c,"guideY",Location.Y);
  if(Screen.AllScreens.Any(s=>s.WorkingArea.IntersectsWith(new Rectangle(x,y,Width,Height))))Location=new(x,y);
  timer.Tick+=(_,_)=>{var revision=store.Revision;if(revision!=guideConfigRevision){showGuide=ConfigStore.B(store.Snapshot(),"modePanel",true);guideConfigRevision=revision;}if(showGuide){if(!Visible)Show();var now=controller.GuideState().ToJsonString();if(now!=last){last=now;Invalidate();}}else if(Visible)Hide();};
  timer.Start();
 }
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ClientSize=new((int)(330*DeviceDpi/96f),(int)((compact?70:430)*DeviceDpi/96f));var a=Screen.FromPoint(Location).WorkingArea;Location=new(Math.Clamp(Left,a.Left,Math.Max(a.Left,a.Right-Width)),Math.Clamp(Top,a.Top,Math.Max(a.Top,a.Bottom-Height)));}
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
  var s=controller.GuideState();var states=s["buttons"]!.AsArray();
  string[] names=["middle","back","left","right"],defaults=["Middle key","Back","Left click","Right click"];
  for(int i=0;i<4;i++){
   float x=i%2==0?10:168,y=40+(i/2)*27;
   bool down=states[i]!.GetValue<bool>();var a=ConfigStore.S(s["bindings"]?[names[i]],"action","normal");
   using var pen=new Pen(down?Color.FromArgb(53,198,220):Color.FromArgb(140,146,151),1);
   g.DrawRectangle(pen,x,y,39,21);g.DrawString("btn"+(i+1),font,down?accent:text,x+4,y+2);
   string label=a=="normal"?defaults[i]:a switch{"keys"=>ConfigStore.S(s["bindings"]?[names[i]],"keys"),"panToggle"=>"Orbit / pan","lock3d"=>"3D on/off",_=>a};
   g.DrawString(label,tiny,text,new RectangleF(x+47,y+3,109,21));
  }
  var mode=ConfigStore.S(s,"mode");var title=mode switch{"Idle"=>"Mouse","PanZoom"=>"Pan + zoom","Navigate"=>"Navigate","Snap"=>ConfigStore.S(s,"snapLabel","Snap faces"),"Roll"=>"Twist",_=>mode};
  var measure=g.MeasureString(title,bold);g.DrawString(title,bold,mode=="Idle"?text:accent,(330-measure.Width)/2,99);
  string program="Program: "+ConfigStore.S(s,"program");var pm=g.MeasureString(program,font);g.DrawString(program,font,muted,(330-pm.Width)/2,123);
  using var ball=new SolidBrush(Color.FromArgb(12,16,18));
  g.FillEllipse(ball,61,167,42,42);g.FillEllipse(ball,219,167,42,42);
  using var arrow=new Pen(Color.FromArgb(105,118,123),3){StartCap=LineCap.ArrowAnchor,EndCap=LineCap.ArrowAnchor};
  g.DrawLine(arrow,41,188,122,188);g.DrawLine(arrow,81,151,81,225);g.DrawLine(arrow,135,151,135,225);g.DrawLine(arrow,200,213,281,213);
  foreach(int yy in new[]{218,227,236}){g.DrawLine(line,37-(yy-218)/2,yy,128+(yy-218)/2,yy);g.DrawLine(line,194-(yy-218)/2,yy,287+(yy-218)/2,yy);}
  string roll=mode switch{"Idle"=>"Pointer","Orbit"=>"Orbit","PanZoom"=>"Pan","Zoom"=>"Zoom","Snap"=>"Snap","Roll"=>"Turn face","Navigate"=>"Move",_=>mode};
  string wheel=mode=="Orbit"?"Roll":mode=="Idle"?"Scroll":mode=="Snap"?"Turn face":mode=="Navigate"?"Turn":"Zoom";
  g.DrawString(mode=="Snap"?"Ball: Faces   ·   Left wheel: 90°":"Roll: "+roll+"   ·   Wheel: "+wheel,font,text,mode=="Snap"?35:53,249);
  g.DrawString("Left wheel: Up / down",tiny,muted,13,278);g.DrawString("Right wheel: Horizontal",tiny,muted,177,278);
  g.DrawLine(line,12,301,318,301);
  var keys=new HashSet<int>(s["keys"]!.AsArray().Select(key=>key!.GetValue<int>()));
  int shortcutRow=0;
  foreach(var item in s["shortcuts"]!.AsArray()){
   string chord=ConfigStore.S(item,"chord","none");if(chord is "" or "none")continue;
   bool held=false;try{held=KeyChord.Parse(chord).Matches(keys);}catch(FormatException){}
   float y=307+shortcutRow++*18;string label=chord.Replace("win","Windows",StringComparison.OrdinalIgnoreCase).Replace("+"," + ");
   g.DrawString(label,tiny,held?accent:muted,new RectangleF(13,y,156,18));
   g.DrawString(ConfigStore.S(item,"label"),tiny,held?accent:text,180,y);
  }
  string footer=ConfigStore.B(s,"knobActive")?"Turn: "+ConfigStore.D(s,"knobDegrees").ToString("0")+"° · next "+ConfigStore.D(s,"knobProgress").ToString("0")+"%":mode=="Snap"?ConfigStore.S(s["snapTurn"],"message","Roll up/down/left/right to switch faces"):"Physical keyboard · held keys light up";
  g.DrawString(footer,tiny,mode is "Snap" or "Roll"?accent:muted,new RectangleF(13,412,304,18));

 }
 protected override void OnMouseDown(MouseEventArgs e){
  base.OnMouseDown(e);float scale=DeviceDpi/96f;float x=e.X/scale,y=e.Y/scale;
  if(y<34&&x>302){var c=store.Snapshot();c["modePanel"]=false;store.Save(c);Hide();return;}
  if(y<34&&x>275){compact=!compact;ClientSize=new((int)(330*scale),(int)((compact?70:430)*scale));Invalidate();return;}
  if(y<34){dragging=true;drag=e.Location;Capture=true;}
 }
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)Location=new(Left+e.X-drag.X,Top+e.Y-drag.Y);}
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(!dragging)return;dragging=false;Capture=false;var c=store.Snapshot();c["guideX"]=Left;c["guideY"]=Top;store.Save(c);}
 protected override void Dispose(bool disposing){if(disposing){timer.Dispose();logo.Dispose();}base.Dispose(disposing);}
}
