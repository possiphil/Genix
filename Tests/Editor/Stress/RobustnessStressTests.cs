using System.Collections.Generic;
using Genix.Areas;
using Genix.Core;
using Genix.Placement;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests.Stress
{
    [Category(GenixTestCategories.Stress)]
    [Category(GenixTestCategories.RobustnessArea)]
    public sealed class RobustnessStressTests
    {
        [Test]
        public void TenThousandRandomObbPairsRemainSymmetric()
        {
            GenerationRandom random = new(0x5EED);

            for (int i = 0; i < 10_000; i++)
            {
                OrientedBounds first = CreateBounds(random);
                OrientedBounds second = CreateBounds(random);
                Assert.That(first.Intersects(second), Is.EqualTo(second.Intersects(first)), $"Pair {i}");
            }
        }

        [Test]
        public void LargeSparseVoxelMaskMatchesReferenceMembership()
        {
            GenerationRandom random = new(0xC0FFEE);
            List<Vector3Int> cells = new(50_000);

            for (int i = 0; i < 50_000; i++)
            {
                cells.Add(new Vector3Int(
                    random.Range(-1_000_000, 1_000_000),
                    random.Range(-1_000_000, 1_000_000),
                    random.Range(-1_000_000, 1_000_000)));
            }

            HashSet<Vector3Int> reference = new(cells);
            VoxelCellMask mask = new(cells);

            foreach (Vector3Int cell in reference)
                Assert.That(mask.Contains(cell), Is.True);
        }

        private static OrientedBounds CreateBounds(GenerationRandom random) => new(
            new Vector3(
                random.Range(-1_000f, 1_000f),
                random.Range(-1_000f, 1_000f),
                random.Range(-1_000f, 1_000f)),
            new Vector3(
                random.Range(0.01f, 100f),
                random.Range(0.01f, 100f),
                random.Range(0.01f, 100f)),
            Quaternion.Euler(
                random.Range(-180f, 180f),
                random.Range(-180f, 180f),
                random.Range(-180f, 180f)));
    }
}
