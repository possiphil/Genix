using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Genix.Editor.Diagnostics
{
    public static partial class DiagnosticsGizmos
    {
        private static void DrawCandidateSeeds(SceneViewData data)
        {
            if (data.CandidateSeeds.Count == 0)
                return;

            EnsureCandidateSeedMesh(data);
            EnsureCandidateSeedMaterial();

            _candidateSeedMaterial.SetPass(0);
            Graphics.DrawMeshNow(_candidateSeedMesh, Matrix4x4.identity);
        }

        private static void EnsureCandidateSeedMesh(SceneViewData data)
        {
            if (_candidateSeedMesh != null && _candidateSeedMeshKey == data.Key)
                return;

            RebuildCandidateSeedMesh(data);
        }

        private static void RebuildCandidateSeedMesh(SceneViewData data)
        {
            IReadOnlyList<Vector3> seeds = data.CandidateSeeds;

            _candidateSeedMeshKey = data.Key;

            if (_candidateSeedMesh)
                Object.DestroyImmediate(_candidateSeedMesh);

            _candidateSeedMesh = new Mesh { name = "Genix Candidate Seed Diagnostics Mesh", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };

            int verticesPerSeed = SeedCircleSegments + 1;
            int trianglesPerSeed = SeedCircleSegments * 3;

            List<Vector3> vertices = new(seeds.Count * verticesPerSeed);
            List<int> triangles = new(seeds.Count * trianglesPerSeed);
            List<Color> colors = new(seeds.Count * verticesPerSeed);

            foreach (Vector3 seed in seeds)
                AddSeedDisc(vertices, triangles, colors, seed);

            _candidateSeedMesh.SetVertices(vertices);
            _candidateSeedMesh.SetTriangles(triangles, 0);
            _candidateSeedMesh.SetColors(colors);
            _candidateSeedMesh.RecalculateBounds();
        }

        private static void AddSeedDisc(List<Vector3> vertices, List<int> triangles, List<Color> colors, Vector3 center)
        {
            int centerIndex = vertices.Count;
            Vector3 discCenter = center + Vector3.up * SeedYOffset;

            vertices.Add(discCenter);
            colors.Add(SeedColor);

            for (int i = 0; i < SeedCircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / SeedCircleSegments;

                vertices.Add(new Vector3(discCenter.x + Mathf.Cos(angle) * SeedWorldRadius, discCenter.y, discCenter.z + Mathf.Sin(angle) * SeedWorldRadius));
                colors.Add(SeedColor);
            }

            for (int i = 0; i < SeedCircleSegments; i++)
            {
                int current = centerIndex + 1 + i;
                int next = centerIndex + 1 + ((i + 1) % SeedCircleSegments);

                triangles.Add(centerIndex);
                triangles.Add(current);
                triangles.Add(next);
            }
        }

        private static void EnsureCandidateSeedMaterial()
        {
            if (_candidateSeedMaterial)
                return;

            _candidateSeedMaterial = CreatePreviewMaterial();
        }

    }
}
