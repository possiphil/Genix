using UnityEngine;

namespace Genix.Geometry
{
    /// <summary>Provides reusable bounds operations.</summary>
    public static class BoundsUtility
    {
        /// <summary>Attempts to get renderer bounds.</summary>
        public static bool TryGetRendererBounds(
            Transform root,
            out Bounds bounds,
            bool includeInactive = false,
            bool requireEnabled = true)
        {
            bounds = default;

            if (!root)
                return false;

            bool hasBounds = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(includeInactive))
            {
                if (!renderer || requireEnabled && !renderer.enabled)
                    continue;

                Encapsulate(renderer.bounds, ref bounds, ref hasBounds);
            }

            return hasBounds;
        }

        /// <summary>Attempts to get collider bounds.</summary>
        public static bool TryGetColliderBounds(
            Transform root,
            out Bounds bounds,
            bool includeInactive = false,
            bool requireEnabled = true)
        {
            bounds = default;

            if (!root)
                return false;

            bool hasBounds = false;

            foreach (Collider collider in root.GetComponentsInChildren<Collider>(includeInactive))
            {
                if (!collider || requireEnabled && !collider.enabled)
                    continue;

                Encapsulate(collider.bounds, ref bounds, ref hasBounds);
            }

            return hasBounds;
        }

        /// <summary>Attempts to get combined bounds.</summary>
        public static bool TryGetCombinedBounds(
            Transform root,
            out Bounds bounds,
            bool includeInactive = true,
            bool requireEnabled = true)
        {
            bool hasRenderers = TryGetRendererBounds(
                root,
                out Bounds rendererBounds,
                includeInactive,
                requireEnabled);
            bool hasColliders = TryGetColliderBounds(
                root,
                out Bounds colliderBounds,
                includeInactive,
                requireEnabled);

            if (!hasRenderers && !hasColliders)
            {
                bounds = new Bounds(root ? root.position : Vector3.zero, Vector3.zero);
                return false;
            }

            bounds = hasRenderers ? rendererBounds : colliderBounds;

            if (hasRenderers && hasColliders)
                bounds.Encapsulate(colliderBounds);

            return true;
        }

        private static void Encapsulate(Bounds value, ref Bounds bounds, ref bool hasBounds)
        {
            if (!hasBounds)
            {
                bounds = value;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(value);
            }
        }
    }
}
