using System;
using System.Collections.Generic;

namespace hds
{
    internal sealed class SpatialGrid<T>
    {
        private readonly float cellSize;
        private readonly Dictionary<long, List<T>> cells = new Dictionary<long, List<T>>();

        public SpatialGrid(float cellSize)
        {
            this.cellSize = cellSize;
        }

        public void Add(float x, float z, T item)
        {
            int cellX = CellCoord(x);
            int cellZ = CellCoord(z);
            long key = MakeKey(cellX, cellZ);
            if (!cells.TryGetValue(key, out List<T> list))
            {
                list = new List<T>();
                cells.Add(key, list);
            }

            list.Add(item);
        }

        public IEnumerable<List<T>> GetNeighborCells(float x, float z, float radius)
        {
            int minX = CellCoord(x - radius);
            int maxX = CellCoord(x + radius);
            int minZ = CellCoord(z - radius);
            int maxZ = CellCoord(z + radius);

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    long key = MakeKey(cx, cz);
                    if (cells.TryGetValue(key, out List<T> list))
                    {
                        yield return list;
                    }
                }
            }
        }

        private int CellCoord(float value)
        {
            return (int)Math.Floor(value / cellSize);
        }

        private static long MakeKey(int cellX, int cellZ)
        {
            return ((long)cellX << 32) | (uint)cellZ;
        }
    }
}
