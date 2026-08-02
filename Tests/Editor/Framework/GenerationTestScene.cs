using System;
using System.Collections.Generic;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Semantics;
using Genix.Styles;
using UnityEditor;
using UnityEngine;

namespace Genix.Tests.Framework
{
    internal sealed class GenerationTestScene : IDisposable
    {
        private readonly List<UnityEngine.Object> _objects = new();

        public GameObject AreaRoot { get; }
        public GameObject GeneratedRoot { get; }
        public AssetPool Pool { get; }
        public StubAreaSource AreaSource { get; }
        public PlacementArea Area { get; }

        public GenerationTestScene(
            AreaBuildSettings? areaSettings = null,
            Bounds? worldBounds = null,
            string sourceName = "Test Area")
        {
            AreaRoot = CreateGameObject(sourceName + " Root");
            GeneratedRoot = CreateGameObject(sourceName + " Generated");
            Pool = Track(ScriptableObject.CreateInstance<AssetPool>());
            Pool.Initialize(sourceName + " Pool", AssetPoolMode.Static);

            Bounds bounds = worldBounds ?? new Bounds(new Vector3(0f, 5f, 0f), new Vector3(20f, 10f, 20f));
            AreaBuildSettings settings = areaSettings ?? new AreaBuildSettings(
                AreaDecompositionMode.Precise,
                ~0,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.SfsBoundaries);
            Area = new PlacementArea(
                new SpatialSourceInfo("Test", sourceName, Guid.NewGuid().ToString("N")),
                bounds,
                new[] { SurfaceRegion.CreateFloor("Floor", bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, bounds.min.y) },
                new[]
                {
                    SurfaceRegion.CreateWall(
                        "Wall",
                        new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                        new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                        bounds.max.y,
                        Vector3.forward)
                },
                settings: settings,
                ceilingRegions: new[]
                {
                    SurfaceRegion.CreateCeiling(
                        "Ceiling",
                        bounds.min.x,
                        bounds.max.x,
                        bounds.min.z,
                        bounds.max.z,
                        bounds.max.y)
                });
            AreaSource = new StubAreaSource(AreaRoot.transform, Area);
        }

        public AssetDefinition CreateAsset(
            string name,
            PlacementType placementType = PlacementType.Floor,
            Vector3? size = null,
            GameObject prefab = null)
        {
            GameObject sourcePrefab = prefab ? prefab : CreateGameObject(name + " Prefab");
            AssetDefinition asset = Track(ScriptableObject.CreateInstance<AssetDefinition>());
            asset.name = name;
            asset.Initialize(sourcePrefab, size ?? Vector3.one);
            SetSerialized(asset, "placementType", placementType);
            Pool.AddStaticAsset(asset);
            return asset;
        }

        public GenerationRequest CreateRequest(
            int count = 4,
            PlacementTarget targets = PlacementTarget.Floor,
            TargetDistributionMode distribution = TargetDistributionMode.Random,
            TargetDistributionWeights? weights = null,
            SamplingAlgorithm algorithm = SamplingAlgorithm.Random,
            AreaBuildSettings? areaSettings = null,
            bool bestEffort = true,
            int seed = 123)
        {
            StyleSettings style = new(
                string.Empty,
                algorithm,
                new PlacementSettings(),
                new CandidateSettings(8, 32, false),
                new GridSettings(1f, 0f),
                new ClusterSettings(2, 1f),
                new PoissonSettings(1f, 30));

            return new GenerationRequest(
                AreaSource,
                Pool,
                count,
                targets,
                distribution,
                weights ?? TargetDistributionWeights.Default,
                style,
                areaSettings ?? default,
                useFixedSeed: true,
                randomSeed: seed,
                bestEffort: bestEffort);
        }

        public GenerationContext CreateContext(GenerationRequest request = null) =>
            new(
                request ?? CreateRequest(),
                GeneratedRoot.transform,
                Area,
                0f,
                null,
                SceneObjectIndex.Empty,
                SceneObjectIndex.Empty);

        public GameObject CreateGameObject(string name)
        {
            GameObject value = new(name);
            _objects.Add(value);
            return value;
        }

        public T Track<T>(T value) where T : UnityEngine.Object
        {
            _objects.Add(value);
            return value;
        }

        public void Dispose()
        {
            PlacementSolver.ClearCandidateCache();
            PlacementSolver.ClearSceneObjectCache();

            for (int i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i])
                    UnityEngine.Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        public static void SetSerialized<T>(UnityEngine.Object target, string propertyName, T value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(propertyName);

            switch (value)
            {
                case Enum enumValue:
                    property.enumValueIndex = Convert.ToInt32(enumValue);
                    break;
                case float floatValue:
                    property.floatValue = floatValue;
                    break;
                case bool boolValue:
                    property.boolValue = boolValue;
                    break;
                default:
                    throw new ArgumentException($"Unsupported serialized test value '{typeof(T).Name}'.", nameof(value));
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal sealed class StubAreaSource : IAreaSource
        {
            private readonly PlacementArea _area;

            public SpatialSourceInfo SourceInfo => _area.SourceInfo;
            public Transform ParentTransform { get; }
            public IReadOnlyList<SemanticTag> SemanticTags => Array.Empty<SemanticTag>();
            public IReadOnlyList<TagCategory> AnyTagCategories => Array.Empty<TagCategory>();
            public int BuildCalls { get; private set; }
            public AreaBuildSettings LastSettings { get; private set; }
            public string Error { get; set; }

            public StubAreaSource(Transform parentTransform, PlacementArea area)
            {
                ParentTransform = parentTransform;
                _area = area;
            }

            public bool IsSourceCollider(Collider collider) => false;

            public bool TryBuildArea(AreaBuildSettings settings, out PlacementArea area, out string error)
            {
                BuildCalls++;
                LastSettings = settings;
                area = string.IsNullOrEmpty(Error) ? _area : null;
                error = Error ?? string.Empty;
                return area != null;
            }
        }
    }
}
