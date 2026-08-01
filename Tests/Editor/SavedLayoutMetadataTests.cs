using System;
using Genix.Core;
using Genix.Layouts;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    public sealed class SavedLayoutMetadataTests
    {
        private SavedLayout _layout;

        [TearDown]
        public void TearDown()
        {
            if (_layout)
                UnityEngine.Object.DestroyImmediate(_layout);
        }

        [Test]
        public void InitializeStoresSceneAndTargetAreaMetadata()
        {
            _layout = ScriptableObject.CreateInstance<SavedLayout>();

            _layout.Initialize(
                "Test Layout",
                null,
                "Bridge Scene",
                "Assets/Scenes/Bridge.unity",
                "North Room",
                "area-42",
                "Test Source",
                GenerationMode.TargetPlacement,
                PlacementTarget.Floor,
                TargetDistributionMode.Random,
                TargetDistributionWeights.Default,
                null,
                "Natural",
                3,
                new Bounds(Vector3.zero, Vector3.one),
                "2026-07-31 16:00",
                Array.Empty<LayoutAssetSummary>());

            Assert.That(_layout.SceneName, Is.EqualTo("Bridge Scene"));
            Assert.That(_layout.ScenePath, Is.EqualTo("Assets/Scenes/Bridge.unity"));
            Assert.That(_layout.TargetAreaName, Is.EqualTo("North Room"));
            Assert.That(_layout.TargetAreaId, Is.EqualTo("area-42"));
        }
    }
}
