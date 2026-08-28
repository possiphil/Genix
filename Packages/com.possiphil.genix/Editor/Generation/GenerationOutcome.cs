using System.Collections.Generic;
using System.Linq;
using Genix.Assets;
using Genix.Core;
using Genix.Diagnostics;
using Genix.Placement;
using Genix.Profiling;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Genix.Editor.Generation
{
    /// <summary>Completion state, accepted count, and designer-facing message returned by planning.</summary>
    internal readonly struct GenerationOutcome
    {
        public bool ShouldApply { get; }
        public bool IsComplete { get; }
        public int PlacedCount { get; }
        public string Message { get; }

        private GenerationOutcome(bool shouldApply, bool isComplete, int placedCount, string message)
        {
            ShouldApply = shouldApply;
            IsComplete = isComplete;
            PlacedCount = placedCount;
            Message = message;
        }

        public static GenerationOutcome Completed(int count) =>
            new(true, true, count, string.Empty);

        public static GenerationOutcome Partial(int count, string message) =>
            new(true, false, count, message);

        public static GenerationOutcome Failed(string message) =>
            Failed(0, message);

        public static GenerationOutcome Failed(int count, string message) =>
            new(false, false, count, message);
    }
}

