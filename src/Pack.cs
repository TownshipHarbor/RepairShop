using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnityEngine;
using Il2CppTMPro;
using Object = UnityEngine.Object;

namespace RepairShopQuest
{
    internal sealed class CompDef
    {
        internal byte Kind;
        internal bool Enabled, Trigger, Convex;
        internal int Mesh;
        internal int[] Mats;
        internal float[] A;
        internal string Text;
        internal int FontSize, Anchor, Align, Style;
        internal bool Rich;
    }

    internal sealed class NodeDef
    {
        internal string Name;
        internal int Parent, Layer;
        internal bool Active;
        internal Vector3 Pos, Scale;
        internal Quaternion Rot;
        internal List<CompDef> Comps = new List<CompDef>();
    }

    internal sealed class MeshDef
    {
        internal string Name;
        internal Vector3[] V, N;
        internal Vector2[] UV;
        internal List<int[]> Subs = new List<int[]>();
        internal Mesh Made;
    }

    internal sealed class MatDef
    {
        internal string Name, Shader;
        internal int Tex = -1;
        internal Color Color = Color.white;
        internal Material Made;
    }

    internal sealed class TexDef
    {
        internal string Name;
        internal int W, H, Mips;
        internal byte[] Data;
        internal Texture2D Made;
    }

    internal static class Pack
    {
        internal const byte MeshFilterKind = 1, RendererKind = 2, BoxKind = 3, SphereKind = 4, MeshColliderKind = 5, TextKind = 6;
        internal const string Simple = "Township Tale/Cheap/SimpleStandard";
        internal const string TextShader = "RepairShop/World Text";
        static readonly List<TexDef> Textures = new List<TexDef>();
        static readonly List<MatDef> Materials = new List<MatDef>();
        static readonly List<MeshDef> Meshes = new List<MeshDef>();
        static readonly Dictionary<string, List<NodeDef>> Objects = new Dictionary<string, List<NodeDef>>();
        static readonly Dictionary<string, byte[]> Blobs = new Dictionary<string, byte[]>();
        static readonly Dictionary<int, Mesh> Builtin = new Dictionary<int, Mesh>();
        static readonly Dictionary<long, MeshDef> ByMesh = new Dictionary<long, MeshDef>();
        static Material _template, _white;
        static TMP_FontAsset _font;
        static bool _looked;
        internal static bool Loaded;

        internal static void Read()
        {
            if (Loaded) return;
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("RepairShopQuest.repairshop.pack"))
            {
                if (s == null) throw new InvalidDataException("the repair shop pack is missing from the DLL");
                byte[] head = new byte[4];
                if (s.Read(head, 0, 4) != 4) throw new InvalidDataException("the repair shop pack is empty");
                using (DeflateStream d = new DeflateStream(s, CompressionMode.Decompress))
                using (BinaryReader r = new BinaryReader(new BufferedStream(d, 1 << 16)))
                    Load(r);
            }
            Loaded = true;
        }

