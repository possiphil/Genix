using System;
using System.Collections.Generic;
using Genix.Assets;
using Genix.Layouts;
using Genix.Profiling;
using Genix.Semantics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Areas
{
    internal sealed partial class SurfaceProjector
    {
        /// <summary>
        /// Probes an asset footprint and derives support ratio, placement height, and an optional fitted normal.
        /// </summary>
        /// <remarks>Returns false when support or height variation violates the asset's adaptive-fit constraints.</remarks>
        public bool TryEvaluateSurfaceFit(
            Vector3 surfaceCenter,
            Quaternion footprintRotation,
            AssetDefinition asset,
            Collider expectedSurfaceCollider,
            int? voxelLayer,
            PlacementType placementType,
            out SurfaceFitResult result,
            IGenerationProfiler profiler = null)
        {
            result = default;

            if (!asset || placementType == PlacementType.InsideSpace)
                return false;

            if (placementType == PlacementType.Wall)
            {
                return TryEvaluateWallSurfaceFit(
                    surfaceCenter,
                    footprintRotation,
                    asset,
                    expectedSurfaceCollider,
                    voxelLayer,
                    out result,
                    profiler);
            }

            Vector3 right = NormalizeOrFallback(footprintRotation * Vector3.right, Vector3.right);
            Vector3 forward = NormalizeOrFallback(footprintRotation * Vector3.forward, Vector3.forward);
            float width = Mathf.Max(0.01f, asset.Width);
            float depth = Mathf.Max(0.01f, asset.Depth);

            if (asset.MinSurfaceSupport >= FullSurfaceSupportThreshold &&
                !IsFootprintInsideWorldBoundsXZ(surfaceCenter, right, forward, width, depth))
            {
                return false;
            }

            int widthSegments = _occupancy.GetFootprintSegmentCount(width);
            int depthSegments = _occupancy.GetFootprintSegmentCount(depth);
            int totalSamples = (widthSegments + 1) * (depthSegments + 1);
            int processedSamples = 0;
            int supportedSamples = 0;
            int requiredSupportedSamples = Mathf.CeilToInt(
                Mathf.Max(0f, asset.MinSurfaceSupport - 0.0001f) * totalSamples);
            float maxHeightDifference = asset.MaxSurfaceHeightDifference;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            float sumY = 0f;
            Vector3 normalSum = Vector3.zero;

            for (int x = 0; x <= widthSegments; x++)
            {
                float offsetX = Mathf.Lerp(-width * 0.5f, width * 0.5f, x / (float)widthSegments);

                for (int z = 0; z <= depthSegments; z++)
                {
                    processedSamples++;
                    float offsetZ = Mathf.Lerp(-depth * 0.5f, depth * 0.5f, z / (float)depthSegments);
                    Vector3 samplePosition = surfaceCenter + right * offsetX + forward * offsetZ;

                    if (!TryFindSupportPoint(
                            samplePosition,
                            expectedSurfaceCollider,
                            voxelLayer,
                            placementType,
                            out SurfacePoint support,
                            profiler))
                    {
                        if (!CanStillReachRequiredSupport(
                                supportedSamples,
                                processedSamples,
                                totalSamples,
                                requiredSupportedSamples))
                        {
                            return false;
                        }

                        continue;
                    }

                    supportedSamples++;
                    minY = Mathf.Min(minY, support.Position.y);
                    maxY = Mathf.Max(maxY, support.Position.y);

                    if (maxY - minY > maxHeightDifference)
                    {
                        return false;
                    }

                    sumY += support.Position.y;
                    normalSum += support.Normal.normalized;
                }
            }

            if (supportedSamples == 0)
                return false;

            float supportRatio = supportedSamples / (float)Mathf.Max(1, totalSamples);

            if (supportRatio + 0.0001f < asset.MinSurfaceSupport)
                return false;

            float heightDifference = maxY - minY;

            if (heightDifference > asset.MaxSurfaceHeightDifference)
                return false;

            float surfaceY = asset.SurfaceHeightMode switch
            {
                SurfaceHeightMode.Lowest => minY,
                SurfaceHeightMode.Highest => maxY,
                _ => sumY / supportedSamples
            };
            Vector3 normal = normalSum.sqrMagnitude > 0.001f
                ? normalSum.normalized
                : placementType == PlacementType.Ceiling ? Vector3.down : Vector3.up;
            result = new SurfaceFitResult(
                new Vector3(surfaceCenter.x, surfaceY, surfaceCenter.z),
                normal,
                heightDifference,
                supportRatio);
            return true;
        }

        private bool TryEvaluateWallSurfaceFit(
            Vector3 surfaceCenter,
            Quaternion footprintRotation,
            AssetDefinition asset,
            Collider expectedSurfaceCollider,
            int? voxelLayer,
            out SurfaceFitResult result,
            IGenerationProfiler profiler)
        {
            result = default;
            Vector3 normal = NormalizeOrFallback(footprintRotation * Vector3.forward, Vector3.forward);
            Vector3 right = NormalizeOrFallback(footprintRotation * Vector3.right, Vector3.right);
            Vector3 up = NormalizeOrFallback(footprintRotation * Vector3.up, Vector3.up);
            float width = Mathf.Max(0.01f, asset.Width);
            float height = Mathf.Max(0.01f, asset.Height);
            int widthSegments = _occupancy.GetFootprintSegmentCount(width);
            int heightSegments = _occupancy.GetFootprintSegmentCount(height);
            int totalSamples = (widthSegments + 1) * (heightSegments + 1);
            int processedSamples = 0;
            int supportedSamples = 0;
            int requiredSupportedSamples = Mathf.CeilToInt(
                Mathf.Max(0f, asset.MinSurfaceSupport - 0.0001f) * totalSamples);
            float minDepth = float.PositiveInfinity;
            float maxDepth = float.NegativeInfinity;
            Vector3 normalSum = Vector3.zero;
            Span<Vector3> supportPositions = stackalloc Vector3[totalSamples];

            for (int x = 0; x <= widthSegments; x++)
            {
                float offsetX = Mathf.Lerp(-width * 0.5f, width * 0.5f, x / (float)widthSegments);

                for (int y = 0; y <= heightSegments; y++)
                {
                    processedSamples++;
                    float offsetY = Mathf.Lerp(-height * 0.5f, height * 0.5f, y / (float)heightSegments);
                    Vector3 samplePosition = surfaceCenter + right * offsetX + up * offsetY;

                    if (!TryFindWallSupportPoint(
                            samplePosition,
                            normal,
                            asset.MaxSurfaceHeightDifference,
                            expectedSurfaceCollider,
                            voxelLayer,
                            out SurfacePoint support,
                            profiler))
                    {
                        if (!CanStillReachRequiredSupport(
                                supportedSamples,
                                processedSamples,
                                totalSamples,
                                requiredSupportedSamples))
                        {
                            return false;
                        }

                        continue;
                    }

                    supportPositions[supportedSamples] = support.Position;
                    supportedSamples++;
                    float depth = Vector3.Dot(support.Position - samplePosition, normal);
                    minDepth = Mathf.Min(minDepth, depth);
                    maxDepth = Mathf.Max(maxDepth, depth);

                    if (maxDepth - minDepth > asset.MaxSurfaceHeightDifference)
                        return false;

                    Vector3 supportNormal = support.Normal.normalized;
                    normalSum += Vector3.Dot(supportNormal, normal) < 0f ? -supportNormal : supportNormal;
                }
            }

            if (supportedSamples == 0)
                return false;

            float supportRatio = supportedSamples / (float)Mathf.Max(1, totalSamples);

            if (supportRatio + 0.0001f < asset.MinSurfaceSupport)
                return false;

            Vector3 fittedNormal = normalSum.sqrMagnitude > 0.001f ? normalSum.normalized : normal;
            float fittedMinDepth = float.PositiveInfinity;
            float fittedMaxDepth = float.NegativeInfinity;
            float fittedDepthSum = 0f;

            for (int i = 0; i < supportedSamples; i++)
            {
                float fittedDepth = Vector3.Dot(supportPositions[i] - surfaceCenter, fittedNormal);
                fittedMinDepth = Mathf.Min(fittedMinDepth, fittedDepth);
                fittedMaxDepth = Mathf.Max(fittedMaxDepth, fittedDepth);
                fittedDepthSum += fittedDepth;
            }

            float depthDifference = fittedMaxDepth - fittedMinDepth;
            if (depthDifference > asset.MaxSurfaceHeightDifference)
                return false;

            float surfaceDepth = asset.SurfaceHeightMode switch
            {
                SurfaceHeightMode.Lowest => fittedMinDepth,
                SurfaceHeightMode.Highest => fittedMaxDepth,
                _ => fittedDepthSum / supportedSamples
            };
            result = new SurfaceFitResult(
                surfaceCenter + fittedNormal * surfaceDepth,
                fittedNormal,
                depthDifference,
                supportRatio);
            return true;
        }

        private static bool CanStillReachRequiredSupport(
            int supportedSamples,
            int processedSamples,
            int totalSamples,
            int requiredSupportedSamples)
        {
            int remainingSamples = Mathf.Max(0, totalSamples - processedSamples);
            return supportedSamples + remainingSamples >= requiredSupportedSamples;
        }
    }
}
