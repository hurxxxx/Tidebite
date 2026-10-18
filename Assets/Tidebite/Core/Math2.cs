using System;

namespace Tidebite.Core
{
    [Serializable]
    public struct V2
    {
        public float X, Y;
        public V2(float x, float y) { X = x; Y = y; }
        public float SqrMagnitude => X * X + Y * Y;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);
        public V2 Normalized => Magnitude > 0.0001f ? this / Magnitude : default;
        public bool IsFinite => Math2.Finite(X) && Math2.Finite(Y);
        public static V2 operator +(V2 a, V2 b) => new V2(a.X + b.X, a.Y + b.Y);
        public static V2 operator -(V2 a, V2 b) => new V2(a.X - b.X, a.Y - b.Y);
        public static V2 operator -(V2 a) => new V2(-a.X, -a.Y);
        public static V2 operator *(V2 a, float f) => new V2(a.X * f, a.Y * f);
        public static V2 operator /(V2 a, float f) => new V2(a.X / f, a.Y / f);
        public static float Dot(V2 a, V2 b) => a.X * b.X + a.Y * b.Y;
        public static V2 Lerp(V2 a, V2 b, float t) => a + (b - a) * Math2.Clamp01(t);
        public static float Distance(V2 a, V2 b) => (a - b).Magnitude;
        public static V2 FromAngle(float angle) => new V2((float)Math.Sin(angle), (float)Math.Cos(angle));
        public override string ToString() => $"({X:0.00}, {Y:0.00})";
    }

    public static class Math2
    {
        public const float Pi = (float)Math.PI;
        public static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
        public static float Clamp01(float f) => Clamp(f, 0, 1);
        public static float MoveTowards(float a, float b, float step) => a + Clamp(b - a, -step, step);
        public static float DeltaAngle(float a, float b)
        {
            float d = (b - a) % (2 * Pi);
            if (d > Pi) d -= 2 * Pi;
            if (d < -Pi) d += 2 * Pi;
            return d;
        }
        public static float Turn(float a, float b, float step) => a + Clamp(DeltaAngle(a, b), -step, step);
        public static float Angle(V2 v) => (float)Math.Atan2(v.X, v.Y);
        public static float SegmentDistance(V2 point, V2 a, V2 b)
        {
            V2 d = b - a;
            float t = d.SqrMagnitude > 0.00001f ? Clamp01(V2.Dot(point - a, d) / d.SqrMagnitude) : 0;
            return V2.Distance(point, a + d * t);
        }
    }

    // Explicit, portable RNG: tests and recorded seeds reproduce the same AI choices.
    public sealed class Rng
    {
        private uint state;
        public Rng(uint seed) { state = seed == 0 ? 1u : seed; }
        public uint Next() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
        public float Unit() => (Next() & 0xFFFFFF) / 16777216f;
        public float Range(float min, float max) => min + (max - min) * Unit();
        public V2 InSquare(float extent) => new V2(Range(-extent, extent), Range(-extent, extent));
    }
}
