using System;
using System.Collections.Generic;
using UnityEngine;

namespace Genix.Diagnostics
{
    /// <summary>Contains accepted and rejected counts for unique diagnostic positions.</summary>
    public readonly struct PositionOutcomeCounts
    {
        /// <summary>Gets accepted positions.</summary>
        public int AcceptedPositions { get; }
        /// <summary>Gets rejected positions.</summary>
        public int RejectedPositions { get; }

        /// <summary>Initializes a new instance of position outcome counts.</summary>
        public PositionOutcomeCounts(int acceptedPositions, int rejectedPositions)
        {
            AcceptedPositions = acceptedPositions;
            RejectedPositions = rejectedPositions;
        }
    }

    /// <summary>Counts quantized diagnostic positions without double-counting repeated asset attempts.</summary>
    public static class DiagnosticPositionCounter
    {
        private const float PositionKeyScale = 1000f;

        /// <summary>Counts unique quantized positions, treating a position as accepted if any entry at that position was accepted.</summary>
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
