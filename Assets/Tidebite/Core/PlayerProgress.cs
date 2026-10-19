using System;

namespace Tidebite.Core
{
    // Local collection and cosmetics; never grants permanent combat-stat upgrades.
    [Serializable]
    public sealed class PlayerProgress
    {
        public int Version=1,TotalFood,TotalEats,TotalDodges,TotalLives,TotalRounds,TotalWins,TotalPodiums;
        public int TotalManualRetries;
        public int BestLifeScore,BestRoundScore,BestLevel=1,Shells;
        public ushort Discovered;
        public uint PaidChallenges;
        public byte OwnedSkins=1,SelectedSkin,SelectedFamily;
        public static readonly string[] ChallengeNames={"첫 포식","진주 수집가","돌진의 달인","첫 진화","바다 탐험가","심해로 한 걸음","순위 경쟁자","바다의 지배자"};
        public static readonly int[] Goals={1,25,3,6,6,16,1,1};
        public static readonly int[] Rewards={25,40,60,60,80,100,100,120};
        public static readonly int[] SkinCosts={0,50,100,150};
        public static readonly string[] SkinNames={"원래 빛","새벽빛","황금빛","황혼빛"};
        public int Discoveries
        {get{int n=0;for(int i=0;i<12;i++)if((Discovered&(1<<i))!=0)n++;return n;}}
        public int ChallengeValue(int index)
        {
            switch(index)
            {
                case 0:return TotalEats;case 1:return TotalFood;case 2:return TotalDodges;
                case 3:return BestLevel;case 4:return Discoveries;case 5:return BestLevel;
                case 6:return TotalPodiums;case 7:return TotalWins;default:return 0;
            }
        }
        public uint ClaimRewards()
        {
            uint earned=0;
            for(int i=0;i<Goals.Length;i++)if((PaidChallenges&(1u<<i))==0&&ChallengeValue(i)>=Goals[i])
            {earned|=1u<<i;PaidChallenges|=1u<<i;Shells+=Rewards[i];}
            return earned;
        }
        public void Discover(int form){if(form>=0&&form<12)Discovered|=(ushort)(1<<form);}
        public void Observe(int score,int level)
        {BestLifeScore=Math.Max(BestLifeScore,score);BestLevel=Math.Max(BestLevel,level);}
        public void RecordLife(int score,int level){TotalLives++;Observe(score,level);}
        public void RecordRound(int score,int rank)
        {if(rank<=0)return;TotalRounds++;BestRoundScore=Math.Max(BestRoundScore,score);if(rank<=3)TotalPodiums++;if(rank==1)TotalWins++;}
        public bool FamilyUnlocked(byte family)=>family==0||family==1&&TotalEats>=3;
        public bool SelectFamily(byte family){if(!FamilyUnlocked(family))return false;SelectedFamily=family;return true;}
        public bool EquipSkin(byte skin)
        {
            if(skin>3)return false;
            if((OwnedSkins&(1<<skin))==0)
            {if(Shells<SkinCosts[skin])return false;Shells-=SkinCosts[skin];OwnedSkins|=(byte)(1<<skin);}
            SelectedSkin=skin;return true;
        }
        public void Sanitize()
        {
            TotalFood=Math.Max(0,TotalFood);TotalEats=Math.Max(0,TotalEats);TotalDodges=Math.Max(0,TotalDodges);
            TotalLives=Math.Max(0,TotalLives);TotalRounds=Math.Max(0,TotalRounds);TotalWins=Math.Max(0,TotalWins);TotalPodiums=Math.Max(0,TotalPodiums);
            TotalManualRetries=Math.Max(0,TotalManualRetries);
            BestLifeScore=Math.Max(0,BestLifeScore);BestRoundScore=Math.Max(0,BestRoundScore);BestLevel=Math.Max(1,Math.Min(30,BestLevel));Shells=Math.Max(0,Shells);
            Discovered&=0xfff;PaidChallenges&=0xff;OwnedSkins=(byte)((OwnedSkins&15)|1);
            if(!FamilyUnlocked(SelectedFamily))SelectedFamily=0;
            if(SelectedSkin>3||(OwnedSkins&(1<<SelectedSkin))==0)SelectedSkin=0;
        }
    }
}
