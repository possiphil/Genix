using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.SpatialArea)]
    public sealed class AreaBuildSettingsTests
    {
        [TestCase(SurfaceDiscoveryMode.SfsBoundaries, false, false, false)]
        [TestCase(SurfaceDiscoveryMode.NearSfsBoundaries, true, true, false)]
        [TestCase(SurfaceDiscoveryMode.AllMatchingSurfacesInVolume, true, false, true)]
        public void DiscoveryModeFlagsAreMutuallyConsistent(
            SurfaceDiscoveryMode mode,
            bool usesPhysics,
            bool usesBoundary,
            bool usesAll)
        {
            AreaBuildSettings settings = Create(mode, 1 << 4);

            Assert.That(settings.UsesPhysicsSurfaceProjection, Is.EqualTo(usesPhysics));
            Assert.That(settings.UsesBoundarySurfaceProjection, Is.EqualTo(usesBoundary));
            Assert.That(settings.UsesAllMatchingSurfaceSearch, Is.EqualTo(usesAll));
        }

        [Test]
        public void UnknownDiscoveryModeFallsBackToAllMatchingSurfaces()
        {
            AreaBuildSettings settings = Create((SurfaceDiscoveryMode)999, 1);

            Assert.That(settings.EffectiveSurfaceDiscoveryMode, Is.EqualTo(SurfaceDiscoveryMode.AllMatchingSurfacesInVolume));
        }

        [Test]
        public void SharedLayerMaskPopulatesAllTargetMasks()
        {
            AreaBuildSettings settings = Create(SurfaceDiscoveryMode.AllMatchingSurfacesInVolume, 1 << 6);

            Assert.That(settings.GetSurfaceLayers(PlacementType.Floor).value, Is.EqualTo(1 << 6));
            Assert.That(settings.GetSurfaceLayers(PlacementType.Wall).value, Is.EqualTo(1 << 6));
            Assert.That(settings.GetSurfaceLayers(PlacementType.Ceiling).value, Is.EqualTo(1 << 6));
        }

        [Test]
        public void SpecificLayerMasksOverrideSharedMask()
        {
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Fast,
                1,
                2,
                4,
                8);

            Assert.That(settings.GetSurfaceLayers(PlacementType.Floor).value, Is.EqualTo(2));
            Assert.That(settings.GetSurfaceLayers(PlacementType.Wall).value, Is.EqualTo(4));
            Assert.That(settings.GetSurfaceLayers(PlacementType.Ceiling).value, Is.EqualTo(8));
        }

        [Test]
        public void ConstructorClampsNormalThresholdsAndNormalizesEmptyTargets()
        {
            AreaBuildSettings settings = new(
                AreaDecompositionMode.Fast,
                1,
                surfaceRaycastHeight: 2f,
                surfaceRaycastDistance: 3f,
                floorNormalYThreshold: 5f,
                ceilingNormalYThreshold: -5f,
                placementTargets: PlacementTarget.None);

            Assert.That(settings.floorNormalYThreshold, Is.EqualTo(1f));
            Assert.That(settings.ceilingNormalYThreshold, Is.EqualTo(-1f));
            Assert.That(settings.placementTargets, Is.EqualTo(PlacementTarget.All));
        }

        [Test]
        public void WithMethodsReturnCopiesWithoutMutatingOriginal()
        {
            AreaBuildSettings original = Create(SurfaceDiscoveryMode.NearSfsBoundaries, 1);
            AreaBuildProfile profile = new();

            AreaBuildSettings withTargets = original.WithPlacementTargets(PlacementTarget.Floor | PlacementTarget.Wall);
            AreaBuildSettings withProfile = original.WithProfile(profile);

            Assert.That(original.placementTargets, Is.EqualTo(PlacementTarget.All));
            Assert.That(original.profile, Is.Null);
            Assert.That(withTargets.placementTargets, Is.EqualTo(PlacementTarget.Floor | PlacementTarget.Wall));
            Assert.That(withProfile.profile, Is.SameAs(profile));
        }

        private static AreaBuildSettings Create(SurfaceDiscoveryMode mode, LayerMask layers) => new(
            AreaDecompositionMode.Fast,
            layers,
            surfaceDiscoveryMode: mode);
    }
}
