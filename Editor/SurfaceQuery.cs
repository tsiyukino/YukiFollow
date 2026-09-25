using System.Collections.Generic;
using UnityEngine;

namespace TsiYuki.Follow.Editor
{
    /// <summary>
    /// Closest-point queries on a triangle mesh given as world-space arrays. The normal returned is
    /// the vertex normals interpolated at the closest point, so it is as smooth as the mesh shades.
    /// </summary>
    internal static class SurfaceQuery
    {
        /// <summary>Indices (into <paramref name="triangles"/>, multiples of 3) of the triangles whose box meets the bounds.</summary>
        public static int[] TrianglesNear(Bounds bounds, Vector3[] positions, int[] triangles)
        {
            var near = new List<int>();
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                var box = new Bounds(positions[triangles[t]], Vector3.zero);
                box.Encapsulate(positions[triangles[t + 1]]);
                box.Encapsulate(positions[triangles[t + 2]]);
                if (box.Intersects(bounds)) near.Add(t);
            }
            return near.ToArray();
        }

        /// <summary>Closest point to <paramref name="p"/> over the listed triangles; false when the list is empty.</summary>
        public static bool Closest(Vector3 p, Vector3[] positions, Vector3[] normals, int[] triangles, int[] candidates,
            out Vector3 point, out Vector3 normal, out float distance)
        {
            point = p;
            normal = Vector3.up;
            distance = float.MaxValue;
            foreach (var t in candidates)
            {
                int ia = triangles[t], ib = triangles[t + 1], ic = triangles[t + 2];
                var q = ClosestOnTriangle(p, positions[ia], positions[ib], positions[ic], out var bary);
                var d = (q - p).sqrMagnitude;
                if (d >= distance) continue;
                distance = d;
                point = q;
                normal = normals[ia] * bary.x + normals[ib] * bary.y + normals[ic] * bary.z;
            }
            if (distance == float.MaxValue) return false;
            distance = Mathf.Sqrt(distance);
            normal = normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up;
            return true;
        }

        // Real-Time Collision Detection (Ericson) 5.1.5, returning barycentric weights for a, b, c.
        internal static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bary)
        {
            var ab = b - a;
            var ac = c - a;
            var ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) { bary = new Vector3(1, 0, 0); return a; }

            var bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) { bary = new Vector3(0, 1, 0); return b; }

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                bary = new Vector3(1 - v, v, 0);
                return a + ab * v;
            }

            var cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) { bary = new Vector3(0, 0, 1); return c; }

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                bary = new Vector3(1 - w, 0, w);
                return a + ac * w;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
            {
                float w = (d4 - d3) / (d4 - d3 + (d5 - d6));
                bary = new Vector3(0, 1 - w, w);
                return b + (c - b) * w;
            }

            float denom = 1f / (va + vb + vc);
            float bv = vb * denom, bw = vc * denom;
            bary = new Vector3(1 - bv - bw, bv, bw);
            return a + ab * bv + ac * bw;
        }
    }
}
