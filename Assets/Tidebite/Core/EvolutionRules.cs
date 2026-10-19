using System;

namespace Tidebite.Core
{
    public enum Adaptation:byte { None, Hunter, Armor, Feeder }

    public static class EvolutionRules
    {
        public static int Available(in FishState fish,GameConfig c)
            =>Math.Max(0,GrowthCurve.Form(fish.Mass,c)-fish.Hunter-fish.Armor-fish.Feeder);
        public static int Rank(in FishState fish,Adaptation choice)
            =>choice==Adaptation.Hunter?fish.Hunter:choice==Adaptation.Armor?fish.Armor:choice==Adaptation.Feeder?fish.Feeder:3;
        public static bool Choose(ref FishState fish,Adaptation choice,GameConfig c)
        {
            if(!fish.Occupied||!fish.Alive||choice<Adaptation.Hunter||choice>Adaptation.Feeder||Available(fish,c)<=0||Rank(fish,choice)>=3)return false;
            if(choice==Adaptation.Hunter)fish.Hunter++;
            else if(choice==Adaptation.Armor){fish.Armor++;fish.WardCooldown=0;}
            else fish.Feeder++;
            return true;
        }
        public static float BiteReach(in FishState fish,GameConfig c)=>c.BiteReach*(1+fish.Hunter*.12f);
        public static float DashDuration(in FishState fish,GameConfig c)=>c.DashDuration*(1+fish.Hunter*.10f);
        public static float PickupReach(in FishState fish,GameConfig c)=>c.FoodPickupReach*(1+fish.Feeder*.5f)*(fish.Species==1?1.1f:1);
        public static float FoodGain(in FishState fish,GameConfig c)=>c.FoodMass*(GrowthCurve.Form(fish.Mass,c)==0?1.5f:1)*(1+fish.Feeder*.12f);
    }
}
