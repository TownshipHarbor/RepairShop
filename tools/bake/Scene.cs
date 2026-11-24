using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

sealed class Comp
{
    internal byte Kind;
    internal bool Enabled = true, Trigger, Convex;
    internal int Mesh = -1;
    internal int[] Mats = new int[0];
    internal float[] A = new float[0];
    internal string Text = "";
    internal int FontSize, Anchor, Align, Style;
    internal bool Rich;
}

sealed class Node
{
    internal string Name;
    internal int Parent;
    internal bool Active;
    internal int Layer;
    internal float[] Trs;
    internal List<Comp> Comps = new List<Comp>();
}

sealed class Tex
{
    internal string Name;
    internal int W, H, Mips;
    internal byte[] Data;
}

sealed class Mat
{
    internal string Name, Shader;
    internal List<(string, int, float[])> Texs = new List<(string, int, float[])>();
    internal List<(string, float)> Floats = new List<(string, float)>();
    internal List<(string, float[])> Colors = new List<(string, float[])>();
}

sealed class Scene
{
    internal const byte MeshFilter = 1, MeshRenderer = 2, Box = 3, Sphere = 4, MeshCol = 5, Text = 6;
    readonly int _cap;
    internal readonly List<Tex> Textures = new List<Tex>();
    internal readonly List<Mat> Materials = new List<Mat>();
    internal readonly List<MeshData> Meshes = new List<MeshData>();
    internal readonly List<(string, List<Node>)> Objects = new List<(string, List<Node>)>();
    internal readonly List<(string, byte[])> Blobs = new List<(string, byte[])>();
    readonly Dictionary<string, int> _tex = new Dictionary<string, int>();
    readonly Dictionary<string, int> _mat = new Dictionary<string, int>();
    readonly Dictionary<string, int> _mesh = new Dictionary<string, int>();

    internal Scene(int cap)
    {
        _cap = cap;
    }

    internal void AddRoots(Bundle b, params string[] names)
    {
        var want = new HashSet<string>(names);
        foreach (var info in b.All(AssetClassID.GameObject))
        {
            var go = b.Am.GetBaseField(b.Inst, info);
            string name = go["m_Name"].AsString;
            if (!want.Contains(name)) continue;
            var tr = Transform(b, go);
            if (tr == null || Bundle.Id(tr["m_Father"]) != 0) continue;
            want.Remove(name);
            var nodes = new List<Node>();
            Walk(b, tr, -1, nodes);
            Objects.Add((name, nodes));
        }
        if (want.Count > 0) throw new InvalidDataException(b.Tag + ": roots not found: " + string.Join(", ", want));
    }

    internal void AddBlobs(Bundle b)
    {
        foreach (var info in b.All(AssetClassID.TextAsset))
        {
            var t = b.Am.GetBaseField(b.Inst, info);
            Blobs.Add((t["m_Name"].AsString, t["m_Script"].AsByteArray));
        }
    }

    static AssetTypeValueField Transform(Bundle b, AssetTypeValueField go)
    {
        foreach (var c in go["m_Component"]["Array"].Children)
        {
            var pptr = c["component"];
            var info = b.Info(Bundle.Id(pptr));
            if (info != null && info.TypeId == (int)AssetClassID.Transform) return b.Am.GetBaseField(b.Inst, info);
        }
        return null;
    }

