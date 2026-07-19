using System;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Utilities;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Inspectors
{
    public sealed partial class AssetPoolEditor
    {
        private void DrawStaticPool()
        {
            RemoveMissingStaticAssets();

            DrawStaticAssetsHeader();

            if (!_staticAssets.isExpanded)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < _staticAssets.arraySize; i++)
                    DrawStaticAssetElement(i);

                DrawStaticAssetAddSlot();
                DrawStaticAssetMessage();
            }
        }

        private void DrawStaticAssetsHeader()
        {
            const float clearWidth = 60f;
            const float rightPadding = 4f;

            Rect rowRect = EditorGUILayout.GetControlRect(
                false,
                EditorGUIUtility.singleLineHeight + 2f);

            Rect clearRect = new(
                rowRect.xMax - clearWidth,
                rowRect.y,
                clearWidth,
                rowRect.height);

            Rect headerRect = new(
                rowRect.x,
                rowRect.y,
                clearRect.x - rightPadding - rowRect.x,
                rowRect.height);

            DrawFoldoutHeaderBackground(headerRect);

            _staticAssets.isExpanded = EditorGUI.Foldout(
                headerRect,
                _staticAssets.isExpanded,
                "Static Assets",
                true,
                EditorStyles.foldout);

            using (new EditorGUI.DisabledScope(_staticAssets.arraySize == 0))
            {
                if (GUI.Button(clearRect, "Clear"))
                    ClearStaticAssets();
            }
        }

        private static void DrawFoldoutHeaderBackground(Rect rect)
        {
            Color color = EditorGUIUtility.isProSkin
                ? new Color(0f, 0f, 0f, 0.22f)
                : new Color(0f, 0f, 0f, 0.08f);

            EditorGUI.DrawRect(rect, color);
        }

        private void DrawStaticAssetElement(int index)
        {
            const float buttonWidth = 24f;
            const float spacing = 4f;

            SerializedProperty assetProperty = _staticAssets.GetArrayElementAtIndex(index);
            Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

            Rect fieldRect = new(
                rowRect.x,
                rowRect.y,
                rowRect.width - buttonWidth - spacing,
                rowRect.height);

            Rect buttonRect = new(
                fieldRect.xMax + spacing,
                rowRect.y,
                buttonWidth,
                rowRect.height);

            EditorGUI.PropertyField(
                fieldRect,
                assetProperty,
                new GUIContent($"Element {index}"));

            if (GUI.Button(buttonRect, "-"))
                RemoveStaticAssetAt(index);
        }

        private void DrawStaticAssetAddSlot()
        {
            const float buttonWidth = 24f;
            const float spacing = 4f;

            Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);

            Rect fieldWithLabelRect = new(
                rowRect.x,
                rowRect.y,
                rowRect.width - buttonWidth - spacing,
                rowRect.height);

            Rect objectFieldRect = EditorGUI.PrefixLabel(
                fieldWithLabelRect,
                new GUIContent($"Element {_staticAssets.arraySize}"));

            DrawAddStaticAssetField(objectFieldRect);
            HandleAddStaticAssetFieldClick(objectFieldRect);
            HandleAddStaticAssetFieldDragAndDrop(objectFieldRect);
            HandleStaticAssetPickerEvents();
        }

        private static void DrawAddStaticAssetField(Rect rect)
        {
            GUI.Box(rect, GUIContent.none, EditorStyles.objectField);

            Rect textRect = new(
                rect.x + 4f,
                rect.y,
                rect.width - 8f,
                rect.height);

            GUIStyle style = new(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0),
                contentOffset = Vector2.zero,
                normal =
                {
                    textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.75f, 0.75f, 0.75f)
                        : new Color(0.35f, 0.35f, 0.35f)
                }
            };

            GUI.Label(textRect, "Add or drop Asset(s) here", style);
        }

        private void HandleAddStaticAssetFieldClick(Rect rect)
        {
            Event currentEvent = Event.current;

            if (currentEvent.type != EventType.MouseDown ||
                currentEvent.button != 0 ||
                !rect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            _staticAssetAddSlotPickerControlId = GUIUtility.GetControlID(FocusType.Passive);

            EditorGUIUtility.ShowObjectPicker<AssetDefinition>(
                null,
                false,
                string.Empty,
                _staticAssetAddSlotPickerControlId);

            currentEvent.Use();
        }

        private void HandleStaticAssetPickerEvents()
        {
            Event currentEvent = Event.current;

            if (currentEvent.type != EventType.ExecuteCommand)
                return;

            if (currentEvent.commandName != "ObjectSelectorClosed")
                return;

            if (EditorGUIUtility.GetObjectPickerControlID() != _staticAssetAddSlotPickerControlId)
                return;

            AssetDefinition selectedAsset = EditorGUIUtility.GetObjectPickerObject() as AssetDefinition;
            AddStaticAssetElement(selectedAsset);

            _staticAssetAddSlotPickerControlId = -1;

            currentEvent.Use();
        }

        private void HandleAddStaticAssetFieldDragAndDrop(Rect rect)
        {
            Event currentEvent = Event.current;

            if (!rect.Contains(currentEvent.mousePosition))
                return;

            AssetDefinition draggedAsset = DragAndDrop.objectReferences
                .OfType<AssetDefinition>()
                .FirstOrDefault();

            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = draggedAsset
                        ? DragAndDropVisualMode.Copy
                        : DragAndDropVisualMode.Rejected;

                    currentEvent.Use();
                    break;

                case EventType.DragPerform:
                    if (!draggedAsset)
                        return;

                    DragAndDrop.AcceptDrag();
                    AddStaticAssetElement(draggedAsset);

                    currentEvent.Use();
                    break;
            }
        }



        private static void DrawObjectFieldPlaceholder(Rect fieldRect, string text)
        {
            Rect textRect = new(
                fieldRect.x + 6f,
                fieldRect.y + 1f,
                fieldRect.width - 26f,
                fieldRect.height - 2f);

            Color backgroundColor = EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.22f, 0.22f, 1f)
                : new Color(0.82f, 0.82f, 0.82f, 1f);

            EditorGUI.DrawRect(textRect, backgroundColor);

            GUIStyle style = new(EditorStyles.label)
            {
                normal =
                {
                    textColor = EditorGUIUtility.isProSkin
                        ? new Color(0.75f, 0.75f, 0.75f)
                        : new Color(0.35f, 0.35f, 0.35f)
                }
            };

            EditorGUI.LabelField(textRect, text, style);
        }

        private void AddStaticAssetElement(AssetDefinition asset)
        {
            if (!asset)
                return;

            if (ContainsStaticAsset(asset))
            {
                ShowStaticAssetMessage(
                    $"'{asset.AssetName}' is already in this pool.",
                    MessageType.Warning);

                return;
            }

            int index = _staticAssets.arraySize;
            _staticAssets.InsertArrayElementAtIndex(index);
            _staticAssets.GetArrayElementAtIndex(index).objectReferenceValue = asset;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void ShowStaticAssetMessage(string message, MessageType messageType)
        {
            _staticAssetMessage = message;
            _staticAssetMessageType = messageType;
            _staticAssetMessageUntil = EditorApplication.timeSinceStartup + 3.0;

            Repaint();
        }

        private void DrawStaticAssetMessage()
        {
            if (string.IsNullOrWhiteSpace(_staticAssetMessage))
                return;

            if (EditorApplication.timeSinceStartup > _staticAssetMessageUntil)
                return;

            EditorGUILayout.Space(3f);
            EditorGUILayout.HelpBox(_staticAssetMessage, _staticAssetMessageType);
        }

        private bool ContainsStaticAsset(AssetDefinition asset)
        {
            for (int i = 0; i < _staticAssets.arraySize; i++)
            {
                SerializedProperty assetProperty = _staticAssets.GetArrayElementAtIndex(i);

                if (assetProperty.objectReferenceValue == asset)
                    return true;
            }

            return false;
        }

        private void RemoveStaticAssetAt(int index)
        {
            if (index < 0 || index >= _staticAssets.arraySize)
                return;

            SerializedProperty assetProperty = _staticAssets.GetArrayElementAtIndex(index);
            bool hadObjectReference = assetProperty.objectReferenceValue;

            _staticAssets.DeleteArrayElementAtIndex(index);

            if (hadObjectReference && index < _staticAssets.arraySize)
                _staticAssets.DeleteArrayElementAtIndex(index);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private bool HasMissingStaticAssets()
        {
            for (int i = 0; i < _staticAssets.arraySize; i++)
            {
                SerializedProperty assetProperty = _staticAssets.GetArrayElementAtIndex(i);

                if (!assetProperty.objectReferenceValue)
                    return true;
            }

            return false;
        }



        private void SetStaticAssetCount(int newSize)
        {
            newSize = Mathf.Max(0, newSize);

            while (_staticAssets.arraySize < newSize)
                _staticAssets.InsertArrayElementAtIndex(_staticAssets.arraySize);

            while (_staticAssets.arraySize > newSize)
                _staticAssets.DeleteArrayElementAtIndex(_staticAssets.arraySize - 1);

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void ClearStaticAssets()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear Static Assets",
                "Remove all assets from this static pool?",
                "Clear",
                "Cancel");

            if (!confirmed)
                return;

            _staticAssets.ClearArray();

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

    }
}
