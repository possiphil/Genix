using UnityEngine;

namespace Genix.Layouts
{
    public sealed class SavedLayoutRoot : MonoBehaviour
    {
        [SerializeField] private string displayName;
        [SerializeField] private string targetAreaName;
        [SerializeField] private string targetAreaId;
        [SerializeField] private string createdAt;
        [SerializeField] private int objectCount;

        public string DisplayName => displayName;
        public string TargetAreaName => targetAreaName;
        public string TargetAreaId => targetAreaId;
        public string CreatedAt => createdAt;
        public int ObjectCount => objectCount;

        public void Initialize(
            string layoutName,
            string areaName,
            string areaId,
            string creationTime,
            int count)
        {
            displayName = layoutName;
            targetAreaName = areaName;
            targetAreaId = areaId;
            createdAt = creationTime;
            objectCount = Mathf.Max(0, count);
            hideFlags = HideFlags.HideInInspector;
        }
    }
}
