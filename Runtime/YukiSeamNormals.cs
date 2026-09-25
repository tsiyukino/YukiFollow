using UnityEngine;
using VRC.SDKBase;

namespace TsiYuki.Follow
{
    /// <summary>
    /// Makes this mesh shade like the body where the two meet. A separate mesh that fills a hole in
    /// the body (a replaced body part, a cap) keeps its author's normals, which rarely match the
    /// body's, so the seam shows under directional light. When the avatar is built, the normals
    /// along the seam are replaced by the body surface's and fade back to this mesh's own over the
    /// blend width. Nothing changes in the editor.
    /// </summary>
    [AddComponentMenu("TsiYuki/Follow/Yuki Seam Normals")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    [HelpURL("https://github.com/tsiyukino/YukiFollow")]
    public sealed class YukiSeamNormals : MonoBehaviour, IEditorOnly
    {
        [Tooltip("Mesh this one blends into. Empty uses the mesh nearest to the seam when the avatar is built.")]
        public SkinnedMeshRenderer surface;

        [Tooltip("Distance along this mesh from the seam over which the normals return to their own.")]
        [Range(0.001f, 0.05f)]
        public float blendWidth = 0.008f;

        [Tooltip("Open edges of this mesh closer than this to the surface count as the seam.")]
        [Range(0.0005f, 0.02f)]
        public float seamGap = 0.005f;
    }
}
