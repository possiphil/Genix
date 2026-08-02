using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Editor.Generation;
using Genix.Placement;
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
    }
}
