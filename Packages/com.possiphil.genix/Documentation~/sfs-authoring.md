# Space setup

Genix includes a **Space Setup** workflow for creating voxel-aligned Space Foundation System (SFS) inputs. Open it through **Tools > Genix > Space Setup**. Individual elements are also available from **GameObject > Genix > Space Foundation**.

## Basic elements

- **Space Foundation** creates the SFS configuration object. Its voxel size defines the spatial grid.
- **Anchor** creates a location seed and links it to the selected Foundation.
- **Box Delimiter** creates a `4 x 4 x 1`-cell wall on the Foundation's voxel grid. It is a blocked SFS volume, not a container that generates a location inside itself.
- **Convert Selected** adds delimiter components and layer configuration to selected collider objects without duplicating existing components.

Opening Space Setup, selecting a Foundation, or creating delimiters automatically adds the SFS
Delimiter layer to the Foundation's delimiting mask. No separate layer-configuration action is required.

Anchors and delimiters must not be children of the `SpaceFoundation` GameObject. SFS clears that object's children before computing. The authoring commands therefore place such elements beside the Foundation when a Foundation hierarchy is used as their context.

## Layout generators

### Bounded Location

Creates one rectangular location, six boundary volumes, and one anchor. Use world units for designer-facing dimensions, exact voxel counts for reproducible grid tests, or Fit Selection to derive the center and size from selected colliders and renderers.

`Position Source` accepts a manual center, the current Scene view pivot, or the center of selected geometry. `Fit Selection` derives both position and size from the selection, so it replaces the separate position source. With **Advanced** disabled, the workflow offers world-space sizing and Fit Selection; enabling it additionally exposes exact voxel counts.

### Location Grid

Creates aligned adjacent or stacked locations. `Location Counts` controls the location count on X, Y, and Z. Uniform sizing is the simplest option. Per-axis sizing assigns one width to each X column, one height to each Y level, and one depth to each Z row while preserving alignment.

Every internal division reserves at least one blocked voxel band. A `2 x 1 x 1` grid with two ten-cell rooms and one separator therefore occupies 21 inner cells, not 20. Each division is one continuous shared delimiter slab rather than duplicate walls per room.

### Footprint Location

Creates one connected non-rectangular location by extruding a two-dimensional occupancy mask. Rectangle, L, U, T, and courtyard templates are provided. Custom masks must be connected through horizontal or vertical neighbours; diagonally touching modules alone do not form one SFS location.

`Module Size (cells)` controls horizontal footprint resolution, while `Height (cells)` controls vertical free space.

## Voxel alignment

World-space sizes always round up to whole voxel cells and never become smaller than requested. With **Advanced** enabled, the voxel-aligned position and size appear directly below their corresponding inputs. The position can move by up to half a voxel per axis because SFS voxel centers lie on integer multiples of the Foundation's voxel size.

Generated colliders occupy the planned blocked cells with a small inset. This leaves a numerical gap to neighbouring free cells while retaining overlap with the voxel probe used by SFS. SFS classifies these volumes through physics overlap checks, so imported mesh normals do not affect a Box Delimiter.

## Anchor range

Automatic range is the default. It covers the generated location's half diagonal plus a two-voxel margin. Manual range is available for experiments, but a value that is too small can truncate a location before its delimiters are reached.

## Validation and compute

**Check Scene** checks Foundation count, voxel size, delimiter layer and mask, collider state, anchor references and ranges, anchor/delimiter overlap, and unsafe parenting below a Foundation.

**Create Location** only creates editable scene objects. **Create and Compute** creates the location, validates it, synchronizes physics transforms, and then invokes the installed SFS Compute Graph command. With multiple Foundations loaded, compute is blocked because the current SFS backend resolves its Foundation globally.

The Scene preview is capped for large grids to keep authoring responsive. This cap affects only visualization, not layout creation.
