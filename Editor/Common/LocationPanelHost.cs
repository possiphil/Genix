using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.TargetAreas
{
    public sealed class LocationPanelHost
    {
        private readonly Dictionary<string, ILocationPanel> _panels = new();

        private IReadOnlyList<ITargetAreaProvider> _providers = Array.Empty<ITargetAreaProvider>();
        private string _selectedProviderId;
        private int _selectedProviderIndex;

        public void Refresh()
        {
            string selectedId = GetSelectedProvider()?.Id ?? _selectedProviderId;
            _providers = TargetAreaProviderRegistry.CreateProviders()
                .Where(provider => GetOrCreatePanel(provider) != null)
                .ToArray();

            _selectedProviderIndex = _providers
                .Select((provider, index) => new { provider, index })
                .FirstOrDefault(item => item.provider.Id == selectedId)
                ?.index ?? 0;

            _selectedProviderIndex = Mathf.Clamp(_selectedProviderIndex, 0, Mathf.Max(0, _providers.Count - 1));
            _selectedProviderId = GetSelectedProvider()?.Id;
        }

        public void Draw(AssetCatalog catalog)
        {
            if (_providers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No location provider is available. Install or enable a Genix integration such as Space Foundation.",
                    MessageType.Info);
                return;
            }

            DrawProviderDropdownIfNeeded();
            GetOrCreatePanel(GetSelectedProvider())?.Draw(catalog);
        }

        private void DrawProviderDropdownIfNeeded()
        {
            if (_providers.Count <= 1)
                return;

            string[] options = _providers
                .Select(provider => provider.DisplayName)
                .ToArray();

            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup("Location Provider", _selectedProviderIndex, options);

            if (!EditorGUI.EndChangeCheck())
                return;

            _selectedProviderIndex = Mathf.Clamp(selectedIndex, 0, _providers.Count - 1);
            _selectedProviderId = GetSelectedProvider()?.Id;
        }

        private ITargetAreaProvider GetSelectedProvider()
        {
            if (_providers.Count == 0)
                return null;

            _selectedProviderIndex = Mathf.Clamp(_selectedProviderIndex, 0, _providers.Count - 1);
            return _providers[_selectedProviderIndex];
        }

        private ILocationPanel GetOrCreatePanel(ITargetAreaProvider provider)
        {
            if (provider == null)
                return null;

            if (_panels.TryGetValue(provider.Id, out ILocationPanel panel))
                return panel;

            panel = provider.CreateLocationPanel();

            if (panel == null)
                return null;

            _panels[provider.Id] = panel;
            return panel;
        }
    }
}