    void Walk(Bundle b, AssetTypeValueField tr, int parent, List<Node> nodes)
    {
        var go = b.Ref(tr["m_GameObject"]);
        var n = new Node
        {
            Name = go["m_Name"].AsString,
            Parent = parent,
            Active = go["m_IsActive"].AsBool,
            Layer = (int)go["m_Layer"].AsUInt,
            Trs = new[]
            {
                tr["m_LocalPosition"]["x"].AsFloat, tr["m_LocalPosition"]["y"].AsFloat, tr["m_LocalPosition"]["z"].AsFloat,
                tr["m_LocalRotation"]["x"].AsFloat, tr["m_LocalRotation"]["y"].AsFloat, tr["m_LocalRotation"]["z"].AsFloat, tr["m_LocalRotation"]["w"].AsFloat,
                tr["m_LocalScale"]["x"].AsFloat, tr["m_LocalScale"]["y"].AsFloat, tr["m_LocalScale"]["z"].AsFloat
            }
        };
        int me = nodes.Count;
        nodes.Add(n);
        foreach (var c in go["m_Component"]["Array"].Children)
        {
            var info = b.Info(Bundle.Id(c["component"]));
            if (info == null) continue;
            var f = b.Am.GetBaseField(b.Inst, info);
            var id = (AssetClassID)info.TypeId;
            Comp k = null;
            switch (id)
            {
                case AssetClassID.Transform:
                    break;
                case AssetClassID.MeshFilter:
                    k = new Comp { Kind = MeshFilter, Mesh = MeshOf(b, f["m_Mesh"]) };
                    break;
                case AssetClassID.MeshRenderer:
                    k = new Comp { Kind = MeshRenderer, Enabled = f["m_Enabled"].AsBool, Mats = f["m_Materials"]["Array"].Children.Select(m => MatOf(b, m)).ToArray() };
                    break;
                case AssetClassID.BoxCollider:
                    k = new Comp { Kind = Box, Enabled = f["m_Enabled"].AsBool, Trigger = f["m_IsTrigger"].AsBool, A = new[] { f["m_Center"]["x"].AsFloat, f["m_Center"]["y"].AsFloat, f["m_Center"]["z"].AsFloat, f["m_Size"]["x"].AsFloat, f["m_Size"]["y"].AsFloat, f["m_Size"]["z"].AsFloat } };
                    break;
                case AssetClassID.SphereCollider:
                    k = new Comp { Kind = Sphere, Enabled = f["m_Enabled"].AsBool, Trigger = f["m_IsTrigger"].AsBool, A = new[] { f["m_Center"]["x"].AsFloat, f["m_Center"]["y"].AsFloat, f["m_Center"]["z"].AsFloat, f["m_Radius"].AsFloat } };
                    break;
                case AssetClassID.MeshCollider:
                    k = new Comp { Kind = MeshCol, Enabled = f["m_Enabled"].AsBool, Trigger = f["m_IsTrigger"].AsBool, Convex = f["m_Convex"].AsBool, Mesh = MeshOf(b, f["m_Mesh"]) };
                    break;
                case AssetClassID.TextMesh:
                    var col = f["m_Color"];
                    k = new Comp
                    {
                        Kind = Text,
                        Text = f["m_Text"].AsString,
                        FontSize = f["m_FontSize"].AsInt,
                        Anchor = f["m_Anchor"].AsInt,
                        Align = f["m_Alignment"].AsInt,
                        Style = f["m_FontStyle"].AsInt,
                        Rich = f["m_RichText"].AsBool,
                        A = new[] { f["m_OffsetZ"].AsFloat, f["m_CharacterSize"].AsFloat, f["m_LineSpacing"].AsFloat, f["m_TabSize"].AsFloat, Rgba(col.Children.Count == 1 ? col["rgba"].AsUInt : 0xFFFFFFFF, 0), Rgba(col.Children.Count == 1 ? col["rgba"].AsUInt : 0xFFFFFFFF, 1), Rgba(col.Children.Count == 1 ? col["rgba"].AsUInt : 0xFFFFFFFF, 2), Rgba(col.Children.Count == 1 ? col["rgba"].AsUInt : 0xFFFFFFFF, 3) }
                    };
                    break;
                default:
                    throw new InvalidDataException(b.Tag + ": " + n.Name + " has an unexpected " + id);
            }
            if (k != null) n.Comps.Add(k);
        }
        BakeUv(n);
        foreach (var ch in tr["m_Children"]["Array"].Children)
        {
            var ct = b.Ref(ch);
            if (ct == null) throw new InvalidDataException(b.Tag + ": " + n.Name + " has an external child");
            Walk(b, ct, me, nodes);
        }
    }

    readonly Dictionary<string, int> _derived = new Dictionary<string, int>();

