# Changelog

## [0.1.0] - 2026-09-24

### Added
- `Yuki Surface Anchor` component: keeps an object (an SPS socket, a name tag, a charm) on a point of a skinned mesh while the mesh's blendshapes change. Position and tilt follow wherever the blendshape is animated from (menus, wardrobes, Modular Avatar, VRCFury), including motion-time sliders, blend trees and several layers at once.
- Blendshapes an SPS socket's own depth animations drive (e.g. its opening blendshapes) are ignored automatically; more can be listed per anchor.
- Placement binding: moving a blendshape slider in the scene after placing the object no longer leaves it floating; the object is moved onto the surface at build time and the Scene view shows where.
- `Yuki Blendshape Sync` component: every mesh on the avatar follows the body's blendshapes of the same name (scene values and animations), with excluded meshes and name patterns. Shrink blendshapes, visemes and separator lines are excluded by default.
- Inspectors list the blendshapes that move an anchored point and the blendshapes each mesh will follow.
- English, Chinese and Japanese UI.
