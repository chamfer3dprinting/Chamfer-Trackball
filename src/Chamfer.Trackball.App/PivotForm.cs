using System.Drawing.Drawing2D;
namespace Chamfer.Trackball.App;
// A tiny click-through marker at the native orbit cursor. Camera/pivot math
// stays in the host; no sensor callback waits on a repaint or window move.
internal sealed class PivotForm:Form {
    public PivotForm(){
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;
        StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;
        BackColor=TransparencyKey=Color.Magenta;DoubleBuffered=true;
        ClientSize=new(18,18);Text="Chamfer cursor pivot";
    }
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80|0x20;return p;}}
    internal void UpdateMarker(bool active,Point point){
        if(!active){if(Visible)Hide();return;}
        int size=Math.Max(18,(int)Math.Round(18*DeviceDpi/96f));
        var bounds=new Rectangle(point.X-size/2,point.Y-size/2,size,size);
        if(Bounds!=bounds)Bounds=bounds;
        if(!Visible)Show();
    }
    protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=-1;return;}base.WndProc(ref m);}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        float s=ClientSize.Width/18f;e.Graphics.ScaleTransform(s,s);
        using var border=new Pen(Color.FromArgb(25,35,40),3);
        using var line=new Pen(Color.FromArgb(53,198,220),1.5f);
        e.Graphics.DrawEllipse(border,4,4,10,10);e.Graphics.DrawEllipse(line,4,4,10,10);
        using var dot=new SolidBrush(Color.FromArgb(53,198,220));e.Graphics.FillEllipse(dot,7,7,4,4);
    }
}
