# Architecture

Two independent components, one NDMF plugin (`FollowPlugin`, `moe.tsiyuki.follow`).

| Module | Assembly | Role |
|---|---|---|
| `YukiSurfaceAnchor`, `YukiBlendshapeSync` | TsiYuki.Follow (runtime) | Pure configuration, `IEditorOnly`, removed at build. |
| `SurfaceSolver` | Editor | Skins a mesh from its data and measures how each blendshape moves a point (rigid fit). |
| `SurfaceAnchorPass`, `AnchorBinding` | Editor | Build step for anchors; bind bookkeeping shared with the inspector. |
| `BlendshapeSyncPass`, `SyncPlan` | Editor | Build step for sync; the plan is shared with the inspector preview. |
| `FollowText` | Editor | Localized strings for inspectors and NDMF error reports. |
| `YukiSurfaceAnchorEditor`, `YukiBlendshapeSyncEditor` | Editor | Inspectors. |

## Why animation curves

VRChat avatars cannot run scripts, and blendshapes are evaluated on the GPU, so nothing at runtime can read where a vertex went. The only thing that knows a blendshape's value at runtime is the animation that sets it. The anchor step therefore adds transform curves to exactly the clips that write the blendshape. The curve is an affine map of the blendshape curve (`offset × (weight − rest) / frameWeight`), so tangents, write defaults, blend trees, motion time and layer priority behave exactly like the blendshape.

One empty "follow" transform is inserted above the object per animated blendshape. Separate transforms keep contributions additive when different layers drive different blendshapes (e.g. a breast-size slider and a wardrobe). Each follow transform sits at the object's rest position with the parent's axes, so its rotation pivots about the object.

Blendshapes move vertices linearly, so the per-blendshape translations add up exactly; rotations are small and combined as a chain, which is accurate to well under a millimetre for typical avatar blendshapes.

## Order

Both steps run in `Optimizing`, after Modular Avatar, VRCFury and the wardrobe produced their clips and before Avatar Optimizer merges meshes (which would rename blendshape bindings). Sync runs before anchors so an anchor on an outfit sees the outfit's synced animations. Clips are edited through NDMF's `AnimatorServicesContext`, which clones them, so project assets are never modified.

## Binding

`bindWeights` records the surface's blendshape values when the object was placed. At build, if the scene values differ, the object is first moved from the bind geometry to the current one. Moving the object after binding voids the bind (the new place is taken as placed at the current values).
