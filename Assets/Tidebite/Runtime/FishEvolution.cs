using Tidebite.Core;
using UnityEngine;

namespace Tidebite
{
    internal static class FishEvolution
    {
        public static readonly string[] Assets = {
            "profile-01-sprat", "profile-02-perch", "profile-03-fang",
            "profile-04-razor", "profile-05-shark", "profile-06-leviathan"
        };
        public static readonly string[] Names = { "새끼 물고기", "산호 농어", "송곳니 포식어", "은빛 바라쿠다", "청상어", "심해의 지배자" };
        private static Texture2D[] textures;
        public static Texture2D Texture(int form)
        {
            if (textures == null)
            {
                textures = new Texture2D[GrowthCurve.FormCount];
                for (int i = 0; i < textures.Length; i++) textures[i] = Resources.Load<Texture2D>("Art/" + Assets[i]);
            }
            return textures[Mathf.Clamp(form, 0, textures.Length - 1)];
        }
    }
}
