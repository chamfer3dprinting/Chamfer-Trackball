using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace Chamfer.Trackball.Core;

public readonly record struct TwistSample(float X,float Y,float Wheel,long Time){
 public bool Moving=>X!=0||Y!=0||Wheel!=0;
}
public sealed record TwistPattern(string Kind,float[] Values);
public sealed class TwistCalibrationModel{
 static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
 static readonly float[] Floors=[.04f,.04f,.08f,.12f,.06f,.04f];
 public int Version{get;set;}=1;
 public string Source{get;set;}="xy";
 public float AxisX{get;set;}=1;
 public float AxisY{get;set;}
 public int WheelSign{get;set;}=1;
 public float QuarterCounts{get;set;}=120;
 public float Separation{get;set;}
 public float[] Scales{get;set;}=[..Floors];
 public TwistPattern[] Patterns{get;set;}=[];
 public string Quality=>Source=="wheel"?"Scroll signal":Separation>=.65f?"Good separation":"Overlapping patterns";
 public void Validate(){
  if(Version!=1||Source is not "xy" and not "wheel"||!float.IsFinite(AxisX)||!float.IsFinite(AxisY)||Math.Abs(AxisX*AxisX+AxisY*AxisY-1)>.01f||WheelSign is not 1 and not -1
   ||!float.IsFinite(QuarterCounts)||QuarterCounts is <8 or >32000||!float.IsFinite(Separation)||Separation is <0 or >1||Scales==null||Scales.Length!=6||Scales.Any(x=>!float.IsFinite(x)||x<.01f||x>10)
   ||Patterns==null||Patterns.Length>32||Patterns.Any(p=>p==null||p.Kind is not "faces" and not "clockwise" and not "anticlockwise"||p.Values==null||p.Values.Length!=6||p.Values.Any(x=>!float.IsFinite(x)||Math.Abs(x)>20)))
   throw new InvalidDataException("Invalid twist calibration.");
  if(Source=="xy"&&(!Patterns.Any(p=>p.Kind=="faces")||!Patterns.Any(p=>p.Kind=="clockwise")||!Patterns.Any(p=>p.Kind=="anticlockwise")))throw new InvalidDataException("Record normal rolling and both twist directions.");
 }
 public JsonObject ToNode(){Validate();return JsonSerializer.SerializeToNode(this,Json)!.AsObject();}
 public static TwistCalibrationModel? FromNode(JsonNode? node){
  if(node==null)return null;
  try{var model=node.Deserialize<TwistCalibrationModel>(Json)??throw new InvalidDataException("Invalid twist calibration.");model.Validate();return model;}
  catch(Exception ex)when(ex is JsonException or NotSupportedException or InvalidOperationException or NullReferenceException){throw new InvalidDataException("Invalid twist calibration.",ex);}
 }
 public float Project(float x,float y,float wheel)=>Source=="wheel"?wheel*WheelSign:x*AxisX+y*AxisY;
 public float NudgeDelta(float x,float y,float wheel,float gain,bool reverse){
  float value=Project(x,y,wheel);if(!float.IsFinite(value)||!float.IsFinite(gain))throw new ArgumentException("Invalid calibrated motion.");
  // Calibration supplies axis/sign and intent. Recorded turn distance does
  // not force the user to repeat a full physical turn to trigger a cube snap.
  return Math.Clamp(value*(reverse?.2f:-.2f)*Math.Clamp(gain,0,4),-1000,1000);
 }
 public float CameraDelta(float x,float y,float wheel,float gain,bool reverse){
  float value=Project(x,y,wheel);if(!float.IsFinite(value)||!float.IsFinite(gain))throw new ArgumentException("Invalid calibrated motion.");
  return Math.Clamp(-value/QuarterCounts*KnobSteps.QuarterDelta*Math.Clamp(gain,0,4)*(reverse?-1:1),-1000,1000);
 }
 public static float[] Features(IReadOnlyList<TwistSample> samples,float quantum,bool nudge=false){
  double x=0,y=0,xx=0,yy=0,xy=0,travel=0;int moving=0;
  foreach(var s in samples){if(!float.IsFinite(s.X)||!float.IsFinite(s.Y)||!float.IsFinite(s.Wheel))throw new ArgumentException("Invalid calibration sample.");x+=s.X;y+=s.Y;xx+=s.X*s.X;yy+=s.Y*s.Y;xy+=s.X*s.Y;travel+=Math.Sqrt(s.X*s.X+s.Y*s.Y);if(s.X!=0||s.Y!=0)moving++;}
  if(travel<KnobSteps.NudgeCounts||moving<(nudge?1:2))return [];
  return [(float)(x/travel),(float)(y/travel),(float)((xx-yy)/Math.Max(xx+yy,.001)),(float)(xy/Math.Max(Math.Sqrt(xx*yy),1)),MathF.Log(1+(float)travel/Math.Max(quantum,8)*128/Math.Max(32,(float)(samples[^1].Time-samples[0].Time+16))),(float)(Math.Sqrt(x*x+y*y)/travel)];
 }
 public (bool Twist,int Direction,float Confidence) Decide(IReadOnlyList<TwistSample> samples)=>Decide(samples,false);
 // Short nudges preserve direction/shape, but their amount and speed differ
 // from the longer recorded training strokes. Do not reject them for that.
 public (bool Twist,int Direction,float Confidence) DecideNudge(IReadOnlyList<TwistSample> samples)=>Decide(samples,true);
 (bool Twist,int Direction,float Confidence) Decide(IReadOnlyList<TwistSample> samples,bool nudge){
  if(Source=="wheel"){float amount=samples.Sum(s=>s.Wheel)*WheelSign;return (amount!=0,Math.Sign(amount),amount==0?0:1);}
  var f=Features(samples,QuarterCounts,nudge);if(f.Length==0)return (false,0,0);
  double face=double.PositiveInfinity,twist=double.PositiveInfinity;int direction=0;
  foreach(var p in Patterns){double distance=0;for(int n=0;n<6;n++){if(nudge&&n==4)continue;double v=(f[n]-p.Values[n])/Scales[n];distance+=v*v;}
   if(p.Kind=="faces")face=Math.Min(face,distance);else if(distance<twist){twist=distance;direction=p.Kind=="clockwise"?1:-1;}
  }
  float confidence=(float)Math.Clamp((face-twist)/Math.Max(face+twist,.001),0,1);
  return (twist<36&&face-twist>1&&twist*1.35<face,direction,confidence);
 }
 static List<float[]> Windows(IReadOnlyList<TwistSample> samples,float quantum){
  var result=new List<float[]>();for(int first=0;first+8<=samples.Count;first+=4){var window=samples.Skip(first).Take(8).ToArray();var f=Features(window,quantum);if(f.Length>0)result.Add(f);}return result;
 }
 static float BurstQuantum(IReadOnlyList<TwistSample> samples,Func<TwistSample,float> project){
  var amounts=new List<float>();long prior=0;float value=0;
  foreach(var s in samples){if(!s.Moving)continue;if(prior!=0&&s.Time-prior>180){if(Math.Abs(value)>=8)amounts.Add(Math.Abs(value));value=0;}value+=project(s);prior=s.Time;}
  if(Math.Abs(value)>=8)amounts.Add(Math.Abs(value));if(amounts.Count==0)return 0;amounts.Sort();return amounts[amounts.Count/2];
 }
 public static TwistCalibrationModel Train(IReadOnlyList<TwistSample> faces,IReadOnlyList<TwistSample> clockwise,IReadOnlyList<TwistSample> anticlockwise){
  if(faces.Count>512||clockwise.Count>512||anticlockwise.Count>512)throw new InvalidDataException("Calibration recording is too long.");
  if(faces.Count(s=>s.Moving)<8||clockwise.Count(s=>s.Moving)<1||anticlockwise.Count(s=>s.Moving)<1)throw new InvalidDataException("Not enough movement. Record normal rolls and both knob directions.");
  foreach(var s in faces.Concat(clockwise).Concat(anticlockwise))if(!float.IsFinite(s.X)||!float.IsFinite(s.Y)||!float.IsFinite(s.Wheel)||Math.Abs(s.X)>16000||Math.Abs(s.Y)>16000||Math.Abs(s.Wheel)>16000)throw new InvalidDataException("Invalid calibration sample.");
  float cwWheel=clockwise.Sum(s=>s.Wheel),ccwWheel=anticlockwise.Sum(s=>s.Wheel);
  if(cwWheel*ccwWheel<0&&Math.Abs(cwWheel)>=60&&Math.Abs(ccwWheel)>=60&&faces.Sum(s=>Math.Abs(s.Wheel))<30){
   var model=new TwistCalibrationModel{Source="wheel",WheelSign=Math.Sign(cwWheel),Separation=1};
   model.QuarterCounts=Math.Clamp((BurstQuantum(clockwise,s=>s.Wheel)+BurstQuantum(anticlockwise,s=>s.Wheel))/2,8,32000);model.Validate();return model;
  }
  if(clockwise.Count(s=>s.Moving)<8||anticlockwise.Count(s=>s.Moving)<8)throw new InvalidDataException("Record a little more ball movement in both twist directions.");
  var cw=new Vector2(clockwise.Sum(s=>s.X),clockwise.Sum(s=>s.Y));var ccw=new Vector2(anticlockwise.Sum(s=>s.X),anticlockwise.Sum(s=>s.Y));
  if(cw.Length()<16||ccw.Length()<16||Vector2.Dot(Vector2.Normalize(cw),Vector2.Normalize(ccw))>-.2f)throw new InvalidDataException("The twist directions were not distinct. Make separate clockwise and anticlockwise turns; lift your fingers to reposition.");
  var axis=Vector2.Normalize(Vector2.Normalize(cw)-Vector2.Normalize(ccw));
  var trained=new TwistCalibrationModel{AxisX=axis.X,AxisY=axis.Y};
  trained.QuarterCounts=Math.Clamp((BurstQuantum(clockwise,s=>s.X*axis.X+s.Y*axis.Y)+BurstQuantum(anticlockwise,s=>s.X*axis.X+s.Y*axis.Y))/2,8,32000);
  var sets=new[]{("faces",Windows(faces,trained.QuarterCounts)),("clockwise",Windows(clockwise,trained.QuarterCounts)),("anticlockwise",Windows(anticlockwise,trained.QuarterCounts))};
  if(sets.Any(set=>set.Item2.Count<2))throw new InvalidDataException("Record a little more movement in each sample.");
  var patterns=new List<TwistPattern>();
  foreach(var (kind,values) in sets){
   var groups=values.GroupBy(f=>((int)MathF.Floor((MathF.Atan2(f[1],f[0])+MathF.PI)/(MathF.PI/4)))%8);
   foreach(var group in groups){var median=new float[6];for(int n=0;n<6;n++){var valuesForFeature=group.Select(f=>f[n]).Order().ToArray();median[n]=valuesForFeature[valuesForFeature.Length/2];}patterns.Add(new(kind,median));}
  }
  trained.Patterns=patterns.ToArray();
  // Report the weakest labelled class in these samples, not a general accuracy rate.
  float weakest=1;
  foreach(var (kind,values) in sets){
   int good=0;
   foreach(var f in values){
    double face=double.PositiveInfinity,twist=double.PositiveInfinity;
    foreach(var p in trained.Patterns){double distance=0;for(int n=0;n<6;n++){double v=(f[n]-p.Values[n])/trained.Scales[n];distance+=v*v;}if(p.Kind=="faces")face=Math.Min(face,distance);else twist=Math.Min(twist,distance);}
    bool predicted=twist<36&&face-twist>1&&twist*1.35<face;if(predicted==(kind!="faces"))good++;
   }
   weakest=Math.Min(weakest,(float)good/Math.Max(1,values.Count));
  }
  trained.Separation=weakest;trained.Validate();return trained;
 }
}
