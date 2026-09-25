using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace TsiYuki.Follow
{
    /// <summary>
    /// Makes every mesh on the avatar follow the body's blendshapes of the same name, like Modular
    /// Avatar's Blendshape Sync but for all meshes at once. Outfits, accessories and hair that ship
    /// with e.g. "Breasts_big" change together with the body, whatever animates the body.
    ///
    /// Applied when the avatar is built; the scene is not changed.
    /// </summary>
    [AddComponentMenu("TsiYuki/Yuki Blendshape Sync")]
    [DisallowMultipleComponent]
    [HelpURL("https://github.com/tsiyukino/YukiFollow")]
    public sealed class YukiBlendshapeSync : MonoBehaviour, IEditorOnly
    {
        [Tooltip("Meshes whose blendshapes the others follow. Empty uses the meshes named \"Body…\" directly under the avatar.")]
        public List<SkinnedMeshRenderer> sources = new List<SkinnedMeshRenderer>();

        [Tooltip("Meshes that keep their own blendshapes.")]
        public List<SkinnedMeshRenderer> excludedMeshes = new List<SkinnedMeshRenderer>();

        [Tooltip("Blendshape names that are never synced. * matches anything; case is ignored.")]
        public List<string> excludedBlendshapes = new List<string>(DefaultExclusions);

        [Tooltip("Give followers the body's scene values when the avatar is built.")]
        public bool copyDefaultWeights = true;

        [Tooltip("Add the followers to every animation that changes the body's blendshape.")]
        public bool syncAnimations = true;

        // Body-only shapes (shrinking skin under clothes), visemes and separator lines.
        public static readonly string[] DefaultExclusions = { "Shrink*", "vrc.*", "=*", "-*", "*==*" };
    }
}
