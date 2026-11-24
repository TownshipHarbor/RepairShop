using System;
using System.Collections.Generic;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

static class Probe
{
    internal static void Run(string bundlePath)
    {
        var b = new Bundle(bundlePath, "probe");
        var stats = new Dictionary<string, List<string>>();
        foreach (var info in b.All(AssetClassID.MeshRenderer))
        {
            var r = b.Am.GetBaseField(b.Inst, info);
            var go = b.Ref(r["m_GameObject"]);
            AssetTypeValueField mf = null;
            foreach (var c in go["m_Component"]["Array"].Children)
            {
                var ci = b.Info(Bundle.Id(c["component"]));
                if (ci != null && ci.TypeId == (int)AssetClassID.MeshFilter) mf = b.Am.GetBaseField(b.Inst, ci);
            }
            if (mf == null) continue;
            var mesh = b.Ref(mf["m_Mesh"]);
            if (mesh == null) continue;
            var chans = mesh["m_VertexData"]["m_Channels"]["Array"].Children;
            string used = string.Join(",", Enumerable.Range(0, chans.Count).Where(i => (chans[i]["dimension"].AsInt & 0xF) > 0).Select(i => i + ":" + (chans[i]["dimension"].AsInt & 0xF) + "f" + chans[i]["format"].AsInt));
            var md = Decode.Mesh(b, mesh);
            string uvr = md.Uv == null ? "nouv" : "u[" + Enumerable.Range(0, md.Count).Min(i => md.Uv[i * 2]).ToString("0.##") + "," + Enumerable.Range(0, md.Count).Max(i => md.Uv[i * 2]).ToString("0.##") + "] v[" + Enumerable.Range(0, md.Count).Min(i => md.Uv[i * 2 + 1]).ToString("0.##") + "," + Enumerable.Range(0, md.Count).Max(i => md.Uv[i * 2 + 1]).ToString("0.##") + "]";
            string col = md.Col == null ? "" : " col[" + md.Col.Where((x, i) => i % 4 == 0).Min() + "-" + md.Col.Where((x, i) => i % 4 == 0).Max() + "]";
            foreach (var m in r["m_Materials"]["Array"].Children)
            {
                var mat = b.Ref(m);
                if (mat == null) continue;
                var sh = b.Ref(mat["m_Shader"]);
                string key = (sh == null ? "?" : sh["m_ParsedForm"]["m_Name"].AsString) + " | " + mat["m_Name"].AsString;
                if (!stats.TryGetValue(key, out var l)) stats[key] = l = new List<string>();
                l.Add(go["m_Name"].AsString + " " + used + " " + uvr + col);
            }
        }
        foreach (var kv in stats.OrderBy(k => k.Key))
        {
            Console.WriteLine(kv.Key + " x" + kv.Value.Count);
            foreach (var s in kv.Value.Take(4)) Console.WriteLine("   " + s);
        }
        foreach (var info in b.All(AssetClassID.Shader))
        {
            var sh = b.Am.GetBaseField(b.Inst, info);
            var pf = sh["m_ParsedForm"];
            Console.WriteLine("shader " + pf["m_Name"].AsString);
            foreach (var p in pf["m_PropInfo"]["m_Props"]["Array"].Children) Console.Write(" " + p["m_Name"].AsString);
            Console.WriteLine();
            foreach (var ss in pf["m_SubShaders"]["Array"].Children)
                foreach (var pass in ss["m_Passes"]["Array"].Children)
                {
                    Console.Write("  pass '" + pass["m_State"]["m_Name"].AsString + "' tags:");
                    foreach (var t in pass["m_Tags"]["tags"]["Array"].Children) Console.Write(" " + t["first"].AsString + "=" + t["second"].AsString);
                    var st = pass["m_State"];
                    Console.WriteLine(" zwrite=" + st["zWrite"]["val"].AsFloat + " cull=" + st["culling"]["val"].AsFloat + " blend=" + st["rtBlend0"]["srcBlend"]["val"].AsFloat + "/" + st["rtBlend0"]["destBlend"]["val"].AsFloat);
                }
        }
    }
}
