using System;
using System.Collections.Generic;
using UnityEngine;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Serialized LRU store for run-length encoded SFS subspace cells.</summary>
    public sealed class SfsSubspaceCacheAsset : ScriptableObject
    {
        [SerializeField] private List<Entry> entries = new();

        /// <summary>Restores the cached subspace for a stable key when it contains enough cells.</summary>
        /// <param name="key">Stable identifier of the SFS source and cache configuration.</param>
        /// <param name="minimumCellCount">Minimum accepted number of cached cells.</param>
        /// <param name="subspace">Decoded voxel cells when a valid entry is found.</param>
        /// <returns><see langword="true"/> when a valid subspace was restored.</returns>
        public bool TryGet(string key, int minimumCellCount, out HashSet<Vector3Int> subspace)
        {
            subspace = null;
            Entry entry = entries.Find(item => item.Key == key);

            if (entry == null || entry.CellCount < minimumCellCount)
                return false;

            return entry.TryGetCells(out subspace);
        }

        /// <summary>Checks whether a sufficiently large cached subspace exists for a stable key.</summary>
        /// <param name="key">Stable identifier of the SFS source and cache configuration.</param>
        /// <param name="minimumCellCount">Minimum accepted number of cached cells.</param>
        /// <returns><see langword="true"/> when a matching entry satisfies the size requirement.</returns>
        public bool Contains(string key, int minimumCellCount)
        {
            Entry entry = entries.Find(item => item.Key == key);
            return entry != null && entry.CellCount >= minimumCellCount;
        }

        /// <summary>Stores a subspace and evicts least-recent entries until both cache limits are met.</summary>
        /// <param name="key">Stable identifier of the SFS source and cache configuration.</param>
        /// <param name="subspace">Voxel cells to encode and persist.</param>
        /// <param name="maxEntries">Maximum number of retained entries.</param>
        /// <param name="maxCells">Maximum combined decoded cell count.</param>
        public void Store(string key, HashSet<Vector3Int> subspace, int maxEntries, int maxCells)
        {
            entries.RemoveAll(entry => entry.Key == key || entry.CellCount == 0);
            entries.Insert(0, new Entry(key, subspace));
            Trim(maxEntries, maxCells);
        }

        /// <summary>Removes all persistent subspace entries.</summary>
        public void Clear() => entries.Clear();

        private void Trim(int maxEntries, int maxCells)
        {
            while (entries.Count > Mathf.Max(1, maxEntries))
                entries.RemoveAt(entries.Count - 1);

            while (GetCellCount() > maxCells && entries.Count > 0)
                entries.RemoveAt(entries.Count - 1);
        }

        private int GetCellCount()
        {
            int count = 0;

            foreach (Entry entry in entries)
                count += entry.CellCount;

            return count;
        }

        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string key;
            [SerializeField] private List<Vector3Int> cells = new();
            [SerializeField] private List<CellRun> runs = new();
            [SerializeField] private int cellCount;

            public string Key => key;
            public int CellCount => cellCount > 0 ? cellCount : cells?.Count ?? 0;

            public Entry()
            {
            }

            public Entry(string key, IEnumerable<Vector3Int> cells)
            {
                this.key = key;
                this.cells = new List<Vector3Int>();
                runs = CreateRuns(cells, out cellCount);
            }

            public bool TryGetCells(out HashSet<Vector3Int> subspace)
            {
                subspace = new HashSet<Vector3Int>();

                if (runs != null && runs.Count > 0)
                {
                    foreach (CellRun run in runs)
                        run.AddCells(subspace);

                    return subspace.Count > 0;
                }

                if (cells == null || cells.Count == 0)
                    return false;

                subspace = new HashSet<Vector3Int>(cells);
                return true;
            }

            private static List<CellRun> CreateRuns(IEnumerable<Vector3Int> sourceCells, out int count)
            {
                count = 0;
                Dictionary<RowKey, List<int>> rows = new();

                foreach (Vector3Int cell in sourceCells)
                {
                    RowKey key = new(cell.y, cell.z);

                    if (!rows.TryGetValue(key, out List<int> xValues))
                    {
                        xValues = new List<int>();
                        rows[key] = xValues;
                    }

                    xValues.Add(cell.x);
                    count++;
                }

                List<KeyValuePair<RowKey, List<int>>> sortedRows = new(rows);
                sortedRows.Sort((a, b) => a.Key.CompareTo(b.Key));

                List<CellRun> result = new();

                foreach (KeyValuePair<RowKey, List<int>> row in sortedRows)
                {
                    List<int> xValues = row.Value;

                    if (xValues.Count == 0)
                        continue;

                    xValues.Sort();

                    int startX = xValues[0];
                    int previousX = startX;

                    for (int i = 1; i < xValues.Count; i++)
                    {
                        int x = xValues[i];

                        if (x == previousX)
                            continue;

                        if (x == previousX + 1)
                        {
                            previousX = x;
                            continue;
                        }

                        result.Add(new CellRun(row.Key.Y, row.Key.Z, startX, previousX - startX + 1));
                        startX = x;
                        previousX = x;
                    }

                    result.Add(new CellRun(row.Key.Y, row.Key.Z, startX, previousX - startX + 1));
                }

                return result;
            }
        }

        [Serializable]
        private sealed class CellRun
        {
            [SerializeField] private int y;
            [SerializeField] private int z;
            [SerializeField] private int startX;
            [SerializeField] private int length;

            public CellRun()
            {
            }

            public CellRun(int y, int z, int startX, int length)
            {
                this.y = y;
                this.z = z;
                this.startX = startX;
                this.length = Mathf.Max(0, length);
            }

            public void AddCells(HashSet<Vector3Int> subspace)
            {
                for (int x = startX; x < startX + length; x++)
                    subspace.Add(new Vector3Int(x, y, z));
            }
        }

        private readonly struct RowKey : IEquatable<RowKey>, IComparable<RowKey>
        {
            public int Y { get; }
            public int Z { get; }

            public RowKey(int y, int z)
            {
                Y = y;
                Z = z;
            }

            public bool Equals(RowKey other) => Y == other.Y && Z == other.Z;
            public override bool Equals(object obj) => obj is RowKey other && Equals(other);
            public override int GetHashCode() => (Y * 397) ^ Z;

            public int CompareTo(RowKey other)
            {
                int yComparison = Y.CompareTo(other.Y);
                return yComparison != 0 ? yComparison : Z.CompareTo(other.Z);
            }
        }
    }
}
