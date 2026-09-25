using System.Collections.Generic;
using UnityEngine;

namespace TsiYuki.Follow.Editor
{
    /// <summary>World-space geometry of a mesh as the seam solver sees it.</summary>
    internal sealed class SeamGeometry
    {
        public Vector3[] Positions;
        public Vector3[] Normals;
        public int[] Triangles;
    }

    internal sealed class SeamNormalResult
    {
        public Vector3[] Normals;   // world space, one per part vertex; null on error
        public float[] Weights;     // 1 on the seam, 0 from the blend width on
        public List<Vector3> SeamPoints = new List<Vector3>();
        public int BlendedVertices;
        public float MeanAngle;
        public float MaxAngle;
        public string Error;
        public int SeamVertices => SeamPoints.Count;
    }

    /// <summary>
    /// Where a part mesh meets a body, gives the part the body's normals so both shade alike, fading
    /// back to the part's own normals with the distance walked along the part from the seam. The seam
    /// is every open edge of the part that lies on the body surface. Walking along the part (rather
    /// than measuring the gap to the body) keeps detail that happens to lie on the old body surface,
    /// such as the inside of a replaced body part, out of the blend.
    /// </summary>
    internal static class SeamNormalSolver
    {
        const float WeldGrid = 1e-5f; // vertices closer than 0.01 mm are one point (UV and normal splits)

        public static SeamNormalResult Solve(SeamGeometry part, SeamGeometry surface, float blendWidth, float seamGap)
        {
            var result = new SeamNormalResult();
            if (part.Positions.Length == 0) { result.Error = "no_seam"; return result; }
            var weld = Weld(part.Positions);
            var candidates = SurfaceQuery.TrianglesNear(Reach(part.Positions, blendWidth + seamGap), surface.Positions, surface.Triangles);
            var seam = new List<int>();
            foreach (var v in OpenBoundary(part.Triangles, weld))
                if (SurfaceQuery.Closest(part.Positions[v], surface.Positions, surface.Normals, surface.Triangles, candidates, out _, out _, out var gap) && gap <= seamGap)
                {
                    seam.Add(v);
                    result.SeamPoints.Add(part.Positions[v]);
                }
            if (seam.Count == 0) { result.Error = "no_seam"; return result; }

            var distance = WalkFrom(seam, part, weld, blendWidth);
            Blend(part, surface, candidates, weld, distance, blendWidth, result);
            return result;
        }

        /// <summary>Middle of the part's open edges, or of the whole part when it is closed.</summary>
        public static Vector3 OpenBoundaryCentroid(SeamGeometry part)
        {
            var open = OpenBoundary(part.Triangles, Weld(part.Positions));
            if (open.Count == 0) return Reach(part.Positions, 0f).center;
            var sum = Vector3.zero;
            foreach (var v in open) sum += part.Positions[v];
            return sum / open.Count;
        }

        /// <summary>1 at the seam, easing to 0 at the blend width (t = distance / width).</summary>
        public static float Falloff(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - t * t * (3f - 2f * t);
        }

        static void Blend(SeamGeometry part, SeamGeometry surface, int[] candidates, int[] weld, float[] distance, float width, SeamNormalResult result)
        {
            result.Normals = (Vector3[])part.Normals.Clone();
            result.Weights = new float[part.Positions.Length];
            double sum = 0;
            for (int i = 0; i < part.Positions.Length; i++)
            {
                float w = Falloff(distance[weld[i]] / width);
                if (w <= 0f) continue;
                if (!SurfaceQuery.Closest(part.Positions[i], surface.Positions, surface.Normals, surface.Triangles, candidates, out _, out var target, out _)) continue;
                var before = part.Normals[i];
                result.Normals[i] = Vector3.Slerp(before, target, w).normalized;
                result.Weights[i] = w;
                float angle = Vector3.Angle(before, result.Normals[i]);
                sum += angle;
                result.MaxAngle = Mathf.Max(result.MaxAngle, angle);
                result.BlendedVertices++;
            }
            if (result.BlendedVertices > 0) result.MeanAngle = (float)(sum / result.BlendedVertices);
        }

        // Shortest distance along the part's edges from the seam, up to the limit (Dijkstra).
        static float[] WalkFrom(List<int> seam, SeamGeometry part, int[] weld, float limit)
        {
            var neighbours = Neighbours(part.Triangles, weld, part.Positions.Length);
            var distance = new float[part.Positions.Length];
            for (int i = 0; i < distance.Length; i++) distance[i] = float.PositiveInfinity;
            var queue = new SortedSet<(float, int)>();
            foreach (var s in seam) { distance[s] = 0f; queue.Add((0f, s)); }
            while (queue.Count > 0)
            {
                var (d, v) = queue.Min;
                queue.Remove(queue.Min);
                if (d > distance[v]) continue;
                foreach (var u in neighbours[v])
                {
                    var next = d + (part.Positions[u] - part.Positions[v]).magnitude;
                    if (next >= limit || next >= distance[u]) continue;
                    distance[u] = next;
                    queue.Add((next, u));
                }
            }
            return distance;
        }

        static List<int>[] Neighbours(int[] triangles, int[] weld, int count)
        {
            var neighbours = new List<int>[count];
            for (int i = 0; i < count; i++) neighbours[i] = new List<int>();
            for (int t = 0; t + 2 < triangles.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = weld[triangles[t + k]], b = weld[triangles[t + (k + 1) % 3]];
                    if (a == b) continue;
                    neighbours[a].Add(b);
                    neighbours[b].Add(a);
                }
            return neighbours;
        }

        // Welded vertices on edges that only one triangle uses.
        static List<int> OpenBoundary(int[] triangles, int[] weld)
        {
            var uses = new Dictionary<long, int>();
            for (int t = 0; t + 2 < triangles.Length; t += 3)
                for (int k = 0; k < 3; k++)
                {
                    int a = weld[triangles[t + k]], b = weld[triangles[t + (k + 1) % 3]];
                    if (a == b) continue;
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    uses.TryGetValue(key, out var n);
                    uses[key] = n + 1;
                }
            var open = new HashSet<int>();
            foreach (var e in uses)
                if (e.Value == 1) { open.Add((int)(e.Key >> 32)); open.Add((int)(e.Key & 0xffffffff)); }
            return new List<int>(open);
        }

        // Each vertex mapped to the first vertex at the same position.
        static int[] Weld(Vector3[] positions)
        {
            var first = new Dictionary<Vector3Int, int>();
            var weld = new int[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var p = positions[i];
                var key = new Vector3Int(Mathf.RoundToInt(p.x / WeldGrid), Mathf.RoundToInt(p.y / WeldGrid), Mathf.RoundToInt(p.z / WeldGrid));
                if (!first.TryGetValue(key, out var w)) first[key] = w = i;
                weld[i] = w;
            }
            return weld;
        }

        static Bounds Reach(Vector3[] positions, float margin)
        {
            var bounds = new Bounds(positions[0], Vector3.zero);
            foreach (var p in positions) bounds.Encapsulate(p);
            bounds.Expand(margin * 2f);
            return bounds;
        }
    }
}
