using UnityEngine;

namespace Genix.Extensions
{
    /// <summary>Provides extension methods for unity object.</summary>
    public static class UnityObjectExtensions
    {
        /// <summary>Returns local object id.</summary>
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
