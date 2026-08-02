using System;
using UnityEditor;

namespace Genix.Tests.Framework
{
    internal enum GenixTestPreset
    {
        Quick,
        Full,
        Stress,
        Performance
    }

    /// <summary>Shares the selected dashboard preset with property tests in the same editor process.</summary>
    internal static class GenixTestPresetContext
    {
        private const string SessionKey = "Genix.Tests.ActivePreset";
        private const string PropertyCountEnvironmentVariable = "GENIX_PROPERTY_TESTS";

        public static GenixTestPreset Current
        {
            get
            {
                string value = SessionState.GetString(SessionKey, GenixTestPreset.Full.ToString());
                return Enum.TryParse(value, out GenixTestPreset preset) ? preset : GenixTestPreset.Full;
            }
            set => SessionState.SetString(SessionKey, value.ToString());
        }

        public static int PropertyTestCount
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable(PropertyCountEnvironmentVariable);

                if (int.TryParse(configured, out int count) && count > 0)
                    return count;

                return Current switch
                {
                    GenixTestPreset.Quick => 32,
                    GenixTestPreset.Stress => 2000,
                    _ => 250
                };
            }
        }
    }
}
