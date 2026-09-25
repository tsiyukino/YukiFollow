using System.Collections.Generic;
using System.Linq;
using TsiYuki.Core.Editor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace TsiYuki.Follow.Editor
{
    [CustomEditor(typeof(YukiBlendshapeSync))]
    internal sealed class YukiBlendshapeSyncEditor : UnityEditor.Editor
    {
        static YukiLocalizer L => FollowText.L;

        readonly HashSet<SkinnedMeshRenderer> _expanded = new HashSet<SkinnedMeshRenderer>();

        YukiBlendshapeSync Target => (YukiBlendshapeSync)target;

        void OnEnable() => YukiLanguage.Changed += Repaint;
        void OnDisable() => YukiLanguage.Changed -= Repaint;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            YukiGUI.Header("Yuki Blendshape Sync", FollowText.Version);
            EditorGUILayout.LabelField(L["sync.intro"], YukiGUI.WrapMini);

            var avatar = Target.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (avatar == null)
            {
                EditorGUILayout.HelpBox(L["ui.no_avatar"], MessageType.Error);
                return;
            }
            var all = avatar.GetComponentsInChildren<YukiBlendshapeSync>(true);
            if (all.Length > 1 && all[0] != Target)
                EditorGUILayout.HelpBox(L["sync.duplicate"], MessageType.Warning);

            YukiGUI.Section(L["sync.settings"]);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sources"), new GUIContent(L["sync.sources"], L["sync.sources.tip"]), true);
            if (Target.sources.All(s => s == null))
            {
                var auto = SyncPlan.AutoSources(avatar.transform);
                EditorGUILayout.LabelField(auto.Count > 0
                    ? L.Tr("sync.sources.auto", string.Join(", ", auto.Select(s => s.name)))
                    : L["sync.sources.none"], YukiGUI.WrapMini);
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("excludedMeshes"), new GUIContent(L["sync.excluded_meshes"], L["sync.excluded_meshes.tip"]), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("excludedBlendshapes"), new GUIContent(L["sync.excluded_names"], L["sync.excluded_names.tip"]), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("copyDefaultWeights"), new GUIContent(L["sync.copy_defaults"], L["sync.copy_defaults.tip"]));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("syncAnimations"), new GUIContent(L["sync.animations"], L["sync.animations.tip"]));
            serializedObject.ApplyModifiedProperties();

            DrawPlan(avatar);
        }

        void DrawPlan(VRCAvatarDescriptor avatar)
        {
            var plan = SyncPlan.Resolve(avatar.transform, Target);
            YukiGUI.Section(L["sync.preview"]);
            if (plan.Links.Count == 0)
            {
                EditorGUILayout.LabelField(L["sync.preview.none"], YukiGUI.WrapMini);
                return;
            }
            var byTarget = plan.Links.GroupBy(l => l.Target).OrderBy(g => g.Key.name).ToList();
            EditorGUILayout.LabelField(L.Tr("sync.preview.summary", byTarget.Count, plan.Links.Count), YukiGUI.WrapMini);

            int differing = 0;
            foreach (var group in byTarget)
            {
                var diff = group.Count(l => !Mathf.Approximately(SyncPlan.SourceWeight(l), SyncPlan.TargetWeight(l)));
                differing += diff;
                var label = group.Key.name + "  (" + group.Count() + (diff > 0 ? " · " + L.Tr("sync.preview.differs", diff) : "") + ")";
                var open = _expanded.Contains(group.Key);
                var now = EditorGUILayout.Foldout(open, label, true);
                if (now != open) { if (now) _expanded.Add(group.Key); else _expanded.Remove(group.Key); }
                if (!now) continue;
                using (new EditorGUI.IndentLevelScope())
                    foreach (var link in group)
                    {
                        var s = SyncPlan.SourceWeight(link);
                        var t = SyncPlan.TargetWeight(link);
                        var text = Mathf.Approximately(s, t) ? s.ToString("0.#") : t.ToString("0.#") + " → " + s.ToString("0.#");
                        EditorGUILayout.LabelField(link.Name, link.Source.name + " · " + text);
                    }
            }

            if (differing > 0 && Target.copyDefaultWeights)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(L.Tr("sync.apply_now.hint", differing), YukiGUI.WrapMini);
                if (GUILayout.Button(L["sync.apply_now"]))
                {
                    foreach (var link in plan.Links)
                    {
                        var value = SyncPlan.SourceWeight(link);
                        if (Mathf.Approximately(SyncPlan.TargetWeight(link), value)) continue;
                        Undo.RecordObject(link.Target, "Sync Blendshapes");
                        link.Target.SetBlendShapeWeight(link.Target.sharedMesh.GetBlendShapeIndex(link.Name), value);
                    }
                }
            }
        }
    }
}
