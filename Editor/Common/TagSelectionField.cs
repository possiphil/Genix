using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Semantics;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Genix.Editor.Common
{
    public static class TagSelectionField
    {
        public enum SpecialSelection
        {
            None,
            Any
        }

        public static void Draw(string label, TagCategory category, IEnumerable<SemanticTag> availableTags, IEnumerable<SemanticTag> selectedTags, Action<IReadOnlyList<SemanticTag>> onChanged,
            bool forceMultiSelect = false, bool anySelected = false, Action<IReadOnlyList<SemanticTag>, SpecialSelection> onChangedWithSpecialSelection = null, bool showNoneOption = true)
        {
            if (!category)
                return;

            List<SemanticTag> tags = availableTags.Where(tag => tag && tag.Category == category).OrderBy(tag => tag.DisplayName).ToList();

            List<SemanticTag> selection = selectedTags.Where(tag => tag && tag.Category == category && tags.Contains(tag)).Distinct().ToList();

            bool allowMultipleSelection = forceMultiSelect || category.AllowMultipleTags;

            if (!allowMultipleSelection && selection.Count > 1)
                selection = selection.Take(1).ToList();

            Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            Rect fieldRect = EditorGUI.PrefixLabel(rowRect, new GUIContent(label));

            bool hasSelectableOptions = tags.Count > 0 || showNoneOption;

            using (new EditorGUI.DisabledScope(!hasSelectableOptions))
            {
                if (allowMultipleSelection)
                {
                    if (EditorGUI.DropdownButton(fieldRect, new GUIContent(GetSelectionLabel(selection, anySelected, showNoneOption)), FocusType.Keyboard, EditorStyles.popup))
                    {
                        PopupWindow.Show(fieldRect, new TagSelectionPopup(tags, selection, anySelected, onChanged, onChangedWithSpecialSelection, showNoneOption, fieldRect.width));
                    }

                    return;
                }

                DrawSingleSelectPopup(fieldRect, tags, selection, anySelected, onChanged, onChangedWithSpecialSelection, showNoneOption);
            }
        }

        private static void DrawSingleSelectPopup(Rect fieldRect, IReadOnlyList<SemanticTag> tags, IReadOnlyList<SemanticTag> selection, bool anySelected,
            Action<IReadOnlyList<SemanticTag>> onChanged, Action<IReadOnlyList<SemanticTag>, SpecialSelection> onChangedWithSpecialSelection, bool showNoneOption)
        {
            int specialOptionCount = showNoneOption ? 2 : 1;
            string[] options = new string[tags.Count + specialOptionCount];

            if (showNoneOption)
            {
                options[0] = "None";
                options[1] = "Any";
            }
            else
            {
                options[0] = "Any";
            }

            for (int i = 0; i < tags.Count; i++)
                options[i + specialOptionCount] = tags[i].DisplayName;

            int selectedIndex = showNoneOption ? 0 : 0;

            if (anySelected)
            {
                selectedIndex = showNoneOption ? 1 : 0;
            }
            else if (selection.Count > 0)
            {
                int tagIndex = GetTagIndex(tags, selection[0]);
                selectedIndex = tagIndex >= 0 ? tagIndex + specialOptionCount : 0;
            }

            EditorGUI.BeginChangeCheck();

            int newIndex = EditorGUI.Popup(fieldRect, selectedIndex, options, EditorStyles.popup);

            if (!EditorGUI.EndChangeCheck())
                return;

            if (!showNoneOption)
            {
                if (newIndex == 0)
                    NotifyChanged(onChanged, onChangedWithSpecialSelection, Array.Empty<SemanticTag>(), SpecialSelection.Any);
                else
                    NotifyChanged(onChanged, onChangedWithSpecialSelection, new[] { tags[newIndex - 1] }, SpecialSelection.None);

                return;
            }

            switch (newIndex)
            {
                case 0:
                    NotifyChanged(onChanged, onChangedWithSpecialSelection, Array.Empty<SemanticTag>(), SpecialSelection.None);
                    break;

                case 1:
                    NotifyChanged(onChanged, onChangedWithSpecialSelection, Array.Empty<SemanticTag>(), SpecialSelection.Any);
                    break;

                default:
                    NotifyChanged(onChanged, onChangedWithSpecialSelection, new[] { tags[newIndex - specialOptionCount] }, SpecialSelection.None);
                    break;
            }
        }

        private static int GetTagIndex(IReadOnlyList<SemanticTag> tags, SemanticTag targetTag)
        {
            if (!targetTag)
                return -1;

            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == targetTag)
                    return i;
            }

            return -1;
        }

        private static string GetSelectionLabel(IReadOnlyList<SemanticTag> selection, bool anySelected, bool showNoneOption)
        {
            if (anySelected)
                return "Any";

            return selection.Count switch
            {
                0 => showNoneOption ? "None" : "Any",
                1 => selection[0].DisplayName,
                2 => $"{selection[0].DisplayName}, {selection[1].DisplayName}",
                _ => $"{selection.Count} selected"
            };
        }

        private static void NotifyChanged(Action<IReadOnlyList<SemanticTag>> onChanged, Action<IReadOnlyList<SemanticTag>, SpecialSelection> onChangedWithSpecialSelection,
            IReadOnlyList<SemanticTag> selectedTags, SpecialSelection specialSelection)
        {
            if (onChangedWithSpecialSelection != null)
            {
                onChangedWithSpecialSelection(selectedTags, specialSelection);
                return;
            }

            onChanged?.Invoke(selectedTags);
        }

        private sealed class TagSelectionPopup : PopupWindowContent
        {
            private const float RowHeight = 20f;
            private const float VerticalPadding = 4f;
            private const float MaxHeight = 260f;

            private readonly List<SemanticTag> _tags;
            private readonly List<SemanticTag> _selection;
            private bool _anySelected;
            private readonly Action<IReadOnlyList<SemanticTag>> _onChanged;
            private readonly Action<IReadOnlyList<SemanticTag>, SpecialSelection> _onChangedWithSpecialSelection;
            private readonly bool _showNoneOption;
            private readonly float _width;

            private Vector2 _scroll;

            public TagSelectionPopup(IReadOnlyList<SemanticTag> tags, IReadOnlyList<SemanticTag> selection, bool anySelected, Action<IReadOnlyList<SemanticTag>> onChanged,
                Action<IReadOnlyList<SemanticTag>, SpecialSelection> onChangedWithSpecialSelection, bool showNoneOption, float width)
            {
                _tags = tags.Where(tag => tag).ToList();
                _selection = selection.Where(tag => tag).ToList();
                _anySelected = anySelected;
                _onChanged = onChanged;
                _onChangedWithSpecialSelection = onChangedWithSpecialSelection;
                _showNoneOption = showNoneOption;
                _width = Mathf.Max(180f, width);
            }

            public override Vector2 GetWindowSize()
            {
                int specialRows = _showNoneOption ? 2 : 1;
                float targetHeight = VerticalPadding * 2f + (_tags.Count + specialRows) * RowHeight;
                return new Vector2(_width, Mathf.Min(MaxHeight, targetHeight));
            }

            public override void OnGUI(Rect rect)
            {
                _scroll = EditorGUILayout.BeginScrollView(_scroll);

                if (_showNoneOption)
                    DrawRow("None", !_anySelected && _selection.Count == 0, SelectNone);

                bool anyRowSelected = _showNoneOption ? _anySelected : _selection.Count == 0;

                DrawRow("Any", anyRowSelected, SelectAny);

                foreach (SemanticTag tag in _tags)
                    DrawTagRow(tag);

                EditorGUILayout.EndScrollView();
            }

            private void DrawTagRow(SemanticTag tag)
            {
                bool isSelected = _selection.Contains(tag);

                DrawRow(tag.DisplayName, isSelected, () =>
                {
                    if (isSelected)
                        _selection.Remove(tag);
                    else
                        _selection.Add(tag);

                    _anySelected = false;
                    NotifyChanged(SpecialSelection.None);
                    editorWindow.Repaint();
                });
            }

            private void SelectNone()
            {
                _selection.Clear();
                _anySelected = false;

                NotifyChanged(SpecialSelection.None);
                editorWindow.Repaint();
            }

            private void SelectAny()
            {
                _selection.Clear();
                _anySelected = true;

                NotifyChanged(SpecialSelection.Any);
                editorWindow.Repaint();
            }

            private void DrawRow(string label, bool selected, Action onClick)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, RowHeight);

                if (rowRect.Contains(Event.current.mousePosition))
                    EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.08f));

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                    onClick?.Invoke();

                Rect checkRect = new(rowRect.x + 6f, rowRect.y, 18f, rowRect.height);
                Rect labelRect = new(rowRect.x + 26f, rowRect.y, rowRect.width - 32f, rowRect.height);

                if (selected)
                    GUI.Label(checkRect, "✓");

                GUI.Label(labelRect, label);
            }

            private void NotifyChanged(SpecialSelection specialSelection)
            {
                TagSelectionField.NotifyChanged(_onChanged, _onChangedWithSpecialSelection, _selection.ToArray(), specialSelection);
            }
        }
    }
}
