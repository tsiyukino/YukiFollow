# Build passes (Editor/)

`FollowPlugin` — `InPhase(Resolving)`: **Collect SPS depth blendshapes** (`AnchorBinding.CollectSocketShapes`) copies the blendshape names of a VRCFury socket's depth actions on an anchored object into its `ignoredBlendshapes`, before VRCFury consumes the socket component.

Then `InPhase(Optimizing)`, after `nadena.dev.modular-avatar` and `moe.tsiyuki.wardrobe`, before `com.anatawa12.avatar-optimizer`, with `AnimatorServicesContext`:

1. **Sync blendshapes** (`BlendshapeSyncPass.Run`)
   - Resolves a `SyncPlan` (source, follower, name) for every follower blendshape.
   - Copies scene weights when `copyDefaultWeights`.
   - For every clip that animates `source.blendShape.<name>`, adds an identical curve for each follower that the clip does not already animate.
2. **Anchor objects to surfaces** (`SurfaceAnchorPass.Run`), per anchor:
   - Resolves the surface; `AnchorBinding.EffectiveBind` gives the bind weights or null.
   - `SurfaceSolver.Solve` skins the mesh at the bind (or current) weights, samples the patch around the object, and for each blendshape fits a weighted rigid motion (translation = weighted mean displacement, rotation = Horn's quaternion fit). Ignored blendshapes are skipped; motions under 0.05 mm and 0.05° are dropped.
   - Applies the bind correction to the object's pose.
   - Keeps motions whose blendshape binding is animated (`AnimationIndex.GetClipsForBinding`).
   - Inserts `"<name> (Follow <blendshape>)"` transforms between the parent and the object, one per kept motion.
   - In every clip that animates the blendshape, writes `m_LocalPosition.*` and, when following rotation, `localEulerAnglesRaw.*` on the matching follow transform, mapped from the blendshape curve.
   - Replaces the object with the top follow transform in `ignoreTransforms` of PhysBones whose chain contains it, so no follow transform joins a chain.
   - Warns when the object's own transform is animated.

Errors go to NDMF's error report through `FollowText.Report` (keys `error.*` in `Localization/`).
