using System;

namespace Tidebite.Core
{
    // Deterministic world features are shared by authority, prediction, AI and presentation.
    public static class OceanHabitats
    {
        public static readonly V2[] Kelp={new V2(-30,18),new V2(26,30),new V2(-34,-32),new V2(32,-30)};
        public static readonly V2[] Passages={new V2(-18,14),new V2(24,-12),new V2(-30,-6)};
        public static readonly V2[] Currents={new V2(17,8),new V2(-18,-28),new V2(40,-4)};
        public static readonly V2[] Flow={new V2(.8944f,.4472f),new V2(-.7071f,.7071f),new V2(0,1)};
        private static readonly V2[] Gold={new V2(12,24),new V2(-26,-20),new V2(38,20),new V2(-36,34)};
        public const float KelpRadius=4.8f,RockRadius=2.1f,CurrentRadius=5.5f;
        public static bool InKelp(V2 position)
        {for(int i=0;i<Kelp.Length;i++)if((position-Kelp[i]).SqrMagnitude<KelpRadius*KelpRadius)return true;return false;}
        public static bool CanSee(in FishState observer,in FishState target,GameConfig c)
            =>!c.OceanFeatures||target.Radius>1.1f||!InKelp(target.Position)||(target.Position-observer.Position).SqrMagnitude<9;
        public static V2 CurrentAt(V2 position,GameConfig c)
        {
            if(!c.OceanFeatures)return default;
            for(int i=0;i<Currents.Length;i++)if((position-Currents[i]).SqrMagnitude<CurrentRadius*CurrentRadius)return Flow[i]*2.4f;
            return default;
        }
        public static V2 Rock(int index)=>Passages[index/2]+new V2(0,index%2==0?3:-3);
        public static bool Blocked(V2 from,V2 to,float radius)
        {for(int i=0;i<Passages.Length*2;i++)if(Math2.SegmentDistance(Rock(i),from,to)<RockRadius+radius+.02f)return true;return false;}
        public static V2 Move(V2 from,V2 to,float radius)
        {
            // Swept circles prevent bursts tunnelling through a narrow passage pillar.
            float r=RockRadius+radius+.01f;
            for(int pass=0;pass<2;pass++)for(int i=0;i<Passages.Length*2;i++)
            {
                V2 centre=Rock(i),delta=to-from,offset=from-centre;
                if(offset.SqrMagnitude<r*r)
                {
                    V2 normal=(to-centre).Normalized;if(normal.SqrMagnitude<.1f)normal=new V2(1,0);
                    to=centre+normal*r;continue;
                }
                float a=delta.SqrMagnitude,b=2*V2.Dot(offset,delta),c=offset.SqrMagnitude-r*r,disc=b*b-4*a*c;
                if(a<.000001f||disc<0)continue;
                float t=(-b-(float)Math.Sqrt(disc))/(2*a);if(t<0||t>1)continue;
                V2 hit=from+delta*Math.Max(0,t-.001f),n=(hit-centre).Normalized,remaining=to-hit;
                to=hit+remaining-n*Math.Min(0,V2.Dot(remaining,n));
                if((to-centre).SqrMagnitude<r*r)to=centre+(to-centre).Normalized*r;
            }
            return to;
        }
        public static V2 Avoid(V2 position,V2 desired,float radius)
        {
            float probe=radius+2.3f;
            if(!Blocked(position,position+desired*probe,radius))return desired;
            V2 best=-desired;float bestScore=float.NegativeInfinity;
            for(int i=0;i<16;i++)
            {
                V2 d=V2.FromAngle(i*Math2.Pi/8),end=position+d*probe;
                float score=V2.Dot(d,desired)-(Blocked(position,end,radius)?5:0);
                if(score>bestScore){best=d;bestScore=score;}
            }
            return best;
        }
        public static int GoldCount(GameConfig c)=>c.OceanFeatures?Math.Min(24,c.FoodCount/8):0;
        public static bool GoldActive(float time)=>time>=45&&(time-45)%60<18;
        public static int GoldCycle(float time)=>Math.Max(0,(int)((time-45)/60));
        public static float GoldCountdown(float time)=>time<45?45-time:GoldActive(time)?18-(time-45)%60:60-(time-45)%60;
        public static V2 GoldCentre(float time)=>Gold[GoldCycle(time)%Gold.Length];
        public static void PlaceGold(FoodState[] food,GameConfig c,float time)
        {
            int count=GoldCount(c);V2 centre=GoldCentre(time);
            for(int i=0;i<count;i++)
            {
                float angle=i*2.399963f,r=.5f+(float)Math.Sqrt(i/(float)Math.Max(1,count))*3.6f;
                food[food.Length-count+i].Position=centre+V2.FromAngle(angle)*r;
            }
        }
    }
}
