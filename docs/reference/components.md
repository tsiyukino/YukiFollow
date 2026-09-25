# Components (Runtime/)

## YukiSurfaceAnchor

`TsiYuki.Follow.YukiSurfaceAnchor : MonoBehaviour, IEditorOnly` — on the object that should stay on a surface. Consumed and removed at build.

- `SkinnedMeshRenderer surface` — mesh to follow; null picks the mesh with the nearest vertex within 5 cm at build.
- `bool followRotation` — also tilt with the surface (default true).
- `float sampleRadius` — radius of the surface patch used for the fit, 2–50 mm (default 8 mm). Widened automatically up to 8× when it holds fewer than 6 vertices.
- `List<string> ignoredBlendshapes` — name patterns (`*` wildcard, case-insensitive) that never move the object. Blendshapes driven by the depth animations of a VRCFury SPS socket on the same object are added automatically at build (Resolving phase), so a socket's own opening blendshapes do not push it around.
- `bool bound`, `SkinnedMeshRenderer bindSurface`, `List<BindWeight> bindWeights`, `Vector3 bindPoint`, `Quaternion bindRotation` — placement bind, written by the inspector. `bindPoint`/`bindRotation` are in the avatar root's space.

## YukiBlendshapeSync

`TsiYuki.Follow.YukiBlendshapeSync : MonoBehaviour, IEditorOnly` — one per avatar, anywhere inside it. Consumed and removed at build; extra instances are ignored.

- `List<SkinnedMeshRenderer> sources` — meshes followed; empty uses the `Body…` meshes directly under the avatar root. For a name present on several sources, the first listed wins.
- `List<SkinnedMeshRenderer> excludedMeshes` — meshes never changed.
- `List<string> excludedBlendshapes` — name patterns never synced (`*` wildcard, case-insensitive). Default: `Shrink*`, `vrc.*`, `=*`, `-*`, `*==*`.
- `bool copyDefaultWeights` — copy source scene values to followers at build (default true).
- `bool syncAnimations` — add follower curves to every clip that animates the source blendshape (default true). A clip that already animates the follower's blendshape is left alone.

## YukiSeamNormals

`TsiYuki.Follow.YukiSeamNormals : MonoBehaviour, IEditorOnly` — on a `SkinnedMeshRenderer` whose mesh meets the body at an open edge. Consumed and removed at build.

- `SkinnedMeshRenderer surface` — mesh to blend into; null picks the mesh with the nearest vertex within 5 cm of the part's open edges at build (the part itself excluded).
- `float blendWidth` — distance along the part from the seam at which its own normals are back, 1–50 mm (default 8 mm).
- `float seamGap` — open edges within this distance of the surface count as the seam, 0.5–20 mm (default 5 mm).
