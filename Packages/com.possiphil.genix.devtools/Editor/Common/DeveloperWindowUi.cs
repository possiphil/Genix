using System;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.DevTools
{
    /// <summary>Shared layout and control conventions for Genix developer windows.</summary>
    internal static class DeveloperWindowUi
    {
        private const float HorizontalChrome = 12f;

        private static GUIStyle _selectableRowStyle;
        private static GUIStyle _paneStyle;

        /// <summary>Gets a HelpBox-style frame without implicit outer margins.</summary>
        public static GUIStyle PaneStyle
        {
            get
            {
                _paneStyle ??= new GUIStyle(EditorStyles.helpBox)
                {
                    margin = new RectOffset()
                };
                return _paneStyle;
            }
        }

        /// <summary>Creates a framed list whose contents are clipped inside the frame and only scroll vertically.</summary>
        public static VerticalScrollViewScope VerticalScrollView(
            Vector2 scrollPosition,
            GUIStyle background,
            params GUILayoutOption[] options)
        {
            return new VerticalScrollViewScope(scrollPosition, background, options);
        }

        internal sealed class VerticalScrollViewScope : GUI.Scope
        {
            private readonly EditorGUILayout.VerticalScope _backgroundScope;

            public Vector2 ScrollPosition { get; }

            public VerticalScrollViewScope(
                Vector2 scrollPosition,
                GUIStyle background,
                params GUILayoutOption[] options)
            {
                _backgroundScope = new EditorGUILayout.VerticalScope(background, options);
                ScrollPosition = EditorGUILayout.BeginScrollView(
                    scrollPosition,
                    false,
                    false,
                    GUIStyle.none,
                    GUI.skin.verticalScrollbar,
                    GUIStyle.none,
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true));
            }

            protected override void CloseScope()
            {
                EditorGUILayout.EndScrollView();
                _backgroundScope.Dispose();
            }
        }

        /// <summary>Calculates a list width that grows with the window while preserving useful detail space.</summary>
        public static float ResponsiveListWidth(
            float windowWidth,
            float minimum = 250f,
            float maximum = 520f,
            float proportion = 0.35f,
            float minimumDetailsWidth = 390f)
        {
            float available = Mathf.Max(minimum, windowWidth - minimumDetailsWidth - HorizontalChrome);
            return Mathf.Clamp(windowWidth * proportion, minimum, Mathf.Min(maximum, available));
        }

        /// <summary>Draws a persistent, left-aligned list selection with ellipsized text.</summary>
        public static bool SelectableRow(bool selected, GUIContent content, float height = 24f, float width = 0f)
        {
            EnsureSelectableRowStyle();

            GUILayoutOption widthOption = width > 0f
                ? GUILayout.Width(width)
                : GUILayout.ExpandWidth(true);
            bool value = GUILayout.Toggle(
                selected,
                content,
                _selectableRowStyle,
                GUILayout.Height(height),
                widthOption);
            return value && !selected;
        }

        /// <summary>Draws only the visible rows of a large selectable list.</summary>
        public static int VirtualizedSelectableList(
            ref Vector2 scrollPosition,
            int itemCount,
            int selectedIndex,
            Func<int, GUIContent> contentAt,
            float width,
            float height,
            float rowHeight = 25f,
            GUIStyle background = null)
        {
            EnsureSelectableRowStyle();
            background ??= EditorStyles.helpBox;

            Rect frame = GUILayoutUtility.GetRect(
                GUIContent.none,
                background,
                GUILayout.Width(width),
                GUILayout.Height(height));
            GUI.Box(frame, GUIContent.none, background);

            RectOffset padding = background.padding;
            Rect viewport = new(
                frame.x + padding.left,
                frame.y + padding.top,
                Mathf.Max(1f, frame.width - padding.horizontal),
                Mathf.Max(1f, frame.height - padding.vertical));
            float scrollbarWidth = Mathf.Max(14f, GUI.skin.verticalScrollbar.fixedWidth);
            float contentWidth = Mathf.Max(1f, viewport.width - scrollbarWidth - 2f);
            float contentHeight = Mathf.Max(viewport.height, itemCount * rowHeight);
            Rect contentRect = new(0f, 0f, contentWidth, contentHeight);

            scrollPosition = GUI.BeginScrollView(
                viewport,
                scrollPosition,
                contentRect,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar);

            int activated = -1;
            try
            {
                int first = Mathf.Clamp(
                    Mathf.FloorToInt(scrollPosition.y / rowHeight),
                    0,
                    Mathf.Max(0, itemCount - 1));
                int last = Mathf.Min(
                    itemCount,
                    Mathf.CeilToInt((scrollPosition.y + viewport.height) / rowHeight) + 1);
                float controlHeight = Mathf.Max(1f, rowHeight - EditorGUIUtility.standardVerticalSpacing);

                for (int index = first; index < last; index++)
                {
                    Rect row = new(0f, index * rowHeight, contentWidth, controlHeight);
                    bool selected = index == selectedIndex;
                    bool value = GUI.Toggle(row, selected, contentAt(index), _selectableRowStyle);
                    if (value && !selected)
                        activated = index;
                }
            }
            finally
            {
                GUI.EndScrollView();
            }

            return activated;
        }

        private static void EnsureSelectableRowStyle()
        {
            _selectableRowStyle ??= new GUIStyle(EditorStyles.miniButton)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Ellipsis,
                padding = new RectOffset(7, 7, 2, 2)
            };
        }

        /// <summary>Draws a section heading with an optional compact action on the right.</summary>
        public static bool SectionHeader(GUIContent title, GUIContent action = null, bool actionEnabled = true, float actionWidth = 72f)
        {
            bool invoked = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (action != null)
                {
                    using (new EditorGUI.DisabledScope(!actionEnabled))
                        invoked = GUILayout.Button(action, EditorStyles.miniButton, GUILayout.Width(actionWidth));
                }
            }

            return invoked;
        }

        /// <summary>Draws connected command buttons with stable, matching dimensions.</summary>
        public static bool CommandButton(GUIContent content, int index, int count, float width = 108f, float height = 28f)
        {
            GUIStyle style = index switch
            {
                0 when count > 1 => EditorStyles.miniButtonLeft,
                _ when index == count - 1 && count > 1 => EditorStyles.miniButtonRight,
                _ when count > 1 => EditorStyles.miniButtonMid,
                _ => EditorStyles.miniButton
            };

            return GUILayout.Button(content, style, GUILayout.Width(width), GUILayout.Height(height));
        }
    }
}
