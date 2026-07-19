using UnityEngine;
using SfsAnchor = SpaceFoundationSystem.Anchor;
using SfsFoundation = SpaceFoundationSystem.SpaceFoundation;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    internal static class SfsFoundationUtility
    {
        public static SfsFoundation Find(SfsSpace space, SfsAnchor anchor)
        {
            if (anchor && anchor.correspondingSpaceFoundation)
                return anchor.correspondingSpaceFoundation;

            return space ? space.GetComponentInParent<SfsFoundation>() : null;
        }

        public static float GetVoxelSize(SfsFoundation foundation)
        {
            return foundation
                ? Mathf.Max(0.01f, foundation.voxelSize)
                : Mathf.Max(0.01f, SfsFoundation.s_VoxelSize);
        }
    }
}
