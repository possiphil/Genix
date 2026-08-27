using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Converts authoring requests into deterministic voxel-cell geometry without modifying a Unity scene.</summary>
    internal static class SfsAuthoringPlanner
    {
        private const int MaxAxisCells = 100000;
        private const int MaxGridLocations = 10000;

        public static Vector3Int WorldSizeToCells(Vector3 worldSize, float voxelSize)
        {
            return new Vector3Int(
                Mathf.Max(1, Mathf.CeilToInt(worldSize.x / voxelSize)),
                Mathf.Max(1, Mathf.CeilToInt(worldSize.y / voxelSize)),
                Mathf.Max(1, Mathf.CeilToInt(worldSize.z / voxelSize)));
        }

        public static bool TryCreate(
            SfsAuthoringRequest request,
            float voxelSize,
            out SfsAuthoringPlan plan,
            out string error)
        {
            plan = null;
            error = string.Empty;

            if (request == null)
            {
                error = "The authoring request is missing.";
                return false;
            }

            if (!IsFinitePositive(voxelSize))
            {
                error = "Voxel size must be a finite value greater than zero.";
                return false;
            }

            if (!IsFinite(request.Center))
            {
                error = "The requested center contains an invalid value.";
                return false;
            }

            return request.LayoutType switch
            {
                SfsAuthoringLayoutType.BoundedLocation => TryCreateBounded(request, voxelSize, out plan, out error),
                SfsAuthoringLayoutType.LocationGrid => TryCreateGrid(request, voxelSize, out plan, out error),
                SfsAuthoringLayoutType.FootprintLocation => TryCreateFootprint(request, voxelSize, out plan, out error),
                _ => Fail("Unsupported authoring layout.", out plan, out error)
            };
        }

        public static HashSet<Vector2Int> CreateFootprintMask(
            SfsFootprintTemplate template,
            Vector2Int dimensions,
            IReadOnlyCollection<Vector2Int> custom)
        {
            int width = Mathf.Max(1, dimensions.x);
            int depth = Mathf.Max(1, dimensions.y);
            HashSet<Vector2Int> result = new();

            if (template == SfsFootprintTemplate.Custom)
            {
                if (custom != null)
                {
                    foreach (Vector2Int cell in custom)
                    {
                        if (cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < depth)
                            result.Add(cell);
                    }
                }

                return result;
            }

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool include = template switch
                    {
                        SfsFootprintTemplate.Rectangle => true,
                        SfsFootprintTemplate.LShape => x == 0 || z == 0,
                        SfsFootprintTemplate.UShape => x == 0 || x == width - 1 || z == 0,
                        SfsFootprintTemplate.TShape => z == depth - 1 || x == (width - 1) / 2,
                        SfsFootprintTemplate.Courtyard => x == 0 || x == width - 1 || z == 0 || z == depth - 1,
                        _ => false
                    };

                    if (include)
                        result.Add(new Vector2Int(x, z));
                }
            }

            return result;
        }

        public static bool IsConnected(IReadOnlyCollection<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
                return false;

            HashSet<Vector2Int> remaining = new(cells);
            Queue<Vector2Int> frontier = new();
            Vector2Int first = remaining.First();
            frontier.Enqueue(first);
            remaining.Remove(first);

            Vector2Int[] directions =
            {
                Vector2Int.left,
                Vector2Int.right,
                Vector2Int.up,
                Vector2Int.down
            };

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();
                foreach (Vector2Int direction in directions)
                {
                    Vector2Int next = current + direction;
                    if (!remaining.Remove(next))
                        continue;

                    frontier.Enqueue(next);
                }
            }

            return remaining.Count == 0;
        }

        private static bool TryCreateBounded(
            SfsAuthoringRequest request,
            float voxelSize,
            out SfsAuthoringPlan plan,
            out string error)
        {
            Vector3Int cells = ResolveBoundedCells(request, voxelSize);
            if (!ValidateCells(cells, out error))
            {
                plan = null;
                return false;
            }

            Vector3Int start = CenteredStart(request.Center, cells, voxelSize);
            plan = CreateBasePlan(request, voxelSize, cells);
            AddOuterShell(plan, start, cells, "Boundary");
            AddLocation(plan, request, start, cells, "Location 1", plan.Name, voxelSize);
            SetActualMetrics(plan, start, cells, voxelSize);
            error = string.Empty;
            return true;
        }

        private static bool TryCreateGrid(
            SfsAuthoringRequest request,
            float voxelSize,
            out SfsAuthoringPlan plan,
            out string error)
        {
            if (!ValidateGridCounts(request.GridCounts, out error))
            {
                plan = null;
                return false;
            }

            Vector3Int separator = request.SeparatorCells;
            if (separator.x < 1 || separator.y < 1 || separator.z < 1)
            {
                plan = null;
                error = "Every grid separator must reserve at least one blocked voxel cell.";
                return false;
            }

            int[] xSizes = ResolveAxisSizes(request.XRoomCells, request.GridCounts.x, request.UniformRoomCells.x, request.UsePerAxisRoomSizes);
            int[] ySizes = ResolveAxisSizes(request.YRoomCells, request.GridCounts.y, request.UniformRoomCells.y, request.UsePerAxisRoomSizes);
            int[] zSizes = ResolveAxisSizes(request.ZRoomCells, request.GridCounts.z, request.UniformRoomCells.z, request.UsePerAxisRoomSizes);

            if (!ValidateAxisSizes(xSizes, "X", out error) ||
                !ValidateAxisSizes(ySizes, "Y", out error) ||
                !ValidateAxisSizes(zSizes, "Z", out error))
            {
                plan = null;
                return false;
            }

            Vector3Int total = new(
                xSizes.Sum() + separator.x * (xSizes.Length - 1),
                ySizes.Sum() + separator.y * (ySizes.Length - 1),
                zSizes.Sum() + separator.z * (zSizes.Length - 1));

            if (!ValidateCells(total, out error))
            {
                plan = null;
                return false;
            }

            Vector3Int start = CenteredStart(request.Center, total, voxelSize);
            int[] xStarts = BuildAxisStarts(start.x, xSizes, separator.x);
            int[] yStarts = BuildAxisStarts(start.y, ySizes, separator.y);
            int[] zStarts = BuildAxisStarts(start.z, zSizes, separator.z);

            plan = CreateBasePlan(request, voxelSize, total);
            AddOuterShell(plan, start, total, "Boundary");
            AddGridSeparators(plan, start, total, xStarts, xSizes, separator.x, 0);
            AddGridSeparators(plan, start, total, yStarts, ySizes, separator.y, 1);
            AddGridSeparators(plan, start, total, zStarts, zSizes, separator.z, 2);

            int index = 1;
            int locationCount = xSizes.Length * ySizes.Length * zSizes.Length;
            for (int y = 0; y < ySizes.Length; y++)
            for (int z = 0; z < zSizes.Length; z++)
            for (int x = 0; x < xSizes.Length; x++)
            {
                Vector3Int roomStart = new(xStarts[x], yStarts[y], zStarts[z]);
                Vector3Int roomSize = new(xSizes[x], ySizes[y], zSizes[z]);
                string anchorName = locationCount == 1 ? plan.Name : $"{plan.Name} {index}";
                AddLocation(plan, request, roomStart, roomSize, $"Location {index++}", anchorName, voxelSize);
            }

            plan.SeparatorCellCount =
                (xSizes.Length - 1) * separator.x +
                (ySizes.Length - 1) * separator.y +
                (zSizes.Length - 1) * separator.z;
            SetActualMetrics(plan, start, total, voxelSize);
            error = string.Empty;
            return true;
        }

        private static bool TryCreateFootprint(
            SfsAuthoringRequest request,
            float voxelSize,
            out SfsAuthoringPlan plan,
            out string error)
        {
            if (request.FootprintDimensions.x < 1 || request.FootprintDimensions.y < 1 ||
                request.FootprintDimensions.x > 64 || request.FootprintDimensions.y > 64)
            {
                plan = null;
                error = "Footprint dimensions must be between 1 and 64 modules per axis.";
                return false;
            }

            if (request.FootprintTileCells.x < 1 || request.FootprintTileCells.y < 1 || request.FootprintHeightCells < 1)
            {
                plan = null;
                error = "Footprint module size and height must contain at least one voxel cell.";
                return false;
            }

            HashSet<Vector2Int> mask = CreateFootprintMask(
                request.FootprintTemplate,
                request.FootprintDimensions,
                request.CustomFootprint);

            if (mask.Count == 0)
            {
                plan = null;
                error = "The footprint does not contain an occupied module.";
                return false;
            }

            if (!IsConnected(mask))
            {
                plan = null;
                error = "The footprint must be a single 4-neighbour-connected region.";
                return false;
            }

            Vector3Int total = new(
                request.FootprintDimensions.x * request.FootprintTileCells.x,
                request.FootprintHeightCells,
                request.FootprintDimensions.y * request.FootprintTileCells.y);
            if (!ValidateCells(total, out error))
            {
                plan = null;
                return false;
            }

            Vector3Int start = CenteredStart(request.Center, total, voxelSize);
            plan = CreateBasePlan(request, voxelSize, total);
            AddFootprintHorizontalBoundaries(plan, mask, request, start);
            AddFootprintVerticalBoundaries(plan, mask, request, start);

            foreach (Vector2Int module in mask.OrderBy(value => value.y).ThenBy(value => value.x))
            {
                Vector3Int min = new(
                    start.x + module.x * request.FootprintTileCells.x,
                    start.y,
                    start.z + module.y * request.FootprintTileCells.y);
                Vector3Int size = new(
                    request.FootprintTileCells.x,
                    request.FootprintHeightCells,
                    request.FootprintTileCells.y);
                plan.InteriorVolumes.Add(new SfsAuthoringCellVolume($"Module {module.x},{module.y}", min, size));
            }

            Vector2 center = new(
                (float)mask.Average(value => value.x),
                (float)mask.Average(value => value.y));
            Vector2Int anchorModule = mask.OrderBy(value => Vector2.SqrMagnitude((Vector2)value - center)).First();
            Vector3Int anchorMin = new(
                start.x + anchorModule.x * request.FootprintTileCells.x,
                start.y,
                start.z + anchorModule.y * request.FootprintTileCells.y);
            Vector3Int anchorCell = anchorMin + new Vector3Int(
                (request.FootprintTileCells.x - 1) / 2,
                (request.FootprintHeightCells - 1) / 2,
                (request.FootprintTileCells.y - 1) / 2);
            float autoRange = ((Vector3)total * voxelSize).magnitude * 0.5f + voxelSize * 2f;
            plan.Anchors.Add(new SfsAuthoringAnchorPlan(
                plan.Name,
                anchorCell,
                ResolveAnchorRange(request, autoRange)));
            plan.LocationCount = 1;
            SetActualMetrics(plan, start, total, voxelSize);
            error = string.Empty;
            return true;
        }

        private static SfsAuthoringPlan CreateBasePlan(
            SfsAuthoringRequest request,
            float voxelSize,
            Vector3Int totalCells)
        {
            return new SfsAuthoringPlan
            {
                Name = string.IsNullOrWhiteSpace(request.Name) ? "SFS Layout" : request.Name.Trim(),
                LayoutType = request.LayoutType,
                VoxelSize = voxelSize,
                RequestedCenter = request.Center,
                RequestedSize = request.LayoutType == SfsAuthoringLayoutType.BoundedLocation
                    ? request.SizeMode == SfsAuthoringSizeMode.VoxelCounts
                        ? (Vector3)request.VoxelCounts * voxelSize
                        : request.WorldSize
                    : (Vector3)totalCells * voxelSize
            };
        }

        private static void AddLocation(
            SfsAuthoringPlan plan,
            SfsAuthoringRequest request,
            Vector3Int min,
            Vector3Int size,
            string interiorName,
            string anchorName,
            float voxelSize)
        {
            plan.InteriorVolumes.Add(new SfsAuthoringCellVolume(interiorName, min, size));
            Vector3Int anchorCell = min + new Vector3Int(
                (size.x - 1) / 2,
                (size.y - 1) / 2,
                (size.z - 1) / 2);
            float autoRange = ((Vector3)size * voxelSize).magnitude * 0.5f + voxelSize * 2f;
            plan.Anchors.Add(new SfsAuthoringAnchorPlan(
                anchorName,
                anchorCell,
                ResolveAnchorRange(request, autoRange)));
            plan.LocationCount++;
        }

        private static void AddOuterShell(
            SfsAuthoringPlan plan,
            Vector3Int min,
            Vector3Int size,
            string prefix)
        {
            Vector3Int outerMin = min - Vector3Int.one;
            Vector3Int outerSize = size + Vector3Int.one * 2;
            plan.Delimiters.Add(new SfsAuthoringCellVolume($"{prefix} Left", outerMin, new Vector3Int(1, outerSize.y, outerSize.z)));
            plan.Delimiters.Add(new SfsAuthoringCellVolume($"{prefix} Right", new Vector3Int(min.x + size.x, outerMin.y, outerMin.z), new Vector3Int(1, outerSize.y, outerSize.z)));
            plan.Delimiters.Add(new SfsAuthoringCellVolume($"{prefix} Bottom", outerMin, new Vector3Int(outerSize.x, 1, outerSize.z)));
            plan.Delimiters.Add(new SfsAuthoringCellVolume($"{prefix} Top", new Vector3Int(outerMin.x, min.y + size.y, outerMin.z), new Vector3Int(outerSize.x, 1, outerSize.z)));
            plan.Delimiters.Add(new SfsAuthoringCellVolume($"{prefix} Back", outerMin, new Vector3Int(outerSize.x, outerSize.y, 1)));
            plan.Delimiters.Add(new SfsAuthoringCellVolume($"{prefix} Front", new Vector3Int(outerMin.x, outerMin.y, min.z + size.z), new Vector3Int(outerSize.x, outerSize.y, 1)));
        }

        private static void AddGridSeparators(
            SfsAuthoringPlan plan,
            Vector3Int innerMin,
            Vector3Int total,
            IReadOnlyList<int> starts,
            IReadOnlyList<int> sizes,
            int separator,
            int axis)
        {
            Vector3Int outerMin = innerMin - Vector3Int.one;
            Vector3Int outerSize = total + Vector3Int.one * 2;
            for (int i = 0; i < sizes.Count - 1; i++)
            {
                int separatorStart = starts[i] + sizes[i];
                Vector3Int min = outerMin;
                Vector3Int size = outerSize;
                if (axis == 0)
                {
                    min.x = separatorStart;
                    size.x = separator;
                }
                else if (axis == 1)
                {
                    min.y = separatorStart;
                    size.y = separator;
                }
                else
                {
                    min.z = separatorStart;
                    size.z = separator;
                }

                string axisName = axis == 0 ? "X" : axis == 1 ? "Y" : "Z";
                plan.Delimiters.Add(new SfsAuthoringCellVolume($"Separator {axisName} {i + 1}", min, size));
            }
        }

        private static void AddFootprintHorizontalBoundaries(
            SfsAuthoringPlan plan,
            HashSet<Vector2Int> mask,
            SfsAuthoringRequest request,
            Vector3Int start)
        {
            foreach ((int z, int xStart, int length) in MergeRows(mask))
            {
                Vector3Int min = new(
                    start.x + xStart * request.FootprintTileCells.x,
                    start.y - 1,
                    start.z + z * request.FootprintTileCells.y);
                Vector3Int size = new(
                    length * request.FootprintTileCells.x,
                    1,
                    request.FootprintTileCells.y);
                plan.Delimiters.Add(new SfsAuthoringCellVolume($"Boundary Bottom {z}-{xStart}", min, size));
                min.y = start.y + request.FootprintHeightCells;
                plan.Delimiters.Add(new SfsAuthoringCellVolume($"Boundary Top {z}-{xStart}", min, size));
            }
        }

        private static void AddFootprintVerticalBoundaries(
            SfsAuthoringPlan plan,
            HashSet<Vector2Int> mask,
            SfsAuthoringRequest request,
            Vector3Int start)
        {
            Dictionary<int, List<(int start, int length)>> xWalls = new();
            Dictionary<int, List<(int start, int length)>> zWalls = new();

            foreach (Vector2Int module in mask)
            {
                if (!mask.Contains(module + Vector2Int.left))
                    AddInterval(xWalls, module.x * request.FootprintTileCells.x - 1, module.y * request.FootprintTileCells.y, request.FootprintTileCells.y);
                if (!mask.Contains(module + Vector2Int.right))
                    AddInterval(xWalls, (module.x + 1) * request.FootprintTileCells.x, module.y * request.FootprintTileCells.y, request.FootprintTileCells.y);
                if (!mask.Contains(module + Vector2Int.down))
                    AddInterval(zWalls, module.y * request.FootprintTileCells.y - 1, module.x * request.FootprintTileCells.x, request.FootprintTileCells.x);
                if (!mask.Contains(module + Vector2Int.up))
                    AddInterval(zWalls, (module.y + 1) * request.FootprintTileCells.y, module.x * request.FootprintTileCells.x, request.FootprintTileCells.x);
            }

            foreach ((int coordinate, List<(int start, int length)> intervals) in xWalls)
            {
                foreach ((int intervalStart, int length) in MergeIntervals(intervals))
                {
                    plan.Delimiters.Add(new SfsAuthoringCellVolume(
                        $"Boundary X {coordinate}-{intervalStart}",
                        new Vector3Int(start.x + coordinate, start.y - 1, start.z + intervalStart),
                        new Vector3Int(1, request.FootprintHeightCells + 2, length)));
                }
            }

            foreach ((int coordinate, List<(int start, int length)> intervals) in zWalls)
            {
                foreach ((int intervalStart, int length) in MergeIntervals(intervals))
                {
                    plan.Delimiters.Add(new SfsAuthoringCellVolume(
                        $"Boundary Z {coordinate}-{intervalStart}",
                        new Vector3Int(start.x + intervalStart, start.y - 1, start.z + coordinate),
                        new Vector3Int(length, request.FootprintHeightCells + 2, 1)));
                }
            }
        }

        private static IEnumerable<(int z, int xStart, int length)> MergeRows(HashSet<Vector2Int> mask)
        {
            foreach (IGrouping<int, Vector2Int> row in mask.GroupBy(value => value.y).OrderBy(value => value.Key))
            {
                int[] values = row.Select(value => value.x).OrderBy(value => value).ToArray();
                int start = values[0];
                int previous = start;
                for (int i = 1; i < values.Length; i++)
                {
                    if (values[i] == previous + 1)
                    {
                        previous = values[i];
                        continue;
                    }

                    yield return (row.Key, start, previous - start + 1);
                    start = previous = values[i];
                }

                yield return (row.Key, start, previous - start + 1);
            }
        }

        private static void AddInterval(
            IDictionary<int, List<(int start, int length)>> values,
            int coordinate,
            int start,
            int length)
        {
            if (!values.TryGetValue(coordinate, out List<(int start, int length)> intervals))
            {
                intervals = new List<(int start, int length)>();
                values.Add(coordinate, intervals);
            }

            intervals.Add((start, length));
        }

        private static IEnumerable<(int start, int length)> MergeIntervals(List<(int start, int length)> intervals)
        {
            (int start, int length)[] ordered = intervals.OrderBy(value => value.start).ToArray();
            int start = ordered[0].start;
            int end = start + ordered[0].length;
            for (int i = 1; i < ordered.Length; i++)
            {
                int nextStart = ordered[i].start;
                int nextEnd = nextStart + ordered[i].length;
                if (nextStart <= end)
                {
                    end = Mathf.Max(end, nextEnd);
                    continue;
                }

                yield return (start, end - start);
                start = nextStart;
                end = nextEnd;
            }

            yield return (start, end - start);
        }

        private static Vector3Int ResolveBoundedCells(SfsAuthoringRequest request, float voxelSize)
        {
            return request.SizeMode == SfsAuthoringSizeMode.VoxelCounts
                ? request.VoxelCounts
                : WorldSizeToCells(request.WorldSize, voxelSize);
        }

        private static int[] ResolveAxisSizes(
            IReadOnlyList<int> values,
            int count,
            int uniform,
            bool perAxis)
        {
            int[] result = new int[count];
            for (int i = 0; i < count; i++)
                result[i] = perAxis && values != null && i < values.Count ? values[i] : uniform;
            return result;
        }

        private static int[] BuildAxisStarts(int start, IReadOnlyList<int> sizes, int separator)
        {
            int[] result = new int[sizes.Count];
            int cursor = start;
            for (int i = 0; i < sizes.Count; i++)
            {
                result[i] = cursor;
                cursor += sizes[i] + (i < sizes.Count - 1 ? separator : 0);
            }
            return result;
        }

        private static Vector3Int CenteredStart(Vector3 center, Vector3Int totalCells, float voxelSize)
        {
            return new Vector3Int(
                Mathf.RoundToInt(center.x / voxelSize - (totalCells.x - 1) * 0.5f),
                Mathf.RoundToInt(center.y / voxelSize - (totalCells.y - 1) * 0.5f),
                Mathf.RoundToInt(center.z / voxelSize - (totalCells.z - 1) * 0.5f));
        }

        private static void SetActualMetrics(
            SfsAuthoringPlan plan,
            Vector3Int start,
            Vector3Int totalCells,
            float voxelSize)
        {
            plan.ActualCenter = (Vector3)start * voxelSize + (Vector3)(totalCells - Vector3Int.one) * (voxelSize * 0.5f);
            plan.ActualSize = (Vector3)totalCells * voxelSize;
        }

        private static float ResolveAnchorRange(SfsAuthoringRequest request, float automatic)
        {
            return request.AutomaticAnchorRange ? automatic : Mathf.Max(0.01f, request.AnchorRangeOverride);
        }

        private static bool ValidateGridCounts(Vector3Int counts, out string error)
        {
            long total = (long)counts.x * counts.y * counts.z;
            if (counts.x < 1 || counts.y < 1 || counts.z < 1)
            {
                error = "Grid counts must be at least one on every axis.";
                return false;
            }

            if (total > MaxGridLocations)
            {
                error = $"The grid contains {total} locations. The authoring limit is {MaxGridLocations}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateAxisSizes(IReadOnlyList<int> sizes, string axis, out string error)
        {
            if (sizes.Any(value => value < 1))
            {
                error = $"Every {axis}-axis room size must contain at least one voxel cell.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool ValidateCells(Vector3Int cells, out string error)
        {
            if (cells.x < 1 || cells.y < 1 || cells.z < 1)
            {
                error = "Every axis must contain at least one voxel cell.";
                return false;
            }

            if (cells.x > MaxAxisCells || cells.y > MaxAxisCells || cells.z > MaxAxisCells)
            {
                error = $"No axis may exceed {MaxAxisCells} voxel cells.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool Fail(string message, out SfsAuthoringPlan plan, out string error)
        {
            plan = null;
            error = message;
            return false;
        }
    }
}
