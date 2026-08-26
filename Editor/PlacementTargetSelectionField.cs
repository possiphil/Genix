using System;
using System.Collections.Generic;
using Genix.Core;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor
{
    /// <summary>Draws the shared multi-selection popup for placement target masks.</summary>
    internal static class PlacementTargetSelectionField
    {
        public static PlacementTarget Normalize(PlacementTarget targets)
        {
            return targets & PlacementTarget.All;
        }

        public static string GetLabel(PlacementTarget targets, string noneLabel)
        {
            targets = Normalize(targets);

            if (targets == PlacementTarget.All)
                return "Any";

            if (targets == PlacementTarget.None)
                return noneLabel;

            List<string> labels = new();

            if ((targets & PlacementTarget.Floor) != 0)
                labels.Add("Floor");

            if ((targets & PlacementTarget.Wall) != 0)
                labels.Add("Wall");

            if ((targets & PlacementTarget.Ceiling) != 0)
                labels.Add("Ceiling");

            if ((targets & PlacementTarget.InsideSpace) != 0)
                labels.Add("Inside Space");

            return string.Join(", ", labels);
        }

        public static void Show(
            Rect dropdownRect,
            PlacementTarget targets,
            Action<PlacementTarget> onChanged,
            string noneTooltip,
            string anyTooltip)
        {
            PopupWindow.Show(
                dropdownRect,
                new PlacementTargetPopup(
                    targets,
                    dropdownRect.width,
                    onChanged,
                    noneTooltip,
                    anyTooltip));
        }

        private sealed class PlacementTargetPopup : PopupWindowContent
        {
            private const float RowHeight = 20f;
            private const float VerticalPadding = 4f;

            private readonly float _width;
            private readonly Action<PlacementTarget> _onChanged;
            private readonly string _noneTooltip;
            private readonly string _anyTooltip;

            private PlacementTarget _targets;

            public PlacementTargetPopup(
                PlacementTarget targets,
                float width,
                Action<PlacementTarget> onChanged,
                string noneTooltip,
                string anyTooltip)
            {
                _targets = Normalize(targets);
                _width = width;
                _onChanged = onChanged;
                _noneTooltip = noneTooltip;
                _anyTooltip = anyTooltip;
            }

            public override Vector2 GetWindowSize()
            {
                return new Vector2(_width, VerticalPadding * 2f + RowHeight * 6f);
            }

            public override void OnGUI(Rect rect)
            {
                GUILayout.Space(VerticalPadding);

                DrawRow(new GUIContent("None", _noneTooltip), _targets == PlacementTarget.None, () => SetTargets(PlacementTarget.None));
                DrawRow(new GUIContent("Any", _anyTooltip), _targets == PlacementTarget.All, () => SetTargets(PlacementTarget.All));
                DrawTargetRow(new GUIContent("Floor", "Include floor-compatible placement candidates."), PlacementTarget.Floor);
                DrawTargetRow(new GUIContent("Wall", "Include wall-compatible placement candidates."), PlacementTarget.Wall);
                DrawTargetRow(new GUIContent("Ceiling", "Include ceiling-compatible placement candidates."), PlacementTarget.Ceiling);
                DrawTargetRow(new GUIContent("Inside Space", "Include volume-compatible placement candidates inside the target area."), PlacementTarget.InsideSpace);
            }

            private void DrawTargetRow(GUIContent label, PlacementTarget target)
            {
                DrawRow(label, (_targets & target) != 0, () => ToggleTarget(target));
            }

            private void ToggleTarget(PlacementTarget target)
            {
                PlacementTarget updatedTargets = (_targets & target) != 0
                    ? _targets & ~target
                    : _targets | target;

                SetTargets(updatedTargets);
            }

            private void SetTargets(PlacementTarget targets)
            {
                _targets = Normalize(targets);
                _onChanged?.Invoke(_targets);
                editorWindow.Repaint();
            }

            private static void DrawRow(GUIContent label, bool selected, Action onClick)
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
        }
    }
}
