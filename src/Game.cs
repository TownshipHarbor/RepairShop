using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Blacksmithing;
using Il2CppAlta.Customization;
using Il2CppAlta.Impact;
using Il2CppAlta.Intelligence;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;
using Il2CppFeatures;
using Il2CppInterop.Runtime;
using Object = UnityEngine.Object;

namespace RepairShopQuest
{
    internal static class Game
    {
        internal static PlayerCustomizationHandler GhostHandler(PlayerLikeSpectralGhost g)
        {
            if (g == null) return null;
            return g.handler;
        }

        internal static void DisableAgent(PlayerLikeSpectralGhost g)
        {
            if (g == null) return;
            g.isEnabled = false;
        }

        internal static void SetEntityHash(NetworkEntity e, int hash)
        {
            e.hash = hash;
        }

        internal static void SetBehaviourEntity(NetworkEntityBehaviour b, NetworkEntity e)
        {
            b.entity = e;
        }

        internal static void SetBehaviours(NetworkEntity e)
        {
            List<MonoBehaviour> found = new List<MonoBehaviour>();
            foreach (MonoBehaviour m in Plat.Here<MonoBehaviour>(e))
            {
                if (m.TryCast<INetworkEntityBehaviour>() != null) found.Add(m);
            }
            e.networkEntityBehaviours = Plat.RefArray(found);
            e.hasNetworkBehaviours = found.Count > 0;
        }

        internal static bool HasBehaviour(NetworkEntity e)
        {
            foreach (MonoBehaviour m in Plat.Here<MonoBehaviour>(e))
            {
                if (m.TryCast<INetworkEntityBehaviour>() != null) return true;
            }
            return false;
        }

        internal static void EntityAttributes(NetworkEntity e)
        {
            NetworkPrefab p = Plat.Get<NetworkPrefab>(e);
            Pickup pickup = Plat.Get<Pickup>(e);
            Interactable inter = Plat.Get<Interactable>(e);
            ImpactTool tool = Plat.Get<ImpactTool>(e);
            if (p != null) e.prefab = p;
            e.hasNetworkBehaviours = HasBehaviour(e);
            if (pickup != null) e.commonPickup = pickup;
            if (inter != null) e.commonInteractable = inter;
            if (tool != null) e.commonImpactTool = tool;
            SetBehaviours(e);
        }

        internal static void PrefabAttributes(NetworkPrefab p)
        {
            NetworkEntity e = Plat.Get<NetworkEntity>(p);
            if (e == null) return;
            p.entity = e;
        }

        internal static void DockAttributes(PickupDock d)
        {
            NearbyPlayerScaler s = Plat.Get<NearbyPlayerScaler>(d);
            if (d.snapPoint == null) d.snapPoint = d.transform;
            if (s != null) d.scaler = s;
        }

        internal static void VisualAttributes(DockVisual v)
        {
            PickupDock d = Plat.InParent<PickupDock>(v);
            NearbyPlayerScaler s = Plat.InParent<NearbyPlayerScaler>(v);
            if (d != null) v.dock = d;
            if (s != null) v.nearbyScaler = s;
        }

        internal static void RigidbodyAttributes(NetworkRigidbody r)
        {
            r.cachedTransform = r.transform;
        }

        internal static PickupDock FirstOreDock(Smelter s)
        {
            var docks = s.oreDocks;
            return docks == null || docks.Length == 0 ? null : docks[0];
        }

        internal static void CopyDock(PickupDock from, PickupDock to)
        {
            to.settings = from.settings;
            to.snapPoint = from.snapPoint;
            to.pickupRange = from.pickupRange;
            to.dockVisual = from.dockVisual;
            to.hasContentsRange = from.hasContentsRange;
            to.dockedType = from.dockedType;
            to.quantityStored = from.quantityStored;
            to.scaler = from.scaler;
            to.overrideInteractionPriorityModifier = from.overrideInteractionPriorityModifier;
            to.multipleAllowance = from.multipleAllowance;
            to.multipleNotAllowedAction = from.multipleNotAllowedAction;
            to.channels = from.channels;
            to.isNetworked = from.isNetworked;
            to.entity = from.entity;
        }

        internal static readonly string[] DockFields =
        {
            "settings", "snapPoint", "pickupRange", "dockVisual", "hasContentsRange", "dockedType", "quantityStored", "scaler",
            "overrideInteractionPriorityModifier", "multipleAllowance", "multipleNotAllowedAction", "channels", "isNetworked", "entity"
        };

        internal static void SetVisualDock(DockVisual v, PickupDock d)
        {
            v.dock = d;
        }

        internal static void SetContainer(PickupContainer c, PickupDock d)
        {
            c.dock = d;
            c.pickup = null;
            c.startActive = true;
            c.isBreakable = false;
        }

