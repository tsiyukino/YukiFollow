using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;

[assembly: ExportsPlugin(typeof(TsiYuki.Follow.Editor.FollowPlugin))]

namespace TsiYuki.Follow.Editor
{
    /// <summary>
    /// Both steps rewrite animation clips, so they run late (Optimizing): after Modular Avatar,
    /// VRCFury and the wardrobe have produced every clip that drives a blendshape, and before
    /// Avatar Optimizer merges meshes and renames their blendshapes. Sync runs first so that
    /// anchors on outfits see the outfit's newly synced animations.
    /// </summary>
    public sealed class FollowPlugin : Plugin<FollowPlugin>
    {
        public override string QualifiedName => "moe.tsiyuki.follow";
        public override string DisplayName => "Yuki Follow";

        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving)
                .Run("Collect SPS depth blendshapes", AnchorBinding.CollectSocketShapes);

            InPhase(BuildPhase.Optimizing)
                .AfterPlugin("nadena.dev.modular-avatar")
                .AfterPlugin("moe.tsiyuki.wardrobe")
                .BeforePlugin("com.anatawa12.avatar-optimizer")
                .WithRequiredExtension(typeof(AnimatorServicesContext), seq =>
                {
                    seq.Run("Sync blendshapes", BlendshapeSyncPass.Run);
                    seq.Run("Anchor objects to surfaces", SurfaceAnchorPass.Run);
                });
        }
    }
}
