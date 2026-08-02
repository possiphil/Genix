using UnityEngine;

namespace Genix.Layouts
{
    /// <summary>Stores scene and target-area provenance on the root object of a saved layout.</summary>
    public sealed class SavedLayoutRoot : MonoBehaviour
    {
        [SerializeField] private string displayName;
        [SerializeField] private string sceneName;
        [SerializeField] private string scenePath;
        [SerializeField] private string targetAreaName;
        [SerializeField] private string targetAreaId;
        [SerializeField] private string createdAt;
        [SerializeField] private int objectCount;

        /// <summary>Gets display name.</summary>
        public string DisplayName => displayName;
        /// <summary>Gets scene name.</summary>
        public string SceneName => sceneName;
        /// <summary>Gets scene path.</summary>
        public string ScenePath => scenePath;
        /// <summary>Gets target area name.</summary>
        public string TargetAreaName => targetAreaName;
        /// <summary>Gets target area id.</summary>
        public string TargetAreaId => targetAreaId;
        /// <summary>Gets created at.</summary>
        public string CreatedAt => createdAt;
        /// <summary>Gets the number of object items.</summary>
        public int ObjectCount => objectCount;

        /// <summary>Initializes the instance from the supplied runtime or serialized data.</summary>
        public void Initialize(
            string layoutName,
            string layoutSceneName,
            string layoutScenePath,
            string areaName,
            string areaId,
            string creationTime,
            int count)
        {
            displayName = layoutName;
            sceneName = layoutSceneName;
            scenePath = layoutScenePath;
            targetAreaName = areaName;
            targetAreaId = areaId;
            createdAt = creationTime;
            objectCount = Mathf.Max(0, count);
            hideFlags = HideFlags.HideInInspector;
        }
    }
}
