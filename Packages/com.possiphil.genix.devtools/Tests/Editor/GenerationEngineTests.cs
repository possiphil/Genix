using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Editor.Generation;
using Genix.Layouts;
using Genix.Placement;
using Genix.Sampling;
using Genix.Semantics;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Integration)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class GenerationEngineTests
    {
        private GenerationTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new GenerationTestScene();

        [TearDown]
        public void TearDown() => _scene.Dispose();

        [Test]
        public void RandomPlanCompletesRequestedCount()
        {
            AssetDefinition asset = _scene.CreateAsset("Floor");
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(count: 6));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { asset },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.ShouldApply, Is.True, outcome.Message);
            Assert.That(outcome.IsComplete, Is.True, outcome.Message);
            Assert.That(outcome.PlacedCount, Is.EqualTo(6));
            Assert.That(context.Plan.Count, Is.EqualTo(6));
            Assert.That(context.Plan.Objects, Has.All.Matches<PlannedObject>(item =>
                item.Asset == asset && item.Candidate.PlacementType == PlacementType.Floor));
        }

        [Test]
        public void PlacementLimitStopsAtConfiguredAssetMaximum()
        {
            AssetDefinition asset = _scene.CreateAsset("Unique Smoke Detector");
            asset.SetPlacementLimit(true, 1);
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(count: 4));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { asset },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.ShouldApply, Is.True, outcome.Message);
            Assert.That(outcome.IsComplete, Is.False);
            Assert.That(outcome.PlacedCount, Is.EqualTo(1));
            Assert.That(context.Plan.GetAssetCount(asset), Is.EqualTo(1));
            Assert.That(outcome.Message, Does.Contain("Max Placements"));
        }

        [Test]
        public void ImpossibleRequestReportsExhaustedCandidateBudget()
        {
            AssetDefinition asset = _scene.CreateAsset(
                "Oversized Floor Asset",
                PlacementType.Floor,
                Vector3.one * 100f);
            GenerationRequest template = _scene.CreateRequest(count: 2, bestEffort: true);
            StyleSettings style = template.StyleSettings;
            style.candidates = new CandidateSettings(multiplier: 2, minimumCount: 1, shuffle: false);
            GenerationRequest request = new(
                template.AreaSource,
                template.AssetPool,
                template.ObjectCount,
                template.PlacementTargets,
                template.TargetDistributionMode,
                template.TargetDistributionWeights,
                style,
                template.AreaBuildSettings,
                template.RelativePlacement,
                template.StyleName,
                template.UseFixedSeed,
                template.RandomSeed,
                template.BestEffort,
                template.DetailedDiagnostics,
                template.SupportDistribution);
            GenerationContext context = _scene.CreateContext(request);

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { asset },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.ShouldApply, Is.False);
            Assert.That(outcome.Message, Does.Contain("Candidate search budget exhausted"));
            Assert.That(outcome.Message, Does.Contain("4 candidates"));
        }

        [Test]
        public void PlacementLimitCountsExistingGeneratedObjectsAcrossRuns()
        {
            AssetDefinition asset = _scene.CreateAsset("Persistent Smoke Detector");
            asset.SetPlacementLimit(true, 1);
            GameObject existing = _scene.Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            existing.name = "Existing Smoke Detector";
            existing.transform.SetParent(_scene.GeneratedRoot.transform);
            existing.AddComponent<GeneratedObjectMetadata>()
                .Initialize(PlacementType.Floor, null, asset);
            SceneObjectIndex generated = SceneObjectIndex.CollectGenerated(_scene.GeneratedRoot.transform);
            GenerationRequest request = _scene.CreateRequest(count: 1);
            GenerationContext context = new(
                request,
                _scene.GeneratedRoot.transform,
                _scene.Area,
                0f,
                null,
                generated,
                SceneObjectIndex.Empty);

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { asset },
                NullDiagnosticsSink.Instance);

            Assert.That(generated.GetAssetCount(asset), Is.EqualTo(1));
            Assert.That(outcome.ShouldApply, Is.False);
            Assert.That(outcome.PlacedCount, Is.Zero);
            Assert.That(context.Plan.Count, Is.Zero);
            Assert.That(outcome.Message, Does.Contain("Max Placements"));
        }

        [Test]
        public void SharedTagCountSelectsExactlyOneVariantInLargerPool()
        {
            SemanticTag coatRack = CreateAssetTag("Coat Rack Choice");
            AssetDefinition standing = _scene.CreateAsset(
                "Standing Coat Rack",
                PlacementType.Floor,
                Vector3.one * 0.25f);
            AssetDefinition wall = _scene.CreateAsset(
                "Wall Coat Rack",
                PlacementType.Wall,
                Vector3.one * 0.25f);
            AssetDefinition filler = _scene.CreateAsset(
                "Office Filler",
                PlacementType.Floor,
                Vector3.one * 0.25f);
            standing.AddTag(coatRack);
            wall.AddTag(coatRack);
            AssetPoolTagLimit choice = new();
            choice.Configure(coatRack, 1, 1);
            _scene.Pool.SetTagPlacementLimits(new[] { choice });
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(
                count: 5,
                targets: PlacementTarget.Floor | PlacementTarget.Wall,
                seed: 412));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { standing, wall, filler },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.IsComplete, Is.True, outcome.Message);
            Assert.That(context.Plan.GetAssetTagCount(coatRack), Is.EqualTo(1));
            Assert.That(
                context.Plan.GetAssetCount(standing) + context.Plan.GetAssetCount(wall),
                Is.EqualTo(1));
        }

        [Test]
        public void SharedTagCountIncludesExistingVariantAcrossRuns()
        {
            SemanticTag coatRack = CreateAssetTag("Persistent Coat Rack Choice");
            AssetDefinition standing = _scene.CreateAsset(
                "Existing Standing Coat Rack",
                PlacementType.Floor,
                Vector3.one * 0.25f);
            AssetDefinition wall = _scene.CreateAsset(
                "Candidate Wall Coat Rack",
                PlacementType.Wall,
                Vector3.one * 0.25f);
            AssetDefinition filler = _scene.CreateAsset(
                "Persistent Office Filler",
                PlacementType.Floor,
                Vector3.one * 0.25f);
            standing.AddTag(coatRack);
            wall.AddTag(coatRack);
            AssetPoolTagLimit choice = new();
            choice.Configure(coatRack, 1, 1);
            _scene.Pool.SetTagPlacementLimits(new[] { choice });

            GameObject existing = _scene.Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            existing.transform.SetParent(_scene.GeneratedRoot.transform);
            existing.AddComponent<GeneratedObjectMetadata>()
                .Initialize(PlacementType.Floor, null, standing);
            SceneObjectIndex generated = SceneObjectIndex.CollectGenerated(_scene.GeneratedRoot.transform);
            GenerationRequest request = _scene.CreateRequest(
                count: 2,
                targets: PlacementTarget.Floor | PlacementTarget.Wall,
                seed: 913);
            GenerationContext context = new(
                request,
                _scene.GeneratedRoot.transform,
                _scene.Area,
                0f,
                null,
                generated,
                SceneObjectIndex.Empty);

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { standing, wall, filler },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.IsComplete, Is.True, outcome.Message);
            Assert.That(context.Plan.GetAssetCount(filler), Is.EqualTo(2));
            Assert.That(context.Plan.GetAssetTagCount(coatRack), Is.Zero);
        }

        [Test]
        public void SharedTagCountReportsMissingRequiredVariant()
        {
            SemanticTag coatRack = CreateAssetTag("Impossible Coat Rack Choice");
            AssetDefinition wall = _scene.CreateAsset(
                "Wall-Only Coat Rack",
                PlacementType.Wall,
                Vector3.one * 0.25f);
            AssetDefinition filler = _scene.CreateAsset(
                "Floor-Only Office Filler",
                PlacementType.Floor,
                Vector3.one * 0.25f);
            wall.AddTag(coatRack);
            AssetPoolTagLimit choice = new();
            choice.Configure(coatRack, 1, 1);
            _scene.Pool.SetTagPlacementLimits(new[] { choice });
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(
                count: 2,
                targets: PlacementTarget.Floor,
                seed: 177));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { wall, filler },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.IsComplete, Is.False);
            Assert.That(outcome.PlacedCount, Is.EqualTo(1));
            Assert.That(outcome.Message, Does.Contain("Impossible Coat Rack Choice 0/1"));
        }

        [Test]
        public void FixedSeedProducesSamePlanAfterCandidateCacheClear()
        {
            AssetDefinition asset = _scene.CreateAsset("Deterministic Floor");
            GenerationRequest request = _scene.CreateRequest(count: 5, seed: 9876);
            GenerationContext first = _scene.CreateContext(request);
            GenerationEngine.BuildPlan(first, new[] { asset }, NullDiagnosticsSink.Instance);
            Vector3[] firstPositions = first.Plan.Objects.Select(item => item.Candidate.Position).ToArray();
            Quaternion[] firstRotations = first.Plan.Objects.Select(item => item.Candidate.Rotation).ToArray();
            PlacementSolver.ClearCandidateCache();
            GenerationContext second = _scene.CreateContext(request);

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                second,
                new[] { asset },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.IsComplete, Is.True, outcome.Message);
            Assert.That(second.Plan.Objects.Select(item => item.Candidate.Position), Is.EqualTo(firstPositions));
            Assert.That(second.Plan.Objects.Select(item => item.Candidate.Rotation), Is.EqualTo(firstRotations));
        }

        [Test]
        public void SameRunAssetDependenciesUnlockAndPlanInOrder()
        {
            AssetDefinition monitor = _scene.CreateAsset("Monitor", size: Vector3.one * 0.25f);
            AssetDefinition keyboard = _scene.CreateAsset("Keyboard", size: Vector3.one * 0.25f);
            AssetDefinition mouse = _scene.CreateAsset("Mouse", size: Vector3.one * 0.25f);
            keyboard.AssetRelativePlacement.ConfigureAsset(
                monitor,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                100f,
                AssetRelativeFacing.Any);
            mouse.AssetRelativePlacement.ConfigureAsset(
                keyboard,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                100f,
                AssetRelativeFacing.Any);
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(count: 3, seed: 9281));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { mouse, keyboard, monitor },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.IsComplete, Is.True, outcome.Message);
            Assert.That(
                context.Plan.Objects.Select(item => item.Asset),
                Is.EqualTo(new[] { monitor, keyboard, mouse }));
        }

        [Test]
        public void ExactRelationsCompleteTransitiveCompositionLocally()
        {
            AssetDefinition monitor = _scene.CreateAsset("Required Monitor", size: Vector3.one * 0.2f);
            AssetDefinition keyboard = _scene.CreateAsset("Required Keyboard", size: Vector3.one * 0.2f);
            AssetDefinition mouse = _scene.CreateAsset("Required Mouse", size: Vector3.one * 0.2f);
            keyboard.AssetRelativePlacement.ConfigureAsset(
                monitor,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                100f,
                AssetRelativeFacing.Any);
            keyboard.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);
            mouse.AssetRelativePlacement.ConfigureAsset(
                keyboard,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                100f,
                AssetRelativeFacing.Any);
            mouse.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(count: 3, seed: 9281));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { mouse, keyboard, monitor },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.IsComplete, Is.True, outcome.Message);
            Assert.That(
                context.Plan.Objects.Select(item => item.Asset),
                Is.EqualTo(new[] { monitor, keyboard, mouse }));
            Assert.That(context.Plan.Objects[1].RelationAnchorIdentity, Is.Not.Null);
            Assert.That(context.Plan.Objects[2].RelationAnchorIdentity, Is.Not.Null);
        }

        [Test]
        public void RequiredCompositionDoesNotStartWithoutEnoughObjectBudget()
        {
            AssetDefinition monitor = _scene.CreateAsset("Budget Monitor", size: Vector3.one * 0.2f);
            AssetDefinition keyboard = _scene.CreateAsset("Budget Keyboard", size: Vector3.one * 0.2f);
            AssetDefinition mouse = _scene.CreateAsset("Budget Mouse", size: Vector3.one * 0.2f);
            keyboard.AssetRelativePlacement.ConfigureAsset(
                monitor,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                100f,
                AssetRelativeFacing.Any);
            keyboard.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.AtLeast, 1);
            mouse.AssetRelativePlacement.ConfigureAsset(
                keyboard,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                100f,
                AssetRelativeFacing.Any);
            mouse.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.AtLeast, 1);
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(count: 2));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { mouse, keyboard, monitor },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.ShouldApply, Is.False);
            Assert.That(context.Plan.Count, Is.Zero);
        }

        [Test]
        public void BudgetPlannerAvoidsIncompleteCompositions()
        {
            AssetDefinition monitor = _scene.CreateAsset("Reserved Monitor", size: Vector3.one * 0.2f);
            AssetDefinition keyboard = _scene.CreateAsset("Reserved Keyboard", size: Vector3.one * 0.2f);
            AssetDefinition mouse = _scene.CreateAsset("Reserved Mouse", size: Vector3.one * 0.2f);
            AssetDefinition book = _scene.CreateAsset("Independent Book", size: Vector3.one * 0.2f);
            keyboard.AssetRelativePlacement.ConfigureAsset(
                monitor,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                100f,
                AssetRelativeFacing.Any);
            keyboard.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);
            mouse.AssetRelativePlacement.ConfigureAsset(
                keyboard,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                100f,
                AssetRelativeFacing.Any);
            mouse.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(count: 2, seed: 713));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { mouse, keyboard, monitor, book },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.IsComplete, Is.True, outcome.Message);
            Assert.That(context.Plan.Objects, Has.All.Matches<PlannedObject>(item => item.Asset == book));
        }

        [Test]
        public void ExactlyCompletesEachExplicitSceneAnchorAndPreventsOverflow()
        {
            AssetDefinition desk = _scene.CreateAsset("Scene Desk", size: Vector3.one * 0.2f);
            AssetDefinition bin = _scene.CreateAsset("Scene Bin", size: Vector3.one * 0.2f);
            bin.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                4f,
                AssetRelativeFacing.Any);
            bin.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);
            CreateRelationAnchor("Left Scene Desk", desk, new Vector3(-4f, 0f, 0f));
            CreateRelationAnchor("Right Scene Desk", desk, new Vector3(4f, 0f, 0f));
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(count: 3, seed: 811));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { bin },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.ShouldApply, Is.True, outcome.Message);
            Assert.That(outcome.IsComplete, Is.False);
            Assert.That(context.Plan.GetAssetCount(bin), Is.EqualTo(2));
            Assert.That(
                context.Plan.Objects.Select(item => item.RelationAnchorIdentity).Distinct().Count(),
                Is.EqualTo(2));
        }

        [Test]
        public void BetweenCompletesMinimumForEveryExplicitSceneAnchor()
        {
            AssetDefinition desk = _scene.CreateAsset("Between Scene Desk", size: Vector3.one * 0.2f);
            AssetDefinition monitor = _scene.CreateAsset("Between Scene Monitor", size: Vector3.one * 0.2f);
            monitor.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                4f,
                AssetRelativeFacing.Any);
            monitor.AssetRelativePlacement.SetCardinalityRange(1, 2);
            CreateRelationAnchor("First Between Desk", desk, new Vector3(-4f, 0f, 0f));
            CreateRelationAnchor("Second Between Desk", desk, new Vector3(4f, 0f, 0f));
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(count: 2, seed: 117));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { monitor },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.IsComplete, Is.True, outcome.Message);
            Assert.That(context.Plan.GetAssetCount(monitor), Is.EqualTo(2));
            Assert.That(
                context.Plan.Objects.Select(item => item.RelationAnchorIdentity).Distinct().Count(),
                Is.EqualTo(2));
        }

        [Test]
        public void AnchorGroupExactlyCompletesOneTaggedVariantForEveryDesk()
        {
            SemanticTag display = CreateAssetTag("Display Group");
            AssetDefinition desk = _scene.CreateAsset("Grouped Desk", size: Vector3.one * 0.2f);
            AssetDefinition monitor = _scene.CreateAsset("Grouped Monitor", size: Vector3.one * 0.2f);
            AssetDefinition laptop = _scene.CreateAsset("Grouped Laptop", size: Vector3.one * 0.2f);
            monitor.AddTag(display);
            laptop.AddTag(display);
            monitor.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                4f,
                AssetRelativeFacing.Any);
            laptop.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                4f,
                AssetRelativeFacing.Any);
            AssetPoolAnchorGroupLimit group = new();
            group.ConfigureAsset(desk, display, AssetRelativeAnchorSource.SceneAnchors);
            group.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);
            _scene.Pool.SetAnchorGroupLimits(new[] { group });
            CreateRelationAnchor("Left Grouped Desk", desk, new Vector3(-4f, 0f, 0f));
            CreateRelationAnchor("Right Grouped Desk", desk, new Vector3(4f, 0f, 0f));
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(count: 2, seed: 917));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { monitor, laptop },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.IsComplete, Is.True, outcome.Message);
            Assert.That(context.Plan.GetAssetTagCount(display), Is.EqualTo(2));
            Assert.That(
                context.Plan.Objects.Select(item => item.RelationAnchorIdentity).Distinct().Count(),
                Is.EqualTo(2));
        }

        [Test]
        public void DesktopCompositionUsesLocalFallbackForTightRelations()
        {
            _scene.Dispose();
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                ~0,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.AllMatchingSurfacesInVolume);
            _scene = new GenerationTestScene(
                settings,
                new Bounds(new Vector3(0f, 2f, 0f), new Vector3(20f, 4f, 12f)));
            AssetDefinition desk = _scene.CreateAsset("Fallback Desk", size: new Vector3(2f, 0.1f, 1f));
            AssetDefinition monitor = _scene.CreateAsset("Fallback Monitor", size: new Vector3(0.5f, 0.35f, 0.12f));
            AssetDefinition keyboard = _scene.CreateAsset("Fallback Keyboard", size: new Vector3(0.45f, 0.04f, 0.15f));
            AssetDefinition mouse = _scene.CreateAsset("Fallback Mouse", size: new Vector3(0.1f, 0.04f, 0.06f));
            GenerationTestScene.SetSerialized(monitor, "randomYawRotation", false);
            GenerationTestScene.SetSerialized(keyboard, "randomYawRotation", false);
            GenerationTestScene.SetSerialized(mouse, "randomYawRotation", false);
            monitor.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Back,
                0f,
                1f,
                AssetRelativeFacing.Any,
                true);
            monitor.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);
            keyboard.AssetRelativePlacement.ConfigureAsset(
                monitor,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Front,
                0f,
                0.4f,
                AssetRelativeFacing.Any,
                true);
            keyboard.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);
            mouse.AssetRelativePlacement.ConfigureAsset(
                keyboard,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Right,
                0f,
                0.1f,
                AssetRelativeFacing.Any,
                true);
            mouse.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);

            for (int i = 0; i < 4; i++)
                CreateSupportedRelationAnchor($"Fallback Desk {i + 1}", desk, new Vector3(-6f + i * 4f, 1f, 0f));

            Physics.SyncTransforms();
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(count: 12, seed: 381));
            DiagnosticsRecorder diagnostics = new(context, DiagnosticsMode.Summary);

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { mouse, keyboard, monitor },
                diagnostics);

            string rejections = string.Join(", ", diagnostics.Diagnostics.CandidateRejectionCounts
                .OrderByDescending(entry => entry.Value)
                .Select(entry => $"{entry.Key}: {entry.Value}"));

            Assert.That(outcome.IsComplete, Is.True, $"{outcome.Message} Rejections: {rejections}");
            Assert.That(context.Plan.GetAssetCount(monitor), Is.EqualTo(4));
            Assert.That(context.Plan.GetAssetCount(keyboard), Is.EqualTo(4));
            Assert.That(context.Plan.GetAssetCount(mouse), Is.EqualTo(4));
        }

        [Test]
        public void RequiredFloorRelationKeepsFallbackOnAnchorSupport()
        {
            _scene.Dispose();
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                ~0,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.AllMatchingSurfacesInVolume);
            _scene = new GenerationTestScene(
                settings,
                new Bounds(new Vector3(0f, 2f, 0f), new Vector3(20f, 4f, 12f)));

            TagCategory supportKind = _scene.Track(ScriptableObject.CreateInstance<TagCategory>());
            supportKind.Initialize(categoryUsage: TagCategoryUsage.Surface);
            SemanticTag floorTag = _scene.Track(ScriptableObject.CreateInstance<SemanticTag>());
            floorTag.Initialize(supportKind);
            SemanticTag desktopTag = _scene.Track(ScriptableObject.CreateInstance<SemanticTag>());
            desktopTag.Initialize(supportKind);

            GameObject floorObject = _scene.CreateGameObject("Required Relation Floor");
            floorObject.transform.position = new Vector3(0f, -0.05f, 0f);
            BoxCollider floorCollider = floorObject.AddComponent<BoxCollider>();
            floorCollider.size = new Vector3(20f, 0.1f, 12f);
            floorObject.AddComponent<PlacementSurfaceDescriptor>().SetSurfaceTags(new[] { floorTag });

            AssetDefinition desk = _scene.CreateAsset("Required Relation Desk", size: new Vector3(2f, 0.1f, 1f));
            GameObject deskObject = _scene.CreateGameObject("Required Relation Desk Anchor");
            deskObject.transform.position = new Vector3(0f, 1f, 0f);
            BoxCollider deskCollider = deskObject.AddComponent<BoxCollider>();
            deskCollider.size = new Vector3(2f, 0.1f, 1f);
            PlacementSurfaceDescriptor desktop = deskObject.AddComponent<PlacementSurfaceDescriptor>();
            desktop.SetSurfaceTags(new[] { desktopTag });

            AssetDefinition chair = _scene.CreateAsset(
                "Required Relation Chair",
                PlacementType.Floor,
                new Vector3(0.5f, 1f, 0.5f));
            chair.SetRequiredSupportTags(new[] { floorTag });
            chair.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Front,
                0f,
                0.8f,
                AssetRelativeFacing.Toward,
                sameSupportSurface: false);
            chair.AssetRelativePlacement.SetAlignment(AssetRelativeAlignment.Center);

            Physics.SyncTransforms();
            RelativeAnchor anchor = new(
                deskCollider.bounds.center,
                deskCollider.bounds,
                deskObject.name,
                deskObject.transform.forward,
                deskObject.transform.right,
                desk,
                supportSurface: desktop,
                identity: deskObject);

            var seeds = RequiredRelationCandidateFactory.Create(
                _scene.CreateContext(_scene.CreateRequest(count: 1)),
                chair,
                anchor,
                null);

            Assert.That(seeds, Is.Not.Empty);
            Assert.That(seeds, Has.All.Matches<CandidateSeed>(seed =>
                PlacementSupportRules.GetDescriptor(seed.SurfaceCollider)?.HasTag(floorTag) == true));
            Assert.That(seeds[0].Position.x, Is.EqualTo(anchor.Position.x).Within(0.001f));
            Assert.That(seeds.Any(seed =>
                seed.Position.x < deskCollider.bounds.min.x ||
                seed.Position.x > deskCollider.bounds.max.x ||
                seed.Position.z < deskCollider.bounds.min.z ||
                seed.Position.z > deskCollider.bounds.max.z), Is.True);

            chair.AssetRelativePlacement.SetAlignment(AssetRelativeAlignment.Start);
            var startSeeds = RequiredRelationCandidateFactory.Create(
                _scene.CreateContext(_scene.CreateRequest(count: 1)),
                chair,
                anchor,
                null);
            chair.AssetRelativePlacement.SetAlignment(AssetRelativeAlignment.End);
            var endSeeds = RequiredRelationCandidateFactory.Create(
                _scene.CreateContext(_scene.CreateRequest(count: 1)),
                chair,
                anchor,
                null);

            Assert.That(startSeeds[0].Position.x, Is.LessThan(anchor.Position.x));
            Assert.That(endSeeds[0].Position.x, Is.GreaterThan(anchor.Position.x));
        }

        [Test]
        public void RequiredFloorRelationCentersOnCompatibleSupport()
        {
            _scene.Dispose();
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Precise,
                ~0,
                surfaceDiscoveryMode: SurfaceDiscoveryMode.AllMatchingSurfacesInVolume);
            _scene = new GenerationTestScene(
                settings,
                new Bounds(new Vector3(0f, 2f, 0f), new Vector3(24f, 4f, 20f)));

            TagCategory supportCategory = _scene.Track(ScriptableObject.CreateInstance<TagCategory>());
            supportCategory.Initialize(categoryUsage: TagCategoryUsage.Surface);
            SemanticTag markerSupport = _scene.Track(ScriptableObject.CreateInstance<SemanticTag>());
            markerSupport.Initialize(supportCategory);

            Vector3 supportPosition = new(3.137f, 1f, 2.719f);
            GameObject supportObject = _scene.CreateGameObject("Semantic Marker Support");
            supportObject.transform.position = supportPosition;
            BoxCollider supportCollider = supportObject.AddComponent<BoxCollider>();
            supportCollider.size = new Vector3(0.9f, 0.1f, 0.9f);
            supportObject.AddComponent<PlacementSurfaceDescriptor>().SetSurfaceTags(new[] { markerSupport });

            AssetDefinition anchorAsset = _scene.CreateAsset("Trailhead", size: Vector3.one);
            AssetDefinition sign = _scene.CreateAsset(
                "Semantic Sign",
                PlacementType.Floor,
                new Vector3(0.4f, 1f, 0.4f));
            sign.SetRequiredSupportTags(new[] { markerSupport });
            sign.AssetRelativePlacement.ConfigureAsset(
                anchorAsset,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Any,
                0f,
                10f,
                AssetRelativeFacing.Any);
            Physics.SyncTransforms();

            RelativeAnchor anchor = new(
                Vector3.zero,
                new Bounds(Vector3.zero, Vector3.one),
                "Trailhead Anchor",
                Vector3.forward,
                Vector3.right,
                anchorAsset,
                identity: supportObject);
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(
                count: 1,
                targets: PlacementTarget.Floor,
                areaSettings: settings));

            var seeds = RequiredRelationCandidateFactory.Create(context, sign, anchor, null);

            Assert.That(seeds, Has.Some.Matches<CandidateSeed>(seed =>
                seed.SurfaceCollider == supportCollider &&
                Vector2.Distance(
                    new Vector2(seed.Position.x, seed.Position.z),
                    new Vector2(supportCollider.bounds.center.x, supportCollider.bounds.center.z)) < 0.001f));
        }

        [Test]
        public void RequiredInsideSpaceRelationCentersFallbackAboveAnchor()
        {
            AssetDefinition desk = _scene.CreateAsset(
                "Floating Relation Desk",
                size: new Vector3(2f, 1f, 1f));
            AssetDefinition coin = _scene.CreateAsset(
                "Floating Relation Coin",
                PlacementType.InsideSpace,
                Vector3.one * 0.2f);
            coin.AssetRelativePlacement.ConfigureAsset(
                desk,
                AssetRelativeAnchorSource.SceneAnchors,
                AssetRelativeSide.Above,
                0.25f,
                1.5f,
                AssetRelativeFacing.Any);
            coin.AssetRelativePlacement.SetAlignment(AssetRelativeAlignment.Center);
            coin.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);
            Vector3 anchorPosition = new(0f, 2f, 0f);
            CreateRelationAnchor("Floating Relation Desk Anchor", desk, anchorPosition);

            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(
                count: 1,
                targets: PlacementTarget.InsideSpace,
                seed: 482));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { coin },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.IsComplete, Is.True, outcome.Message);
            PlannedObject planned = context.Plan.Objects.Single();
            Assert.That(planned.Asset, Is.EqualTo(coin));
            Assert.That(planned.Candidate.Position.y, Is.GreaterThan(anchorPosition.y));
            Assert.That(planned.Candidate.Position.x, Is.EqualTo(anchorPosition.x).Within(0.001f));
            Assert.That(planned.Candidate.Position.z, Is.EqualTo(anchorPosition.z).Within(0.001f));
        }

        [Test]
        public void FailedRequiredCompositionRollsBackRootAndAcceptedDiagnostics()
        {
            AssetDefinition monitor = _scene.CreateAsset("Rollback Monitor", size: Vector3.one * 0.2f);
            AssetDefinition keyboard = _scene.CreateAsset("Impossible Keyboard", size: Vector3.one * 0.2f);
            keyboard.AssetRelativePlacement.ConfigureAsset(
                monitor,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                100f,
                100f,
                AssetRelativeFacing.Any);
            keyboard.AssetRelativePlacement.SetCardinality(AssetRelativeCardinalityMode.Exactly, 1);
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(count: 2));
            DiagnosticsRecorder recorder = new(context, DiagnosticsMode.Summary);

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { keyboard, monitor },
                recorder);

            Assert.That(outcome.ShouldApply, Is.False);
            Assert.That(context.Plan.Count, Is.Zero);
            Assert.That(recorder.Diagnostics.PlacedObjectCount, Is.Zero);
            Assert.That(recorder.Diagnostics.AcceptedCandidateCount, Is.Zero);
            Assert.That(outcome.Message, Does.Contain("Required composition rolled back"));
        }

        [Test]
        public void CircularSameRunAssetDependenciesReportAnchorProblem()
        {
            AssetDefinition first = _scene.CreateAsset("First", size: Vector3.one * 0.25f);
            AssetDefinition second = _scene.CreateAsset("Second", size: Vector3.one * 0.25f);
            first.AssetRelativePlacement.ConfigureAsset(
                second,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                2f,
                AssetRelativeFacing.Any);
            second.AssetRelativePlacement.ConfigureAsset(
                first,
                AssetRelativeAnchorSource.GeneratedObjects,
                AssetRelativeSide.Any,
                0f,
                2f,
                AssetRelativeFacing.Any);
            GenerationContext context = _scene.CreateContext(_scene.CreateRequest(count: 1));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { first, second },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.ShouldApply, Is.False);
            Assert.That(outcome.Message, Does.Contain("missing or circular asset-relative anchors"));
        }

        [Test]
        public void BalancedDistributionHonorsPerTargetBudgets()
        {
            AssetDefinition floor = _scene.CreateAsset("Balanced Floor", PlacementType.Floor);
            AssetDefinition inside = _scene.CreateAsset("Balanced Inside", PlacementType.InsideSpace);
            GenerationRequest request = _scene.CreateRequest(
                count: 6,
                targets: PlacementTarget.Floor | PlacementTarget.InsideSpace,
                distribution: TargetDistributionMode.Balanced);
            GenerationContext context = _scene.CreateContext(request);

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { floor, inside },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.IsComplete, Is.True, outcome.Message);
            Assert.That(
                context.Plan.Objects.Count(item => item.Candidate.PlacementType == PlacementType.Floor),
                Is.EqualTo(3));
            Assert.That(
                context.Plan.Objects.Count(item => item.Candidate.PlacementType == PlacementType.InsideSpace),
                Is.EqualTo(3));
        }

        [Test]
        public void BuildPlanFailsWhenNoAssetMatchesSelectedTarget()
        {
            AssetDefinition floor = _scene.CreateAsset("Floor Only", PlacementType.Floor);
            GenerationContext context = _scene.CreateContext(
                _scene.CreateRequest(targets: PlacementTarget.Wall));

            GenerationOutcome outcome = GenerationEngine.BuildPlan(
                context,
                new[] { floor },
                NullDiagnosticsSink.Instance);

            Assert.That(outcome.ShouldApply, Is.False);
            Assert.That(outcome.PlacedCount, Is.Zero);
            Assert.That(outcome.Message, Does.Contain("No selected placement target"));
        }

        private void CreateRelationAnchor(string name, AssetDefinition asset, Vector3 position)
        {
            GameObject anchorObject = _scene.CreateGameObject(name);
            anchorObject.transform.position = position;
            AssetRelationAnchor anchor = anchorObject.AddComponent<AssetRelationAnchor>();
            anchor.SetRepresentedAsset(asset);
            anchor.SetCustomBounds(true, Vector3.zero, Vector3.one);
        }

        private void CreateSupportedRelationAnchor(string name, AssetDefinition asset, Vector3 position)
        {
            GameObject anchorObject = _scene.CreateGameObject(name);
            anchorObject.transform.position = position;
            BoxCollider collider = anchorObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(2f, 0.1f, 1f);
            PlacementSurfaceDescriptor surface = anchorObject.AddComponent<PlacementSurfaceDescriptor>();
            AssetRelationAnchor anchor = anchorObject.AddComponent<AssetRelationAnchor>();
            anchor.SetRepresentedAsset(asset);
            anchor.SetSupportSurface(surface);
            anchor.SetCustomBounds(true, Vector3.zero, collider.size);
        }

        private SemanticTag CreateAssetTag(string name)
        {
            TagCategory category = _scene.Track(ScriptableObject.CreateInstance<TagCategory>());
            category.name = name + " Category";
            category.Initialize(categoryUsage: TagCategoryUsage.Asset);
            SemanticTag tag = _scene.Track(ScriptableObject.CreateInstance<SemanticTag>());
            tag.name = name;
            tag.Initialize(category);
            return tag;
        }
    }
}
