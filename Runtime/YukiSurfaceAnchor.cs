using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace TsiYuki.Follow
{
    /// <summary>
    /// Keeps this object on a point of a skinned mesh while the mesh's blendshapes change, like a
    /// vertex parent in Blender. The object follows position and (optionally) the surface's tilt.
    ///
    /// Nothing moves in the editor. When the avatar is built (Play mode or upload), every animation
    /// that drives a blendshape moving this point also gets matching transform curves, so the object
    /// follows wherever the blendshape is animated from: menus, wardrobes, Modular Avatar, VRCFury.
    /// </summary>
    [AddComponentMenu("TsiYuki/Follow/Yuki Surface Anchor")]
    [DisallowMultipleComponent]
    [HelpURL("https://github.com/tsiyukino/YukiFollow")]
    public sealed class YukiSurfaceAnchor : MonoBehaviour, IEditorOnly
    {
        [Tooltip("Mesh this object sticks to. Empty uses the mesh whose surface is nearest when the avatar is built.")]
        public SkinnedMeshRenderer surface;

        [Tooltip("Tilt with the surface as well as move with it.")]
        public bool followRotation = true;

        [Tooltip("Radius of the surface patch around this object used to measure its movement.")]
        [Range(0.002f, 0.05f)]
        public float sampleRadius = 0.008f;

        [Tooltip("Blendshapes that must not move this object. * matches anything; case is ignored. Blendshapes driven by an SPS socket's own depth animations on this object are ignored automatically.")]
        public List<string> ignoredBlendshapes = new List<string>();

        // Bind: the blendshape weights the surface had when the object was placed. At build time the
        // object is first moved from this state to the weights the mesh has then, so changing a
        // blendshape slider in the scene after placing the object does not leave it floating.
        public bool bound;
        public SkinnedMeshRenderer bindSurface;
        public List<BindWeight> bindWeights = new List<BindWeight>();

        // Pose (in the avatar root's space) when bound. If the object is moved afterwards, the new
        // place counts as placed at the current weights and the bind is ignored.
        public Vector3 bindPoint;
        public Quaternion bindRotation = Quaternion.identity;
    }

    [Serializable]
    public sealed class BindWeight
    {
        public string blendshape;
        public float weight;
    }
}
