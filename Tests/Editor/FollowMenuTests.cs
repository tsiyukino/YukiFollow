using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace TsiYuki.Follow.Editor.Tests
{
    // The menu adds components through Undo, so the test objects are created and removed through Undo
    // too: destroying them directly would let the test runner's own undo cleanup bring them back.
    // Removal records a destroy instead of reverting: a revert fires every editor's undo callback,
    // and the VRC SDK's expressions menu editor throws there when it was enabled but never drawn.
    public class FollowMenuTests
    {
        GameObject _avatar, _part, _prop, _outside;

        static readonly MenuCommand FromMenuBar = new MenuCommand(null);

        [SetUp]
        public void SetUp()
        {
            _avatar = Create("Avatar", null, typeof(VRCAvatarDescriptor));
            _part = Create("Part", _avatar, typeof(SkinnedMeshRenderer));
            _prop = Create("Prop", _avatar);
            _outside = Create("Outside", null, typeof(SkinnedMeshRenderer));
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = new Object[0];
            Undo.DestroyObjectImmediate(_avatar);
            Undo.DestroyObjectImmediate(_outside);
        }

        static GameObject Create(string name, GameObject parent, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            if (parent != null) go.transform.SetParent(parent.transform);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }

        [Test]
        public void AnchorGoesOnEverySelectedObjectInsideTheAvatar()
        {
            Selection.objects = new Object[] { _prop, _part, _outside, _avatar };
            Assert.IsTrue(FollowMenu.CanAddAnchor());
            FollowMenu.AddAnchor(FromMenuBar);
            Assert.IsNotNull(_prop.GetComponent<YukiSurfaceAnchor>());
            Assert.IsNotNull(_part.GetComponent<YukiSurfaceAnchor>());
            Assert.IsNull(_outside.GetComponent<YukiSurfaceAnchor>());
            Assert.IsNull(_avatar.GetComponent<YukiSurfaceAnchor>());
        }

        [Test]
        public void SyncIsAddedOnceToTheAvatarRoot()
        {
            Selection.objects = new Object[] { _prop };
            FollowMenu.AddSync(FromMenuBar);
            Selection.objects = new Object[] { _part };
            FollowMenu.AddSync(FromMenuBar);
            Assert.That(_avatar.GetComponentsInChildren<YukiBlendshapeSync>(true).Length, Is.EqualTo(1));
            Assert.IsNotNull(_avatar.GetComponent<YukiBlendshapeSync>());
            Assert.That(Selection.activeGameObject, Is.EqualTo(_avatar));
        }

        [Test]
        public void SeamNormalsOnlyGoOnSkinnedMeshesInsideTheAvatar()
        {
            Selection.objects = new Object[] { _prop, _part, _outside };
            Assert.IsTrue(FollowMenu.CanAddSeam());
            FollowMenu.AddSeam(FromMenuBar);
            Assert.IsNotNull(_part.GetComponent<YukiSeamNormals>());
            Assert.IsNull(_prop.GetComponent<YukiSeamNormals>());
            Assert.IsNull(_outside.GetComponent<YukiSeamNormals>());
        }

        [Test]
        public void MenusAreOffOutsideAnAvatar()
        {
            Selection.objects = new Object[] { _outside };
            Assert.IsFalse(FollowMenu.CanAddAnchor());
            Assert.IsFalse(FollowMenu.CanAddSync());
            Assert.IsFalse(FollowMenu.CanAddSeam());
        }

        [Test]
        public void ActsOnceForASelectionOfSeveralObjects()
        {
            // The hierarchy menu calls the item once per selected object, each time with that object as context.
            Selection.objects = new Object[] { _prop, _part };
            var active = Selection.activeGameObject;
            var other = active == _prop ? _part : _prop;
            FollowMenu.AddAnchor(new MenuCommand(other));
            Assert.IsNull(_prop.GetComponent<YukiSurfaceAnchor>());
            Assert.IsNull(_part.GetComponent<YukiSurfaceAnchor>());
            FollowMenu.AddAnchor(new MenuCommand(active));
            Assert.IsNotNull(_prop.GetComponent<YukiSurfaceAnchor>());
            Assert.IsNotNull(_part.GetComponent<YukiSurfaceAnchor>());
        }
    }
}
