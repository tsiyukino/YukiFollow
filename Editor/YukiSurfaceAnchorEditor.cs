using System.Linq;
using TsiYuki.Core.Editor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace TsiYuki.Follow.Editor
{
    [CustomEditor(typeof(YukiSurfaceAnchor))]
    [CanEditMultipleObjects]
    internal sealed class YukiSurfaceAnchorEditor : UnityEditor.Editor
    {
        static YukiLocalizer L => FollowText.L;

        AnchorSolution _solution;
        int _solutionKey;
        bool _showAll;

        YukiSurfaceAnchor Target => (YukiSurfaceAnchor)target;

        void OnEnable()
        {
            YukiLanguage.Changed += Repaint;
            // A freshly added anchor is bound to the surface's weights right away.
            foreach (var t in targets.Cast<YukiSurfaceAnchor>())
            {
                if (t.bound) continue;
                var avatar = t.GetComponentInParent<VRCAvatarDescriptor>(true);
                if (avatar == null) continue;
                var surface = t.surface != null ? t.surface : SurfaceSolver.FindNearestSurface(avatar.transform, t.transform.position);
                if (surface == null) continue;
                Undo.RecordObject(t, "Bind Surface Anchor");
                if (t.surface == null) t.surface = surface;
                AnchorBinding.Bind(t, avatar.transform, surface);
                EditorUtility.SetDirty(t);
            }
        }

        void OnDisable() => YukiLanguage.Changed -= Repaint;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            YukiGUI.Header("Yuki Surface Anchor", FollowText.Version);
            EditorGUILayout.LabelField(L["anchor.intro"], YukiGUI.WrapMini);

            var avatar = Target.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (avatar == null)
            {
                EditorGUILayout.HelpBox(L["ui.no_avatar"], MessageType.Error);
                return;
            }

            YukiGUI.Section(L["anchor.settings"]);
            var surfaceProp = serializedObject.FindProperty("surface");
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(surfaceProp, new GUIContent(L["anchor.surface"], L["anchor.surface.tip"]));
                var changed = EditorGUI.EndChangeCheck();
                if (GUILayout.Button(L["anchor.find_nearest"], GUILayout.Width(110)))
                {
                    surfaceProp.objectReferenceValue = SurfaceSolver.FindNearestSurface(avatar.transform, Target.transform.position);
                    changed = true;
                }
                if (changed)
                {
                    serializedObject.ApplyModifiedProperties();
                    Rebind(avatar);
                }
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("followRotation"), new GUIContent(L["anchor.follow_rotation"], L["anchor.follow_rotation.tip"]));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sampleRadius"), new GUIContent(L["anchor.sample_radius"], L["anchor.sample_radius.tip"]));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("ignoredBlendshapes"), new GUIContent(L["anchor.ignored"], L["anchor.ignored.tip"]), true);
            serializedObject.ApplyModifiedProperties();
            var socketShapes = AnchorBinding.SocketDepthShapes(Target);
            if (socketShapes.Count > 0)
                EditorGUILayout.LabelField(L.Tr("anchor.ignored.sps", string.Join(", ", socketShapes)), YukiGUI.WrapMini);

            if (targets.Length > 1) return;

            var surface = Target.surface;
            if (surface == null)
            {
                EditorGUILayout.HelpBox(L["anchor.no_surface_now"], MessageType.Warning);
                return;
            }

            DrawBind(avatar, surface);
            DrawMotions(avatar, surface);
        }

        void DrawBind(VRCAvatarDescriptor avatar, SkinnedMeshRenderer surface)
        {
            YukiGUI.Section(L["anchor.bind"]);
            if (!Target.bound || Target.bindSurface != surface)
                EditorGUILayout.HelpBox(L["anchor.bind.none"], MessageType.Info);
            else if (AnchorBinding.MovedSinceBind(Target, avatar.transform))
                EditorGUILayout.HelpBox(L["anchor.bind.moved"], MessageType.Info);
            else
            {
                var solution = Solve(avatar, surface);
                if (solution != null && solution.Corrected)
                {
                    var moveMm = (solution.RestPosition - Target.transform.position).magnitude * 1000f;
                    EditorGUILayout.HelpBox(L.Tr("anchor.bind.corrected", moveMm.ToString("F1")), MessageType.Info);
                }
                else EditorGUILayout.LabelField(L["anchor.bind.ok"], YukiGUI.WrapMini);
            }
            if (GUILayout.Button(L["anchor.rebind"])) Rebind(avatar);
        }

        void DrawMotions(VRCAvatarDescriptor avatar, SkinnedMeshRenderer surface)
        {
            YukiGUI.Section(L["anchor.motions"]);
            var solution = Solve(avatar, surface);
            if (solution == null) return;
            if (solution.Error != null)
            {
                EditorGUILayout.HelpBox(L["anchor.error." + solution.Error], MessageType.Warning);
                return;
            }
            if (solution.SurfaceDistance > 0.01f)
                EditorGUILayout.HelpBox(L.Tr("anchor.far", (solution.SurfaceDistance * 1000f).ToString("F1")), MessageType.Warning);

            EditorGUILayout.LabelField(L.Tr("anchor.samples", solution.SampleCount, (solution.SampleRadius * 1000f).ToString("F1")), YukiGUI.WrapMini);
            if (solution.Motions.Count == 0)
            {
                EditorGUILayout.LabelField(L["anchor.motions.none"], YukiGUI.WrapMini);
                return;
            }
            EditorGUILayout.LabelField(L["anchor.motions.hint"], YukiGUI.WrapMini);
            int shown = _showAll ? solution.Motions.Count : Mathf.Min(8, solution.Motions.Count);
            for (int i = 0; i < shown; i++)
            {
                var m = solution.Motions[i];
                EditorGUILayout.LabelField(m.Name, L.Tr("anchor.motion_row", (m.Distance * 1000f).ToString("F1"), m.Angle.ToString("F1")));
            }
            if (solution.Motions.Count > 8)
                _showAll = EditorGUILayout.Foldout(_showAll, L.Tr("anchor.show_all", solution.Motions.Count), true);
        }

        void Rebind(VRCAvatarDescriptor avatar)
        {
            Undo.RecordObject(Target, "Bind Surface Anchor");
            AnchorBinding.Bind(Target, avatar.transform, Target.surface);
            EditorUtility.SetDirty(Target);
            _solution = null;
        }

        // Solving skins the whole mesh; cache it until the object, weights or settings change.
        AnchorSolution Solve(VRCAvatarDescriptor avatar, SkinnedMeshRenderer surface)
        {
            var key = Key(surface);
            if (_solution != null && key == _solutionKey) return _solution;
            _solutionKey = key;
            var bind = AnchorBinding.EffectiveBind(Target, avatar.transform, surface);
            _solution = SurfaceSolver.Solve(surface, Target.transform.position, Target.transform.rotation, Target.sampleRadius, Target.followRotation, bind,
                AnchorBinding.IgnoreFilter(Target));
            return _solution;
        }

        int Key(SkinnedMeshRenderer surface)
        {
            unchecked
            {
                int h = surface.GetInstanceID();
                h = h * 31 + Target.transform.position.GetHashCode();
                h = h * 31 + Target.transform.rotation.GetHashCode();
                h = h * 31 + Target.sampleRadius.GetHashCode();
                h = h * 31 + Target.followRotation.GetHashCode();
                h = h * 31 + Target.bound.GetHashCode();
                h = h * 31 + Target.bindWeights.Count;
                foreach (var p in Target.ignoredBlendshapes) h = h * 31 + (p ?? "").GetHashCode();
                var mesh = surface.sharedMesh;
                if (mesh != null)
                    for (int b = 0; b < mesh.blendShapeCount; b++) h = h * 31 + surface.GetBlendShapeWeight(b).GetHashCode();
                return h;
            }
        }

        // Where the object will be at the scene's current blendshape values.
        void OnSceneGUI()
        {
            if (targets.Length > 1 || Target.surface == null) return;
            var avatar = Target.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (avatar == null) return;
            var solution = Solve(avatar, Target.surface);
            if (solution == null || solution.Error != null) return;

            var size = HandleUtility.GetHandleSize(solution.RestPosition) * 0.08f;
            Handles.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            Handles.DrawWireDisc(solution.RestPosition, solution.RestRotation * Vector3.forward, solution.SampleRadius);
            if (solution.Corrected)
            {
                Handles.DrawDottedLine(Target.transform.position, solution.RestPosition, 3f);
                Handles.SphereHandleCap(0, solution.RestPosition, Quaternion.identity, size, EventType.Repaint);
                Handles.ArrowHandleCap(0, solution.RestPosition, solution.RestRotation, size * 6f, EventType.Repaint);
            }
        }
    }
}
