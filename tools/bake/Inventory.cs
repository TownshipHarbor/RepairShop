using System;
using System.Collections.Generic;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

static class Inventory
{
    internal static void Run(string path)
    {
        var am = new AssetsManager();
        var bun = am.LoadBundleFile(path, true);
        int n = bun.file.BlockAndDirInfo.DirectoryInfos.Count;
        Console.WriteLine("== " + path + " dirs=" + n);
        for (int d = 0; d < n; d++)
        {
            var di = bun.file.BlockAndDirInfo.DirectoryInfos[d];
            Console.WriteLine("  dir " + d + " " + di.Name + " size=" + di.DecompressedSize);
        }
        var inst = am.LoadAssetsFileFromBundle(bun, 0, false);
        var f = inst.file;
        Console.WriteLine("  unity=" + f.Metadata.UnityVersion + " target=" + f.Metadata.TargetPlatform + " typetree=" + f.Metadata.TypeTreeEnabled + " externals=" + f.Metadata.Externals.Count);
        foreach (var ex in f.Metadata.Externals) Console.WriteLine("    external " + ex.PathName);
        var counts = new SortedDictionary<string, int>();
        foreach (var info in f.AssetInfos)
        {
            string t = ((AssetClassID)info.TypeId).ToString();
            counts[t] = counts.TryGetValue(t, out int c) ? c + 1 : 1;
        }
        foreach (var kv in counts) Console.WriteLine("  " + kv.Key + " x" + kv.Value);
        foreach (var info in f.AssetInfos)
        {
            var id = (AssetClassID)info.TypeId;
            if (id != AssetClassID.Texture2D && id != AssetClassID.Shader && id != AssetClassID.Material && id != AssetClassID.Mesh && id != AssetClassID.Font) continue;
            var bf = am.GetBaseField(inst, info);
            string name = bf["m_Name"].IsDummy ? "" : bf["m_Name"].AsString;
            string extra = "";
            if (id == AssetClassID.Texture2D) extra = bf["m_Width"].AsInt + "x" + bf["m_Height"].AsInt + " fmt=" + bf["m_TextureFormat"].AsInt + " mips=" + bf["m_MipCount"].AsInt + " bytes=" + bf["m_CompleteImageSize"].AsUInt;
            if (id == AssetClassID.Shader) { var pf = bf["m_ParsedForm"]; extra = pf.IsDummy ? "" : pf["m_Name"].AsString; }
            if (id == AssetClassID.Material)
            {
                var sh = bf["m_Shader"];
                long pid = sh["m_PathID"].AsLong; int fid = sh["m_FileID"].AsInt;
                extra = "shader=(" + fid + "," + pid + ")";
                if (fid == 0) { var si = f.GetAssetInfo(pid); if (si != null) { var sbf = am.GetBaseField(inst, si); extra += " " + sbf["m_ParsedForm"]["m_Name"].AsString; } }
                var kw = bf["m_ShaderKeywords"]; if (!kw.IsDummy) extra += " kw=[" + kw.AsString + "]";
            }
            if (id == AssetClassID.Mesh) extra = "verts=" + bf["m_VertexData"]["m_VertexCount"].AsUInt + " submeshes=" + bf["m_SubMeshes"]["Array"].Children.Count + " idxfmt=" + bf["m_IndexFormat"].AsInt + " compr=" + bf["m_MeshCompression"].AsInt + " readable=" + bf["m_IsReadable"].AsBool + " stream=" + (bf["m_StreamData"]["path"].IsDummy ? "" : bf["m_StreamData"]["path"].AsString);
            Console.WriteLine("    " + id + " #" + info.PathId + " '" + name + "' " + extra);
        }
    }
}
