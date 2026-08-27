using System.Collections.Generic;
using FsCheck;
using FsCheck.Fluent;
using Genix.Areas;
using Genix.Core;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests.Properties
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.SpatialArea)]
    public sealed class VoxelCellMaskPropertyTests
    {
        [Test]
        [Category(GenixTestCategories.Property)]
        public void MembershipMatchesReferenceSetForDenseAndSparseInputs()
        {
            Arbitrary<int> seeds = Arb.From(Gen.Choose(-1_000_000, 1_000_000));

            GenixProperty.Check(
                nameof(MembershipMatchesReferenceSetForDenseAndSparseInputs),
                Prop.ForAll(seeds, seed =>
                {
                    GenerationRandom random = new(seed);
                    List<Vector3Int> cells = new();
                    int count = random.Range(0, 256);

                    for (int i = 0; i < count; i++)
                    {
                        int spread = i % 2 == 0 ? 8 : 10_000;
                        cells.Add(new Vector3Int(
                            random.Range(-spread, spread + 1),
                            random.Range(-spread, spread + 1),
                            random.Range(-spread, spread + 1)));
                    }

                    HashSet<Vector3Int> reference = new(cells);
                    VoxelCellMask mask = new(cells);

                    foreach (Vector3Int cell in reference)
                    {
                        if (!mask.Contains(cell))
                            return false;
                    }

                    for (int i = 0; i < 256; i++)
                    {
                        Vector3Int query = new(
                            random.Range(-10_000, 10_001),
                            random.Range(-10_000, 10_001),
                            random.Range(-10_000, 10_001));

                        if (mask.Contains(query) != reference.Contains(query))
                            return false;
                    }

                    return true;
                }));
        }

        [Test]
        public void EmptyInputHasNoBoundsAndContainsNothing()
        {
            VoxelCellMask mask = new(null);

            Assert.That(mask.Count, Is.Zero);
            Assert.That(mask.HasBounds, Is.False);
            Assert.That(mask.Contains(Vector3Int.zero), Is.False);
        }
    }
}
