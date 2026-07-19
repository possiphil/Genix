using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Editor.TargetAreas;
using UnityEditor;
using UnityEngine;
using SfsFoundation = SpaceFoundationSystem.SpaceFoundation;
using SfsSpace = SpaceFoundationSystem.Space;

namespace Genix.SpaceFoundation.Editor
{
    public sealed class SfsLocationSelector : ITargetAreaSelector
    {
        private SfsSpace[] _locations = Array.Empty<SfsSpace>();
        private string[] _options = Array.Empty<string>();
        private int _selectedIndex;

        public void Refresh()
        {
            string selectedId = GetSelectedId();
            List<SfsSpace> locations = new();

            foreach (SfsFoundation foundation in UnityEngine.Object.FindObjectsByType<SfsFoundation>())
                locations.AddRange(foundation.GetComponentsInChildren<SfsSpace>(true));

            if (locations.Count == 0)
                locations.AddRange(UnityEngine.Object.FindObjectsByType<SfsSpace>());

            _locations = locations
                .Where(location => location && location.anchor)
                .Distinct()
                .OrderBy(location => AreaName.ToDesignerName(location.name), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _options = _locations
                .Select(location => AreaName.ToUnitySafeDisplayName(location.name))
                .ToArray();

            int preservedIndex = Array.FindIndex(_locations, location => GetId(location) == selectedId);
            _selectedIndex = preservedIndex >= 0
                ? preservedIndex
                : Mathf.Clamp(_selectedIndex, 0, Mathf.Max(0, _locations.Length - 1));
        }

        public void Draw(string label)
        {
            if (_locations.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No Space Foundation locations were found. Rebuild the Space Foundation or open a scene with generated subspaces.",
                    MessageType.Warning);
                return;
            }

            _selectedIndex = EditorGUILayout.Popup(label, _selectedIndex, _options);
        }

        public IAreaSource CreateAreaSource()
        {
            SfsSpace location = GetSelected();
            return location ? new SfsAreaSource(location) : null;
        }

        private SfsSpace GetSelected()
        {
            if (_locations.Length == 0)
                return null;

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _locations.Length - 1);
            return _locations[_selectedIndex];
        }

        private string GetSelectedId()
        {
            return GetId(GetSelected());
        }

        private static string GetId(SfsSpace location)
        {
            return location && location.anchor ? location.anchor.GetUniqueId() : string.Empty;
        }
    }
}
