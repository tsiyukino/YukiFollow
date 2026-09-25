using TsiYuki.Core.Editor;
using UnityEditor;

namespace TsiYuki.Follow.Editor
{
    // UI strings (Localization/<code>.txt) for both the inspectors and NDMF's error report window.
    internal static class FollowText
    {
        public const string Package = "moe.tsiyuki.follow";
        public static readonly YukiLocalizer L = new YukiLocalizer(Package);

        static string _version;

        public static string Version
        {
            get
            {
                if (_version != null) return _version;
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(FollowText).Assembly);
                return _version = info != null ? "v" + info.version : "";
            }
        }

        public static readonly YukiNdmfReport Errors = new YukiNdmfReport(L);
    }
}