    void BakeUv(Node n)
    {
        Comp mf = n.Comps.Find(c => c.Kind == MeshFilter);
        Comp mr = n.Comps.Find(c => c.Kind == MeshRenderer);
        if (mf == null || mr == null || mf.Mesh < 0 || mr.Mats.Length == 0) return;
        MeshData src = Meshes[mf.Mesh];
        var modes = new float[mr.Mats.Length][];
        bool any = false;
        var sig = new System.Text.StringBuilder(mf.Mesh.ToString());
        for (int i = 0; i < mr.Mats.Length; i++)
        {
            float[] mode = null;
            if (mr.Mats[i] >= 0)
            {
                Mat m = Materials[mr.Mats[i]];
                bool atlas = m.Floats.Exists(f => f.Item1 == "_UseAtlas" && f.Item2 > 0.5f);
                var st = _st.TryGetValue(mr.Mats[i], out float[] keep) ? keep : m.Texs.Find(t => t.Item1 == "_MainTex").Item3;
                if (atlas && src.Rect != null) mode = new[] { src.Rect[0], src.Rect[1], src.Rect[2], src.Rect[3], 1f };
                else if (atlas) throw new InvalidDataException(n.Name + ": atlas material on a mesh without a rect");
                else if (st != null && (st[0] != 1f || st[1] != 1f || st[2] != 0f || st[3] != 0f))
                {
                    mode = st;
                    _st[mr.Mats[i]] = st;
                }
            }
            modes[i] = mode;
            if (mode != null) any = true;
            sig.Append('|').Append(mode == null ? "-" : string.Join(",", mode));
        }
        if (!any) return;
        string key = sig.ToString();
        if (!_derived.TryGetValue(key, out int idx))
        {
            Meshes.Add(Uv.Bake(src, modes));
            idx = Meshes.Count - 1;
            _derived[key] = idx;
        }
        mf.Mesh = idx;
    }

    readonly Dictionary<int, float[]> _st = new Dictionary<int, float[]>();

    static float Rgba(uint v, int k)
    {
        return ((v >> (8 * k)) & 0xFF) / 255f;
    }

    int MeshOf(Bundle b, AssetTypeValueField pptr)
    {
        long id = Bundle.Id(pptr);
        if (id == 0) return -1;
        if (Bundle.FileId(pptr) != 0)
        {
            if (id < 10200 || id > 10220) throw new InvalidDataException(b.Tag + ": external mesh " + id);
            return (int)-id;
        }
        string key = b.Tag + ":" + id;
        if (_mesh.TryGetValue(key, out int i)) return i;
        Meshes.Add(Decode.Mesh(b, b.Field(id)));
        _mesh[key] = Meshes.Count - 1;
        return Meshes.Count - 1;
    }

    int MatOf(Bundle b, AssetTypeValueField pptr)
    {
        long id = Bundle.Id(pptr);
        if (id == 0) return -1;
        if (Bundle.FileId(pptr) != 0) return (int)-id;
        string key = b.Tag + ":" + id;
        if (_mat.TryGetValue(key, out int i)) return i;
        var f = b.Field(id);
        var m = new Mat { Name = f["m_Name"].AsString };
        var sh = b.Ref(f["m_Shader"]);
        m.Shader = sh == null ? "" : sh["m_ParsedForm"]["m_Name"].AsString;
        var sp = f["m_SavedProperties"];
        foreach (var e in sp["m_TexEnvs"]["Array"].Children)
        {
            var s = e["second"];
            int t = TexOf(b, s["m_Texture"]);
            m.Texs.Add((e["first"].AsString, t, new[] { s["m_Scale"]["x"].AsFloat, s["m_Scale"]["y"].AsFloat, s["m_Offset"]["x"].AsFloat, s["m_Offset"]["y"].AsFloat }));
        }
        foreach (var e in sp["m_Floats"]["Array"].Children) m.Floats.Add((e["first"].AsString, e["second"].AsFloat));
        foreach (var e in sp["m_Colors"]["Array"].Children)
        {
            var c = e["second"];
            m.Colors.Add((e["first"].AsString, new[] { c["r"].AsFloat, c["g"].AsFloat, c["b"].AsFloat, c["a"].AsFloat }));
        }
        Materials.Add(m);
        _mat[key] = Materials.Count - 1;
        return Materials.Count - 1;
    }

    int TexOf(Bundle b, AssetTypeValueField pptr)
    {
        long id = Bundle.Id(pptr);
        if (id == 0) return -1;
        if (Bundle.FileId(pptr) != 0) return -2;
        var f = b.Field(id);
        byte[] rgba = Decode.Texture(b, f, out int w, out int h);
        string hash = Convert.ToHexString(SHA256.HashData(rgba));
        if (_tex.TryGetValue(hash, out int i)) return i;
        while (Math.Max(w, h) > _cap) rgba = Decode.Half(rgba, w, h, out w, out h);
        var all = new MemoryStream();
        all.Write(rgba);
        int mips = 1, mw = w, mh = h;
        byte[] cur = rgba;
        while (mw > 1 || mh > 1)
        {
            cur = Decode.Half(cur, mw, mh, out mw, out mh);
            all.Write(cur);
            mips++;
        }
        Textures.Add(new Tex { Name = f["m_Name"].AsString, W = w, H = h, Mips = mips, Data = all.ToArray() });
        _tex[hash] = Textures.Count - 1;
        return Textures.Count - 1;
    }

