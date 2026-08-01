using System.Collections.Generic;
using UnityEngine;

namespace Genix.Areas
{
    public enum AreaBuildProfileStep
    {
        SubspaceResolve,
        LiveCacheStore,
        VoxelMaskBuild,
        VoxelScan,
        SurfaceExtraction,
        SurfaceRegionBuild,
        WallExtraction,
        WallRegionBuild,
        OccupancyBuild,
        SceneIndex,
        AreaCacheLookup,
        AreaCacheStore
    }

    public sealed class AreaBuildProfile
    {
        private readonly Dictionary<AreaBuildProfileStep, AreaBuildStepProfile> _steps = new();

        public IReadOnlyCollection<AreaBuildStepProfile> Steps => _steps.Values;

        public void AddStepTime(AreaBuildProfileStep step, float milliseconds)
        {
            if (!_steps.TryGetValue(step, out AreaBuildStepProfile profile))
            {
                profile = new AreaBuildStepProfile(step);
                _steps[step] = profile;
            }

            profile.Add(milliseconds);
        }
    }

    public sealed class AreaBuildStepProfile
    {
        public AreaBuildProfileStep Step { get; }
        public int Calls { get; private set; }
        public float Milliseconds { get; private set; }

        public AreaBuildStepProfile(AreaBuildProfileStep step)
        {
            Step = step;
        }

        public void Add(float milliseconds)
        {
            Calls++;
            Milliseconds += Mathf.Max(0f, milliseconds);
        }
    }
}
