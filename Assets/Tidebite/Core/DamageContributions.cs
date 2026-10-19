using System;

namespace Tidebite.Core
{
    // Authority-only, fixed-capacity ledger. Respawn clears the victim row, disconnect clears the attacker column.
    // Contributions survive the attacker's respawn, but never a reused connection slot or a new round.
    public sealed class DamageContributions
    {
        private readonly int capacity;
        private readonly float[] damage;
        private readonly int[] hitTick;
        public DamageContributions(int capacity)
        {this.capacity=capacity;damage=new float[capacity*capacity];hitTick=new int[damage.Length];}
        public void Record(int victim,int attacker,float amount,int tick,int windowTicks)
        {
            int at=victim*capacity+attacker;
            if(tick-hitTick[at]>windowTicks)damage[at]=0;
            damage[at]+=amount;hitTick[at]=tick;
        }
        public float Get(int victim,int attacker,int tick,int windowTicks)
        {int at=victim*capacity+attacker;return tick-hitTick[at]<=windowTicks?damage[at]:0;}
        public void Heal(int victim,float before,float after,float maxHealth)
        {
            if(after<=before)return;
            float remaining=Math2.Clamp01((maxHealth-after)/Math.Max(.0001f,maxHealth-before));
            int start=victim*capacity;
            for(int i=0;i<capacity;i++)damage[start+i]*=remaining;
        }
        public void ClearVictim(int victim)=>Array.Clear(damage,victim*capacity,capacity);
        public void Forget(int attacker)
        {ClearVictim(attacker);for(int i=0;i<capacity;i++)damage[i*capacity+attacker]=0;}
        public void Clear()=>Array.Clear(damage,0,damage.Length);
    }
}
