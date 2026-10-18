using System;

namespace Tidebite.Core
{
    // Only the rendered pose is interpolated. Authoritative collision/mass remain on current.
    public static class FishPresentation
    {
        public static FishState Interpolate(FishState previous,FishState current,float alpha)
        {
            if(previous.Generation!=current.Generation || !previous.Alive || !current.Alive ||
                !previous.Occupied || !current.Occupied) return current;
            alpha=Math.Max(0,Math.Min(1,alpha));
            current.Position=V2.Lerp(previous.Position,current.Position,alpha);
            current.Heading=previous.Heading+Math2.DeltaAngle(previous.Heading,current.Heading)*alpha;
            return current;
        }
    }
}
