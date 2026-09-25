# Architecture

Three independent components, one NDMF plugin (`FollowPlugin`, `moe.tsiyuki.follow`).

| Module | Assembly | Role |
|---|---|---|
| `YukiSurfaceAnchor`, `YukiBlendshapeSync`, `YukiSeamNormals` | TsiYuki.Follow (runtime) | Pure configuration, `IEditorOnly`, removed at build. |
| `SurfaceSolver` | Editor | Skins a mesh from its data (positions, normals, per-vertex matrices) and measures how each blendshape moves a point (rigid fit). |
| `SurfaceAnchorPass`, `AnchorBinding` | Editor | Build step for anchors; bind bookkeeping shared with the inspector. |
| `BlendshapeSyncPass`, `SyncPlan` | Editor | Build step for sync; the plan is shared with the inspector preview. |
| `SurfaceQuery` | Editor | Closest point on a triangle mesh, with the vertex normals interpolated there. |
| `SeamNormalSolver` | Editor | Finds where a part meets the body and blends the part's normals toward the body's. Arrays in, arrays out. |
| `SeamNormalsPass` | Editor | Build step for seam normals; its geometry helper is shared with the inspector preview. |
| `FollowText` | Editor | Localized strings for inspectors and NDMF error reports. |
| `YukiSurfaceAnchorEditor`, `YukiBlendshapeSyncEditor`, `YukiSeamNormalsEditor` | Editor | Inspectors. |
| `FollowMenu` | Editor | Hierarchy right-click entries under `GameObject/TsiYuki/Follow/` that add the components to the selection. |
| `SurfaceQueryTests`, `SeamNormalSolverTests`, `FollowMenuTests` | TsiYuki.Follow.Editor.Tests | EditMode tests on generated geometry and a throwaway scene. |

## Why animation curves

VRChat avatars cannot run scripts, and blendshapes are evaluated on the GPU, so nothing at runtime can read where a vertex went. The only thing that knows a blendshape's value at runtime is the animation that sets it. The anchor step therefore adds transform curves to exactly the clips that write the blendshape. The curve is an affine map of the blendshape curve (`offset × (weight − rest) / frameWeight`), so tangents, write defaults, blend trees, motion time and layer priority behave exactly like the blendshape.

One empty "follow" transform is inserted above the object per animated blendshape. Separate transforms keep contributions additive when different layers drive different blendshapes (e.g. a breast-size slider and a wardrobe). Each follow transform sits at the object's rest position with the parent's axes, so its rotation pivots about the object.

Blendshapes move vertices linearly, so the per-blendshape translations add up exactly; rotations are small and combined as a chain, which is accurate to well under a millimetre for typical avatar blendshapes.

## Seam normals

A part that fills a hole in the body (a replaced body part, the body's polygons under it removed by a mask) is a second mesh with its author's normals. Where it meets the body the two rarely agree, and lighting shows the seam. Normals are all that decides the shading difference once both use the same material settings and probe anchor, so the fix is to make them agree at the seam and nowhere else.

The seam is every open edge of the part within `seamGap` of the body surface. From there the solver walks along the part's edges (Dijkstra over position-welded vertices) and replaces each normal by the body's interpolated normal at the closest surface point, weighted 1 on the seam and easing to 0 at `blendWidth`. Walking along the part rather than measuring the gap to the body matters: a replaced part usually lies on the old body surface almost everywhere, detail included, and a gap test would flatten that detail. The result is written back in mesh space through each vertex's skin matrix, so it holds while skinning; blendshape normal deltas are left as they are.

## Order

All three steps run in `Optimizing`, after Modular Avatar, VRCFury and the wardrobe produced their clips and before Avatar Optimizer merges meshes (which would rename blendshape bindings). Sync runs before anchors so an anchor on an outfit sees the outfit's synced animations. Clips are edited through NDMF's `AnimatorServicesContext`, which clones them, so project assets are never modified. Seam normals run last, at the synced blendshape values, and before Avatar Optimizer removes body polygons under the part: those polygons are the surface the part replaces. The part's mesh is replaced by a copy saved through NDMF's asset saver.

## Binding

`bindWeights` records the surface's blendshape values when the object was placed. At build, if the scene values differ, the object is first moved from the bind geometry to the current one. Moving the object after binding voids the bind (the new place is taken as placed at the current values).