        static void Load(BinaryReader r)
        {
            {
                if (new string(r.ReadChars(4)) != "RSQ1") throw new InvalidDataException("the repair shop pack has the wrong header");
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    TexDef t = new TexDef { Name = r.ReadString(), W = r.ReadInt32(), H = r.ReadInt32(), Mips = r.ReadInt32() };
                    t.Data = r.ReadBytes(r.ReadInt32());
                    Textures.Add(t);
                }
                count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    MatDef m = new MatDef { Name = r.ReadString(), Shader = r.ReadString() };
                    int texs = r.ReadInt32();
                    for (int k = 0; k < texs; k++)
                    {
                        string n = r.ReadString();
                        int t = r.ReadInt32();
                        for (int j = 0; j < 4; j++) r.ReadSingle();
                        if (n == "_MainTex") m.Tex = t;
                    }
                    int floats = r.ReadInt32();
                    for (int k = 0; k < floats; k++) { r.ReadString(); r.ReadSingle(); }
                    int colors = r.ReadInt32();
                    for (int k = 0; k < colors; k++)
                    {
                        string n = r.ReadString();
                        Color c = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        if (n == "_Color") m.Color = c;
                    }
                    Materials.Add(m);
                }
                count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    MeshDef m = new MeshDef { Name = r.ReadString() };
                    int n = r.ReadInt32();
                    byte flags = r.ReadByte();
                    m.V = new Vector3[n];
                    for (int k = 0; k < n; k++) m.V[k] = V3(r);
                    if ((flags & 1) != 0)
                    {
                        m.N = new Vector3[n];
                        for (int k = 0; k < n; k++) m.N[k] = V3(r);
                    }
                    m.UV = new Vector2[n];
                    if ((flags & 2) != 0) for (int k = 0; k < n; k++) m.UV[k] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    if ((flags & 4) != 0) r.ReadBytes(n * 4);
                    int subs = r.ReadInt32();
                    for (int k = 0; k < subs; k++)
                    {
                        int[] idx = new int[r.ReadInt32()];
                        for (int j = 0; j < idx.Length; j++) idx[j] = r.ReadInt32();
                        m.Subs.Add(idx);
                    }
                    Meshes.Add(m);
                }
                count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string name = r.ReadString();
                    int nodes = r.ReadInt32();
                    List<NodeDef> list = new List<NodeDef>(nodes);
                    for (int k = 0; k < nodes; k++)
                    {
                        NodeDef d = new NodeDef { Name = r.ReadString(), Parent = r.ReadInt32(), Active = r.ReadBoolean(), Layer = r.ReadInt32() };
                        d.Pos = V3(r);
                        d.Rot = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        d.Scale = V3(r);
                        int comps = r.ReadInt32();
                        for (int j = 0; j < comps; j++)
                        {
                            CompDef c = new CompDef { Kind = r.ReadByte(), Enabled = r.ReadBoolean(), Trigger = r.ReadBoolean(), Convex = r.ReadBoolean(), Mesh = r.ReadInt32() };
                            c.Mats = new int[r.ReadInt32()];
                            for (int q = 0; q < c.Mats.Length; q++) c.Mats[q] = r.ReadInt32();
                            c.A = new float[r.ReadInt32()];
                            for (int q = 0; q < c.A.Length; q++) c.A[q] = r.ReadSingle();
                            c.Text = r.ReadString();
                            c.FontSize = r.ReadInt32();
                            c.Anchor = r.ReadInt32();
                            c.Align = r.ReadInt32();
                            c.Style = r.ReadInt32();
                            c.Rich = r.ReadBoolean();
                            d.Comps.Add(c);
                        }
                        list.Add(d);
                    }
                    Objects[name] = list;
                }
                count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string name = r.ReadString();
                    Blobs[name] = r.ReadBytes(r.ReadInt32());
                }
            }
        }

        static Vector3 V3(BinaryReader r)
        {
            return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        internal static bool Has(string name)
        {
            return Objects.ContainsKey(name);
        }

        internal static byte[] Blob(string name)
        {
            byte[] b;
            return Blobs.TryGetValue(name, out b) ? b : null;
        }

        static Mesh MeshOf(int i)
        {
            if (i < 0) return BuiltinMesh(-i);
            MeshDef d = Meshes[i];
            if (d.Made == null)
            {
                Mesh m = Plat.NewMesh("RepairShop " + d.Name, d.V, d.N, d.UV, d.Subs);
                if (d.N == null) m.RecalculateNormals();
                m.hideFlags = HideFlags.DontUnloadUnusedAsset;
                d.Made = m;
                ByMesh[Plat.Key(m)] = d;
            }
            return d.Made;
        }

        static MeshDef DataOf(Mesh m)
        {
            MeshDef d;
            if (ByMesh.TryGetValue(Plat.Key(m), out d)) return d;
            d = new MeshDef { Name = m.name };
            var v = m.vertices;
            var n = m.normals;
            var uv = m.uv;
            d.V = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++) d.V[i] = v[i];
            d.N = new Vector3[n.Length];
            for (int i = 0; i < n.Length; i++) d.N[i] = n[i];
            d.UV = new Vector2[v.Length];
            for (int i = 0; i < uv.Length && i < v.Length; i++) d.UV[i] = uv[i];
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var t = m.GetTriangles(s);
                int[] idx = new int[t.Length];
                for (int i = 0; i < t.Length; i++) idx[i] = t[i];
                d.Subs.Add(idx);
            }
            if (d.N != null && d.N.Length != d.V.Length) d.N = null;
            ByMesh[Plat.Key(m)] = d;
            return d;
        }

        sealed class Batch
        {
            internal Material Mat;
            internal int Layer;
            internal readonly List<Vector3> V = new List<Vector3>();
            internal readonly List<Vector3> N = new List<Vector3>();
            internal readonly List<Vector2> UV = new List<Vector2>();
            internal readonly List<int> I = new List<int>();
            internal int Parts;
        }

        internal static int Merge(GameObject root, Func<Transform, bool> keep)
        {
            Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            int merged = 0;
            foreach (MeshRenderer r in Plat.All<MeshRenderer>(root.transform, true))
            {
                if (r == null || !r.enabled || !r.gameObject.activeSelf || keep(r.transform)) continue;
                bool hidden = false;
                for (Transform t = r.transform; t != null && !Plat.Same(t, root.transform); t = t.parent) if (!t.gameObject.activeSelf) hidden = true;
                if (hidden) continue;
                MeshFilter f = Plat.Get<MeshFilter>(r);
                if (f == null || f.sharedMesh == null) continue;
                List<Material> mats = new List<Material>();
                foreach (Material m in r.sharedMaterials) mats.Add(m);
                MeshDef d = DataOf(f.sharedMesh);
                if (mats.Count == 0 || d.Subs.Count == 0) continue;
                Matrix4x4 m4 = toRoot * r.transform.localToWorldMatrix;
                Matrix4x4 n4 = m4.inverse.transpose;
                bool flip = m4.determinant < 0f;
                for (int s = 0; s < d.Subs.Count; s++)
                {
                    Material mat = mats[Math.Min(s, mats.Count - 1)];
                    if (mat == null) continue;
                    string key = Plat.Key(mat) + "/" + r.gameObject.layer;
                    Batch b;
                    if (!batches.TryGetValue(key, out b))
                    {
                        b = new Batch { Mat = mat, Layer = r.gameObject.layer };
                        batches[key] = b;
                    }
                    int start = b.V.Count;
                    for (int i = 0; i < d.V.Length; i++)
                    {
                        b.V.Add(m4.MultiplyPoint3x4(d.V[i]));
                        b.N.Add(d.N == null ? Vector3.up : n4.MultiplyVector(d.N[i]).normalized);
                        b.UV.Add(d.UV[i]);
                    }
                    int[] tri = d.Subs[s];
                    for (int i = 0; i + 2 < tri.Length; i += 3)
                    {
                        b.I.Add(start + tri[i]);
                        b.I.Add(start + (flip ? tri[i + 2] : tri[i + 1]));
                        b.I.Add(start + (flip ? tri[i + 1] : tri[i + 2]));
                    }
                    b.Parts++;
                }
                r.enabled = false;
                merged++;
            }
            foreach (Batch b in batches.Values)
            {
                GameObject g = new GameObject("Merged " + b.Mat.name);
                g.layer = b.Layer;
                g.transform.SetParent(root.transform, false);
                List<int[]> subs = new List<int[]>();
                subs.Add(b.I.ToArray());
                Mesh mesh = Plat.NewMesh(g.name, b.V.ToArray(), b.N.ToArray(), b.UV.ToArray(), subs);
                mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Plat.Add<MeshFilter>(g).sharedMesh = mesh;
                List<Material> one = new List<Material>();
                one.Add(b.Mat);
                Plat.SetMaterials(Plat.Add<MeshRenderer>(g), one);
            }
            Core.Log("merged " + merged + " renderers into " + batches.Count + " draws");
            return batches.Count;
        }

        static Mesh BuiltinMesh(int id)
        {
            Mesh m;
            if (Builtin.TryGetValue(id, out m) && m != null) return m;
            PrimitiveType type;
            switch (id)
            {
                case 10202: type = PrimitiveType.Cube; break;
                case 10206: type = PrimitiveType.Cylinder; break;
                case 10207: type = PrimitiveType.Sphere; break;
                case 10208: type = PrimitiveType.Capsule; break;
                case 10209: type = PrimitiveType.Plane; break;
                case 10210: type = PrimitiveType.Quad; break;
                default: throw new InvalidDataException("unknown built-in mesh " + id);
            }
            GameObject g = GameObject.CreatePrimitive(type);
            m = Plat.Get<MeshFilter>(g).sharedMesh;
            Object.Destroy(g);
            Builtin[id] = m;
            return m;
        }

        static Texture2D TextureOf(int i)
        {
            if (i < 0 || i >= Textures.Count) return null;
            TexDef d = Textures[i];
            if (d.Made == null && d.Data != null)
            {
                d.Made = Plat.NewTexture("RepairShop " + d.Name, d.W, d.H, d.Data);
                d.Data = null;
            }
            return d.Made;
        }

        internal static Material MaterialOf(int i)
        {
            if (i < 0 || i >= Materials.Count)
            {
                if (_white == null)
                {
                    _white = Lit();
                    _white.name = "RepairShop default";
                    _white.hideFlags = HideFlags.DontUnloadUnusedAsset;
                }
                return _white;
            }
            MatDef d = Materials[i];
            if (d.Made != null) return d.Made;
            Material m;
            if (d.Shader == "IslandRepairShop/Burning Coals" && Shader.Find("Unlit/Texture") != null)
            {
                m = new Material(Shader.Find("Unlit/Texture"));
            }
            else
            {
                m = Lit();
                SetColor(m, "_Color", d.Color);
            }
            m.name = "RepairShop " + d.Name;
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Texture2D t = TextureOf(d.Tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
            d.Made = m;
            return m;
        }

        internal static bool IsText(int i)
        {
            return i >= 0 && i < Materials.Count && Materials[i].Shader == TextShader;
        }

        internal static Material Lit()
        {
            LookUp();
            Material m = null;
            if (_template != null) m = Plat.CopyAsset(_template);
            if (m == null)
            {
                Shader s = Shader.Find(Simple);
                if (s == null) throw new InvalidOperationException("the game shader " + Simple + " is missing");
                m = new Material(s);
            }
            Switch(m, "_MetallicMap", "_UseMetallicMap", false);
            Switch(m, "_Normal", "_UseNormal", false);
            Switch(m, "_ScaledNormal", "_UseScaledNormal", false);
            Switch(m, "_AlphaSmooth", "_UseAlphaSmooth", false);
            Switch(m, "_AlphaEmission", "_UseAlphaEmission", false);
            m.DisableKeyword("_NORMALMAP");
            m.DisableKeyword("_EMISSION");
            m.DisableKeyword("_ALPHATEST_ON");
            SetColor(m, "_EmissionColor", Color.black);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", null);
            SetFloat(m, "_Glossiness", 0.1f);
            SetFloat(m, "_Metallic", 0f);
            SetFloat(m, "_Cull", 0f);
            SetFloat(m, "_ZWrite", 1f);
            SetFloat(m, "_SrcBlend", 1f);
            SetFloat(m, "_DstBlend", 0f);
            m.renderQueue = 2000;
            return m;
        }

        static void LookUp()
        {
            if (_looked) return;
            _looked = true;
            int best = int.MaxValue;
            foreach (Object o in Plat.Loaded<Material>())
            {
                Material m = Plat.As<Material>(o);
                Shader sh = m == null ? null : m.shader;
                if (sh == null || sh.name != Simple) continue;
                if (m.renderQueue >= 2450 || m.IsKeywordEnabled("_UseAlphaSmooth") || m.IsKeywordEnabled("_ALPHATEST_ON")) continue;
                string n = m.name;
                if (n == null || n.StartsWith("RepairShop", StringComparison.Ordinal) || n.StartsWith("PortalGun", StringComparison.Ordinal) || n.StartsWith("ArmorRestored", StringComparison.Ordinal) || n.IndexOf("Font", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                int extra = (m.IsKeywordEnabled("_UseMetallicMap") ? 4 : 0) + (m.IsKeywordEnabled("_UseNormal") ? 4 : 0) + (m.IsKeywordEnabled("_EMISSION") ? 2 : 0) + (m.IsKeywordEnabled("_UseScaledNormal") ? 1 : 0) + (m.IsKeywordEnabled("_UseAlphaEmission") ? 2 : 0);
                if (extra < best)
                {
                    best = extra;
                    _template = m;
                }
            }
            Core.Log("material template: " + (_template == null ? "none, a new " + Simple + " material" : _template.name));
        }

        static void SetColor(Material m, string property, Color c)
        {
            if (m != null && m.HasProperty(property)) m.SetColor(property, c);
        }

        static void SetFloat(Material m, string property, float v)
        {
            if (m.HasProperty(property)) m.SetFloat(property, v);
        }

        static void Switch(Material m, string property, string keyword, bool on)
        {
            if (m.HasProperty(property)) m.SetFloat(property, on ? 1f : 0f);
            if (on) m.EnableKeyword(keyword);
            else m.DisableKeyword(keyword);
        }

        internal static TMP_FontAsset Font
        {
            get
            {
                if (_font != null) return _font;
                _font = TMP_Settings.defaultFontAsset;
                if (_font == null)
                {
                    foreach (Object o in Plat.Loaded<TMP_FontAsset>())
                    {
                        TMP_FontAsset f = Plat.As<TMP_FontAsset>(o);
                        if (f == null || f.name == null) continue;
                        string n = f.name.ToLowerInvariant();
                        if (n.Contains("fallback") || n.Contains("emoji") || n.Contains("icon")) continue;
                        _font = f;
                        break;
                    }
                }
                Core.Log("label font: " + (_font == null ? "none" : _font.name));
                return _font;
            }
        }

        internal static GameObject Build(string name, Transform parent)
        {
            List<NodeDef> nodes;
            if (!Objects.TryGetValue(name, out nodes)) throw new KeyNotFoundException("the repair shop pack has no " + name);
            List<GameObject> made = new List<GameObject>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
            {
                NodeDef d = nodes[i];
                GameObject go = new GameObject(d.Name);
                go.layer = d.Layer;
                Transform t = go.transform;
                t.SetParent(d.Parent < 0 ? parent : made[d.Parent].transform, false);
                t.localPosition = d.Pos;
                t.localRotation = d.Rot;
                t.localScale = d.Scale;
                made.Add(go);
                MeshRenderer mr = null;
                foreach (CompDef c in d.Comps)
                {
                    switch (c.Kind)
                    {
                        case MeshFilterKind:
                            if (c.Mesh != -1) Plat.Add<MeshFilter>(go).sharedMesh = MeshOf(c.Mesh);
                            else Plat.Add<MeshFilter>(go);
                            break;
                        case RendererKind:
                            mr = Plat.Get<MeshRenderer>(go) ?? Plat.Add<MeshRenderer>(go);
                            List<Material> mats = new List<Material>();
                            bool text = false;
                            foreach (int k in c.Mats)
                            {
                                if (IsText(k)) text = true;
                                mats.Add(IsText(k) ? null : MaterialOf(k));
                            }
                            if (!text) Plat.SetMaterials(mr, mats);
                            mr.enabled = c.Enabled;
                            break;
                        case BoxKind:
                            BoxCollider box = Plat.Add<BoxCollider>(go);
                            box.center = new Vector3(c.A[0], c.A[1], c.A[2]);
                            box.size = new Vector3(c.A[3], c.A[4], c.A[5]);
                            box.isTrigger = c.Trigger;
                            box.enabled = c.Enabled;
                            break;
                        case SphereKind:
                            SphereCollider sphere = Plat.Add<SphereCollider>(go);
                            sphere.center = new Vector3(c.A[0], c.A[1], c.A[2]);
                            sphere.radius = c.A[3];
                            sphere.isTrigger = c.Trigger;
                            sphere.enabled = c.Enabled;
                            break;
                        case MeshColliderKind:
                            MeshCollider mc = Plat.Add<MeshCollider>(go);
                            if (c.Mesh != -1) mc.sharedMesh = MeshOf(c.Mesh);
                            mc.convex = c.Convex;
                            mc.isTrigger = c.Trigger;
                            mc.enabled = c.Enabled;
                            break;
                        case TextKind:
                            Text(go, c);
                            break;
                    }
                }
            }
            for (int i = 0; i < nodes.Count; i++) if (!nodes[i].Active) made[i].SetActive(false);
            return made[0];
        }

        static void Text(GameObject go, CompDef c)
        {
            Transform parent = go.transform.parent;
            Vector3 pos = go.transform.localPosition, scale = go.transform.localScale;
            Quaternion rot = go.transform.localRotation;
            go.transform.SetParent(null, false);
            try { Label(go, c); }
            finally
            {
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = rot;
                go.transform.localScale = scale;
            }
        }

        static void Label(GameObject go, CompDef c)
        {
            TextMeshPro t = Plat.Get<TextMeshPro>(go) ?? Plat.Add<TextMeshPro>(go);
            TMP_FontAsset f = Font;
            if (f != null) t.font = f;
            t.enableWordWrapping = false;
            t.enableAutoSizing = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.isOrthographic = false;
            t.rectTransform.sizeDelta = Vector2.zero;
            t.fontSize = (c.FontSize > 0 ? c.FontSize : 13) * c.A[1];
            t.color = new Color(c.A[4], c.A[5], c.A[6], c.A[7]);
            t.alignment = Align(c.Anchor);
            t.text = c.Text;
        }

        static TextAlignmentOptions Align(int anchor)
        {
            int row = anchor / 3, col = anchor % 3;
            int v = row == 0 ? 256 : row == 1 ? 512 : 1024;
            int h = col == 0 ? 1 : col == 1 ? 2 : 4;
            return (TextAlignmentOptions)(v | h);
        }
    }
}
