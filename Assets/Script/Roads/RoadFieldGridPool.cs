using System.Collections.Generic;

namespace JayFos.Roads
{
    public static class RoadFieldGridPool
    {
        private static readonly Stack<RoadFieldGrid> pool = new Stack<RoadFieldGrid>(16);

        public static RoadFieldGrid Get()
        {
            if (pool.Count > 0)
            {
                return pool.Pop();
            }

            return new RoadFieldGrid();
        }

        public static void Return(RoadFieldGrid grid)
        {
            if (grid == null)
                return;

            grid.Clear();
            pool.Push(grid);
        }

        public static void Clear()
        {
            pool.Clear();
        }
    }
}