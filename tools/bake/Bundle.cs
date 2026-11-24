using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

sealed class Bundle
{
    internal readonly AssetsManager Am = new AssetsManager();
    internal BundleFileInstance Bun;
    internal AssetsFileInstance Inst;
    internal string Tag;
    readonly Dictionary<string, byte[]> _res = new Dictionary<string, byte[]>();

    internal Bundle(string path, string tag)
    {
        Tag = tag;
        Bun = Am.LoadBundleFile(path, true);
        Inst = Am.LoadAssetsFileFromBundle(Bun, 0, false);
        if (Inst.file.Metadata.TargetPlatform != 19) throw new InvalidDataException(path + ": target " + Inst.file.Metadata.TargetPlatform + ", expected 19");
        var dirs = Bun.file.BlockAndDirInfo.DirectoryInfos;
        for (int i = 0; i < dirs.Count; i++)
        {
            if (!dirs[i].Name.EndsWith(".resS")) continue;
            var r = Bun.file.DataReader;
            r.Position = dirs[i].Offset;
            _res[dirs[i].Name] = r.ReadBytes((int)dirs[i].DecompressedSize);
        }
    }

    internal IEnumerable<AssetFileInfo> All(AssetClassID id)
    {
        foreach (var info in Inst.file.AssetInfos) if (info.TypeId == (int)id) yield return info;
    }

    internal AssetTypeValueField Field(long pathId)
    {
        var info = Inst.file.GetAssetInfo(pathId);
        if (info == null) return null;
        return Am.GetBaseField(Inst, info);
    }

    internal AssetFileInfo Info(long pathId)
    {
        return Inst.file.GetAssetInfo(pathId);
    }

    internal AssetTypeValueField Ref(AssetTypeValueField pptr)
    {
        if (pptr == null || pptr.IsDummy) return null;
        if (pptr["m_FileID"].AsInt != 0) return null;
        long id = pptr["m_PathID"].AsLong;
        return id == 0 ? null : Field(id);
    }

    internal static long Id(AssetTypeValueField pptr)
    {
        if (pptr == null || pptr.IsDummy) return 0;
        return pptr["m_PathID"].AsLong;
    }

    internal static int FileId(AssetTypeValueField pptr)
    {
        if (pptr == null || pptr.IsDummy) return 0;
        return pptr["m_FileID"].AsInt;
    }

    internal byte[] Stream(AssetTypeValueField sd, AssetTypeValueField inline)
    {
        string path = sd.IsDummy ? "" : sd["path"].AsString;
        if (string.IsNullOrEmpty(path)) return inline.AsByteArray;
        string name = path.Substring(path.LastIndexOf('/') + 1);
        if (!_res.TryGetValue(name, out byte[] all)) throw new InvalidDataException("missing resource " + name);
        long off = (long)sd["offset"].AsULong;
        int size = (int)sd["size"].AsUInt;
        byte[] b = new byte[size];
        Buffer.BlockCopy(all, (int)off, b, 0, size);
        return b;
    }
}
