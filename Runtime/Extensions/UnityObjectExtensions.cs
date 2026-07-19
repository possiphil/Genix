using UnityEngine;

namespace Genix.Extensions
{
    public static class UnityObjectExtensions
    {
        public static string GetLocalObjectId(this Object target)
        {
            if (!target)
                return string.Empty;

#if UNITY_6000_0_OR_NEWER
            return target.GetEntityId().ToString();
#else
            return target.GetInstanceID().ToString();
#endif
        }
    }
}