        internal static void ShopSettings(PickupDockSettings s, bool coins)
        {
            s.maxContainedStacks = coins ? 100 : 1;
            s.isMultiStackingAllowed = false;
            s.isStackingDisabled = !coins;
            s.isStackingEnforced = false;
            s.isAllowingCraftedItems = !coins;
            s.isAllowingSubDocking = false;
            s.minimumCraftingSize = -1;
            s.maximumCraftingSize = -1;
            s.isHandDockingAllowed = true;
            s.isGrabbable = true;
            s.allowContainerInteraction = coins;
            s.isAllowingGlowingItems = true;
            s.allowsCrafting = false;
        }

        internal static void PaySettings(PickupDockSettings s)
        {
            s.isGrabbable = false;
            s.isHandDockingAllowed = true;
            s.allowContainerInteraction = true;
            s.isAllowingCraftedItems = false;
            s.isAllowingSubDocking = false;
        }

        internal static void SetDockSettings(PickupDock d, PickupDockSettings s)
        {
            d.settings = s;
        }

        internal static void ClearItems(PickupDockSettings s)
        {
            s.IncludedTags.Clear();
            s.ExcludedTags.Clear();
            s.IncludedItems.Clear();
        }

        internal static void Ranges(PickupDock d, SphereCollider pickup, SphereCollider contents)
        {
            d.pickupRange = pickup;
            var list = d.hasContentsRange;
            if (list == null) throw new InvalidOperationException("the donor dock has no contents range list");
            list.Clear();
            list.Add(contents);
        }

        internal static Collider PickupRange(PickupDock d)
        {
            return d.pickupRange;
        }

        internal static List<Collider> ContentsRange(PickupDock d)
        {
            List<Collider> list = new List<Collider>();
            var have = d.hasContentsRange;
            if (have != null) for (int i = 0; i < have.Count; i++) list.Add(have[i]);
            return list;
        }

        internal static void SetChannel(PickupDock d)
        {
            d.channels = (InteractionChannel)1;
        }

        internal static void NoClimb(GameObject g)
        {
            NonClimbable n = Plat.Get<NonClimbable>(g) ?? Plat.Add<NonClimbable>(g);
            n.climbFailBehaviour = (NonClimbable.ClimbFailBehaviour)0;
        }

        internal static PickupDock[] BoxDocks(RepairBox b, string prefix)
        {
            if (prefix == "dock") return new[] { b.dock1, b.dock2, b.dock3 };
            return new[] { b.cost1, b.cost2, b.cost3 };
        }

        internal static void PaymentBox(RepairBox b, Item coin)
        {
            b.item1 = coin;
            b.itemCount1 = 1u;
            b.item1UsePhysMat = false;
            b.item2 = null;
            b.item3 = null;
            b.itemCount2 = 0u;
            b.itemCount3 = 0u;
            b.isDone = false;
            b.staticHash = 0u;
            b.canBeUndone = false;
            b.isRepeatable = true;
            b.spawning = null;
            b.destroying = null;
            b.boxAnimation = null;
            b.repairBoxVfxController = null;
        }

        internal static void DisableExtras(GameObject g)
        {
            foreach (LODGroup l in Plat.All<LODGroup>(g.transform, true)) l.enabled = false;
            foreach (Animator a in Plat.All<Animator>(g.transform, true)) a.enabled = false;
            foreach (Light l in Plat.All<Light>(g.transform, true)) l.enabled = false;
            foreach (AudioSource a in Plat.All<AudioSource>(g.transform, true)) a.enabled = false;
            foreach (AutomaticDockArea a in Plat.All<AutomaticDockArea>(g.transform, true)) a.enabled = false;
            foreach (RepairBoxVfxController v in Plat.All<RepairBoxVfxController>(g.transform, true)) v.enabled = false;
            foreach (Rigidbody r in Plat.All<Rigidbody>(g.transform, true))
            {
                r.isKinematic = true;
                r.useGravity = false;
            }
        }

        internal static List<NetworkEntity> Embedded(NetworkPrefab p)
        {
            List<NetworkEntity> list = new List<NetworkEntity>();
            var e = p.Embedded;
            for (int i = 0; i < e.Count; i++) list.Add(e[i]);
            return list;
        }

        internal static void Embed(NetworkPrefab p, NetworkEntity e)
        {
            p.Embedded.Add(e);
        }

        internal static Item CoinItem()
        {
            Pickup p = Plat.Get<Pickup>(CommonPrefabs.CoinPrefab);
            return p == null ? null : p.Item;
        }

        internal static List<NetworkPrefab> AllPrefabs()
        {
            List<NetworkPrefab> list = new List<NetworkPrefab>();
            var map = Plat.PrefabMap;
            var e = map.Values.GetEnumerator();
            while (e.MoveNext()) list.Add(e.Current);
            return list;
        }
    }
}
