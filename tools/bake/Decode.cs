using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;

sealed class MeshData
{
    internal string Name;
    internal float[] Pos, Nrm, Uv;
    internal byte[] Col;
    internal List<int[]> Subs = new List<int[]>();
    internal float[] Rect;
    internal int Count { get { return Pos.Length / 3; } }
}

static class Decode
{
    static readonly int[] FormatSize = { 4, 2, 1, 1, 2, 2, 1, 1, 2, 2, 4, 4 };

    internal static MeshData Mesh(Bundle b, AssetTypeValueField m)
    {
        var md = new MeshData { Name = m["m_Name"].AsString };
        var vd = m["m_VertexData"];
        int count = (int)vd["m_VertexCount"].AsUInt;
        var chans = vd["m_Channels"]["Array"].Children;
        byte[] data = b.Stream(m["m_StreamData"], vd["m_DataSize"]);
        int streams = 0;
        foreach (var c in chans) if ((c["dimension"].AsInt & 0xF) > 0) streams = Math.Max(streams, c["stream"].AsInt + 1);
        int[] stride = new int[streams];
        int[] start = new int[streams];
        foreach (var c in chans)
        {
            int dim = c["dimension"].AsInt & 0xF;
            if (dim == 0) continue;
            stride[c["stream"].AsInt] += FormatSize[c["format"].AsInt] * dim;
        }
        int off = 0;
        for (int s = 0; s < streams; s++)
        {
            start[s] = off;
            off += stride[s] * count;
            off = (off + 15) & ~15;
        }
        float[] Read(int ch, int want)
        {
            if (ch >= chans.Count) return null;
            var c = chans[ch];
            int dim = c["dimension"].AsInt & 0xF;
            if (dim == 0) return null;
            int fmt = c["format"].AsInt, st = c["stream"].AsInt, o = c["offset"].AsInt;
            float[] r = new float[count * want];
            for (int v = 0; v < count; v++)
            {
                int p = start[st] + stride[st] * v + o;
                for (int k = 0; k < want && k < dim; k++) r[v * want + k] = Value(data, p + k * FormatSize[fmt], fmt);
            }
            return r;
        }
        md.Pos = Read(0, 3);
        md.Nrm = Read(1, 3);
        md.Uv = Read(4, 2);
        float[] col = Read(3, 4);
        if (col != null)
        {
            md.Col = new byte[col.Length];
            bool unorm = chans[3]["format"].AsInt == 2;
            for (int i = 0; i < col.Length; i++) md.Col[i] = unorm ? (byte)Math.Round(col[i] * 255f) : (byte)Math.Clamp(Math.Round(col[i] * 255f), 0, 255);
        }
        if (md.Pos == null) throw new InvalidDataException(md.Name + ": no positions");
        float[] rect = Read(7, 4);
        if (rect != null && count > 0)
        {
            md.Rect = new[] { rect[0], rect[1], rect[2], rect[3] };
            for (int v = 1; v < count; v++)
                for (int k = 0; k < 4; k++)
                    if (Math.Abs(rect[v * 4 + k] - md.Rect[k]) > 1e-5f) throw new InvalidDataException(md.Name + ": more than one atlas rect");
        }
        byte[] ib = m["m_IndexBuffer"]["Array"].AsByteArray;
        bool wide = m["m_IndexFormat"].AsInt == 1;
        foreach (var sm in m["m_SubMeshes"]["Array"].Children)
        {
            if (sm["topology"].AsInt != 0) throw new InvalidDataException(md.Name + ": topology " + sm["topology"].AsInt);
            int first = (int)sm["firstByte"].AsUInt, n = (int)sm["indexCount"].AsUInt, bv = (int)sm["baseVertex"].AsUInt;
            int[] idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = (wide ? (int)BitConverter.ToUInt32(ib, first + i * 4) : BitConverter.ToUInt16(ib, first + i * 2)) + bv;
            md.Subs.Add(idx);
        }
        return md;
    }

    static float Value(byte[] d, int p, int fmt)
    {
        switch (fmt)
        {
            case 0: return BitConverter.ToSingle(d, p);
            case 1: return (float)BitConverter.ToHalf(d, p);
            case 2: return d[p] / 255f;
            case 3: return Math.Max((sbyte)d[p] / 127f, -1f);
            case 4: return BitConverter.ToUInt16(d, p) / 65535f;
            case 5: return Math.Max(BitConverter.ToInt16(d, p) / 32767f, -1f);
            case 6: return d[p];
            case 7: return (sbyte)d[p];
            case 8: return BitConverter.ToUInt16(d, p);
            case 9: return BitConverter.ToInt16(d, p);
            case 10: return BitConverter.ToUInt32(d, p);
            case 11: return BitConverter.ToInt32(d, p);
        }
        throw new InvalidDataException("vertex format " + fmt);
    }

