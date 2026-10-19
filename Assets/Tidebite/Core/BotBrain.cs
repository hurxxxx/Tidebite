using System;

namespace Tidebite.Core
{
    // Perception/planning is staggered. Steering and contact timing still react every tick.
    public sealed class BotBrain
    {
        private readonly Rng rng;
        private readonly int[] nearby=new int[GameConfig.FishCapacity],predators=new int[GameConfig.FishCapacity],foodCandidates=new int[8];
        private readonly float[] foodDistances=new float[8];
        private float nextThink,safeSince=-1,targetUntil,acquiredAt;
        private int nearbyCount,predatorCount,target=-1,targetGeneration,foodTarget=-1,generation;
        private V2 wander,escape;
        public BotMode Mode { get; private set; }
        public int TargetId=>target;
        public int FoodTargetId=>foodTarget;
        public float Boldness { get; }

        public BotBrain(uint seed,float stagger=0)
        {rng=new Rng(seed);Boldness=rng.Range(.85f,1.15f);nextThink=stagger;}

        public FishCommand Decide(World world,int index)
        {
            FishState self=world.Fish[index];
            if(generation!=self.Generation)
            {
                bool firstSpawn=generation==0;
                generation=self.Generation;target=foodTarget=-1;safeSince=-1;
                Mode=BotMode.Roam;nearbyCount=predatorCount=0;
                if(!firstSpawn)nextThink=world.Time;
            }
            if(target>=0 && (!world.Fish[target].Alive || world.Fish[target].Generation!=targetGeneration ||
                !PredationRules.CanConsume(self,world.Fish[target],world.Config)))nextThink=world.Time;
            if(foodTarget>=0 && !world.Food[foodTarget].Active)nextThink=world.Time;
            if(world.Time>=nextThink)
            {
                nextThink=world.Time+world.Config.BotThinkInterval*rng.Range(.92f,1.08f);
                Plan(world,self);
            }
            return Act(world,self);
        }

        private void Plan(World world,FishState self)
        {
            var c=world.Config;float now=world.Time;
            nearbyCount=world.Grid.Query(self.Position,c.SenseRadius,nearby);predatorCount=0;
            V2 danger=default;float urgency=0;
            int preyId=-1;float preyValue=0;
            FishState vulnerable=self;vulnerable.Protection=0;
            for(int n=0;n<nearbyCount;n++)
            {
                var other=world.Fish[nearby[n]];
                if(other.Id==self.Id || !other.Occupied || !other.Alive)continue;
                V2 offset=other.Position-self.Position;float distance=offset.Magnitude;
                if(distance>c.SenseRadius)continue;
                if(PredationRules.CanConsume(other,vulnerable,c))
                {
                    predators[predatorCount++]=other.Id;
                    float gap=Math.Max(.15f,distance-self.Radius-other.Radius);
                    float closing=V2.Dot(other.Velocity-self.Velocity,-offset.Normalized);
                    float range=4.5f+self.Radius+other.Radius+Math.Max(0,closing)*.6f;
                    if(distance<range)
                    {
                        float weight=(1+Math.Max(0,closing)*.25f)/gap;
                        danger-=offset.Normalized*weight;urgency=Math.Max(urgency,weight);
                    }
                }
                if(!PredationRules.CanConsume(self,other,c))continue;
                float speed=PredationRules.Speed(self,c);
                float away=V2.Dot(other.Velocity,offset.Normalized);
                // A faster fleeing fish is a poor investment once the burst is unavailable.
                if(distance>3.5f && away>speed*.97f && self.DashCooldown>.4f)continue;
                float catchTime=distance/Math.Max(speed*.4f,speed-away*.65f);
                float value=other.Mass*c.ConsumptionGain/(catchTime+1.1f)*Boldness;
                value*=1+(1-other.Health/c.MaxHealth)*.7f;
                if(other.Id==target && now<targetUntil)value*=1.25f;
                if(value>preyValue){preyValue=value;preyId=other.Id;}
            }
            if(urgency>0)
            {
                Mode=BotMode.Evade;safeSince=-1;target=foodTarget=-1;
                escape=ChooseEscape(world,self,danger.Normalized);return;
            }
            if(Mode==BotMode.Evade || Mode==BotMode.Recover)
            {
                if(safeSince<0)safeSince=now;
                if(now-safeSince<c.ThreatMemory){Mode=BotMode.Recover;return;}
            }
            int foodId=ChooseFood(world,self,out float foodValue);
            if(self.Health<c.MaxHealth*.4f && foodId>=0)
            {target=-1;foodTarget=foodId;Mode=BotMode.Forage;return;}
            if(preyId>=0 && preyValue>foodValue*1.15f)
            {
                var prey=world.Fish[preyId];
                // Do not chase prey through the predicted path of a larger fish.
                float risk=RouteRisk(world,self,prey.Position,.45f);
                if(risk<1.5f || V2.Distance(self.Position,prey.Position)<2)
                {
                    if(target!=preyId || targetGeneration!=prey.Generation)acquiredAt=now;
                    target=preyId;targetGeneration=prey.Generation;targetUntil=now+1;
                    foodTarget=-1;Mode=BotMode.Hunt;return;
                }
            }
            target=-1;foodTarget=foodId;
            if(foodId>=0){Mode=BotMode.Forage;return;}
            Mode=BotMode.Roam;
            if(V2.Distance(wander,self.Position)<2.5f || wander.SqrMagnitude<.1f)
                wander=world.RandomSafePoint(self.Id,self.Mass);
        }

