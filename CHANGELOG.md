# Changelog

## [0.2.1] - 2026-09-26

### Changed
- Build errors are reported to NDMF through TsiYuki Core's `YukiNdmfReport` instead of this package's own copy.
  The messages are unchanged. Requires TsiYuki Core 0.4.0.

## [0.2.0] - 2026-09-26

### Added
- `Yuki Seam Normals` component: a separate mesh that fills a hole in the body (a replaced body part) shades like the body where the two meet. At build time the normals along its seam with the body take the body's and fade back to its own over a set width, walked along the mesh so its own detail keeps its shading. The inspector previews how many vertices change and by how much, and the Scene view marks the seam.
- Hierarchy right-click menu **TsiYuki > Follow** with Surface Anchor, Blendshape Sync and Seam Normals. Surface Anchor and Seam Normals go on every selected object inside an avatar (Seam Normals only on skinned meshes); Blendshape Sync goes on the avatar root, or selects the avatar's existing one.
- EditMode tests for the closest-point query, the seam solver and the menu.

### Changed
- The components moved under **Add Component > TsiYuki > Follow**. Existing components are not affected.

## [0.1.0] - 2026-09-24

### Added
- `Yuki Surface Anchor` component: keeps an object (an SPS socket, a name tag, a charm) on a point of a skinned mesh while the mesh's blendshapes change. Position and tilt follow wherever the blendshape is animated from (menus, wardrobes, Modular Avatar, VRCFury), including motion-time sliders, blend trees and several layers at once.
- Blendshapes an SPS socket's own depth animations drive (e.g. its opening blendshapes) are ignored automatically; more can be listed per anchor.
- Placement binding: moving a blendshape slider in the scene after placing the object no longer leaves it floating; the object is moved onto the surface at build time and the Scene view shows where.
- `Yuki Blendshape Sync` component: every mesh on the avatar follows the body's blendshapes of the same name (scene values and animations), with excluded meshes and name patterns. Shrink blendshapes, visemes and separator lines are excluded by default.
- Inspectors list the blendshapes that move an anchored point and the blendshapes each mesh will follow.
- English, Chinese and Japanese UI.
