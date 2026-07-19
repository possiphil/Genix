using System;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Utilities
{
    public static class ValidatedField
    {
        public static float DrawFloatField(GUIContent label, float currentValue, float fallbackValue, string fieldName, Func<float, bool> isValid, string rule, Action<string, string, string> onInvalid)
        {
            EditorGUI.BeginChangeCheck();

            float newValue = EditorGUILayout.DelayedFloatField(label, currentValue);

            if (!EditorGUI.EndChangeCheck())
                return currentValue;

            if (isValid(newValue))
                return newValue;

            onInvalid?.Invoke(fieldName, rule, fallbackValue.ToString("0.###"));
            return fallbackValue;
        }

        public static int DrawIntField(GUIContent label, int currentValue, int fallbackValue, string fieldName, Func<int, bool> isValid, string rule, Action<string, string, string> onInvalid)
        {
            EditorGUI.BeginChangeCheck();

            int newValue = EditorGUILayout.DelayedIntField(label, currentValue);

            if (!EditorGUI.EndChangeCheck())
                return currentValue;

            if (isValid(newValue))
                return newValue;

            onInvalid?.Invoke(fieldName, rule, fallbackValue.ToString());
            return fallbackValue;
        }
    }
}