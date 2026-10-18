using System;

namespace Tidebite.Core
{
    public static class FishJaw
    {
        public const float MaxAngle = .73f;
        public static float Gape(float time)
        {
            if (time <= .12f || time >= .82f) return 0;
            return Smooth((time-.12f)/.24f)*(1-Smooth((time-.50f)/.32f));
        }
        private static float Smooth(float value){value=Math2.Clamp01(value);return value*value*(3-2*value);}
        // Side-profile mandible rotates down from the rear lip hinge; the hinge never translates.
        public static V2 Rotate(V2 point,V2 hinge,float gape)
        {
            float angle=-Math2.Clamp01(gape)*MaxAngle,c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
            V2 p=point-hinge;return hinge+new V2(p.X*c-p.Y*s,p.X*s+p.Y*c);
        }
    }
}
