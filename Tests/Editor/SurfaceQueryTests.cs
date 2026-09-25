using NUnit.Framework;
using UnityEngine;

namespace TsiYuki.Follow.Editor.Tests
{
    public class SurfaceQueryTests
    {
        static readonly Vector3[] Positions = { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1) };
        static readonly int[] Triangles = { 0, 1, 2 };
        static readonly int[] All = { 0 };

        static void Query(Vector3 p, Vector3[] normals, out Vector3 point, out Vector3 normal, out float distance) =>
            Assert.IsTrue(SurfaceQuery.Closest(p, Positions, normals, Triangles, All, out point, out normal, out distance));

        static Vector3[] Up => new[] { Vector3.up, Vector3.up, Vector3.up };

        [Test]
        public void PointAboveTheFaceProjectsOntoIt()
        {
            Query(new Vector3(0.2f, 0.5f, 0.2f), Up, out var point, out var normal, out var distance);
            Assert.That(Vector3.Distance(point, new Vector3(0.2f, 0f, 0.2f)), Is.LessThan(1e-6f));
            Assert.That(distance, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(Vector3.Angle(normal, Vector3.up), Is.LessThan(1e-3f));
        }

        [Test]
        public void PointOutsideAnEdgeLandsOnTheEdge()
        {
            Query(new Vector3(0.5f, 0f, -0.3f), Up, out var point, out _, out var distance);
            Assert.That(Vector3.Distance(point, new Vector3(0.5f, 0f, 0f)), Is.LessThan(1e-6f));
            Assert.That(distance, Is.EqualTo(0.3f).Within(1e-6f));
        }

        [Test]
        public void PointBeyondACornerLandsOnTheCorner()
        {
            Query(new Vector3(1.5f, 0f, -0.5f), Up, out var point, out _, out _);
            Assert.That(Vector3.Distance(point, new Vector3(1f, 0f, 0f)), Is.LessThan(1e-6f));
        }

        [Test]
        public void NormalIsInterpolatedAtThePoint()
        {
            var normals = new[] { Vector3.up, Vector3.right, Vector3.forward };
            Query(new Vector3(1f / 3f, 1f, 1f / 3f), normals, out _, out var normal, out _);
            var expected = (Vector3.up + Vector3.right + Vector3.forward).normalized;
            Assert.That(Vector3.Angle(normal, expected), Is.LessThan(1e-3f));
        }

        [Test]
        public void NoCandidatesFindsNothing()
        {
            Assert.IsFalse(SurfaceQuery.Closest(Vector3.zero, Positions, Up, Triangles, new int[0], out _, out _, out _));
        }

        [Test]
        public void TrianglesNearKeepsOnlyOverlappingBoxes()
        {
            var far = new Bounds(new Vector3(5f, 0f, 5f), Vector3.one);
            var near = new Bounds(new Vector3(0.5f, 0f, 0.2f), Vector3.one * 0.1f);
            Assert.That(SurfaceQuery.TrianglesNear(far, Positions, Triangles), Is.Empty);
            Assert.That(SurfaceQuery.TrianglesNear(near, Positions, Triangles), Is.EqualTo(new[] { 0 }));
        }
    }
}
