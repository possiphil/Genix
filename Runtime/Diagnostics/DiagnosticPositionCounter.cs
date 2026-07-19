using System;
using System.Collections.Generic;
using UnityEngine;

namespace Genix.Diagnostics
{
    public readonly struct PositionOutcomeCounts
    {
        public int AcceptedPositions { get; }
        public int RejectedPositions { get; }

        public PositionOutcomeCounts(int acceptedPositions, int rejectedPositions)
        {
            AcceptedPositions = acceptedPositions;
            RejectedPositions = rejectedPositions;
        }
    }

    public static class DiagnosticPositionCounter
    {
        private const float PositionKeyScale = 1000f;

        public static PositionOutcomeCounts Count<T>(
            IEnumerable<T> entries,
            Func<T, Vector3> getPosition,
            Func<T, bool> isAccepted)
        {
            Dictionary<Vector3Int, bool> positionAcceptedStates = new();

            foreach (T entry in entries)
            {
                Vector3Int key = ToPositionKey(getPosition(entry));
                bool accepted = isAccepted(entry);

                if (!positionAcceptedStates.TryGetValue(key, out bool alreadyAccepted))
                {
                    positionAcceptedStates.Add(key, accepted);
                    continue;
                }

                positionAcceptedStates[key] = alreadyAccepted || accepted;
            }

            int acceptedPositions = 0;
            int rejectedPositions = 0;

            foreach (bool accepted in positionAcceptedStates.Values)
            {
                if (accepted)
                    acceptedPositions++;
                else
                    rejectedPositions++;
            }

            return new PositionOutcomeCounts(acceptedPositions, rejectedPositions);
        }

        private static Vector3Int ToPositionKey(Vector3 position)
        {
            return new Vector3Int(
                Mathf.RoundToInt(position.x * PositionKeyScale),
                Mathf.RoundToInt(position.y * PositionKeyScale),
                Mathf.RoundToInt(position.z * PositionKeyScale));
        }
    }
}
