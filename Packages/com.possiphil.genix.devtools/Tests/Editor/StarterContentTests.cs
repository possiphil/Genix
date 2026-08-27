using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Infrastructure;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Quick)]
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class StarterContentTests
    {
        [Test]
        public void StarterAssetSetContainsCompleteDesktopChainWithoutLaptop()
        {
            CollectionAssert.IsSubsetOf(
                new[] { "Desk", "Monitor", "Keyboard", "Mouse" },
                StarterContentBuilder.StarterAssetNames.ToArray());
            CollectionAssert.DoesNotContain(StarterContentBuilder.StarterAssetNames, "Laptop");
        }

        [Test]
        public void StarterPresetUsesFreshSeedsAndCoreSurfaceTargets()
        {
            AssetPool pool = ScriptableObject.CreateInstance<AssetPool>();
            StylePreset style = ScriptableObject.CreateInstance<StylePreset>();

            try
            {
                GenerationPresetSettings settings = StarterContentBuilder.CreateGenerationSettings(pool, style);

                Assert.That(settings.AssetPool, Is.SameAs(pool));
                Assert.That(settings.StylePreset, Is.SameAs(style));
                Assert.That(settings.ObjectCount, Is.EqualTo(8));
                Assert.That(settings.UseFixedSeed, Is.False);
                Assert.That(settings.PlacementTargets, Is.EqualTo(
                    PlacementTarget.Floor | PlacementTarget.Wall | PlacementTarget.Ceiling));
            }
            finally
            {
                Object.DestroyImmediate(pool);
                Object.DestroyImmediate(style);
            }
        }
    }
}
