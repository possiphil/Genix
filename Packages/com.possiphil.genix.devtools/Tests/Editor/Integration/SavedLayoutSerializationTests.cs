using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Core;
using Genix.Layouts;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Genix.Tests.Integration
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Integration)]
    [Category(GenixTestCategories.Snapshot)]
    [Category(GenixTestCategories.LayoutsArea)]
    public sealed class SavedLayoutSerializationTests
    {
        private SavedLayout _source;
        private SavedLayout _roundTrip;

        [TearDown]
        public void TearDown()
        {
            if (_source)
                UnityEngine.Object.DestroyImmediate(_source);

            if (_roundTrip)
                UnityEngine.Object.DestroyImmediate(_roundTrip);
        }

        [Test]
        public void EditorJsonRoundTripPreservesPersistentMetadata()
        {
            _source = CreateLayout();
            string json = EditorJsonUtility.ToJson(_source, true);
            _roundTrip = ScriptableObject.CreateInstance<SavedLayout>();
            EditorJsonUtility.FromJsonOverwrite(json, _roundTrip);

            Assert.That(_roundTrip.DisplayName, Is.EqualTo(_source.DisplayName));
            Assert.That(_roundTrip.ScenePath, Is.EqualTo(_source.ScenePath));
            Assert.That(_roundTrip.TargetAreaId, Is.EqualTo(_source.TargetAreaId));
            Assert.That(_roundTrip.PlacementTargets, Is.EqualTo(_source.PlacementTargets));
            Assert.That(_roundTrip.ObjectCount, Is.EqualTo(_source.ObjectCount));
            Assert.That(_roundTrip.Bounds, Is.EqualTo(_source.Bounds));
        }

        [Test]
        public void SerializedFieldSchemaMatchesApprovedSnapshot()
        {
            _source = CreateLayout();
            SerializedObject serializedLayout = new(_source);
            SerializedProperty property = serializedLayout.GetIterator();
            List<string> fields = new();
            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (property.depth == 0 && property.name != "m_Script")
                    fields.Add(property.name);
            }

            string snapshot = string.Join("\n", fields
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());

            const string approved =
                "assetPool\n" +
                "assetSummaries\n" +
                "bounds\n" +
                "createdAt\n" +
                "displayName\n" +
                "favorite\n" +
                "locked\n" +
                "notes\n" +
                "objectCount\n" +
                "placementTargets\n" +
                "prefab\n" +
                "sceneName\n" +
                "scenePath\n" +
                "sourceType\n" +
                "styleName\n" +
                "targetAreaId\n" +
                "targetAreaName\n" +
                "targetDistributionMode\n" +
                "targetDistributionWeights";

            Assert.That(snapshot, Is.EqualTo(approved),
                "The serialized SavedLayout schema changed. Review migration compatibility, then approve the new snapshot deliberately.");
        }

        private static SavedLayout CreateLayout()
        {
            SavedLayout layout = ScriptableObject.CreateInstance<SavedLayout>();
            layout.Initialize(
                "Snapshot Layout",
                null,
                "Evaluation Scene",
                "Assets/Scenes/Evaluation.unity",
                "Main Area",
                "area-snapshot",
                "Test Source",
                PlacementTarget.Floor | PlacementTarget.InsideSpace,
                TargetDistributionMode.Weighted,
                new TargetDistributionWeights(3, 0, 0, 2),
                null,
                "Natural",
                42,
                new Bounds(new Vector3(1f, 2f, 3f), new Vector3(4f, 5f, 6f)),
                "2026-08-02 12:00",
                Array.Empty<LayoutAssetSummary>());
            return layout;
        }
    }
}
