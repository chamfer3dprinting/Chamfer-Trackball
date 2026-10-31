using System.Numerics;
namespace Chamfer.Trackball.Core;

// Parallel size bands recognize the same winding at different physical roll
// sizes. A single winner owns each gesture so overlapping bands cannot double-turn.
public sealed class FaceTurnGesture{
 static readonly float[] Sizes=[80,320,1280,5120];
 readonly CircleZoom[] bands=Sizes.Select(_=>new CircleZoom(true)).ToArray();
 int winner=-1,straightFrames;Vector2 straightAnchor;float straightTravel;
 public bool Active=>winner>=0&&bands[winner].Active;
 public bool Curving=>Active||bands.Any(band=>band.Curving);
 public float DetectedSize=>winner<0?0:Sizes[winner];
 public void Reset(){foreach(var band in bands)band.Reset();winner=-1;straightFrames=0;straightTravel=0;straightAnchor=Vector2.Zero;}
 public (bool Consumed,float Wheel) Add(float x,float y,long now){
  Span<bool> consumed=stackalloc bool[4];Span<float> wheel=stackalloc float[4];
  for(int index=0;index<bands.Length;index++){var value=bands[index].Add(x,y,now,Sizes[index]);consumed[index]=value.Consumed;wheel[index]=value.Wheel;}
  if(winner>=0){
   var delta=new Vector2(x,y);
   if(delta.LengthSquared()>0){
    var heading=Vector2.Normalize(delta);
    if(straightFrames==0||Vector2.Dot(heading,straightAnchor)<.9998f){straightAnchor=heading;straightFrames=1;straightTravel=delta.Length();}
    else{straightFrames++;straightTravel+=delta.Length();}
    if(straightFrames>=3&&straightTravel>=Math.Clamp(Sizes[winner]*.12f,18,64)){Reset();return (false,0);}
   }
   if(consumed[winner])return (true,wheel[winner]);
   Reset();return (false,0);
  }
  for(int index=0;index<bands.Length;index++)if(consumed[index]){winner=index;return (true,wheel[index]);}
  return (false,0);
 }
}
