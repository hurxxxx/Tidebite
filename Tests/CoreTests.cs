using System;
using System.Diagnostics;
using Tidebite.Core;

static class CoreTests
{
    static int passed, failed;
    static void Main()
    {
        Run("Default bite telegraphs before damaging and closing frames cannot hit",()=>{
            var w=Empty();w.Config.BiteWindup=.09f;int a=Add(w,3,default),b=Add(w,1,new V2(0,1.3f));
            w.SetInput(a,new FishCommand(default,true));w.Step();Near(w.Fish[b].Health,100);
            w.Step();Near(w.Fish[b].Health,100);w.Step();Near(w.Fish[b].Health,100);w.Step();Check(w.Fish[b].Health<100);
            w.Fish[a].BiteRemaining=.01f;w.Fish[b].HurtProtection=0;Check(!w.TryConsume(a,b));
        });
        Run("A precisely timed burst dodges one attack and refunds cooldown",()=>{
            var w=Empty();int a=Add(w,3,default),b=Add(w,1,new V2(0,1.3f));
            w.Fish[a].BiteRemaining=.15f;w.Fish[b].DodgeRemaining=.1f;w.Fish[b].DashCooldown=2.5f;
            Check(w.TryConsume(a,b));Near(w.Fish[b].Health,100);Near(w.Fish[b].DashCooldown,1.75f);
            Check(w.Fish[b].Score==20&&w.Events[w.EventCount-1].Kind==WorldEventKind.Dodge);Check(!w.TryConsume(a,b));
        });
        Run("Consecutive consumptions award capped combo points and expire",()=>{
            var w=Empty();int a=Add(w,5,default);
            for(int n=0;n<2;n++){int b=Add(w,1,new V2(0,1.3f));w.Fish[b].Health=1;w.Fish[a].BiteRemaining=.15f;Check(w.TryConsume(a,b));}
            Check(w.Fish[a].Combo==2&&w.Fish[a].Score==225);
            for(int i=0;i<250;i++)w.Step();Check(w.Fish[a].Combo==0);
        });
        Run("Growing past a recent attacker awards revenge once",()=>{
            var w=Empty();int a=Add(w,3,default),b=Add(w,1,new V2(0,1.3f));w.Fish[a].BiteRemaining=.15f;Check(w.TryConsume(a,b));
            Check(w.Fish[b].RivalId==a&&w.Fish[b].RivalGeneration==w.Fish[a].Generation);
            w.Fish[b].Mass=6;w.Fish[b].Position=w.Fish[b].PreviousPosition=default;w.Fish[b].Heading=0;
            w.Fish[a].Position=w.Fish[a].PreviousPosition=new V2(0,1.3f);w.Fish[a].Health=1;w.Fish[b].BiteRemaining=.15f;
            Check(w.TryConsume(b,a));Check(w.Fish[b].Score==400&&w.Fish[b].RivalRemaining==0);
        });
        Run("A healthy fish survives the first bite without awarding growth", () => {
            var w=Empty();int a=Add(w,3,default),b=Add(w,1,new V2(0,1.3f));float mass=w.Fish[a].Mass;
            w.SetInput(a,new FishCommand(default,true));w.Step();
            Check(w.Fish[b].Alive&&w.Fish[b].Health>0&&w.Fish[b].Health<100);Near(w.Fish[a].Mass,mass);Check(w.Fish[a].Kills==0);
            Check(w.Fish[b].RegenDelay>4.9f&&w.Fish[b].HurtProtection>.5f);
        });
        Run("Even the largest predator cannot kill a healthy fish with two bites", () => {
            var c=new GameConfig();var a=new FishState{Mass=32};var b=new FishState{Mass=.3f};
            float damage=CombatRules.Damage(a,b,c);Check(damage*2<c.MaxHealth);Check(damage*3>=c.MaxHealth);
        });
        Run("Four separated bites consume once and respawn restores health", () => {
            var w=Empty();int a=Add(w,1.25f,default),b=Add(w,1,new V2(0,1.3f));
            for(int bite=0;bite<4;bite++)
            {
                w.Fish[a].BiteCooldown=0;w.Fish[b].HurtProtection=0;w.Fish[b].Position=w.Fish[b].PreviousPosition=new V2(0,1.3f);
                w.SetInput(a,new FishCommand(default,true));w.Step();if(bite<3)Check(w.Fish[b].Alive);
            }
            Check(!w.Fish[b].Alive&&w.Fish[a].Kills==1);Near(w.Fish[b].Health,0);
            w.Fish[b].RespawnRemaining=0;Check(w.Respawn(b));Near(w.Fish[b].Health,w.Config.MaxHealth);
        });
        Run("One bite window cannot apply damage every simulation tick", () => {
            var w=Empty();int a=Add(w,3,default),b=Add(w,1,new V2(0,1.3f));
            w.SetInput(a,new FishCommand(default,true));w.Step();float hp=w.Fish[b].Health;
            for(int i=0;i<8;i++)w.Step();Near(w.Fish[b].Health,hp);Check(w.Fish[a].BiteRemaining==0);
        });
        Run("Hit protection prevents simultaneous predators from bursting prey", () => {
            var w=Empty();int a=Add(w,3,new V2(-.1f,0)),b=Add(w,3,new V2(.1f,0)),p=Add(w,1,new V2(0,1.3f));
            w.SetInput(a,new FishCommand(default,true));w.SetInput(b,new FishCommand(default,true));w.Step();
            Near(w.Fish[p].Health,100-CombatRules.Damage(w.Fish[a],w.Fish[p],w.Config));Check(w.Fish[p].Alive);
            w.Fish[a].BiteRemaining=.3f;Check(!w.TryConsume(a,p));
        });
        Run("Health recovery is delayed and food heals immediately with a cap", () => {
            var w=Empty();int a=Add(w,1,default);w.Fish[a].Health=40;w.Fish[a].RegenDelay=5;
            for(int i=0;i<60;i++)w.Step();Near(w.Fish[a].Health,40);
            w.Food[0]=new FoodState{Position=default};w.Step();Near(w.Fish[a].Health,44);
            w.Fish[a].RegenDelay=0;for(int i=0;i<600;i++)w.Step();Near(w.Fish[a].Health,100);
        });
        Run("Wounded bots seek healing food without an immediate threat", () => {
            var w=Empty();int a=Add(w,3,default,true);Add(w,.6f,new V2(0,5));w.Fish[a].Health=25;
            w.Food[0]=new FoodState{Position=new V2(4,0)};w.Grid.Rebuild(w.Fish);w.Brains[a].Decide(w,a);
            Check(w.Brains[a].Mode==BotMode.Forage&&w.Brains[a].FoodTargetId==0);
        });
        Run("Expanded world covers the map with food and varied species", () => {
            var c=new GameConfig();var w=new World(c);w.FillPopulation();int alternate=0;bool far=false,apex=false;
            Check(c.WorldHalfSize>=72&&w.Food.Length==1024&&w.Population==48);
            foreach(var f in w.Fish)if(f.Occupied){Near(f.Health,c.MaxHealth);Check(f.Species<2);alternate+=f.Species;apex|=GrowthCurve.Form(f.Mass,c)>=4;}
            foreach(var f in w.Food){Check(Math.Abs(f.Position.X)<c.WorldHalfSize&&Math.Abs(f.Position.Y)<c.WorldHalfSize);far|=Math.Abs(f.Position.X)>60;}
            Check(alternate>0&&alternate<48&&far&&apex);
        });
        Run("Bite animation has anticipation, open hold and a fully closed recovery", () => {
            Near(FishJaw.Gape(0),0);Near(FishJaw.Gape(.10f),0);Near(FishJaw.Gape(.4f),1);Near(FishJaw.Gape(.9f),0);Near(FishJaw.Gape(1),0);
            for(int i=0;i<=100;i++){float gape=FishJaw.Gape(i/100f);Check(gape>=0&&gape<=1);}
        });
        Run("Lower jaw pivots at the rear lip and opens downward without moving the hinge", () => {
            var hinge=new V2(.75f,.44f);var tip=new V2(.96f,.46f);
            var rotated=FishJaw.Rotate(tip,hinge,1);Check(rotated.Y<hinge.Y);Near(V2.Distance(tip,hinge),V2.Distance(rotated,hinge));
            for(int i=0;i<=10;i++){var fixedPoint=FishJaw.Rotate(hinge,hinge,i/10f);Near(fixedPoint.X,hinge.X);Near(fixedPoint.Y,hinge.Y);}
        });
        Run("Closing the mandible exactly restores the original sprite seam", () => {
            var hinge=new V2(.72f,.43f);
            for(int i=0;i<20;i++)
            {
                var p=new V2(.72f+i*.012f,.43f+i*.003f);var closed=FishJaw.Rotate(p,hinge,0);
                Near(closed.X,p.X);Near(closed.Y,p.Y);
            }
        });
        Run("Growth thresholds expose all 30 levels without gaps", () => {
            var c=new GameConfig();float previous=0;
            for(int level=1;level<=GrowthCurve.LevelCount;level++)
            {
                float mass=GrowthCurve.Threshold(level,c);Check(mass>previous);
                Check(GrowthCurve.Level(mass,c)==level);
                if(level>1)Check(GrowthCurve.Level(mass-.00001f,c)==level-1);
                previous=mass;
            }
            Near(previous,c.MaxMass);Check(GrowthCurve.Level(.3f,c)==1);Check(GrowthCurve.Level(100,c)==30);
        });
        Run("Six growth forms unlock at their actual mass thresholds", () => {
            var c=new GameConfig();
            for(int form=0;form<6;form++)
            {
                float start=GrowthCurve.Threshold(form*5+1,c);
                Check(GrowthCurve.Form(start,c)==form);Near(GrowthCurve.FormProgress(start,c),0);
                Check(GrowthCurve.Form(GrowthCurve.Threshold(form*5+5,c),c)==form);
            }
        });
        Run("Growth progress is continuous inside each level and caps at 30", () => {
            var c=new GameConfig{BaseMass=2,MaxMass=80};
            for(int level=1;level<30;level++)
            {
                float start=GrowthCurve.Threshold(level,c),end=GrowthCurve.Threshold(level+1,c);
                Near(GrowthCurve.Progress((start+end)*.5f,c),.5f);
            }
            Near(GrowthCurve.Progress(0,c),0);Near(GrowthCurve.Progress(100,c),1);
        });
        Run("Food and consumption cross evolution thresholds without separate power rules", () => {
            var w=Empty();float boundary=GrowthCurve.Threshold(6,w.Config);
            int a=Add(w,boundary-.04f,default);w.Food[0]=new FoodState{Position=default};w.Step();
            Check(GrowthCurve.Form(w.Fish[a].Mass,w.Config)==1);
            int b=Add(w,.5f,new V2(0,1.3f));w.Fish[b].Health=1;w.SetInput(a,new FishCommand(default,true));w.Step();
            Check(!w.Fish[b].Alive);Check(w.Fish[a].Mass>boundary);
            Check(GrowthCurve.Form(w.Fish[a].Mass,w.Config)==1);
        });
        Run("Earned growth persists while idle and resets on a new life", () => {
            var c=new GameConfig();var f=new FishState{Occupied=true,Alive=true,Mass=20};
            for(int i=0;i<900;i++)FishMotor.Step(ref f,default,c,c.TickDelta);
            Near(f.Mass,20);Check(GrowthCurve.Form(f.Mass,c)==5);
            var w=Empty();int a=Add(w,20,default);w.Fish[a].Alive=false;w.Fish[a].RespawnRemaining=0;
            Check(w.Respawn(a));Check(GrowthCurve.Level(w.Fish[a].Mass,w.Config)==1);
        });
        Run("Render pose advances evenly across 30 Hz tick boundaries", () => {
            var previous=new FishState{Occupied=true,Alive=true,Generation=1,Position=new V2(0,0),Mass=1};
            var current=previous;current.Position=new V2(1,0);current.Mass=2;
            float last=0;
            for(int frame=0;frame<=8;frame++)
            {
                int tick=frame/4;float alpha=(frame%4)/4f;
                previous.Position=new V2(tick,0);current.Position=new V2(tick+1,0);
                var pose=FishPresentation.Interpolate(previous,current,alpha);
                Near(pose.Position.X,frame*.25f);Check(pose.Position.X>=last);Near(pose.Mass,2);last=pose.Position.X;
            }
        });
        Run("Render heading takes the short path across the angle seam", () => {
            var a=new FishState{Occupied=true,Alive=true,Generation=1,Heading=Math2.Pi-.1f};
            var b=a;b.Heading=-Math2.Pi+.1f;
            var mid=FishPresentation.Interpolate(a,b,.5f);Near(Math.Abs(mid.Heading),Math2.Pi);
        });
        Run("Respawn pose never sweeps across the map", () => {
            var a=new FishState{Occupied=true,Alive=true,Generation=1,Position=new V2(-25,-25)};
            var b=a;b.Generation=2;b.Position=new V2(25,25);
            var pose=FishPresentation.Interpolate(a,b,.1f);Near(pose.Position.X,25);Near(pose.Position.Y,25);
        });
        Run("Mass ratio boundary and equal fish", () => {
            var w = Empty(); var a = Add(w, 1.25f, default); var b = Add(w, 1, new V2(0,1));
            Check(PredationRules.CanConsume(w.Fish[a], w.Fish[b], w.Config));
            w.Fish[a].Mass = 1.249f; Check(!PredationRules.CanConsume(w.Fish[a], w.Fish[b], w.Config));
            w.Fish[a].Mass = 1; Check(!PredationRules.CanConsume(w.Fish[a], w.Fish[b], w.Config));
        });
        Run("Protection and self-consumption", () => {
            var w = Empty(); int a = Add(w, 3, default), b = Add(w, 1, new V2(0,1));
            w.Fish[b].Protection = 1;
            Check(!PredationRules.CanConsume(w.Fish[a], w.Fish[b], w.Config));
            Check(!PredationRules.CanConsume(w.Fish[a], w.Fish[a], w.Config));
        });
        Run("Attack cancels spawn protection", () => {
            var w = Empty(); int a = Add(w, 2, default); w.Fish[a].Protection = 2;
            w.SetInput(a, new FishCommand(default, true)); w.Step(); Check(w.Fish[a].Protection == 0);
        });
        Run("Mouth hits in front, not behind", () => {
            var w = Empty(); int a = Add(w, 2, default), b = Add(w, 1, new V2(0,1.3f));
            Check(PredationRules.MouthHits(w.Fish[a], w.Fish[b], w.Config));
            w.Fish[b].Position = w.Fish[b].PreviousPosition = new V2(0,-1.3f);
            Check(!PredationRules.MouthHits(w.Fish[a], w.Fish[b], w.Config));
        });
        Run("Swept mouth catches fast crossing target", () => {
            var w = Empty(); int a = Add(w, 2, new V2(0,1)), b = Add(w, 1, new V2(0,1.8f));
            w.Fish[a].PreviousPosition = new V2(0,-2);
            Check(PredationRules.MouthHits(w.Fish[a], w.Fish[b], w.Config));
        });
        Run("Single kill and no duplicate growth", () => {
            var w = Empty(); int a = Add(w, 2, default), b = Add(w, 1, new V2(0,1.3f));
            w.Fish[b].Health=1;w.SetInput(a, new FishCommand(default,true)); w.Step();
            Check(!w.Fish[b].Alive && w.Fish[a].Kills == 1);
            float mass = w.Fish[a].Mass; Check(!w.TryConsume(a,b)); w.Step(); Near(w.Fish[a].Mass,mass);
        });
        Run("One target per bite", () => {
            var w = Empty(); int a = Add(w, 3, default), b = Add(w, 1, new V2(-.1f,1.5f)), c = Add(w, 1, new V2(.1f,1.5f));
            w.Fish[b].Health=1;w.Fish[c].Health=1;w.SetInput(a, new FishCommand(default,true)); w.Step(); Check(w.Fish[a].Kills == 1);
            Check(w.Fish[b].Alive != w.Fish[c].Alive);
        });
        Run("Simultaneous hunters cannot double-consume", () => {
            var w = Empty(); int a = Add(w,2,new V2(-.1f,0)), b = Add(w,2,new V2(.1f,0)), p = Add(w,1,new V2(0,1.3f));
            w.Fish[p].Health=1;w.SetInput(a,new FishCommand(default,true)); w.SetInput(b,new FishCommand(default,true)); w.Step();
            Check(!w.Fish[p].Alive); Check(w.Fish[a].Kills + w.Fish[b].Kills == 1);
        });
        Run("Respawn waits and resets growth", () => {
            var w = Empty(); int a = Add(w,3,default), b = Add(w,1,new V2(0,1.5f));
            w.Fish[b].Health=1;w.SetInput(a,new FishCommand(default,true)); w.Step(); Check(!w.Respawn(b));
            for(int i=0;i<65;i++) w.Step(); Check(w.Respawn(b));
            Check(w.Fish[b].Alive && w.Fish[b].Protection > 0); Near(w.Fish[b].Mass,1);
        });
        Run("Cooldown prevents attack spam", () => {
            var w = Empty(); int a=Add(w,2,default); w.SetInput(a,new FishCommand(default,true,true)); w.Step();
            for(int i=0;i<10;i++) { w.SetInput(a,new FishCommand(default,true,true)); w.Step(); }
            Check(w.Fish[a].DashRemaining == 0 && w.Fish[a].DashCooldown > 2);
            Check(w.Fish[a].BiteRemaining == 0 && w.Fish[a].BiteCooldown > .05f);
        });
        Run("Invalid movement is sanitised", () => {
            var w=Empty(); int a=Add(w,1,default); w.SetInput(a,new FishCommand(new V2(float.NaN,float.PositiveInfinity))); w.Step();
            Check(w.Fish[a].Position.IsFinite); Near(w.Fish[a].Position.Magnitude,0);
        });
        Run("Dash and movement stay in bounds", () => {
            var w=Empty(); int a=Add(w,1,new V2(29,29));
            for(int i=0;i<300;i++) { w.SetInput(a,new FishCommand(new V2(1,1),false,true)); w.Step(); }
            Check(Math.Abs(w.Fish[a].Position.X) <= w.Config.WorldHalfSize-w.Fish[a].Radius+.001f);
            Check(Math.Abs(w.Fish[a].Position.Y) <= w.Config.WorldHalfSize-w.Fish[a].Radius+.001f);
        });
        Run("Food grows fish and regenerates", () => {
            var w=Empty(); int a=Add(w,1,default); w.Food[0]=new FoodState {Position=default}; w.Step();
            Check(w.Fish[a].Mass>1 && !w.Food[0].Active); w.Fish[a].Position=new V2(20,20);
            for(int i=0;i<280;i++)w.Step(); Check(w.Food[0].Active);
        });
        Run("Food broad phase preserves swept pickup and rejects diagonal near misses", () => {
            var w=new World(new GameConfig{Population=1,FoodCount=1,BaseSpeed=120,Acceleration=10000});
            int a=Add(w,1,default);w.Food[0]=new FoodState{Position=new V2(0,2)};
            w.SetInput(a,new FishCommand(new V2(0,1)));w.Step();
            Check(!w.Food[0].Active&&w.Fish[a].Position.Y>3.9f);
            var diagonal=new World(new GameConfig{Population=1,FoodCount=1,BaseSpeed=120,Acceleration=10000});
            int b=Add(diagonal,1,default);diagonal.Fish[b].Heading=Math2.Pi/4;
            diagonal.Food[0]=new FoodState{Position=new V2(2,0)};
            diagonal.SetInput(b,new FishCommand(new V2(1,1)));diagonal.Step();
            Check(diagonal.Food[0].Active);Near(diagonal.Fish[b].Mass,1);
        });
        Run("AI hunts smaller fish", () => {
            var w=Empty(); int a=Add(w,2,default,true); Add(w,.7f,new V2(0,5)); w.Grid.Rebuild(w.Fish);
            var input=w.Brains[a].Decide(w,a); Check(w.Brains[a].Mode==BotMode.Hunt); Check(input.Move.Y>.8f);
        });
        Run("AI flees predators", () => {
            var w=Empty(); int a=Add(w,1,default,true); Add(w,3,new V2(0,3)); w.Grid.Rebuild(w.Fish);
            var input=w.Brains[a].Decide(w,a); Check(w.Brains[a].Mode==BotMode.Evade); Check(input.Move.Normalized.Y<-.8f);
        });
        Run("Survival overrides an edible target", () => {
            var w=Empty(); int a=Add(w,2,default,true); Add(w,.6f,new V2(0,2)); Add(w,4,new V2(0,5)); w.Grid.Rebuild(w.Fish);
            w.Brains[a].Decide(w,a); Check(w.Brains[a].Mode==BotMode.Evade);
        });
        Run("Growth reverses hunt into escape", () => {
            var w=Empty(); int a=Add(w,2,default,true), b=Add(w,.6f,new V2(0,4)); w.Grid.Rebuild(w.Fish);
            w.Brains[a].Decide(w,a); Check(w.Brains[a].Mode==BotMode.Hunt);
            w.Fish[b].Mass=4; for(int i=0;i<22;i++)w.Step(); Check(w.Brains[a].Mode==BotMode.Evade);
        });
        Run("AI ignores prey beyond perception", () => {
            var w=Empty(); int a=Add(w,2,default,true); Add(w,.6f,new V2(0,25)); w.Grid.Rebuild(w.Fish);
            w.Brains[a].Decide(w,a); Check(w.Brains[a].Mode==BotMode.Roam);
        });
        Run("AI consumes food and grows autonomously", () => {
            var w=Empty(); int a=Add(w,1,default,true); w.Food[0]=new FoodState { Position=new V2(0,2) };
            for(int i=0;i<60;i++)w.Step(); Check(w.Fish[a].Mass>1);
        });
        Run("AI re-aims between perception updates", () => {
            var w=Empty();int a=Add(w,2,default,true),b=Add(w,.6f,new V2(0,5));
            w.Grid.Rebuild(w.Fish);var first=w.Brains[a].Decide(w,a);Near(first.Move.X,0);
            w.Fish[b].Position=new V2(3,5);
            var next=w.Brains[a].Decide(w,a);Check(next.Move.X>.3f&&w.Brains[a].TargetId==b);
        });
        Run("AI intercepts a target moving across its path", () => {
            var w=Empty();int a=Add(w,2,default,true),b=Add(w,.6f,new V2(0,5));
            w.Fish[b].Velocity=new V2(2,0);w.Grid.Rebuild(w.Fish);
            var input=w.Brains[a].Decide(w,a);Check(input.Move.X>.15f&&input.Move.Y>0);
        });
        Run("AI takes a valid bite without a random missed attack", () => {
            for(int seed=1;seed<=20;seed++)
            {
                var w=Empty();int a=Add(w,2,default,true),b=Add(w,.6f,new V2(0,1.3f));
                w.Fish[b].Health=1;w.Brains[a]=new BotBrain((uint)seed);
                for(int tick=0;tick<10;tick++)w.Step();
                Check(w.Fish[a].Kills==1&&!w.Fish[b].Alive);
            }
        });
        Run("AI abandons a consumed pearl immediately", () => {
            var w=new World(new GameConfig{Population=1,FoodCount=2});
            int a=Add(w,1,default,true);
            w.Food[0]=new FoodState{Position=new V2(0,3)};w.Food[1]=new FoodState{Position=new V2(4,0)};
            w.Grid.Rebuild(w.Fish);w.Brains[a].Decide(w,a);Check(w.Brains[a].FoodTargetId==0);
            w.Food[0].RespawnRemaining=9;var input=w.Brains[a].Decide(w,a);
            Check(w.Brains[a].FoodTargetId==1&&input.Move.X>0);
        });
        Run("AI keeps a food target when nearly equal alternatives jitter", () => {
            var w=new World(new GameConfig{Population=1,FoodCount=2});int a=Add(w,1,default,true);
            w.Food[0]=new FoodState{Position=new V2(-.1f,4)};w.Food[1]=new FoodState{Position=new V2(.1f,4.1f)};
            w.Grid.Rebuild(w.Fish);w.Brains[a].Decide(w,a);Check(w.Brains[a].FoodTargetId==0);
            w.Fish[a].Position=new V2(.05f,.02f);
            for(int i=0;i<9;i++)w.Step();Check(w.Brains[a].FoodTargetId==0);
        });
        Run("AI prefers food away from a predator's corridor", () => {
            var w=new World(new GameConfig{Population=1,FoodCount=2});
            int a=Add(w,1,default,true);Add(w,3,new V2(8,0));
            w.Food[0]=new FoodState{Position=new V2(7,0)};w.Food[1]=new FoodState{Position=new V2(0,9)};
            w.Grid.Rebuild(w.Fish);w.Brains[a].Decide(w,a);
            Check(w.Brains[a].Mode==BotMode.Forage&&w.Brains[a].FoodTargetId==1);
        });
        Run("AI refuses prey sheltered by a larger predator", () => {
            var w=Empty();int a=Add(w,2,default,true);Add(w,.6f,new V2(7,0));Add(w,3,new V2(8,0));
            w.Food[0]=new FoodState{Position=new V2(0,4)};
            w.Grid.Rebuild(w.Fish);w.Brains[a].Decide(w,a);
            Check(w.Brains[a].Mode==BotMode.Forage&&w.Brains[a].TargetId<0);
        });
        Run("AI does not waste a burst pointing into the reef wall", () => {
            var w=Empty();float edge=w.Config.WorldHalfSize;
            int a=Add(w,1,new V2(0,edge-1),true);Add(w,3,new V2(0,edge-3));
            w.Grid.Rebuild(w.Fish);var input=w.Brains[a].Decide(w,a);Check(!input.Dash);
        });
        Run("AI stops an unwinnable chase when faster prey flees", () => {
            var w=Empty();int a=Add(w,2,default,true),b=Add(w,.6f,new V2(0,7));
            w.Fish[a].DashCooldown=2;w.Fish[b].Velocity=new V2(0,6);w.Grid.Rebuild(w.Fish);
            w.Brains[a].Decide(w,a);Check(w.Brains[a].Mode!=BotMode.Hunt);
        });
        Run("Corner escape chooses an interior route", () => {
            var w=Empty(); float edge=w.Config.WorldHalfSize;
            int a=Add(w,1,new V2(edge-2,edge-2),true); Add(w,3,new V2(edge-5,edge-5)); w.Grid.Rebuild(w.Fish);
            var input=w.Brains[a].Decide(w,a); Check(input.Move.X<0 || input.Move.Y<0);
        });
        Run("Lost predator retains short recovery memory", () => {
            var w=Empty(); int a=Add(w,1,default,true), b=Add(w,3,new V2(0,3)); w.Grid.Rebuild(w.Fish);
            w.Brains[a].Decide(w,a); w.RemoveFish(b);
            for(int i=0;i<22;i++)w.Step(); Check(w.Brains[a].Mode==BotMode.Recover);
            for(int i=0;i<35;i++)w.Step(); Check(w.Brains[a].Mode!=BotMode.Recover && w.Brains[a].Mode!=BotMode.Evade);
        });
        Run("Population fills without inventing human connections", () => {
            var w=new World(new GameConfig()); w.AddFish(false); w.FillPopulation(); Check(w.Population==48 && w.Humans==1);
            for(int i=1;i<GameConfig.FishCapacity;i++)w.AddFish(false);
            Check(w.Humans==64 && w.Population==64); Check(w.AddFish(false)==-1);
        });
        Run("Removed slot preserves generation across reuse", () => {
            var w=Empty(); int a=Add(w,1,default); int old=w.Fish[a].Generation; w.RemoveFish(a);
            int b=w.AddFish(false); Check(a==b && w.Fish[b].Generation>old);
        });
        Run("Starting predators respect the player's space across 100 seeds", () => {
            for(int seed=1;seed<=100;seed++)
            {
                var w=new World(new GameConfig{Seed=seed});int player=w.AddFish(false);w.FillPopulation();
                foreach(var f in w.Fish)if(f.Occupied&&f.IsBot&&f.Mass>=w.Config.ConsumeRatio)
                    Check(V2.Distance(f.Position,w.Fish[player].Position)>=14);
            }
        });
        Run("Small schools offer nearby prey without crowding human spawns", () => {
            for(int seed=1;seed<=100;seed++)
            {
                var w=new World(new GameConfig{Seed=seed});int player=w.AddFish(false);w.FillPopulation();int nearby=0;
                foreach(var f in w.Fish)if(f.Occupied&&f.IsBot&&f.Mass*w.Config.ConsumeRatio<=w.Fish[player].Mass)
                {
                    float d=V2.Distance(f.Position,w.Fish[player].Position);
                    Check(d>5);if(d<18)nearby++;
                }
                if(nearby<2)throw new Exception($"Seed {seed}: only {nearby} nearby prey");
                Near(w.Fish[player].Protection,8);
            }
        });
        Run("School replenishment never moves or resets existing actors", () => {
            var w=new World(new GameConfig());w.AddFish(false);w.FillPopulation();var before=(FishState[])w.Fish.Clone();
            w.AddFish(true,.6f);
            for(int i=0;i<before.Length;i++)if(before[i].Occupied)
            {
                Near(before[i].Position.X,w.Fish[i].Position.X);Near(before[i].Position.Y,w.Fish[i].Position.Y);
                Near(before[i].Health,w.Fish[i].Health);Check(before[i].Generation==w.Fish[i].Generation);
            }
        });
        Run("Food is available throughout the ocean and grows the player", () => {
            var w=new World(new GameConfig());int player=w.AddFish(false);
            for(int x=-28;x<=28;x+=4)for(int y=-28;y<=28;y+=4)
            {float nearest=float.MaxValue;foreach(var f in w.Food)nearest=Math.Min(nearest,V2.Distance(new V2(x,y),f.Position));Check(nearest<4);}
            float mass=w.Fish[player].Mass;w.Fish[player].Position=w.Fish[player].PreviousPosition=w.Food[0].Position;
            w.Step();Check(w.Fish[player].Mass>mass && w.Fish[player].Protection>0);
        });
        Run("Remote consumption cannot bypass mouth distance", () => {
            var w=Empty();int a=Add(w,3,default),b=Add(w,1,new V2(20,20));w.Fish[a].BiteRemaining=.1f;
            Check(!w.TryConsume(a,b));Check(w.Fish[b].Alive);
        });
        Run("Bite catches very close overlapping prey in front", () => {
            var w=Empty();int a=Add(w,2,default),b=Add(w,.6f,new V2(0,.15f));
            w.Fish[b].Health=1;w.SetInput(a,new FishCommand(default,true));w.Step();Check(!w.Fish[b].Alive);
        });
        Run("Aim assist helps nearby prey but never turns backwards", () => {
            var w=Empty();int a=Add(w,2,default),b=Add(w,.6f,new V2(1,1));
            BiteAssist.Apply(ref w.Fish[a],w.Fish,w.Config);Check(w.Fish[a].Heading>.4f && w.Fish[a].Heading<=.65f);
            w.Fish[a].Heading=0;w.Fish[b].Position=new V2(0,-1);BiteAssist.Apply(ref w.Fish[a],w.Fish,w.Config);Near(w.Fish[a].Heading,0);
        });
        Run("Casual bots are slower than the human player", () => {
            var w=Empty();int a=Add(w,1,default),b=Add(w,1,new V2(10,0),true);
            for(int i=0;i<90;i++){FishMotor.Step(ref w.Fish[a],new FishCommand(new V2(0,1)),w.Config,w.Config.TickDelta);FishMotor.Step(ref w.Fish[b],new FishCommand(new V2(0,1)),w.Config,w.Config.TickDelta);}
            Check(w.Fish[a].Position.Y>w.Fish[b].Position.Y*1.2f);
        });
        Run("30-minute 32-fish simulation and allocation budget", () => {
            var c=new GameConfig { MaxFish=32,Population=32 }; var w=new World(c); w.FillPopulation();
            for(int i=0;i<100;i++)w.Step();
            long before=GC.GetAllocatedBytesForCurrentThread(); var timer=Stopwatch.StartNew(); long eats=0;
            for(int tick=0;tick<30*60*30;tick++)
            {
                w.Step(); for(int i=0;i<w.EventCount;i++)if(w.Events[i].Kind==WorldEventKind.Eat)eats++;
                for(int i=0;i<w.Fish.Length;i++)
                {
                    var f=w.Fish[i]; Check(f.Position.IsFinite && Math2.Finite(f.Mass));
                    Check(f.Mass>0 && f.Mass<=c.MaxMass); Check(Math.Abs(f.Position.X)<=c.WorldHalfSize+.001f && Math.Abs(f.Position.Y)<=c.WorldHalfSize+.001f);
                }
            }
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before; timer.Stop();
            Check(eats>100); Check(allocated<4096);
            Console.WriteLine($"    simulated=30min fish=32 eats={eats} elapsed={timer.Elapsed.TotalSeconds:0.000}s allocated={allocated}B");
        });
        Run("Expanded 64-fish world stays healthy and bounded for ten simulated minutes", () => {
            var c=new GameConfig{Population=64};var w=new World(c);w.FillPopulation();
            for(int tick=0;tick<18000;tick++)
            {
                w.Step();
                foreach(var f in w.Fish)if(f.Occupied)
                {
                    Check(f.Position.IsFinite&&Math2.Finite(f.Health)&&f.Health>=0&&f.Health<=c.MaxHealth);
                    Check(Math.Abs(f.Position.X)<=c.WorldHalfSize&&Math.Abs(f.Position.Y)<=c.WorldHalfSize);
                    Check(f.Alive?f.Health>0:f.Health==0);Check(f.Species<2);
                }
            }
            Check(w.Population==64);
        });
        Console.WriteLine($"\n{passed} passed, {failed} failed");
        Environment.ExitCode=failed==0?0:1;
    }
    // Geometry/health fixtures bypass windup; the timing regression above uses the production windup.
    static World Empty() { var w=new World(new GameConfig {Population=1,FoodCount=1,BiteWindup=0}); w.Food[0].RespawnRemaining=100000; return w; }
    static int Add(World w,float mass,V2 p,bool bot=false)
    {
        int id=w.AddFish(bot,mass); w.Fish[id].Position=w.Fish[id].PreviousPosition=p; w.Fish[id].Heading=0; w.Fish[id].Protection=0;
        if(bot)w.Brains[id]=new BotBrain((uint)(id+17)); return id;
    }
    static void Check(bool value) { if(!value)throw new Exception("Assertion failed"); }
    static void Near(float a,float b) { if(Math.Abs(a-b)>.001f)throw new Exception($"Expected {a} ~= {b}"); }
    static void Run(string name,Action body) { try {body();passed++;Console.WriteLine("PASS "+name);} catch(Exception e) {failed++;Console.WriteLine("FAIL "+name+": "+e.Message);} }
}
