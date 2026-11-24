using System;
using System.IO;
using System.Security.Cryptography;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "inventory")
        {
            for (int i = 1; i < args.Length; i++) Inventory.Run(args[i]);
            return 0;
        }
        if (args.Length == 2 && args[0] == "probe2")
        {
            Probe2.Run(args[1]);
            return 0;
        }
        if (args.Length == 2 && args[0] == "probe")
        {
            Probe.Run(args[1]);
            return 0;
        }
        if (args.Length == 4 && args[0] == "pack")
        {
            var res = Resources.FromDll(args[1]);
            string dir = Path.Combine(Path.GetTempPath(), "repairshop-bake-" + Environment.ProcessId);
            Directory.CreateDirectory(dir);
            foreach (var kv in res) File.WriteAllBytes(Path.Combine(dir, kv.Key), kv.Value);
            int cap = int.Parse(args[3]);
            var scene = new Scene(cap);
            var map = new Bundle(Path.Combine(dir, "RepairShop.Map"), "map");
            scene.AddRoots(map, "ATT_RepairShop_Standalone");
            var ui = new Bundle(Path.Combine(dir, "RepairShop.UI"), "ui");
            scene.AddRoots(ui, "RepairPaymentBox", "RepairReceipt");
            var props = new Bundle(Path.Combine(dir, "RepairShop.WorkerProps"), "props");
            foreach (var info in props.All(AssetsTools.NET.Extra.AssetClassID.GameObject))
            {
                var go = props.Am.GetBaseField(props.Inst, info);
                var tr = go["m_Component"]["Array"][0]["component"];
                var t = props.Field(Bundle.Id(tr));
                if (t != null && Bundle.Id(t["m_Father"]) == 0) scene.AddRoots(props, go["m_Name"].AsString);
            }
            scene.AddBlobs(props);
            foreach (var (bn, bd) in scene.Blobs) File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[2])), bn + ".json"), bd);
            byte[] pack = scene.Write();
            File.WriteAllBytes(args[2], pack);
            File.WriteAllText(Path.ChangeExtension(args[2], ".txt"), scene.Describe());
            Console.WriteLine("pack " + args[2] + " " + pack.Length + " bytes sha256 " + Convert.ToHexString(SHA256.HashData(pack)).Substring(0, 8).ToLower());
            return 0;
        }
        Console.Error.WriteLine("usage: bake inventory <bundle>... | bake pack <caleb client RepairShop.dll> <out.pack> <texture cap>");
        return 2;
    }
}
