using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

static class Resources
{
    internal static Dictionary<string, byte[]> FromDll(string path)
    {
        var all = new Dictionary<string, byte[]>();
        using var fs = File.OpenRead(path);
        using var pe = new PEReader(fs);
        var md = pe.GetMetadataReader();
        var dir = pe.PEHeaders.CorHeader.ResourcesDirectory;
        if (!pe.PEHeaders.TryGetDirectoryOffset(dir, out int start)) throw new InvalidDataException(path + ": no resources");
        foreach (var h in md.ManifestResources)
        {
            var r = md.GetManifestResource(h);
            if (!r.Implementation.IsNil) continue;
            fs.Position = start + r.Offset;
            byte[] len = new byte[4];
            fs.ReadExactly(len);
            byte[] data = new byte[BitConverter.ToInt32(len, 0)];
            fs.ReadExactly(data);
            all[md.GetString(r.Name)] = data;
        }
        return all;
    }
}