        private FishCommand Act(World world,FishState self)
        {
            var c=world.Config;V2 desired;bool bite=false,dash=false;
            if(Mode==BotMode.Evade || Mode==BotMode.Recover)
            {
                desired=escape;
                if(Mode==BotMode.Evade && self.DashCooldown<=c.TickDelta && V2.Dot(self.Forward,escape)>.78f)
                {
                    V2 endpoint=self.Position+self.Forward*PredationRules.Speed(self,c)*c.DashMultiplier*c.DashDuration;
                    dash=WithinBounds(endpoint,self,c) && NearestThreatGap(world,self,endpoint,.28f)>NearestThreatGap(world,self,self.Position,0)+.4f &&
                        NearestThreatGap(world,self,self.Position,0)<2.1f;
                }
            }
            else if(Mode==BotMode.Hunt && target>=0)
            {
                var prey=world.Fish[target];
                float distance=V2.Distance(self.Position,prey.Position);
                if(distance>c.SenseRadius || !PredationRules.CanConsume(self,prey,c))return new FishCommand(Steer(world,self,self.Forward));
                V2 intercept=Intercept(self,prey,c);
                desired=intercept-self.Position;
                float alignment=V2.Dot(self.Forward,(prey.Position-self.Position).Normalized);
                // Use the same mouth geometry as authority, without random missed attacks.
                var futureSelf=self;var futurePrey=prey;
                futureSelf.Position+=self.Velocity*.08f;futurePrey.Position+=prey.Velocity*.08f;
                bite=prey.HurtProtection<=c.TickDelta && world.Time-acquiredAt>=.14f && self.BiteCooldown<=c.TickDelta && alignment>.72f &&
                    (PredationRules.MouthHits(self,prey,c) || PredationRules.MouthHits(futureSelf,futurePrey,c));
                float reach=self.Radius+prey.Radius+c.BiteReach;
                V2 end=self.Position+self.Forward*PredationRules.Speed(self,c)*c.DashMultiplier*c.DashDuration;
                dash=world.Time-acquiredAt>.22f && self.DashCooldown<=c.TickDelta && alignment>.92f && distance>reach*1.25f && distance<reach+4 &&
                    WithinBounds(end,self,c) && V2.Distance(end,prey.Position+prey.Velocity*.24f)<distance-.4f && RouteRisk(world,self,end,.24f)<.9f;
            }
            else if(Mode==BotMode.Forage && foodTarget>=0 && world.Food[foodTarget].Active)
                desired=world.Food[foodTarget].Position-self.Position;
            else desired=wander-self.Position;
            V2 move=Steer(world,self,desired.Normalized);
            float turn=Math.Abs(Math2.DeltaAngle(self.Heading,Math2.Angle(move)));
            // Braking before a sharp turn prevents circles around a nearby pearl.
            float throttle=turn>1.5f?.40f:turn>.8f?.70f:1;
            return new FishCommand(move.Normalized*throttle,bite,dash);
        }

        private int ChooseFood(World world,FishState self,out float bestValue)
        {
            var c=world.Config;
            for(int k=0;k<foodCandidates.Length;k++){foodCandidates[k]=-1;foodDistances[k]=c.SenseRadius*c.SenseRadius;}
            for(int i=0;i<world.Food.Length;i++)
            {
                if(!world.Food[i].Active)continue;
                float d=(world.Food[i].Position-self.Position).SqrMagnitude;
                if(d>=foodDistances[foodDistances.Length-1])continue;
                int at=foodDistances.Length-1;
                while(at>0 && d<foodDistances[at-1]){foodDistances[at]=foodDistances[at-1];foodCandidates[at]=foodCandidates[at-1];at--;}
                foodDistances[at]=d;foodCandidates[at]=i;
            }
            int best=-1;bestValue=0;
            for(int k=0;k<foodCandidates.Length;k++)
            {
                int id=foodCandidates[k];if(id<0)continue;
                float distance=(float)Math.Sqrt(foodDistances[k]);
                float risk=RouteRisk(world,self,world.Food[id].Position,Math.Min(.8f,distance/PredationRules.Speed(self,c)));
                if(risk>2.5f)continue;
                float value=c.FoodMass/(.25f+distance/PredationRules.Speed(self,c))/(1+risk*3);
                if(id==foodTarget)value*=1.18f;
                if(value>bestValue){bestValue=value;best=id;}
            }
            return best;
        }

