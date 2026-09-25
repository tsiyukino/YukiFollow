# Blend seam normals by distance walked from the seam

**Context.** A body part sold as a separate mesh (真実の穴 on Shinano) replaces a region of the body whose polygons are removed by a mask. In worlds with strong or probe lighting the seam showed as a colour step. The textures already matched at the seam; two things did not: the part's probe anchor (Hips, while the body uses a chest anchor 28 cm away; fixed in the scene, not here) and its normals, which were about 30° off the body surface they replace.

**Options.**
- Edit the part's normals once in Blender (Data Transfer). Works, but leaves a derived mesh that must be redone whenever the part or the body mesh changes.
- Blend toward the body's normals wherever the part lies close to the body surface. Measured on the real part, 1075 of 1283 vertices of its inner detail lie within 3 mm of the old body surface, so this would flatten exactly what the part adds.
- Blend toward the body's normals by distance walked along the part from its open edge on the body, at build time.

**Decision.** The third. The seam is the part's open edges within `seamGap` of the body; weights fall from 1 there to 0 at `blendWidth` along the part's own edges; targets are the body's interpolated normals at the closest surface point, measured before Avatar Optimizer removes the body polygons under the part.

**Consequences.** Detail further than `blendWidth` from the seam keeps its author's shading untouched (on the test part: 411 of 5397 normals change, none in the canal or the inner detail). Normals are set for the blendshape values at build time; blendshapes that move the seam a lot bend the part's normals by their own deltas only. The part's mesh is replaced by a generated copy in the build output.
