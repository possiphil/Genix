using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SfsAnchor = SpaceFoundationSystem.Anchor;
using SfsFoundation = SpaceFoundationSystem.SpaceFoundation;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Resolves the owning SFS foundation and normalized voxel size for a selected space.</summary>
    internal static class SfsFoundationUtility
    {
        private static readonly Dictionary<EntityId, CacheIdentityEntry> CacheIdentities = new();

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

        /// <summary>Returns an identity that remains stable when a saved scene or prefab is reloaded.</summary>
        public static string CreateCacheIdentity(SfsFoundation foundation)
        {
            if (!foundation)
                return string.Empty;

            EntityId entityId = foundation.GetEntityId();
            string assetName = foundation.assetName ?? string.Empty;

            if (CacheIdentities.TryGetValue(entityId, out CacheIdentityEntry cached) &&
                cached.Foundation == foundation &&
                cached.AssetName == assetName)
            {
                return cached.Identity;
            }

            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(foundation);
            string identity = globalId.identifierType != 0 && globalId.targetObjectId != 0
                ? $"global:{globalId}:{assetName}"
                : $"session:{entityId}:{assetName}";

            CacheIdentities[entityId] = new CacheIdentityEntry(foundation, assetName, identity);
            return identity;
        }

        internal static void ClearCacheIdentitiesForTests() => CacheIdentities.Clear();

        private readonly struct CacheIdentityEntry
        {
            public SfsFoundation Foundation { get; }
            public string AssetName { get; }
            public string Identity { get; }

            public CacheIdentityEntry(SfsFoundation foundation, string assetName, string identity)
            {
                Foundation = foundation;
                AssetName = assetName;
                Identity = identity;
            }
        }
    }
}
