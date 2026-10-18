using System;

namespace Tidebite.Core
{
    // Reused arrays: no allocations in neighbourhood queries.
    public sealed class SpatialGrid
    {
        private readonly int[] heads, next;
        private readonly int width;
        private readonly float cell, half;
        public SpatialGrid(int capacity, float halfSize, float cellSize = 5)
        {
            half = halfSize; cell = cellSize;
            width = (int)Math.Ceiling(half * 2 / cell) + 1;
            heads = new int[width * width]; next = new int[capacity];
        }
        private int Coord(float p) => (int)Math2.Clamp((p + half) / cell, 0, width - 1);
        public void Rebuild(FishState[] fish)
        {
            for (int i = 0; i < heads.Length; i++) heads[i] = -1;
            for (int i = 0; i < fish.Length; i++)
            {
                if (!fish[i].Occupied || !fish[i].Alive) continue;
                int key = Coord(fish[i].Position.X) + width * Coord(fish[i].Position.Y);
                next[i] = heads[key]; heads[key] = i;
            }
        }
        public int Query(V2 position, float radius, int[] result)
        {
            int count = 0;
            for (int y = Coord(position.Y - radius); y <= Coord(position.Y + radius); y++)
            for (int x = Coord(position.X - radius); x <= Coord(position.X + radius); x++)
            for (int i = heads[x + y * width]; i >= 0; i = next[i])
            {
                if (count == result.Length) return count;
                result[count++] = i;
            }
            return count;
        }
    }
}
