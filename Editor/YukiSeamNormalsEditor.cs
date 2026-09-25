using TsiYuki.Core.Editor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace TsiYuki.Follow.Editor
{
    [CustomEditor(typeof(YukiSeamNormals))]
    [CanEditMultipleObjects]
    internal sealed class YukiSeamNormalsEditor : UnityEditor.Editor
    {
        static YukiLocalizer L => FollowText.L;

        SeamNormalResult _result;
        SkinnedMeshRenderer _resultSurface;
        int _resultKey;

        YukiSeamNormals Target => (YukiSeamNormals)target;

        void OnEnable() => YukiLanguage.Changed += Repaint;
        void OnDisable() => YukiLanguage.Changed -= Repaint;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            YukiGUI.Header("Yuki Seam Normals", FollowText.Version);
            EditorGUILayout.LabelField(L["seam.intro"], YukiGUI.WrapMini);

            var avatar = Target.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (avatar == null)
            {
                EditorGUILayout.HelpBox(L["ui.no_avatar"], MessageType.Error);
                return;
            }

            YukiGUI.Section(L["seam.settings"]);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("surface"), new GUIContent(L["seam.surface"], L["seam.surface.tip"]));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("blendWidth"), new GUIContent(L["seam.blend_width"], L["seam.blend_width.tip"]));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("seamGap"), new GUIContent(L["seam.seam_gap"], L["seam.seam_gap.tip"]));
            serializedObject.ApplyModifiedProperties();

            if (targets.Length > 1) return;
            DrawPreview(avatar);
        }

        void DrawPreview(VRCAvatarDescriptor avatar)
        {
            YukiGUI.Section(L["seam.preview"]);
            var result = Solve(avatar);
            if (_resultSurface == null)
            {
                EditorGUILayout.HelpBox(L["seam.no_surface_now"], MessageType.Warning);
                return;
            }
            if (Target.surface == null)
                EditorGUILayout.LabelField(L.Tr("seam.surface.auto", _resultSurface.name), YukiGUI.WrapMini);
            if (result.Error != null)
            {
                EditorGUILayout.HelpBox(L.Tr("seam.error." + result.Error, (Target.seamGap * 1000f).ToString("F1")), MessageType.Warning);
                return;
            }
            EditorGUILayout.LabelField(L.Tr("seam.preview.summary", result.SeamVertices, result.BlendedVertices,
                result.MeanAngle.ToString("F1"), result.MaxAngle.ToString("F1")), YukiGUI.WrapMini);
        }

        // Solving skins both meshes; cache it until the meshes, their weights or the settings change.
        SeamNormalResult Solve(VRCAvatarDescriptor avatar)
        {
            var renderer = Target.GetComponent<SkinnedMeshRenderer>();
            var key = Key(renderer);
            if (_result != null && key == _resultKey) return _result;
            _resultKey = key;
            _result = new SeamNormalResult();
            _resultSurface = null;
            if (renderer == null || renderer.sharedMesh == null) return _result;

            var part = SeamNormalsPass.Geometry(renderer, out _);
            _resultSurface = SeamNormalsPass.ResolveSurface(avatar.transform, Target, renderer, part);
            if (_resultSurface != null)
                _result = SeamNormalSolver.Solve(part, SeamNormalsPass.Geometry(_resultSurface, out _), Target.blendWidth, Target.seamGap);
            return _result;
        }

        int Key(SkinnedMeshRenderer renderer)
        {
            unchecked
            {
                int h = Target.blendWidth.GetHashCode() * 31 + Target.seamGap.GetHashCode();
                foreach (var smr in new[] { renderer, Target.surface != null ? Target.surface : _resultSurface })
                {
                    if (smr == null || smr.sharedMesh == null) { h = h * 31 + 1; continue; }
                    h = h * 31 + smr.GetInstanceID();
                    h = h * 31 + smr.transform.position.GetHashCode();
                    for (int b = 0; b < smr.sharedMesh.blendShapeCount; b++) h = h * 31 + smr.GetBlendShapeWeight(b).GetHashCode();
                }
                return h;
            }
        }

        // The seam the build will use, marked on the mesh.
        void OnSceneGUI()
        {
            if (targets.Length > 1 || _result == null || _result.Error != null) return;
            Handles.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            foreach (var p in _result.SeamPoints)
                Handles.DotHandleCap(0, p, Quaternion.identity, HandleUtility.GetHandleSize(p) * 0.012f, EventType.Repaint);
        }
    }
}
