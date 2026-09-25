# Follow blendshapes by rewriting animation clips

**Context.** An SPS socket on a nipple had to follow the breast-size blendshapes, which are driven by a motion-time slider in the FX controller and, separately, by the wardrobe. The first solution was hand-made: offset transforms and a merged animator mirroring each driver. It broke as soon as the wardrobe configuration changed.

**Options.**
- Runtime vertex tracking — impossible on VRChat avatars (no scripts; blendshapes are GPU-side).
- Replace blendshapes with bones — changes the avatar's rig and every outfit.
- Mirror each known driver by hand — what failed.
- Rewrite every clip that writes the blendshape at build time — generic.

**Decision.** At build time (NDMF, Optimizing), add transform curves to exactly the clips that write each moving blendshape, one follow transform per blendshape. Whatever drives the blendshape drives the object.

**Consequences.** Works with any driver present at build time. Nothing moves in the editor, so the inspector shows the measured movement and the Scene view marks the corrected placement instead. A blendshape changed by something other than animation at runtime (none exist on VRChat) would not be followed.
