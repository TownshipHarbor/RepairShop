using System;
using System.Collections.Generic;

static class Uv
{
    struct V
    {
        internal float X, Y, Z, Nx, Ny, Nz, U, W;
    }

    internal static MeshData Bake(MeshData src, float[][] modes)
    {
        var o = new MeshData { Name = src.Name };
        var pos = new List<float>();
        var nrm = new List<float>();
        var uv = new List<float>();
        var reuse = new Dictionary<(int, int, int), int>();
        for (int s = 0; s < src.Subs.Count; s++)
        {
            float[] mode = modes[Math.Min(s, modes.Length - 1)];
            int[] tri = src.Subs[s];
            var outIdx = new List<int>();
            for (int t = 0; t + 2 < tri.Length; t += 3)
            {
                var a = Get(src, tri[t]);
                var b = Get(src, tri[t + 1]);
                var c = Get(src, tri[t + 2]);
                if (mode != null && mode.Length == 5)
                {
                    float minU = Math.Min(a.U, Math.Min(b.U, c.U)), maxU = Math.Max(a.U, Math.Max(b.U, c.U));
                    float minV = Math.Min(a.W, Math.Min(b.W, c.W)), maxV = Math.Max(a.W, Math.Max(b.W, c.W));
                    int i0 = (int)Math.Floor(minU + 1e-4f), i1 = (int)Math.Ceiling(maxU - 1e-4f) - 1;
                    int j0 = (int)Math.Floor(minV + 1e-4f), j1 = (int)Math.Ceiling(maxV - 1e-4f) - 1;
                    if (i1 < i0) i1 = i0;
                    if (j1 < j0) j1 = j0;
                    if (i0 == i1 && j0 == j1)
                    {
                        outIdx.Add(Emit(pos, nrm, uv, reuse, tri[t], s, i0, j0, a, mode));
                        outIdx.Add(Emit(pos, nrm, uv, reuse, tri[t + 1], s, i0, j0, b, mode));
                        outIdx.Add(Emit(pos, nrm, uv, reuse, tri[t + 2], s, i0, j0, c, mode));
                        continue;
                    }
                    for (int i = i0; i <= i1; i++)
                    {
                        for (int j = j0; j <= j1; j++)
                        {
                            var poly = new List<V> { a, b, c };
                            poly = Clip(poly, v => v.U - i);
                            poly = Clip(poly, v => i + 1 - v.U);
                            poly = Clip(poly, v => v.W - j);
                            poly = Clip(poly, v => j + 1 - v.W);
                            if (poly.Count < 3) continue;
                            int first = Emit(pos, nrm, uv, null, -1, s, i, j, poly[0], mode);
                            int prev = Emit(pos, nrm, uv, null, -1, s, i, j, poly[1], mode);
                            for (int k = 2; k < poly.Count; k++)
                            {
                                int cur = Emit(pos, nrm, uv, null, -1, s, i, j, poly[k], mode);
                                outIdx.Add(first);
                                outIdx.Add(prev);
                                outIdx.Add(cur);
                                prev = cur;
                            }
                        }
                    }
                }
                else
                {
                    outIdx.Add(Emit(pos, nrm, uv, reuse, tri[t], s, 0, 0, a, mode));
                    outIdx.Add(Emit(pos, nrm, uv, reuse, tri[t + 1], s, 0, 0, b, mode));
                    outIdx.Add(Emit(pos, nrm, uv, reuse, tri[t + 2], s, 0, 0, c, mode));
                }
            }
            o.Subs.Add(outIdx.ToArray());
        }
        o.Pos = pos.ToArray();
        o.Nrm = src.Nrm == null ? null : nrm.ToArray();
        o.Uv = uv.ToArray();
        return o;
    }

    static V Get(MeshData m, int i)
    {
        var v = new V { X = m.Pos[i * 3], Y = m.Pos[i * 3 + 1], Z = m.Pos[i * 3 + 2] };
        if (m.Nrm != null) { v.Nx = m.Nrm[i * 3]; v.Ny = m.Nrm[i * 3 + 1]; v.Nz = m.Nrm[i * 3 + 2]; }
        if (m.Uv != null) { v.U = m.Uv[i * 2]; v.W = m.Uv[i * 2 + 1]; }
        return v;
    }

    static int Emit(List<float> pos, List<float> nrm, List<float> uv, Dictionary<(int, int, int), int> reuse, int orig, int sub, int ci, int cj, V v, float[] mode)
    {
        var key = (orig, ci * 4096 + cj, sub);
        if (reuse != null && orig >= 0 && reuse.TryGetValue(key, out int have)) return have;
        int idx = pos.Count / 3;
        pos.Add(v.X); pos.Add(v.Y); pos.Add(v.Z);
        float len = (float)Math.Sqrt(v.Nx * v.Nx + v.Ny * v.Ny + v.Nz * v.Nz);
        if (len < 1e-6f) len = 1f;
        nrm.Add(v.Nx / len); nrm.Add(v.Ny / len); nrm.Add(v.Nz / len);
        float u = v.U, w = v.W;
        if (mode != null && mode.Length == 5)
        {
            u = mode[0] + Math.Clamp(u - ci, 0f, 1f) * mode[2];
            w = mode[1] + Math.Clamp(w - cj, 0f, 1f) * mode[3];
        }
        else if (mode != null && mode.Length == 4)
        {
            u = u * mode[0] + mode[2];
            w = w * mode[1] + mode[3];
        }
        uv.Add(u); uv.Add(w);
        if (reuse != null && orig >= 0) reuse[key] = idx;
        return idx;
    }

    static List<V> Clip(List<V> poly, Func<V, float> side)
    {
        var o = new List<V>();
        for (int k = 0; k < poly.Count; k++)
        {
            V p = poly[k], q = poly[(k + 1) % poly.Count];
            float dp = side(p), dq = side(q);
            if (dp >= 0) o.Add(p);
            if (dp >= 0 != dq >= 0)
            {
                float t = dp / (dp - dq);
                o.Add(new V
                {
                    X = p.X + (q.X - p.X) * t, Y = p.Y + (q.Y - p.Y) * t, Z = p.Z + (q.Z - p.Z) * t,
                    Nx = p.Nx + (q.Nx - p.Nx) * t, Ny = p.Ny + (q.Ny - p.Ny) * t, Nz = p.Nz + (q.Nz - p.Nz) * t,
                    U = p.U + (q.U - p.U) * t, W = p.W + (q.W - p.W) * t
                });
            }
        }
        return o;
    }
}
