using System.Collections.Generic;
using Genix.Assets;
using Genix.Placement;

namespace Genix.Core
{
    public sealed class GenerationPlan
    {
        private readonly List<PlannedObject> _objects = new();

        public IReadOnlyList<PlannedObject> Objects => _objects;
        public int Count => _objects.Count;

        public void Add(
            AssetDefinition asset,
            PlacementCandidate candidate,
            string objectName)
        {
            _objects.Add(new PlannedObject(
                asset,
                candidate,
                objectName,
                CandidateFactory.GetBounds(candidate, asset)));
        }

        public void Clear() => _objects.Clear();
    }

    public readonly struct PlannedObject
    {
        public AssetDefinition Asset { get; }
        public PlacementCandidate Candidate { get; }
        public string ObjectName { get; }
        public OrientedBounds Bounds { get; }

        public PlannedObject(
            AssetDefinition asset,
            PlacementCandidate candidate,
            string objectName,
            OrientedBounds bounds)
        {
            Asset = asset;
            Candidate = candidate;
            ObjectName = objectName;
            Bounds = bounds;
        }
    }
}
