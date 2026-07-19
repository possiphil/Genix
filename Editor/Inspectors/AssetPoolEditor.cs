using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Editor.Utilities;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Extensions;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    [CustomEditor(typeof(AssetPool))]
    public sealed partial class AssetPoolEditor : UnityEditor.Editor
    {
        private SerializedProperty _mode;
        private SerializedProperty _staticAssets;
        private SerializedProperty _filterByPlacementType;
        private SerializedProperty _placementType;
        private SerializedProperty _filterByOrientationMode;
        private SerializedProperty _orientationMode;
        private SerializedProperty _categoryFilters;

        private int _staticAssetAddSlotPickerControlId = -1;

        private string _staticAssetMessage;
        private MessageType _staticAssetMessageType = MessageType.Info;
        private double _staticAssetMessageUntil;

        private static readonly AssetPoolMode[] PoolModes =
        {
            AssetPoolMode.Static,
            AssetPoolMode.Dynamic
        };

        private static readonly string[] PoolModeLabels =
        {
            AssetPoolMode.Static.ToDisplayName(),
            AssetPoolMode.Dynamic.ToDisplayName()
        };

        private bool _showPreview = true;

        private void OnEnable()
        {
            _mode = serializedObject.FindProperty("mode");
            _staticAssets = serializedObject.FindProperty("staticAssets");
            _filterByPlacementType = serializedObject.FindProperty("filterByPlacementType");
            _placementType = serializedObject.FindProperty("placementType");
            _filterByOrientationMode = serializedObject.FindProperty("filterByOrientationMode");
            _orientationMode = serializedObject.FindProperty("orientationMode");
            _categoryFilters = serializedObject.FindProperty("categoryFilters");

            _staticAssets.isExpanded = true;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Asset Pool", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            DrawDisplayNameField();

            DrawModeField();

            EditorGUILayout.Space(6f);

            if (IsStaticPool())
                DrawStaticPool();
            else
                DrawDynamicPool();

            EditorGUILayout.Space(6f);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawDisplayNameField()
        {
            EditorGUI.BeginChangeCheck();

            string displayName = EditorGUILayout.DelayedTextField("Display Name", target.name);

            if (!EditorGUI.EndChangeCheck())
                return;

            AssetCatalogService.Rename(
                target,
                displayName,
                "New Asset Pool");

            serializedObject.Update();
        }

        private void DrawModeField()
        {
            AssetPoolMode currentMode = GetSerializedMode();
            int currentIndex = System.Array.IndexOf(PoolModes, currentMode);

            if (currentIndex < 0)
                currentIndex = 0;

            int selectedIndex = EditorGUILayout.Popup("Mode", currentIndex, PoolModeLabels);

            SetSerializedMode(PoolModes[selectedIndex]);
        }

        private AssetPoolMode GetSerializedMode()
        {
            string enumName = _mode.enumNames[_mode.enumValueIndex];

            return System.Enum.TryParse(enumName, out AssetPoolMode mode)
                ? mode
                : AssetPoolMode.Static;
        }

        private void SetSerializedMode(AssetPoolMode mode)
        {
            string enumName = mode.ToString();

            for (int i = 0; i < _mode.enumNames.Length; i++)
            {
                if (_mode.enumNames[i] != enumName)
                    continue;

                _mode.enumValueIndex = i;
                return;
            }
        }

        private void RemoveMissingStaticAssets()
        {
            for (int i = _staticAssets.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty assetProperty = _staticAssets.GetArrayElementAtIndex(i);

                if (assetProperty.objectReferenceValue)
                    continue;

                _staticAssets.DeleteArrayElementAtIndex(i);
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private bool IsStaticPool()
        {
            return _mode.enumNames[_mode.enumValueIndex] == nameof(AssetPoolMode.Static);
        }

        private static string SanitizeMenuPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            return value.Replace("/", "-");
        }
    }
}
