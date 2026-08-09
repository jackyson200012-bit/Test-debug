# Design: Procedural water system (per-chunk, stylized URP)

## Goal
A water system for the procedural terrain world: a flat water surface at the
configured `waterLevel`, integrated with the existing chunk-generate/pool
pipeline. Purely procedural — no Simulation, no heavy fluid. Only renders where
terrain actually dips below `waterLevel`, is seamless across chunk borders, and
animates cheaply on the GPU (world-position waves + shoreline foam).

## Chosen mechanism

### 1. Water surface = per-chunk child mesh
- Each terrain `Chunk` gets a child GameObject named `Water` with its own
  `MeshFilter` + `MeshRenderer`.
- A new `WaterMeshGenerator` builds a flat grid **at `waterLevel`** whose vertex
  positions exactly match the terrain grid positions (same chunkSize /
  verticesPerLine, world-space aligned) so the square water quads line up 1:1
  with terrain quads → tiling renders seamless across chunk borders.
- The mesh is **half-size per-chunk** (chunkSize×chunkSize) and offset to the
  chunk origin for clean AOV vs. the terrain mesh which is chunkSize×chunkSize
  *including* the shared edge. Both grids share the same unit spacing, so edges
  coincide.

### 2. Only where water exists
- `Chunk` reads `HeightMap` min/max (already computed) during `UpdateForCoord`.
  If the entire chunk is above `waterLevel`, the `Water` child is disabled.
- If any terrain `< waterLevel`, the water mesh is built (or reused) and the
  child set active.

### 3. Material & texture
- New shared material `Assets/Script/Materials/Water/Water.mat` built in-code
  during bootstrap (`WaterMaterialLocator` or similar ensuring only right),
  using `Assets/Water Voxel.png` (existing 512² flat) as `_BaseMap`, with
  `_BaseColor` default `(0.35, 0.68, 1.0, 0.9)`.
- Same z-writing `Queue Transparent` (alpha 0.9), so it overlays terrain under
  the camera's transparent pass. Fog disabled on the water (renders clean at
  distance).

### 4. Stylized shader: `Assets/Script/Shaders/Water/URPWater.shader`
A single unlit-ish URP shader for the water surface (URP 17), driven by
`WaveTime` (source-poisoned per existing foliage pattern), with:

| Property | Range | Purpose |
|---|---|---|
| `_WaveStrength` | 0–0.5 | vertical wave amplitude |
| `_WaveSpeed` | 0–3 | animation speed |
| `_WaveFrequency` | 0–10 | spatial frequency |
| `_NormalScale` | 0–2 | normal-map detail multiplier |
| `_ShallowColor` | color | blended near shore / shallow |
| `_DeepColor` | color | blended with depth |
| `_DepthMax` | 0–10 | depth ramp range |

- **Movement**: vertex displacement `y += sin(x·wf + t·ws)·str/2 + cos(z·wf ...)`,
  phase from `waveTime = _Time.y · _WaveSpeed` → waves scroll continuously.
- **Depth-blend**: fragment compares per-pixel `LinearEyeDepth(_CameraDepthTexture)`
  with the surface depth; `depthFactor = clamp(depth / _DepthMax)` mixes
  `_ShallowColor` → `_DeepColor`.
- **Shader-enabled**: uses `#if _WATER_DEPTH` only when depth texture present at runtime
  (PC quality has it; Mobile `m_RequireDepthTexture:0` → shader falls back to
  `_ShallowColor`).
- Uses `Water Voxel.png` tiling basecolor only if set (not strictly required).

### 5. Movement lag
- No `.Update()` loops. All motion is done in the vertex shader via `_Time`.

## Where it operates
- New `WaterMeshGenerator.cs` (`Assets/Script/Water/`) — static mesh factory with
  reused buffers (matches existing `MeshGenerator` pool style).
- `Chunk.cs` — construct the `Water` child **once** at `Awake`, regenerate the
  water mesh during `UpdateForCoord` alongside terrain, toggle via
  `chunkHasWater`.

## Files / assets
- New: `Assets/Script/Water/WaterMeshGenerator.cs`
- New: `Assets/Script/Shaders/Water/URPWater.shader`
- New mat: `Assets/Script/Materials/Water/Water.mat` (bootstrap default; can be
  re-assigned via `WorldSettings.waterOverride` if added later).
- Mod: `Assets/Script/HeightMap.cs` (no change — already tracks min/max) — none,
  but `Asset` `ChunkManager` etc. changes not needed.
- Mod: `Assets/Script/WorldSettings.cs` — add `[Header("Water")]` block of the 6
  shader tuning floats + `waterLevel` already exists.
- Reg: `compile_verify.bat` (add `Water\` sources), asset-db import for the
  `.shader`/mat.

## Risks
- Chord-chunk seams: water mesh vertex positions derive from the same chunk grid
  offset so square tiles coincide → minimal seam in flat color; verify with
  screenshots at borders (especially with waves disabled).
- Transparency sorting with foliage/billboards — water is rendered in transparent pass
  at terrain-level y=−5 far below most foliage; tent normals give gradual order.
- Mobile depth disabled → falls back to shallow-only (documented).
- Existing `waterLevel` is per-world, per-biome available in
  `BiomeTerrainParams.overrideWaterLevel` — water surface at world waterLevel is
  the requested global surface; biome overrides affect only foliage gen, no
  changes needed.