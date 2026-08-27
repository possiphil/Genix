# SFS authoring

Genix includes an editor workflow for creating voxel-aligned Space Foundation System (SFS) inputs. Open it through **Tools > Genix > SFS Authoring**. Individual elements are also available from **GameObject > Genix > Space Foundation**.

## Basic elements

- **Space Foundation** creates the SFS configuration object. Its voxel size defines the spatial grid.
- **Anchor** creates a location seed and links it to the selected Foundation.
- **Box Delimiter** creates a collider-backed delimiter on the configured delimiter layer.
- **Convert Colliders** adds delimiter components and layer configuration to selected collider objects without duplicating existing components.

Anchors and delimiters must not be children of the `SpaceFoundation` GameObject. SFS clears that object's children before computing. The authoring commands therefore place such elements beside the Foundation when a Foundation hierarchy is used as their context.

## Layout generators

### Bounded Location

Creates one rectangular location, six boundary volumes, and one anchor. Use world units for designer-facing dimensions, exact voxel counts for reproducible grid tests, or Fit Selection to derive the center and size from selected colliders and renderers.

### Location Grid

Creates aligned adjacent or stacked locations. `Grid Counts` controls the location count on X, Y, and Z. Uniform sizing is the simplest option. Per-axis sizing assigns one width to each X column, one height to each Y level, and one depth to each Z row while preserving alignment.

Every internal division reserves at least one blocked voxel band. A `2 x 1 x 1` grid with two ten-cell rooms and one separator therefore occupies 21 inner cells, not 20. Each division is one continuous shared delimiter slab rather than duplicate walls per room.

### Footprint Location

Creates one connected non-rectangular location by extruding a two-dimensional occupancy mask. Rectangle, L, U, T, and courtyard templates are provided. Custom masks must be connected through horizontal or vertical neighbours; diagonally touching modules alone do not form one SFS location.

`Cells Per Module` controls horizontal footprint resolution, while `Height Cells` controls vertical free space.

## Voxel alignment

World-space sizes always round up to whole voxel cells and never become smaller than requested. The preview reports both requested and actual center and size. The center can move by up to half a voxel per axis because SFS voxel centers lie on integer multiples of the Foundation's voxel size.

Generated colliders occupy the planned blocked cells with a small inset. This leaves a numerical gap to neighbouring free cells while retaining overlap with the voxel probe used by SFS.

## Anchor range

Automatic range is the default. It covers the generated location's half diagonal plus a two-voxel margin. Manual range is available for experiments, but a value that is too small can truncate a location before its delimiters are reached.

## Validation and compute

**Validate Scene** checks Foundation count, voxel size, delimiter layer and mask, collider state, anchor references and ranges, anchor/delimiter overlap, and unsafe parenting below a Foundation.

**Create Layout** only creates editable scene objects. **Create + Compute** creates the layout, validates it, synchronizes physics transforms, and then invokes the installed SFS Compute Graph command. With multiple Foundations loaded, compute is blocked because the current SFS backend resolves its Foundation globally.

The Scene preview is capped for large grids to keep authoring responsive. This cap affects only visualization, not layout creation.
