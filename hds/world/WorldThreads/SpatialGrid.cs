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
            AddToCell(MakeKey(CellCoord(x), CellCoord(z)), item);
        }

        public long GetCellKey(float x, float z)
        {
            return MakeKey(CellCoord(x), CellCoord(z));
        }

        public void AddToCell(long key, T item)
        {
            if (!cells.TryGetValue(key, out List<T> list))
            {
                list = new List<T>();
                cells.Add(key, list);
            }

            list.Add(item);
        }

        public bool RemoveFromCell(long key, T item)
        {
            if (!cells.TryGetValue(key, out List<T> list))
            {
                return false;
            }

            int idx = list.IndexOf(item);
            if (idx < 0)
            {
                return false;
            }

            int last = list.Count - 1;
            if (idx != last)
            {
                list[idx] = list[last];
            }
            list.RemoveAt(last);
            return true;
        }

        public void Clear()
        {
            foreach (List<T> list in cells.Values)
            {
                list.Clear();
            }
        }

        public void CollectNeighborCells(float x, float z, float radius, List<List<T>> destination)
        {
            destination.Clear();
            int minX = CellCoord(x - radius);
            int maxX = CellCoord(x + radius);
            int minZ = CellCoord(z - radius);
            int maxZ = CellCoord(z + radius);

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    long key = MakeKey(cx, cz);
                    if (cells.TryGetValue(key, out List<T> list) && list.Count > 0)
                    {
                        destination.Add(list);
                    }
                }
            }
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
                    if (cells.TryGetValue(key, out List<T> list) && list.Count > 0)
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
