using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Crafting;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;

namespace RepairShopQuest
{
    internal sealed class ServiceJob
    {
        public string owner;
        public string state;
        public int slot;
    }

    internal sealed class ServiceJobs
    {
        public List<ServiceJob> jobs = new List<ServiceJob>();
    }

    internal sealed class RepairService
    {
        internal NetworkEntity Entity;
        internal Transform Shop, KeeperAnchor, BlacksmithAnchor, AnvilAnchor, WoodWorkAnchor, WoodWorkerAnchor, HeatWorkAnchor;
        internal PickupDock[] Storage = new PickupDock[0];
        internal readonly ServiceJobs State = new ServiceJobs();
        internal bool Restoring { get { return false; } }
        internal bool Faulted { get { return false; } }
    }

    internal static class RepairUiStereo
    {
        internal static void Configure(GameObject root)
        {
        }

        internal static void RegisterEffect(Material material)
        {
        }

        internal static void UnregisterEffect(Material material)
        {
        }
    }

    internal static class RepairItems
    {
        static readonly HashSet<uint> LooseHeads = new HashSet<uint>
        {
            35878u, 43146u, 15476u, 34412u, 17400u, 10056u, 9918u, 50322u, 63886u, 56124u,
            11332u, 26788u, 55310u, 25802u, 36438u, 9704u, 49402u, 56720u, 54482u, 22876u,
            13554u, 13680u, 13816u, 17174u, 15124u, 53124u, 48902u
        };

        internal static Pickup Root(Pickup pickup)
        {
            CraftingPart part = pickup == null ? null : pickup.CraftingPart;
            int n = 0;
            while (part != null && part.ConnectedTo != null && n++ < 128) part = part.ConnectedTo;
            return part == null ? pickup : part.Pickup;
        }

        internal static bool IsLooseHead(Pickup pickup)
        {
            return pickup != null && pickup.DurabilityModule != null && pickup.Entity != null && pickup.Entity.Prefab != null && LooseHeads.Contains(pickup.Entity.Prefab.Hash);
        }
    }
}
