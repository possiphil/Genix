using System.Collections.Generic;
using Genix.Sampling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Genix.Editor.Diagnostics
{
    public static partial class DiagnosticsGizmos
    {
        private static void DrawGrid(SceneViewData data)
        {
            if (data.SamplingAlgorithm is not
                (SamplingAlgorithm.Grid or SamplingAlgorithm.JitteredGrid))
            {
                return;
            }

            if (data.StyleSettings.grid.cellSize <= 0f)
                return;

            IReadOnlyList<Vector3> gridPositions = GetGridPositions(data);

            if (gridPositions.Count == 0)
                return;

            EnsureGridMesh(data);
            EnsureGridMaterial();

            _gridMaterial.SetPass(0);
            Graphics.DrawMeshNow(_gridMesh, Matrix4x4.identity);

            DrawGridWireframe(data, gridPositions);
        }

        private static IReadOnlyList<Vector3> GetGridPositions(SceneViewData data)
        {
            return data.RawSamplePositions.Count > 0 ? data.RawSamplePositions : data.CandidateSeeds;
        }

        private static void EnsureGridMesh(SceneViewData data)
        {
            if (_gridMesh != null && _gridMeshKey == data.Key)
                return;

            RebuildGridMesh(data);
        }

        private static void RebuildGridMesh(SceneViewData data)
        {
            _gridMeshKey = data.Key;

            if (_gridMesh)
                Object.DestroyImmediate(_gridMesh);

            IReadOnlyList<Vector3> gridPositions = GetGridPositions(data);
            float cellSize = data.StyleSettings.grid.cellSize;

            if (gridPositions.Count == 0 || cellSize <= 0f)
                return;

            _gridMesh = new Mesh { name = "Genix Grid Diagnostics Mesh", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };

            float halfSize = cellSize * 0.5f - GridCellPadding;

            List<Vector3> vertices = new(gridPositions.Count * 4);
            List<int> triangles = new(gridPositions.Count * 6);
            List<Color> colors = new(gridPositions.Count * 4);

            foreach (Vector3 position in gridPositions)
                AddGridCell(vertices, triangles, colors, position, halfSize);

            _gridMesh.SetVertices(vertices);
            _gridMesh.SetTriangles(triangles, 0);
            _gridMesh.SetColors(colors);
            _gridMesh.RecalculateBounds();
        }

        private static void AddGridCell(List<Vector3> vertices, List<int> triangles, List<Color> colors, Vector3 center, float halfSize)
        {
            if (halfSize <= 0f)
                return;

            int startIndex = vertices.Count;
            float y = center.y + GridYOffset;

            vertices.Add(new Vector3(center.x - halfSize, y, center.z - halfSize));
            vertices.Add(new Vector3(center.x + halfSize, y, center.z - halfSize));
            vertices.Add(new Vector3(center.x + halfSize, y, center.z + halfSize));
            vertices.Add(new Vector3(center.x - halfSize, y, center.z + halfSize));

            colors.Add(GridCellColor);
            colors.Add(GridCellColor);
            colors.Add(GridCellColor);
            colors.Add(GridCellColor);

            triangles.Add(startIndex);
            triangles.Add(startIndex + 1);
            triangles.Add(startIndex + 2);

            triangles.Add(startIndex);
            triangles.Add(startIndex + 2);
            triangles.Add(startIndex + 3);
        }

        private static void DrawGridWireframe(SceneViewData data, IReadOnlyList<Vector3> gridPositions)
        {
            float cellSize = data.StyleSettings.grid.cellSize;
            float halfSize = cellSize * 0.5f - GridCellPadding;

            if (halfSize <= 0f)
                return;

            Handles.color = GridWireColor;

            foreach (Vector3 position in gridPositions)
                DrawGridCellWire(position, halfSize);
        }

        private static void DrawGridCellWire(Vector3 center, float halfSize)
        {
            float y = center.y + GridWireYOffset;

            Vector3 a = new(center.x - halfSize, y, center.z - halfSize);
            Vector3 b = new(center.x + halfSize, y, center.z - halfSize);
            Vector3 c = new(center.x + halfSize, y, center.z + halfSize);
            Vector3 d = new(center.x - halfSize, y, center.z + halfSize);

            Handles.DrawAAPolyLine(GridWireWidth, a, b, c, d, a);
        }

        private static void EnsureGridMaterial()
        {
            if (_gridMaterial)
                return;

            _gridMaterial = CreatePreviewMaterial();
        }

    }
}
