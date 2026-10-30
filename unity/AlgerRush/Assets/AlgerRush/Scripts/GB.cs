using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlgerRush
{
    // Low-poly geometry builder: flat-shaded, vertex-coloured triangles.
    // Shapes are described in the web version's coordinates (nose of a car towards -z) and mirrored
    // on z as they are added, so in Unity the nose points down +z, the direction the road runs.
    // A reference point inside the solid decides which way each face points, so winding never matters.
    public class GB
    {
        readonly List<Vector3> p = new List<Vector3>();
        readonly List<Color> c = new List<Color>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        public bool mirror = true;

        Vector3 M(Vector3 v) => mirror ? new Vector3(v.x, v.y, -v.z) : v;
        public static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        public void Tri(Vector3 a, Vector3 b, Vector3 cc, Color col, Vector3? reff = null, Vector2 ua = default, Vector2 ub = default, Vector2 uc = default)
        {
            a = M(a); b = M(b); cc = M(cc);
            var nn = Vector3.Cross(b - a, cc - a);
            if (nn.sqrMagnitude < 1e-10f) return;
            if (reff.HasValue)
            {
                var mid = (a + b + cc) / 3 - M(reff.Value);
                if (Vector3.Dot(nn, mid) < 0) { var t = b; b = cc; cc = t; var tu = ub; ub = uc; uc = tu; nn = -nn; }
            }
            nn.Normalize();
            p.Add(a); p.Add(b); p.Add(cc);
            for (int i = 0; i < 3; i++) { c.Add(col); n.Add(nn); }
            uv.Add(ua); uv.Add(ub); uv.Add(uc);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col, Vector3? reff = null)
        {
            Tri(a, b, cc, col, reff); Tri(a, cc, d, col, reff);
        }

        public void QuadUV(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col, Vector3 reff, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Tri(a, b, cc, col, reff, ua, ub, uc); Tri(a, cc, d, col, reff, ua, uc, ud);
        }

        public void Box(float x, float y, float z, float sx, float sy, float sz, Color col, float rz = 0, float ry = 0, Color? top = null)
        {
            float hx = sx / 2, hy = sy / 2, hz = sz / 2, cz = Mathf.Cos(rz), szn = Mathf.Sin(rz), cy = Mathf.Cos(ry), syn = Mathf.Sin(ry);
            Vector3 P(float dx, float dy, float dz)
            {
                float px = dx * cz - dy * szn, py = dx * szn + dy * cz, pz = dz;
                float qx = px * cy + pz * syn, qz = -px * syn + pz * cy;
                return new Vector3(x + qx, y + py, z + qz);
            }
            var v = new[] { P(-hx, -hy, -hz), P(hx, -hy, -hz), P(hx, hy, -hz), P(-hx, hy, -hz), P(-hx, -hy, hz), P(hx, -hy, hz), P(hx, hy, hz), P(-hx, hy, hz) };
            var r = new Vector3(x, y, z); var tc = top ?? col;
            Quad(v[0], v[1], v[2], v[3], col, r); Quad(v[4], v[5], v[6], v[7], col, r);
            Quad(v[0], v[4], v[7], v[3], col, r); Quad(v[1], v[5], v[6], v[2], col, r);
            Quad(v[3], v[2], v[6], v[7], tc, r); Quad(v[0], v[1], v[5], v[4], col, r);
        }

        // axis: 'x', 'y' or 'z'; r2 lets the far end taper (r2 = 0 makes a cone)
        public void Cyl(float x, float y, float z, float r, float len, char axis, int segs, Color col, Color? cap = null, float r2 = -1)
        {
            if (r2 < 0) r2 = r;
            Vector3 P(float o, float u, float w) => axis == 'x' ? new Vector3(x + o, y + w, z + u) : axis == 'y' ? new Vector3(x + u, y + o, z + w) : new Vector3(x + u, y + w, z + o);
            var reff = new Vector3(x, y, z); var A = new Vector3[segs]; var B = new Vector3[segs];
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2;
                A[i] = P(-len / 2, Mathf.Cos(a) * r, Mathf.Sin(a) * r); B[i] = P(len / 2, Mathf.Cos(a) * r2, Mathf.Sin(a) * r2);
            }
            for (int i = 0; i < segs; i++) { int j = (i + 1) % segs; Quad(A[i], A[j], B[j], B[i], col, reff); }
            Vector3 ca = P(-len / 2, 0, 0), cb = P(len / 2, 0, 0); var cc = cap ?? col;
            for (int i = 0; i < segs; i++) { int j = (i + 1) % segs; Tri(ca, A[i], A[j], cc, reff); if (r2 > 0) Tri(cb, B[i], B[j], cc, reff); }
        }

        public int Count => p.Count;

        public Mesh Build(string name = "gb")
        {
            var m = new Mesh { name = name };
            if (p.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(p); m.SetColors(c); m.SetNormals(n); m.SetUVs(0, uv);
            var idx = new int[p.Count]; for (int i = 0; i < idx.Length; i++) idx[i] = i;
            m.SetTriangles(idx, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
