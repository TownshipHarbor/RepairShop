using System;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

static class Probe2
{
    internal static void Run(string bundlePath)
    {
        var b = new Bundle(bundlePath, "probe");
        int shown = 0;
        var seen = new System.Collections.Generic.HashSet<string>();
        foreach (var info in b.All(AssetClassID.Mesh))
        {
            var m = b.Am.GetBaseField(b.Inst, info);
            var chans = m["m_VertexData"]["m_Channels"]["Array"].Children;
            if (chans.Count <= 7 || (chans[7]["dimension"].AsInt & 0xF) == 0) continue;
            var md = Decode.Mesh(b, m);
            var c7 = ReadCh(b, m, 7);
            var rects = Enumerable.Range(0, md.Count).Select(i => string.Join(",", Enumerable.Range(0, 4).Select(k => c7[i * 4 + k].ToString("0.####")))).Distinct().ToList();
            if (shown < 12) Console.WriteLine(md.Name + " verts " + md.Count + " rects " + rects.Count + ": " + string.Join(" | ", rects.Take(4)) + "  uv0 sample " + string.Join(" ", Enumerable.Range(0, Math.Min(3, md.Count)).Select(i => md.Uv[i * 2].ToString("0.##") + "/" + md.Uv[i * 2 + 1].ToString("0.##"))));
            shown++;
            foreach (var r in rects) seen.Add(r);
        }
        Console.WriteLine("meshes with uv3 " + shown + ", distinct rects " + seen.Count);
        foreach (var r in seen.Take(40)) Console.WriteLine("  " + r);
    }

    static float[] ReadCh(Bundle b, AssetTypeValueField m, int ch)
    {
        var vd = m["m_VertexData"];
        int count = (int)vd["m_VertexCount"].AsUInt;
        var chans = vd["m_Channels"]["Array"].Children;
        byte[] data = b.Stream(m["m_StreamData"], vd["m_DataSize"]);
        int[] fs = { 4, 2, 1, 1, 2, 2, 1, 1, 2, 2, 4, 4 };
        int streams = 0;
        foreach (var c in chans) if ((c["dimension"].AsInt & 0xF) > 0) streams = Math.Max(streams, c["stream"].AsInt + 1);
        int[] stride = new int[streams]; int[] start = new int[streams];
        foreach (var c in chans) { int d = c["dimension"].AsInt & 0xF; if (d > 0) stride[c["stream"].AsInt] += fs[c["format"].AsInt] * d; }
        int off = 0;
        for (int s = 0; s < streams; s++) { start[s] = off; off += stride[s] * count; off = (off + 15) & ~15; }
        var cc = chans[ch];
        float[] r = new float[count * 4];
        for (int v = 0; v < count; v++) for (int k = 0; k < 4; k++) r[v * 4 + k] = BitConverter.ToSingle(data, start[cc["stream"].AsInt] + stride[cc["stream"].AsInt] * v + cc["offset"].AsInt + k * 4);
        return r;
    }
}
