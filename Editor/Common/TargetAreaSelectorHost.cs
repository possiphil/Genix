using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.TargetAreas
{
    public sealed class TargetAreaSelectorHost
    {
        private readonly Dictionary<string, ITargetAreaSelector> _selectors = new();

        private IReadOnlyList<ITargetAreaProvider> _providers = Array.Empty<ITargetAreaProvider>();
        private string _selectedProviderId;
        private int _selectedProviderIndex;

        public void Refresh()
        {
            string selectedId = GetSelectedProvider()?.Id ?? _selectedProviderId;
            _providers = TargetAreaProviderRegistry.CreateProviders();

            _selectedProviderIndex = _providers
                .Select((provider, index) => new { provider, index })
                .FirstOrDefault(item => item.provider.Id == selectedId)
                ?.index ?? 0;

            _selectedProviderIndex = Mathf.Clamp(_selectedProviderIndex, 0, Mathf.Max(0, _providers.Count - 1));
            _selectedProviderId = GetSelectedProvider()?.Id;

            foreach (ITargetAreaProvider provider in _providers)
                GetOrCreateSelector(provider)?.Refresh();
        }

        public void Draw(string label)
        {
            if (_providers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No target area provider is available. Install or enable a Genix integration such as Space Foundation.",
                    MessageType.Warning);
                return;
            }

            DrawProviderDropdownIfNeeded();

            ITargetAreaSelector selector = GetOrCreateSelector(GetSelectedProvider());
            selector?.Draw(label);
        }

        public IAreaSource CreateAreaSource()
        {
            ITargetAreaSelector selector = GetOrCreateSelector(GetSelectedProvider());
            return selector?.CreateAreaSource();
        }

        private void DrawProviderDropdownIfNeeded()
        {
            if (_providers.Count <= 1)
                return;

            string[] options = _providers
                .Select(provider => provider.DisplayName)
                .ToArray();

            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup("Area Provider", _selectedProviderIndex, options);

            if (!EditorGUI.EndChangeCheck())
                return;

            _selectedProviderIndex = Mathf.Clamp(selectedIndex, 0, _providers.Count - 1);
            _selectedProviderId = GetSelectedProvider()?.Id;
            GetOrCreateSelector(GetSelectedProvider())?.Refresh();
        }

        private ITargetAreaProvider GetSelectedProvider()
        {
            if (_providers.Count == 0)
                return null;

            _selectedProviderIndex = Mathf.Clamp(_selectedProviderIndex, 0, _providers.Count - 1);
            return _providers[_selectedProviderIndex];
        }

        private ITargetAreaSelector GetOrCreateSelector(ITargetAreaProvider provider)
        {
            if (provider == null)
                return null;

            if (_selectors.TryGetValue(provider.Id, out ITargetAreaSelector selector))
                return selector;

            selector = provider.CreateSelector();

            if (selector == null)
                return null;

            _selectors[provider.Id] = selector;
            return selector;
        }
    }
}