    internal static byte[] Texture(Bundle b, AssetTypeValueField t, out int w, out int h)
    {
        w = t["m_Width"].AsInt;
        h = t["m_Height"].AsInt;
        int fmt = t["m_TextureFormat"].AsInt;
        byte[] data = b.Stream(t["m_StreamData"], t["image data"]);
        byte[] rgba = new byte[w * h * 4];
        int bw = (w + 3) / 4, bh = (h + 3) / 4;
        int blockSize = fmt == 10 ? 8 : fmt == 12 ? 16 : 0;
        if (blockSize == 0)
        {
            if (fmt == 4 && data.Length >= rgba.Length) { Buffer.BlockCopy(data, 0, rgba, 0, rgba.Length); return rgba; }
            throw new InvalidDataException(t["m_Name"].AsString + ": texture format " + fmt);
        }
        byte[] block = new byte[64];
        for (int by = 0; by < bh; by++)
        {
            for (int bx = 0; bx < bw; bx++)
            {
                int p = (by * bw + bx) * blockSize;
                if (fmt == 10) Bc1(data, p, block, true);
                else { Bc1(data, p + 8, block, false); Bc3Alpha(data, p, block); }
                for (int y = 0; y < 4; y++)
                {
                    int py = by * 4 + y;
                    if (py >= h) break;
                    for (int x = 0; x < 4; x++)
                    {
                        int px = bx * 4 + x;
                        if (px >= w) break;
                        Buffer.BlockCopy(block, (y * 4 + x) * 4, rgba, (py * w + px) * 4, 4);
                    }
                }
            }
        }
        return rgba;
    }

    static void Bc1(byte[] d, int p, byte[] o, bool alpha)
    {
        ushort c0 = BitConverter.ToUInt16(d, p), c1 = BitConverter.ToUInt16(d, p + 2);
        int[,] c = new int[4, 4];
        Rgb565(c0, c, 0);
        Rgb565(c1, c, 1);
        bool four = c0 > c1 || !alpha;
        for (int k = 0; k < 3; k++)
        {
            if (four)
            {
                c[2, k] = (2 * c[0, k] + c[1, k]) / 3;
                c[3, k] = (c[0, k] + 2 * c[1, k]) / 3;
            }
            else
            {
                c[2, k] = (c[0, k] + c[1, k]) / 2;
                c[3, k] = 0;
            }
        }
        c[0, 3] = c[1, 3] = c[2, 3] = 255;
        c[3, 3] = four ? 255 : 0;
        uint bits = BitConverter.ToUInt32(d, p + 4);
        for (int i = 0; i < 16; i++)
        {
            int s = (int)((bits >> (2 * i)) & 3);
            o[i * 4] = (byte)c[s, 0];
            o[i * 4 + 1] = (byte)c[s, 1];
            o[i * 4 + 2] = (byte)c[s, 2];
            if (alpha) o[i * 4 + 3] = (byte)c[s, 3];
        }
    }

    static void Rgb565(ushort v, int[,] c, int i)
    {
        int r = (v >> 11) & 31, g = (v >> 5) & 63, b = v & 31;
        c[i, 0] = (r << 3) | (r >> 2);
        c[i, 1] = (g << 2) | (g >> 4);
        c[i, 2] = (b << 3) | (b >> 2);
    }

    static void Bc3Alpha(byte[] d, int p, byte[] o)
    {
        int a0 = d[p], a1 = d[p + 1];
        int[] a = new int[8];
        a[0] = a0;
        a[1] = a1;
        if (a0 > a1) for (int i = 1; i < 7; i++) a[i + 1] = ((7 - i) * a0 + i * a1) / 7;
        else
        {
            for (int i = 1; i < 5; i++) a[i + 1] = ((5 - i) * a0 + i * a1) / 5;
            a[6] = 0;
            a[7] = 255;
        }
        ulong bits = 0;
        for (int i = 0; i < 6; i++) bits |= (ulong)d[p + 2 + i] << (8 * i);
        for (int i = 0; i < 16; i++) o[i * 4 + 3] = (byte)a[(int)((bits >> (3 * i)) & 7)];
    }

    internal static byte[] Half(byte[] src, int w, int h, out int nw, out int nh)
    {
        nw = Math.Max(1, w / 2);
        nh = Math.Max(1, h / 2);
        byte[] dst = new byte[nw * nh * 4];
        for (int y = 0; y < nh; y++)
        {
            for (int x = 0; x < nw; x++)
            {
                for (int k = 0; k < 4; k++)
                {
                    int sx = Math.Min(x * 2, w - 1), sy = Math.Min(y * 2, h - 1);
                    int sx1 = Math.Min(sx + 1, w - 1), sy1 = Math.Min(sy + 1, h - 1);
                    int s = src[(sy * w + sx) * 4 + k] + src[(sy * w + sx1) * 4 + k] + src[(sy1 * w + sx) * 4 + k] + src[(sy1 * w + sx1) * 4 + k];
                    dst[(y * nw + x) * 4 + k] = (byte)((s + 2) / 4);
                }
            }
        }
        return dst;
    }
}
