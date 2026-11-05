using System.Numerics;
using Chamfer.Trackball.Core;
public static class GestureMotionChecks {
    public static void Run(Action<bool,string> check){
        foreach(var p in new[]{(12f,0f),(-12f,0f),(0f,-12f),(0f,12f)}){
            var n=new NavigationMotion();NavigationFrame f=default;
            for(int i=0;i<40;i++)f=n.Step(i==0?p.Item1:0,i==0?p.Item2:0,0,1000+i*8,gestureLook:true);
            check(!n.Looking&&f.Pitch==0&&f.Yaw==0,"Straight cardinal push travels without automatically looking");
            check(p.Item1!=0?Math.Sign(f.Right)==Math.Sign(p.Item1):Math.Sign(f.Forward)==-Math.Sign(p.Item2),"Forward/back and sideways travel retain their separate directions");
        }
        foreach(float angle in new[]{0f,MathF.PI/2,MathF.PI,MathF.PI*1.5f})foreach(float sign in new[]{-1f,1f}){
            var n=new NavigationMotion();float yaw=0,pitch=0;bool looked=false;NavigationFrame f=default;
            for(int i=0;i<6;i++){
                var a=angle+sign*i*MathF.PI/8;f=n.Step(5*MathF.Cos(a),5*MathF.Sin(a),0,1000+i*8,gestureLook:true);yaw+=f.Yaw;pitch+=f.Pitch;looked|=n.Looking;
                check(f.Right==0&&f.Forward==0,"A deliberate early curve does not latch a travel command");
            }
            check(looked&&(yaw!=0||pitch!=0),"Coherent curved sweep looks in either direction and orientation");
            for(int i=6;i<30;i++)f=n.Step(0,0,0,1000+i*8,gestureLook:true);
            check(!n.Looking&&n.Centered&&!f.Active,"Pausing ends looking without continued camera drift");
            for(int i=30;i<40;i++)f=n.Step(i==30?12:0,0,0,1000+i*8,gestureLook:true);
            check(f.Right>0&&f.Yaw==0&&f.Pitch==0,"A straight stroke after the pause returns to sideways travel");
        }
        var quantized=new NavigationGestures();bool falseLook=false;
        for(int i=0;i<64;i++){quantized.Step(i%2==0?1:0,i%2==0?0:1,1000+i*8);falseLook|=quantized.Looking;}
        check(!falseLook,"Quantized straight diagonal samples are not mistaken for a curved look sweep");
        foreach(float sign in new[]{-1f,1f}){
            var reverse=new NavigationGestures();for(int i=0;i<8;i++)reverse.Step(i<4?sign*6:-sign*6,0,1000+i*8);
            check(!reverse.Looking,"Reversing a straight stroke remains travel and can return to centre");
        }
        var elbow=new NavigationGestures();for(int i=0;i<8;i++)elbow.Step(i<4?0:6,i<4?-6:0,1000+i*8);
        check(!elbow.Looking,"One square change of travel direction does not activate a look gesture");
        var held=new NavigationMotion();NavigationFrame continued=default;
        for(int i=0;i<40;i++)continued=held.Step(0,i==0?-12:0,0,1000+i*8,gestureLook:true);
        check(continued.Forward>0&&continued.Pitch==0,"Straight forward cruising does not pitch the view");
        float baseline=continued.Forward;bool hadLook=false;
        for(int i=0;i<6;i++){var a=i*MathF.PI/8;continued=held.Step(5*MathF.Cos(a),5*MathF.Sin(a),0,1320+i*8,gestureLook:true);hadLook|=held.Looking;}
        check(hadLook&&continued.Forward>0&&Math.Abs(continued.Forward-baseline)<.0001f,"Looking preserves the independently latched forward travel speed");
        for(int i=6;i<30;i++)continued=held.Step(0,0,0,1320+i*8,gestureLook:true);
        for(int i=30;i<40;i++)continued=held.Step(0,i==30?12:0,0,1320+i*8,gestureLook:true);
        check(!continued.Active&&held.Centered,"Return to the travel centre still stops after a look sweep");
        held.Step(6,0,0,1700,gestureLook:true);check(!held.Step(0,0,0,1708,finish:true,gestureLook:true).Active&&held.Centered,"Release discards unclassified motion and stops immediately");
        bool invalid=false;try{new NavigationGestures().Step(float.NaN,0,1000);}catch(ArgumentException){invalid=true;}check(invalid,"Invalid gesture input cannot corrupt navigation");
    }
}