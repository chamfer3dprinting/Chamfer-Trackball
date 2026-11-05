namespace Chamfer.Trackball.Core;

public readonly record struct NavigationIntent(float TravelX,float TravelY,float LookX,float LookY,bool Started=false,bool BeganLook=false);
// Only coherent curved sweeps enter looking. A straight stroke, diagonal or
// reversal still travels. A short pause starts the next independent stroke.
// The small bounded queue lets high-rate curves be classified before travel.
public sealed class NavigationGestures {
    readonly Queue<(float X,float Y,double Time)> pending=new();
    double lastInput=double.NegativeInfinity,chunkX,chunkY,chunkLength,previousX,previousY,turn,totalTurn;
    int turns;bool haveDirection;
    public bool Looking{get;private set;}
    public bool Pending=>pending.Count!=0;
    public NavigationIntent Step(float dx,float dy,double milliseconds){
        if(!float.IsFinite(dx)||!float.IsFinite(dy)||!double.IsFinite(milliseconds))throw new ArgumentException("Invalid navigation gesture.");
        bool started=false,began=false;
        if(milliseconds-lastInput>=120){
            Looking=false;haveDirection=false;chunkX=chunkY=chunkLength=turn=totalTurn=0;turns=0;
        }
        if(dx!=0||dy!=0){
            started=double.IsNegativeInfinity(lastInput)||milliseconds-lastInput>=120;
            lastInput=milliseconds;pending.Enqueue((dx,dy,milliseconds));
            chunkX+=dx;chunkY+=dy;chunkLength+=Math.Sqrt(dx*dx+dy*dy);
            if(chunkLength>=4){
                double length=Math.Sqrt(chunkX*chunkX+chunkY*chunkY);
                if(length>=2){
                    double x=chunkX/length,y=chunkY/length;
                    if(haveDirection&&!Looking){
                        double angle=Math.Atan2(previousX*y-previousY*x,previousX*x+previousY*y);
                        if(Math.Abs(angle)>Math.PI*.56){turn=totalTurn=0;turns=0;} // reversing centres travel
                        else if(Math.Abs(angle)>.035){
                            turn+=angle;totalTurn+=Math.Abs(angle);turns++;
                            if(turns>=2&&Math.Abs(turn)>=.9&&Math.Abs(turn)/totalTurn>=.85){Looking=true;began=true;}
                            else if(totalTurn>Math.PI||Math.Abs(turn)/totalTurn<.65){turn=totalTurn=0;turns=0;}
                        }
                    }
                    previousX=x;previousY=y;haveDirection=true;
                }
                chunkX=chunkY=chunkLength=0;
            }
        }
        float xMove=0,yMove=0,xLook=0,yLook=0;
        while(pending.Count>0&&(Looking||pending.Count>64||milliseconds-pending.Peek().Time>=32)){
            var point=pending.Dequeue();
            if(Looking){xLook+=point.X;yLook+=point.Y;}else{xMove+=point.X;yMove+=point.Y;}
        }
        return new(xMove,yMove,xLook,yLook,started,began);
    }
    public void Reset(){pending.Clear();lastInput=double.NegativeInfinity;chunkX=chunkY=chunkLength=previousX=previousY=turn=totalTurn=0;turns=0;haveDirection=Looking=false;}
}
