using UnityEngine;

namespace Genix.Sampling.PoissonSampling
{
    internal sealed class PoissonGrid
    {
        private readonly Bounds _bounds;

        private readonly float _cellSize;
        private readonly int _searchRadius;

        private readonly int _width;
        private readonly int _height;

        private readonly Vector3?[,] _cells;

        public PoissonGrid(Bounds bounds, float minDistance)
        {
            _bounds = bounds;

            _cellSize = minDistance / Mathf.Sqrt(2f);
            _searchRadius = Mathf.CeilToInt(minDistance / _cellSize);

            _width = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / _cellSize));
            _height = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / _cellSize));

            _cells = new Vector3?[_width, _height];
        }

        public void Add(Vector3 point)
        {
            Vector2Int index = GetIndex(point);
            _cells[index.x, index.y] = point;
        }

        public bool IsFarEnough(Vector3 point, float minDistance)
        {
            Vector2Int index = GetIndex(point);
            float minDistanceSqr = minDistance * minDistance;

            for (int x = index.x - _searchRadius; x <= index.x + _searchRadius; x++)
            {
                for (int z = index.y - _searchRadius; z <= index.y + _searchRadius; z++)
                {
                    if (!IsInsideGrid(x, z))
                        continue;

                    if (!_cells[x, z].HasValue)
                        continue;

                    if (IsCloserThanMinDistance(point, _cells[x, z].Value, minDistanceSqr))
                        return false;
                }
            }

            return true;
        }

        private Vector2Int GetIndex(Vector3 point)
        {
            int x = Mathf.FloorToInt((point.x - _bounds.min.x) / _cellSize);
            int z = Mathf.FloorToInt((point.z - _bounds.min.z) / _cellSize);

            return new Vector2Int(Mathf.Clamp(x, 0, _width - 1), Mathf.Clamp(z, 0, _height - 1));
        }

        private bool IsInsideGrid(int x, int z)
        {
            return x >= 0 && x < _width && z >= 0 && z < _height;
        }

        private static bool IsCloserThanMinDistance(Vector3 a, Vector3 b, float minDistanceSqr)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;

            return dx * dx + dz * dz < minDistanceSqr;
        }
    }
}