using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace TsiYuki.Follow.Editor
{
    // Hierarchy right-click: GameObject > TsiYuki > Follow > Surface Anchor / Blendshape Sync / Seam Normals.
    // The components are also under Add Component > TsiYuki > Follow.
    internal static class FollowMenu
    {
        const string Root = "GameObject/TsiYuki/Follow/";
        const string AnchorPath = Root + "Surface Anchor";
        const string SyncPath = Root + "Blendshape Sync";
        const string SeamPath = Root + "Seam Normals";

        [MenuItem(AnchorPath, false, 22)]
        internal static void AddAnchor(MenuCommand command)
        {
            if (FirstCall(command)) Add<YukiSurfaceAnchor>(Selection.gameObjects.Where(InsideAvatar));
        }

        [MenuItem(AnchorPath, true)]
        internal static bool CanAddAnchor() => Selection.gameObjects.Any(InsideAvatar);

        // One per avatar: selects the avatar's existing sync instead of adding a second.
        [MenuItem(SyncPath, false, 23)]
        internal static void AddSync(MenuCommand command)
        {
            if (!FirstCall(command) || Selection.activeGameObject == null) return;
            var avatar = Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (avatar == null) return;
            var sync = avatar.GetComponentInChildren<YukiBlendshapeSync>(true);
            if (sync == null) sync = Undo.AddComponent<YukiBlendshapeSync>(avatar.gameObject);
            Selection.activeGameObject = sync.gameObject;
            EditorGUIUtility.PingObject(sync.gameObject);
        }

        [MenuItem(SyncPath, true)]
        internal static bool CanAddSync() =>
            Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>(true) != null;

        [MenuItem(SeamPath, false, 24)]
        internal static void AddSeam(MenuCommand command)
        {
            if (FirstCall(command)) Add<YukiSeamNormals>(Selection.gameObjects.Where(IsSkinnedPart));
        }

        [MenuItem(SeamPath, true)]
        internal static bool CanAddSeam() => Selection.gameObjects.Any(IsSkinnedPart);

        // Unity calls a GameObject menu item once per selected object; act on the whole selection once.
        static bool FirstCall(MenuCommand command) => command.context == null || command.context == Selection.activeGameObject;

        static bool InsideAvatar(GameObject go) =>
            go.GetComponent<VRCAvatarDescriptor>() == null && go.GetComponentInParent<VRCAvatarDescriptor>(true) != null;

        static bool IsSkinnedPart(GameObject go) => InsideAvatar(go) && go.GetComponent<SkinnedMeshRenderer>() != null;

        static void Add<T>(IEnumerable<GameObject> objects) where T : Component
        {
            var touched = new List<GameObject>();
            foreach (var go in objects)
            {
                if (go.GetComponent<T>() == null) Undo.AddComponent<T>(go);
                touched.Add(go);
            }
            if (touched.Count > 0) Selection.objects = touched.ToArray();
        }
    }
}
