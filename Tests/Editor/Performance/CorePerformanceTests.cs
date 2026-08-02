using System.Collections.Generic;
using Genix.Areas;
using Genix.Core;
using Genix.Placement;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Styles;
using Genix.Tests.Framework;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;

namespace Genix.Tests.Performance
{
    [Category(GenixTestCategories.Performance)]
    [Category(GenixTestCategories.PerformanceArea)]
    public sealed class CorePerformanceTests
    {
        private static int _resultSink;

        [Test, Performance]
        public void OrientedBoundsIntersectionBatch()
        {
            OrientedBounds first = new(Vector3.zero, new Vector3(4f, 2f, 7f), Quaternion.Euler(12f, 38f, 4f));
            OrientedBounds[] others = CreateBounds(1024);

            Measure.Method(() =>
                {
                    int intersections = 0;

                    foreach (OrientedBounds other in others)
                    {
                        if (first.Intersects(other))
                            intersections++;
                    }

                    _resultSink = intersections;
                })
                .WarmupCount(5)
                .MeasurementCount(30)
                .Run();
        }

        [Test, Performance]
        public void VoxelMaskMembershipBatch()
        {
            List<Vector3Int> cells = new(100_000);

            for (int x = 0; x < 100; x++)
            for (int y = 0; y < 10; y++)
            for (int z = 0; z < 100; z++)
                cells.Add(new Vector3Int(x, y, z));

            VoxelCellMask mask = new(cells);

            Measure.Method(() =>
                {
                    int hits = 0;

                    for (int i = 0; i < 10_000; i++)
                    {
                        if (mask.Contains(new Vector3Int(i % 120, i % 12, i % 120)))
                            hits++;
                    }

                    _resultSink = hits;
                })
                .WarmupCount(5)
                .MeasurementCount(30)
                .Run();
        }

        [Test, Performance]
        public void PoissonCandidateGenerationThousandPoints()
        {
            SamplingContext context = CreatePoissonContext(1_000);

            Measure.Method(() =>
                {
                    SamplingContext runContext = CreatePoissonContext(1_000);
                    _resultSink = new BridsonPoissonDiskSampler().SamplePositions(runContext).Count;
                })
                .WarmupCount(3)
                .MeasurementCount(15)
                .Run();

            Assert.That(context.CandidateCount, Is.EqualTo(1_000));
        }

        private static OrientedBounds[] CreateBounds(int count)
        {
            GenerationRandom random = new(12345);
            OrientedBounds[] result = new OrientedBounds[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = new OrientedBounds(
                    new Vector3(
                        random.Range(-50f, 50f),
                        random.Range(-20f, 20f),
                        random.Range(-50f, 50f)),
                    new Vector3(
                        random.Range(0.1f, 8f),
                        random.Range(0.1f, 8f),
                        random.Range(0.1f, 8f)),
                    Quaternion.Euler(0f, random.Range(-180f, 180f), 0f));
            }

            return result;
        }

        private static SamplingContext CreatePoissonContext(int count)
        {
            Bounds bounds = new(Vector3.zero, new Vector3(500f, 1f, 500f));
            StyleSettings settings = new(
                string.Empty,
                SamplingAlgorithm.BridsonPoissonDisk,
                new PlacementSettings(),
                new CandidateSettings(1, 0, false),
                new GridSettings(1f, 0f),
                new ClusterSettings(1, 1f),
                new PoissonSettings(2f, 24));
            return new SamplingContext(
                bounds,
                bounds.center,
                settings,
                count,
                new GenerationRandom(12345),
                candidateCountOverride: count);
        }
    }
}
