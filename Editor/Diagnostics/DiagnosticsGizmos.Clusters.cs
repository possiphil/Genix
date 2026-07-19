using System.Collections.Generic;
using Genix.Sampling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Genix.Editor.Diagnostics
{
    public static partial class DiagnosticsGizmos
    {
        private static void DrawClusters(SceneViewData data)
        {
            if (data.SamplingAlgorithm != SamplingAlgorithm.Cluster)
                return;

            if (data.ClusterCenters.Count == 0)
                return;

            if (data.StyleSettings.cluster.radius <= 0f)
                return;

            EnsureClusterMesh(data);
            EnsureClusterMaterial();

            _clusterMaterial.SetPass(0);
            Graphics.DrawMeshNow(_clusterMesh, Matrix4x4.identity);

            DrawClusterWireframes(data);
        }

        private static void EnsureClusterMesh(SceneViewData data)
        {
            if (_clusterMesh != null && _clusterMeshKey == data.Key)
                return;

            RebuildClusterMesh(data);
        }

        private static void RebuildClusterMesh(SceneViewData data)
        {
            IReadOnlyList<Vector3> centers = data.ClusterCenters;
            float radius = data.StyleSettings.cluster.radius;

            _clusterMeshKey = data.Key;

            if (_clusterMesh)
                Object.DestroyImmediate(_clusterMesh);

            _clusterMesh = new Mesh { name = "Genix Cluster Diagnostics Mesh", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };

            int verticesPerCluster = ClusterCircleSegments + 1;
            int trianglesPerCluster = ClusterCircleSegments * 3;

            List<Vector3> vertices = new(centers.Count * verticesPerCluster);
            List<int> triangles = new(centers.Count * trianglesPerCluster);
            List<Color> colors = new(centers.Count * verticesPerCluster);

            foreach (Vector3 center in centers)
                AddClusterFillDisc(vertices, triangles, colors, center, radius);

            _clusterMesh.SetVertices(vertices);
            _clusterMesh.SetTriangles(triangles, 0);
            _clusterMesh.SetColors(colors);
            _clusterMesh.RecalculateBounds();
        }

        private static void AddClusterFillDisc(List<Vector3> vertices, List<int> triangles, List<Color> colors, Vector3 center, float radius)
        {
            int centerIndex = vertices.Count;
            Vector3 discCenter = center + Vector3.up * ClusterYOffset;

            vertices.Add(discCenter);
            colors.Add(ClusterFillColor);

            for (int i = 0; i < ClusterCircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / ClusterCircleSegments;

                vertices.Add(new Vector3(discCenter.x + Mathf.Cos(angle) * radius, discCenter.y, discCenter.z + Mathf.Sin(angle) * radius));
                colors.Add(ClusterFillColor);
            }

            for (int i = 0; i < ClusterCircleSegments; i++)
            {
                int current = centerIndex + 1 + i;
                int next = centerIndex + 1 + ((i + 1) % ClusterCircleSegments);

                triangles.Add(centerIndex);
                triangles.Add(current);
                triangles.Add(next);
            }
        }

        private static void DrawClusterWireframes(SceneViewData data)
        {
            float radius = data.StyleSettings.cluster.radius;

            if (radius <= 0f)
                return;

            Handles.color = ClusterWireColor;

            foreach (Vector3 center in data.ClusterCenters)
                DrawClusterCircle(center, radius);
        }

        private static void DrawClusterCircle(Vector3 center, float radius)
        {
            Vector3[] points = new Vector3[ClusterCircleSegments + 1];
            float y = center.y + ClusterWireYOffset;

            for (int i = 0; i <= ClusterCircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / ClusterCircleSegments;

                points[i] = new Vector3(center.x + Mathf.Cos(angle) * radius, y, center.z + Mathf.Sin(angle) * radius);
            }

            Handles.DrawAAPolyLine(ClusterWireWidth, points);
        }

        private static void EnsureClusterMaterial()
        {
            if (_clusterMaterial)
                return;

            _clusterMaterial = CreatePreviewMaterial();
        }

    }
}
