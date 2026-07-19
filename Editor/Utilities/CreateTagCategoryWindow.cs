using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Editor.Genix.Editor.Assets;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Utilities
{
    public sealed class CreateTagDialog : EditorWindow
    {
        private const string NameControlName = "GenixTagName";

        private string _tagName = "New Tag";
        private TagCategory _category;
        private Action<string, TagCategory> _onConfirm;
        private bool _focusedInput;

        public static void Open(TagCategory defaultCategory, Action<string, TagCategory> onConfirm)
        {
            CreateTagDialog window = CreateInstance<CreateTagDialog>();

            window.titleContent = new GUIContent("Create Tag");
            window._category = defaultCategory;
            window._onConfirm = onConfirm;

            window.minSize = new Vector2(380f, 126f);
            window.maxSize = new Vector2(380f, 126f);

            window.AssignFallbackCategoryIfMissing();

            window.ShowUtility();
            window.Focus();
        }

        private void OnGUI()
        {
            HandleKeyboardInput();

            EditorGUILayout.Space(8f);

            GUI.SetNextControlName(NameControlName);
            _tagName = EditorGUILayout.TextField("Tag Name", _tagName);

            FocusInputOnce();

            DrawCategoryDropdown();

            EditorGUILayout.Space(6f);

            if (!_category)
            {
                EditorGUILayout.HelpBox("Create a category before creating a tag.", MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel", GUILayout.Width(90f)))
                    Close();

                using (new EditorGUI.DisabledScope(!_category))
                {
                    if (GUILayout.Button("Create", GUILayout.Width(90f)))
                        Confirm();
                }
            }
        }

        private void DrawCategoryDropdown()
        {
            List<TagCategory> categories = GetCategories();

            if (categories.Count == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.Popup("Category", 0, new[] { "No categories available" });

                _category = null;
                return;
            }

            int selectedIndex = _category
                ? categories.IndexOf(_category)
                : 0;

            if (selectedIndex < 0)
                selectedIndex = 0;

            string[] options = categories
                .Select(category => category.DisplayName)
                .ToArray();

            EditorGUI.BeginChangeCheck();

            int newIndex = EditorGUILayout.Popup("Category", selectedIndex, options);

            if (!EditorGUI.EndChangeCheck())
                return;

            _category = categories[newIndex];
        }

        private void ShowCategoryMenu(Rect dropdownRect)
        {
            List<TagCategory> categories = GetCategories();

            GenericMenu menu = new();

            if (categories.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No categories available"));
                menu.DropDown(dropdownRect);
                return;
            }

            foreach (TagCategory category in categories)
            {
                TagCategory capturedCategory = category;

                menu.AddItem(
                    new GUIContent(SanitizeMenuPath(capturedCategory.DisplayName)),
                    _category == capturedCategory,
                    () => _category = capturedCategory);
            }

            menu.DropDown(dropdownRect);
        }

        private void AssignFallbackCategoryIfMissing()
        {
            if (_category)
                return;

            _category = GetCategories().FirstOrDefault();
        }

        private static List<TagCategory> GetCategories()
        {
            AssetCatalog catalog = AssetCatalogService.GetOrCreate();

            return catalog.Categories
                .Where(category => category)
                .OrderBy(category => category.DisplayName)
                .ToList();
        }

        private void HandleKeyboardInput()
        {
            Event currentEvent = Event.current;

            if (currentEvent.type != EventType.KeyDown)
                return;

            if (currentEvent.keyCode == KeyCode.Return ||
                currentEvent.keyCode == KeyCode.KeypadEnter)
            {
                if (_category)
                    Confirm();

                currentEvent.Use();
            }

            if (currentEvent.keyCode == KeyCode.Escape)
            {
                Close();
                currentEvent.Use();
            }
        }

        private void FocusInputOnce()
        {
            if (_focusedInput)
                return;

            EditorGUI.FocusTextInControl(NameControlName);
            _focusedInput = true;
        }

        private void Confirm()
        {
            if (!_category)
                return;

            string result = string.IsNullOrWhiteSpace(_tagName)
                ? "New Tag"
                : _tagName.Trim();

            _onConfirm?.Invoke(result, _category);
            Close();
        }

        private static string SanitizeMenuPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";

            return value.Replace("/", "-");
        }
    }
}