        private V2 Steer(World world,FishState self,V2 desired)
        {
            V2 separation=default;
            for(int n=0;n<nearbyCount;n++)
            {
                var other=world.Fish[nearby[n]];
                if(other.Id==self.Id || other.Id==target || !other.Occupied || !other.Alive)continue;
                V2 delta=self.Position-other.Position;float d=delta.Magnitude;
                float personal=self.Radius+other.Radius+.5f;
                if(d<personal && d>.01f)separation+=delta/d*((personal-d)/personal);
            }
            V2 future=self.Position+self.Velocity*.45f;
            float edge=world.Config.WorldHalfSize-self.Radius-2;
            V2 inward=new V2(Math.Abs(future.X)>edge?-Math.Sign(future.X)*(Math.Abs(future.X)-edge):0,
                Math.Abs(future.Y)>edge?-Math.Sign(future.Y)*(Math.Abs(future.Y)-edge):0);
            return (desired+separation*.55f+inward*1.4f).Normalized;
        }

        public static V2 Intercept(FishState self,FishState prey,GameConfig config)
        {
            V2 r=prey.Position-self.Position,v=prey.Velocity;
            float speed=PredationRules.Speed(self,config);
            float a=V2.Dot(v,v)-speed*speed,b=2*V2.Dot(r,v),c=V2.Dot(r,r),time=0;
            float discriminant=b*b-4*a*c;
            if(Math.Abs(a)<.001f){if(Math.Abs(b)>.001f)time=-c/b;}
            else if(discriminant>=0)
            {
                float root=(float)Math.Sqrt(discriminant),t0=(-b-root)/(2*a),t1=(-b+root)/(2*a);
                time=t0>0 && t1>0?Math.Min(t0,t1):Math.Max(t0,t1);
            }
            if(time<=0)time=r.Magnitude/Math.Max(1,speed)*.25f;
            return prey.Position+v*Math2.Clamp(time,0,.65f);
        }

        private V2 ChooseEscape(World world,FishState self,V2 preferred)
        {
            V2 best=preferred;float bestScore=float.NegativeInfinity;
            for(int i=0;i<16;i++)
            {
                V2 direction=V2.FromAngle(i*Math2.Pi/8),future=self.Position+direction*PredationRules.Speed(self,world.Config)*.8f;
                float edge=world.Config.WorldHalfSize-self.Radius-1;
                float score=V2.Dot(direction,preferred)*1.5f+NearestThreatGap(world,self,future,.8f)*.65f;
                score-=Math.Max(0,Math.Abs(future.X)-edge)*10+Math.Max(0,Math.Abs(future.Y)-edge)*10;
                score-=Math.Abs(Math2.DeltaAngle(self.Heading,Math2.Angle(direction)))*.15f;
                if(score>bestScore){bestScore=score;best=direction;}
            }
            return best;
        }

        private float NearestThreatGap(World world,FishState self,V2 position,float time)
        {
            float nearest=12;
            for(int k=0;k<predatorCount;k++)
            {
                var other=world.Fish[predators[k]];
                if(!other.Occupied || !other.Alive)continue;
                nearest=Math.Min(nearest,V2.Distance(position,other.Position+other.Velocity*time)-other.Radius-self.Radius);
            }
            return nearest;
        }

        private float RouteRisk(World world,FishState self,V2 destination,float time)
        {
            float risk=0;
            for(int k=0;k<predatorCount;k++)
            {
                var other=world.Fish[predators[k]];
                if(!other.Occupied || !other.Alive)continue;
                V2 future=other.Position+other.Velocity*time;
                float endpoint=V2.Distance(destination,future)-other.Radius-self.Radius;
                float corridor=Math2.SegmentDistance(future,self.Position,destination)-other.Radius-self.Radius;
                risk+=Math.Max(0,2.5f-endpoint)*.55f+Math.Max(0,.9f-corridor)*1.3f;
            }
            return risk;
        }
        private static bool WithinBounds(V2 point,FishState self,GameConfig c)
            =>Math.Abs(point.X)<c.WorldHalfSize-self.Radius-.5f && Math.Abs(point.Y)<c.WorldHalfSize-self.Radius-.5f;
    }
}
