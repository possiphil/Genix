using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Assets;
using Genix.Geometry;
using Genix.Orientation;
using Genix.Placement;
using Genix.Semantics;
using Genix.Styles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Evaluation
{
    internal static partial class OutdoorEvaluationSetupUtility
    {
        private static PathFrame CreatePathFrame(
            IReadOnlyList<Vector3> points,
            BoxCollider boundary,
            Terrain terrain)
        {
            if (points.Count < 4)
                throw new InvalidOperationException("Path meshes did not expose enough geometry for trailhead setup.");

            Vector3 terrainCenter = terrain.transform.position + terrain.terrainData.size * 0.5f;
            Vector3 inward = Vector3.ProjectOnPlane(terrainCenter - boundary.bounds.center, Vector3.up).normalized;
            if (inward.sqrMagnitude <= 0.001f)
                inward = Vector3.right;

            float minimum = points.Min(point => Vector3.Dot(point, inward));
            Vector3 entry = Average(points.Where(point => Vector3.Dot(point, inward) <= minimum + 1.1f));
            float maximumProgress = points.Max(point => Vector3.Dot(point - entry, inward));
            if (maximumProgress < 2f)
                throw new InvalidOperationException("Path does not extend far enough away from Boundary Left.");

            return new PathFrame(points, entry, inward, maximumProgress);
        }

        private static Transform[] GetPathSegments(GameObject path) => path.transform
            .Cast<Transform>()
            .Where(child => child.GetComponents<Component>().Any(component =>
                component && component.GetType().FullName == "UnityEngine.Splines.SplineContainer"))
            .ToArray();

        private static List<PathStation> CreatePathStations(
            IEnumerable<PathFrame> frames,
            float spacing,
            float endpointMargin)
        {
            List<PathStation> stations = new();
            foreach (PathFrame frame in frames)
            {
                List<Vector3> polyline = new();
                const float sampleStep = 0.5f;
                for (float progress = 0f; progress < frame.MaximumProgress; progress += sampleStep)
                    AddDistinctPoint(polyline, frame.Sample(progress));
                AddDistinctPoint(polyline, frame.Sample(frame.MaximumProgress));

                if (polyline.Count < 2)
                    continue;

                float[] distances = new float[polyline.Count];
                for (int i = 1; i < polyline.Count; i++)
                {
                    distances[i] = distances[i - 1] +
                                   Vector3.ProjectOnPlane(polyline[i] - polyline[i - 1], Vector3.up).magnitude;
                }

                float length = distances[^1];
                if (length < 1f)
                    continue;

                float firstDistance = Mathf.Min(endpointMargin, length * 0.5f);
                float lastDistance = Mathf.Max(firstDistance, length - endpointMargin);
                if (lastDistance - firstDistance < spacing * 0.5f)
                {
                    stations.Add(SamplePolyline(polyline, distances, length * 0.5f));
                    continue;
                }

                for (float distance = firstDistance; distance <= lastDistance + 0.01f; distance += spacing)
                    stations.Add(SamplePolyline(polyline, distances, distance));
            }

            return stations;
        }

        private static PathStation SamplePolyline(
            IReadOnlyList<Vector3> points,
            IReadOnlyList<float> distances,
            float distance)
        {
            int next = 1;
            while (next < distances.Count && distances[next] < distance)
                next++;
            next = Mathf.Clamp(next, 1, points.Count - 1);
            int previous = next - 1;
            float segmentLength = Mathf.Max(0.0001f, distances[next] - distances[previous]);
            float t = Mathf.Clamp01((distance - distances[previous]) / segmentLength);
            Vector3 position = Vector3.Lerp(points[previous], points[next], t);
            Vector3 forward = Vector3.ProjectOnPlane(points[next] - points[previous], Vector3.up).normalized;
            return new PathStation(position, forward);
        }

        private static void AddDistinctPoint(List<Vector3> points, Vector3 point)
        {
            if (points.Count == 0 ||
                Vector3.ProjectOnPlane(points[^1] - point, Vector3.up).sqrMagnitude > 0.01f)
            {
                points.Add(point);
            }
        }

        private static IReadOnlyList<Vector3> CollectPathPoints(GameObject path)
        {
            List<Vector3> points = new();
            foreach (MeshFilter filter in path.GetComponentsInChildren<MeshFilter>(true))
                AddMeshPoints(filter.sharedMesh, filter.transform, points);

            foreach (MeshCollider collider in path.GetComponentsInChildren<MeshCollider>(true))
                AddMeshPoints(collider.sharedMesh, collider.transform, points);

            if (points.Count > 0)
                return points;

            foreach (Renderer renderer in path.GetComponentsInChildren<Renderer>(true))
            {
                Bounds bounds = renderer.bounds;
                points.Add(bounds.min);
                points.Add(bounds.max);
                points.Add(new Vector3(bounds.min.x, bounds.center.y, bounds.max.z));
                points.Add(new Vector3(bounds.max.x, bounds.center.y, bounds.min.z));
            }

            return points;
        }

        private static void AddMeshPoints(Mesh mesh, Transform transform, ICollection<Vector3> points)
        {
            if (!mesh)
                return;

            Vector3[] vertices = mesh.vertices;
            int step = Mathf.Max(1, vertices.Length / 5000);
            for (int i = 0; i < vertices.Length; i += step)
                points.Add(transform.TransformPoint(vertices[i]));
        }

        private static float ChooseTrailSide(PathFrame frame, Terrain terrain, Collider water)
        {
            float progress = Mathf.Clamp(frame.MaximumProgress * 0.08f, 2f, 4.5f);
            float positive = ScoreSide(frame, terrain, water, progress, 1f, 3.2f);
            float negative = ScoreSide(frame, terrain, water, progress, -1f, 3.2f);
            return positive >= negative ? 1f : -1f;
        }

        private static float ResolveUsableSide(
            PathFrame frame,
            Terrain terrain,
            Collider water,
            float progressFraction,
            float preferredSide)
        {
            float progress = Mathf.Clamp(frame.MaximumProgress * progressFraction, 2f, frame.MaximumProgress - 1f);
            float preferred = ScoreSide(frame, terrain, water, progress, preferredSide, 2.45f);
            float opposite = ScoreSide(frame, terrain, water, progress, -preferredSide, 2.45f);
            return preferred >= opposite ? preferredSide : -preferredSide;
        }

        private static float ScoreSide(
            PathFrame frame,
            Terrain terrain,
            Collider water,
            float progress,
            float side,
            float offset)
        {
            Vector3 center = frame.Sample(progress);
            Vector3 right = Vector3.Cross(Vector3.up, frame.Tangent(progress)).normalized;
            Vector3 point = center + right * offset * side;
            Bounds terrainBounds = new(
                terrain.transform.position + terrain.terrainData.size * 0.5f,
                terrain.terrainData.size);
            if (!terrainBounds.Contains(new Vector3(point.x, terrainBounds.center.y, point.z)))
                return float.NegativeInfinity;

            Vector3 terrainPoint = GetTerrainPoint(terrain, point);
            float score = -GetTerrainSteepness(terrain, terrainPoint);
            if (water && water.bounds.Contains(new Vector3(point.x, water.bounds.center.y, point.z)) &&
                terrainPoint.y <= water.bounds.max.y + 0.15f)
            {
                score -= 1000f;
            }

            return score;
        }

        private static Vector3 GetTerrainPoint(Terrain terrain, Vector3 position)
        {
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position;
        }

        private static float GetTerrainSteepness(Terrain terrain, Vector3 position)
        {
            Vector3 local = position - terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            return terrain.terrainData.GetSteepness(
                Mathf.Clamp01(local.x / Mathf.Max(0.01f, size.x)),
                Mathf.Clamp01(local.z / Mathf.Max(0.01f, size.z)));
        }

        private static Vector3 Average(IEnumerable<Vector3> values)
        {
            Vector3 total = Vector3.zero;
            int count = 0;
            foreach (Vector3 value in values)
            {
                total += value;
                count++;
            }

            return count > 0 ? total / count : Vector3.zero;
        }
    }
}