    internal byte[] Write()
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms, Encoding.UTF8);
        w.Write(Encoding.ASCII.GetBytes("RSQ1"));
        w.Write(Textures.Count);
        foreach (var t in Textures)
        {
            w.Write(t.Name); w.Write(t.W); w.Write(t.H); w.Write(t.Mips); w.Write(t.Data.Length); w.Write(t.Data);
        }
        w.Write(Materials.Count);
        for (int mi = 0; mi < Materials.Count; mi++)
        {
            var m = Materials[mi];
            w.Write(m.Name); w.Write(m.Shader);
            w.Write(m.Texs.Count);
            foreach (var (n, t, a) in m.Texs)
            {
                w.Write(n); w.Write(t);
                float[] st = n == "_MainTex" && _st.ContainsKey(mi) ? new[] { 1f, 1f, 0f, 0f } : a;
                foreach (float f in st) w.Write(f);
            }
            w.Write(m.Floats.Count);
            foreach (var (n, f) in m.Floats) { w.Write(n); w.Write(f); }
            w.Write(m.Colors.Count);
            foreach (var (n, c) in m.Colors) { w.Write(n); foreach (float f in c) w.Write(f); }
        }
        w.Write(Meshes.Count);
        foreach (var m in Meshes)
        {
            w.Write(m.Name);
            w.Write(m.Count);
            byte flags = (byte)((m.Nrm != null ? 1 : 0) | (m.Uv != null ? 2 : 0) | (m.Col != null ? 4 : 0));
            w.Write(flags);
            foreach (float f in m.Pos) w.Write(f);
            if (m.Nrm != null) foreach (float f in m.Nrm) w.Write(f);
            if (m.Uv != null) foreach (float f in m.Uv) w.Write(f);
            if (m.Col != null) w.Write(m.Col);
            w.Write(m.Subs.Count);
            foreach (var s in m.Subs) { w.Write(s.Length); foreach (int i in s) w.Write(i); }
        }
        w.Write(Objects.Count);
        foreach (var (name, nodes) in Objects)
        {
            w.Write(name);
            w.Write(nodes.Count);
            foreach (var n in nodes)
            {
                w.Write(n.Name); w.Write(n.Parent); w.Write(n.Active); w.Write(n.Layer);
                foreach (float f in n.Trs) w.Write(f);
                w.Write(n.Comps.Count);
                foreach (var c in n.Comps)
                {
                    w.Write(c.Kind); w.Write(c.Enabled); w.Write(c.Trigger); w.Write(c.Convex); w.Write(c.Mesh);
                    w.Write(c.Mats.Length); foreach (int i in c.Mats) w.Write(i);
                    w.Write(c.A.Length); foreach (float f in c.A) w.Write(f);
                    w.Write(c.Text); w.Write(c.FontSize); w.Write(c.Anchor); w.Write(c.Align); w.Write(c.Style); w.Write(c.Rich);
                }
            }
        }
        w.Write(Blobs.Count);
        foreach (var (n, d) in Blobs) { w.Write(n); w.Write(d.Length); w.Write(d); }
        w.Flush();
        byte[] raw = ms.ToArray();
        var outMs = new MemoryStream();
        outMs.Write(BitConverter.GetBytes(raw.Length));
        using (var d = new DeflateStream(outMs, CompressionLevel.SmallestSize, true)) d.Write(raw);
        return outMs.ToArray();
    }

    internal string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine("textures " + Textures.Count);
        for (int i = 0; i < Textures.Count; i++) sb.AppendLine("  t" + i + " " + Textures[i].Name + " " + Textures[i].W + "x" + Textures[i].H + " mips " + Textures[i].Mips);
        sb.AppendLine("materials " + Materials.Count);
        for (int i = 0; i < Materials.Count; i++)
        {
            var m = Materials[i];
            sb.AppendLine("  m" + i + " " + m.Name + " [" + m.Shader + "]");
            foreach (var (n, t, a) in m.Texs) if (t != -1) sb.AppendLine("    tex " + n + " t" + t + " scale " + a[0] + "," + a[1] + " off " + a[2] + "," + a[3]);
            foreach (var (n, f) in m.Floats) sb.AppendLine("    float " + n + " " + f);
            foreach (var (n, c) in m.Colors) sb.AppendLine("    color " + n + " " + string.Join(",", c.Select(x => x.ToString("0.###"))));
        }
        sb.AppendLine("meshes " + Meshes.Count + " verts " + Meshes.Sum(m => m.Count));
        foreach (var (name, nodes) in Objects)
        {
            sb.AppendLine("object " + name + " nodes " + nodes.Count);
            var depth = new int[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                depth[i] = n.Parent < 0 ? 0 : depth[n.Parent] + 1;
                var parts = n.Comps.Select(c => c.Kind switch
                {
                    MeshFilter => "mesh:" + (c.Mesh >= 0 ? Meshes[c.Mesh].Name : c.Mesh == -1 ? "none" : "builtin" + (-c.Mesh)),
                    MeshRenderer => "rend" + (c.Enabled ? "" : "(off)") + ":" + string.Join("/", c.Mats.Select(x => "m" + x)),
                    Box => "box" + (c.Trigger ? "T" : "") + (c.Enabled ? "" : "(off)"),
                    Sphere => "sphere" + (c.Trigger ? "T" : "") + (c.Enabled ? "" : "(off)"),
                    MeshCol => "meshcol" + (c.Convex ? "C" : "") + (c.Trigger ? "T" : "") + (c.Enabled ? "" : "(off)"),
                    Text => "text(fs " + c.FontSize + " cs " + c.A[1] + " anchor " + c.Anchor + " align " + c.Align + " style " + c.Style + "):\"" + c.Text.Replace("\n", "\\n") + "\"",
                    _ => "?"
                });
                sb.AppendLine(new string(' ', 2 + depth[i] * 2) + n.Name + (n.Active ? "" : " (inactive)") + " L" + n.Layer + " " + string.Join(" ", parts));
            }
        }
        foreach (var (n, d) in Blobs) sb.AppendLine("blob " + n + " " + d.Length);
        foreach (var (name, nodes) in Objects)
        {
            var world = new System.Numerics.Matrix4x4[nodes.Count];
            var lo = new System.Numerics.Vector3(float.MaxValue);
            var hi = new System.Numerics.Vector3(float.MinValue);
            var glo = new System.Numerics.Vector3(float.MaxValue);
            var ghi = new System.Numerics.Vector3(float.MinValue);
            for (int i = 0; i < nodes.Count; i++)
            {
                var t = nodes[i].Trs;
                var m = System.Numerics.Matrix4x4.CreateScale(t[7], t[8], t[9]) * System.Numerics.Matrix4x4.CreateFromQuaternion(new System.Numerics.Quaternion(t[3], t[4], t[5], t[6])) * System.Numerics.Matrix4x4.CreateTranslation(t[0], t[1], t[2]);
                world[i] = nodes[i].Parent < 0 ? System.Numerics.Matrix4x4.Identity : m * world[nodes[i].Parent];
                foreach (var c in nodes[i].Comps)
                {
                    if (c.Kind == MeshFilter && c.Mesh >= 0)
                    {
                        var md = Meshes[c.Mesh];
                        for (int v = 0; v < md.Count; v++)
                        {
                            var w = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(md.Pos[v * 3], md.Pos[v * 3 + 1], md.Pos[v * 3 + 2]), world[i]);
                            lo = System.Numerics.Vector3.Min(lo, w); hi = System.Numerics.Vector3.Max(hi, w);
                        }
                    }
                    if (c.Kind == Box && nodes[i].Layer == 10)
                    {
                        for (int k = 0; k < 8; k++)
                        {
                            var corner = new System.Numerics.Vector3(c.A[0] + c.A[3] * ((k & 1) == 0 ? -0.5f : 0.5f), c.A[1] + c.A[4] * ((k & 2) == 0 ? -0.5f : 0.5f), c.A[2] + c.A[5] * ((k & 4) == 0 ? -0.5f : 0.5f));
                            var w = System.Numerics.Vector3.Transform(corner, world[i]);
                            glo = System.Numerics.Vector3.Min(glo, w); ghi = System.Numerics.Vector3.Max(ghi, w);
                        }
                    }
                }
            }
            sb.AppendLine("bounds " + name + " visual " + lo + " .. " + hi + " ground colliders " + glo + " .. " + ghi);
        }
        return sb.ToString();
    }
}
