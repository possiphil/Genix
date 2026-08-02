using System.Collections.Generic;
using UnityEngine;

namespace Genix.Areas
{
    /// <summary>Available area build profile step values.</summary>
    public enum AreaBuildProfileStep
    {
        /// <summary>Identifies the subspace resolve area-build profiler step.</summary>
        SubspaceResolve,
        /// <summary>Identifies the live cache store area-build profiler step.</summary>
        LiveCacheStore,
        /// <summary>Identifies the voxel mask build area-build profiler step.</summary>
        VoxelMaskBuild,
        /// <summary>Identifies the voxel scan area-build profiler step.</summary>
        VoxelScan,
        /// <summary>Identifies the surface extraction area-build profiler step.</summary>
        SurfaceExtraction,
        /// <summary>Identifies the surface region build area-build profiler step.</summary>
        SurfaceRegionBuild,
        /// <summary>Identifies the wall extraction area-build profiler step.</summary>
        WallExtraction,
        /// <summary>Identifies the wall region build area-build profiler step.</summary>
        WallRegionBuild,
        /// <summary>Identifies the occupancy build area-build profiler step.</summary>
        OccupancyBuild,
        /// <summary>Identifies the scene index area-build profiler step.</summary>
        SceneIndex,
        /// <summary>Identifies the area cache lookup area-build profiler step.</summary>
        AreaCacheLookup,
        /// <summary>Identifies the area cache store area-build profiler step.</summary>
        AreaCacheStore
    }

    /// <summary>Stores area build measurements.</summary>
    public sealed class AreaBuildProfile
    {
        private readonly Dictionary<AreaBuildProfileStep, AreaBuildStepProfile> _steps = new();

        /// <summary>Gets steps.</summary>
        public IReadOnlyCollection<AreaBuildStepProfile> Steps => _steps.Values;

        /// <summary>Adds step time.</summary>
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

    /// <summary>Stores area build step measurements.</summary>
    public sealed class AreaBuildStepProfile
    {
        /// <summary>Gets step.</summary>
        public AreaBuildProfileStep Step { get; }
        /// <summary>Gets the number of recorded  calls.</summary>
        public int Calls { get; private set; }
        /// <summary>Gets the measured  time in milliseconds.</summary>
        public float Milliseconds { get; private set; }

        /// <summary>Initializes a new instance of area build step profile.</summary>
        public AreaBuildStepProfile(AreaBuildProfileStep step)
        {
            Step = step;
        }

        /// <summary>Adds .</summary>
        public void Add(float milliseconds)
        {
            Calls++;
            Milliseconds += Mathf.Max(0f, milliseconds);
        }
    }
}
