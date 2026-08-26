using System.Collections.Generic;
using UnityEngine;

namespace Genix.Authoring
{
    /// <summary>Visualizes the exact free voxel volumes stored by an SFS-authored layout.</summary>
    [AddComponentMenu("Genix/SFS Authoring Layout Display")]
    [DisallowMultipleComponent]
    public sealed class SfsAuthoringLayoutDisplay : MonoBehaviour
    {
        [SerializeField, Tooltip("Keep the authored free-space volumes visible when this layout is not selected.")]
        private bool alwaysShowFreeSpace;

        [SerializeField, HideInInspector] private List<Bounds> localVolumes = new();

        /// <summary>Gets whether the free-space volumes remain visible while the layout is not selected.</summary>
        public bool AlwaysShowFreeSpace => alwaysShowFreeSpace;

        /// <summary>Gets the free-space volumes stored relative to the layout transform.</summary>
        public IReadOnlyList<Bounds> LocalVolumes => localVolumes;

        /// <summary>Replaces the local free-space volumes represented by this display.</summary>
        public void Configure(IEnumerable<Bounds> volumes)
        {
            localVolumes ??= new List<Bounds>();
            localVolumes.Clear();

            if (volumes == null)
                return;

            foreach (Bounds volume in volumes)
            {
                if (volume.size.x <= 0f || volume.size.y <= 0f || volume.size.z <= 0f)
                    continue;

                localVolumes.Add(volume);
            }
        }

        private void OnDrawGizmos()
        {
            if (alwaysShowFreeSpace)
                DrawVolumes(selected: false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawVolumes(selected: true);
        }

        private void DrawVolumes(bool selected)
        {
            if (localVolumes == null || localVolumes.Count == 0)
                return;

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = transform.localToWorldMatrix;

            Color fill = selected
                ? new Color(0.2f, 0.85f, 0.55f, 0.08f)
                : new Color(0.2f, 0.85f, 0.55f, 0.025f);
            Color wire = selected
                ? new Color(0.2f, 0.85f, 0.55f, 0.95f)
                : new Color(0.2f, 0.85f, 0.55f, 0.45f);

            foreach (Bounds volume in localVolumes)
            {
                Gizmos.color = fill;
                Gizmos.DrawCube(volume.center, volume.size);
                Gizmos.color = wire;
                Gizmos.DrawWireCube(volume.center, volume.size);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
