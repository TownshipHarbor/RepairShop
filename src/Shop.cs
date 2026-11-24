using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Blacksmithing;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;
using Il2CppATT.Saving;
using Object = UnityEngine.Object;

namespace RepairShopQuest
{
    internal static class Shop
    {
        internal const string Id = "ATT_RepairShop_Standalone";
        internal const uint RepairBoxHash = 3458;
        internal static uint Hash;
        internal static bool Done;
        internal static int EmbeddedCount, StorageCount;
        static NetworkPrefab _prefab;
        static GameObject _holder;

        internal static bool Register(uint hash)
        {
            if (Done) Unregister();
            DateTime start = DateTime.UtcNow;
            try
            {
                Pack.Read();
                PrefabManager.PrepareSpawnSetups();
                var map = Plat.PrefabMap;
                if (map == null || map.Count == 0)
                {
                    Core.Error("the prefab map is not loaded; nothing registered.");
                    return false;
                }
                if (map.ContainsKey(hash))
                {
                    Core.Error("repair shop number " + hash + " is already a prefab here; nothing registered.");
                    return false;
                }
                _holder = new GameObject("RepairShop templates");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
                _prefab = Build(hash);
                map.Add(hash, _prefab);
                NetworkPrefab back = PrefabManager.GetPrefab(hash);
                if (back == null || !Plat.Same(back, _prefab)) throw new InvalidOperationException("the prefab map did not take the repair shop");
                Hash = hash;
                Done = true;
                RepairWorkerProps.Load();
                Core.Log("REPAIR_SHOP_REGISTERED hash=" + hash + " embedded=" + EmbeddedCount + " storage=" + StorageCount + " in " + (int)(DateTime.UtcNow - start).TotalMilliseconds + " ms");
                return true;
            }
            catch (Exception e)
            {
                Core.Error("registration failed: " + e);
                Unregister();
                return false;
            }
        }

        static bool Keep(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                string n = t.name;
                if (n == "Dock" || n == "DropoffAnchor" || n == "PaymentAnchor" || n == "CoinDockAnchor" || n == "Native repair payment storage" || n.StartsWith("Repair button ", StringComparison.Ordinal) || n.StartsWith("RepairCollection", StringComparison.Ordinal)) return true;
                if (Plat.Get<NetworkEntity>(t) != null && n != Id && n != "Repair Shop") return true;
            }
            return false;
        }

        internal static bool Is(uint hash)
        {
            return Done && hash != 0 && hash == Hash;
        }

        internal static void Unregister()
        {
            var map = Plat.PrefabMap;
            if (_prefab != null && map != null && map.ContainsKey(Hash) && Plat.Same(map[Hash], _prefab)) map.Remove(Hash);
            _prefab = null;
            if (_holder != null) Object.Destroy(_holder);
            _holder = null;
            if (Done) Core.Log("repair shop removed");
            Done = false;
            Hash = 0;
        }

        static NetworkPrefab Build(uint hash)
        {
            GameObject root = Pack.Build(Id, _holder.transform);
            root.name = Id;
            root.SetActive(true);
            int terrain = LayerMask.NameToLayer("Terrain"), ground = LayerMask.NameToLayer("Ground"), wall = LayerMask.NameToLayer("Wall"), overlay = LayerMask.NameToLayer("GroundOverlay"), wallOnly = LayerMask.NameToLayer("TerrainWallOnly");
            if (terrain != 8 || ground != 10 || wall != 13 || overlay != 11 || wallOnly != 19) throw new InvalidOperationException("unexpected collision layer layout");
            List<Collider> colliders = Plat.All<Collider>(root.transform, true);
            if (colliders.Count == 0) throw new InvalidOperationException("the shop has no collision geometry");
            foreach (Collider c in colliders)
            {
                c.isTrigger = false;
                c.enabled = true;
                int layer = c.gameObject.layer;
                if (layer != terrain && layer != ground && layer != wall && layer != overlay && layer != wallOnly) c.gameObject.layer = ground;
                MeshCollider mc = Plat.As<MeshCollider>(c);
                if (mc != null)
                {
                    if (mc.sharedMesh == null) throw new InvalidOperationException("missing collision mesh");
                    mc.convex = false;
                }
            }
            Install(root);
            Pack.Merge(root, Keep);
            NetworkPrefab np = Plat.Get<NetworkPrefab>(root);
            NetworkEntity entity = Plat.Get<NetworkEntity>(root);
            Game.PrefabAttributes(np);
            Game.EntityAttributes(entity);
            Plat.SetPrefabHash(np, hash);
            if (np.Hash != hash) throw new InvalidOperationException("the shop's prefab number did not take");
            np.Initialize();
            entity.OnValidate();
            return np;
        }

        static void Install(GameObject root)
        {
            Transform shop = null;
            foreach (Transform t in Plat.All<Transform>(root.transform, true))
            {
                if (t.name == "Repair Shop") { shop = t; break; }
            }
            if (shop == null) throw new InvalidOperationException("the shop has no Repair Shop node");
            Smelter smelter = null;
            NetworkPrefab smelterPrefab = null;
            List<NetworkPrefab> all = Game.AllPrefabs();
            foreach (NetworkPrefab p in all)
            {
                if (p == null) continue;
                smelter = Plat.Get<Smelter>(p);
                if (smelter != null) { smelterPrefab = p; break; }
            }
            if (smelter == null) throw new InvalidOperationException("native dock donor unavailable");
            PickupDock donor = Game.FirstOreDock(smelter);
            if (donor == null) throw new InvalidOperationException("the smelter donor has no ore dock");
            Core.Log("dock donor: " + smelterPrefab.name + " " + smelterPrefab.Hash + " / " + donor.name);
            List<Item> allowed = new List<Item>();
            HashSet<long> seen = new HashSet<long>();
            foreach (NetworkPrefab p in all)
            {
                Pickup pk = p == null ? null : Plat.Get<Pickup>(p);
                if (pk == null || pk.Item == null) continue;
                if (seen.Add(Plat.Key(pk.Item))) allowed.Add(pk.Item);
            }
            NetworkEntity re = Plat.Get<NetworkEntity>(root) ?? Plat.Add<NetworkEntity>(root);
            NetworkPrefab np = Plat.Get<NetworkPrefab>(root) ?? Plat.Add<NetworkPrefab>(root);
            np.SaveMode = (SaveMode)0;
            Plat.Add<NetworkEntity>(shop.gameObject);
            Layout layout = new Layout { Shop = shop };
            string[] anchors = { "KeeperCloneAnchor", "BlacksmithCloneAnchor", "AnvilWorkAnchor" };
            foreach (string a in anchors) if (shop.Find(a) == null) throw new InvalidOperationException("worker anchor " + a + " missing");
            List<Transform> racks = new List<Transform>();
            for (int i = 0; i < shop.childCount; i++)
            {
                Transform c = shop.GetChild(i);
                if (c.name.StartsWith("ATT collection shelf", StringComparison.Ordinal)) racks.Add(c);
            }
            if (racks.Count == 0) throw new InvalidOperationException("repair shelf missing");
            List<Transform> docks = new List<Transform>();
            foreach (Transform r in racks)
                foreach (Transform t in Plat.All<Transform>(r, true))
                    if (t.name == "Dock") docks.Add(t);
            if (docks.Count < 20 || docks.Count > 128) throw new InvalidOperationException("unexpected shelf dock count " + docks.Count);
            NoClimb(racks, docks);
            layout.Storage = new PickupDock[docks.Count];
            for (int j = 0; j < docks.Count; j++)
            {
                foreach (Renderer r in Plat.All<Renderer>(docks[j], true)) r.enabled = false;
                foreach (Collider c in Plat.All<Collider>(docks[j], true)) c.enabled = false;
                layout.Storage[j] = Dock(donor, docks[j], "Repair storage " + j, false, allowed);
            }
            StorageCount = docks.Count;
            Transform drop = shop.Find("DropoffAnchor"), pay = shop.Find("PaymentAnchor");
            if (drop == null || pay == null) throw new InvalidOperationException("repair counter anchors missing");
            layout.Intake = Dock(donor, drop, "Repair intake", false, allowed);
            PickupDock coins = Dock(donor, pay, "Repair coins", true, allowed);
            Transform box = shop.Find("Repair payment box");
            if (box == null) throw new InvalidOperationException("the authored payment box is missing");
            Transform coinAnchor = box.Find("CoinDockAnchor");
            if (coinAnchor == null) throw new InvalidOperationException("the payment box has no coin anchor");
            coins.transform.position = coinAnchor.position;
            Transform c0 = shop.Find("RepairCollection0"), c1 = shop.Find("RepairCollection1");
            if (c0 == null || c1 == null) throw new InvalidOperationException("authored repair counter anchors missing");
            layout.Collection = new[] { Dock(donor, c0, "Repair collection 0", false, allowed), Dock(donor, c1, "Repair collection 1", false, allowed) };
            Attributes(root);
            List<NetworkEntity> before = new List<NetworkEntity>();
            foreach (NetworkEntity e in Plat.All<NetworkEntity>(root.transform, true)) if (!Plat.Same(e, re)) before.Add(e);
            NetworkEntity coinEntity = Plat.Get<NetworkEntity>(coins);
            PickupDock nativeCoins = PaymentStorage(shop, box);
            int num = 1;
            foreach (NetworkEntity e in before)
            {
                NetworkEntity use = Plat.Same(e, coinEntity) ? Plat.Get<NetworkEntity>(nativeCoins) : e;
                Game.SetEntityHash(use, num++);
                Game.Embed(np, use);
            }
            Plat.Kill(coins.gameObject);
            List<NetworkEntity> embedded = Game.Embedded(np);
            HashSet<long> have = new HashSet<long>();
            foreach (NetworkEntity e in embedded) have.Add(Plat.Key(e));
            foreach (NetworkEntity e in Plat.All<NetworkEntity>(root.transform, true))
            {
                if (Plat.Same(e, re) || have.Contains(Plat.Key(e))) continue;
                Game.SetEntityHash(e, num++);
                Game.Embed(np, e);
                have.Add(Plat.Key(e));
            }
            foreach (NetworkEntityBehaviour b in Plat.All<NetworkEntityBehaviour>(root.transform, true)) Game.SetBehaviourEntity(b, Plat.Get<NetworkEntity>(b));
            foreach (NetworkEntity e in Plat.All<NetworkEntity>(root.transform, true)) Game.SetBehaviours(e);
            EmbeddedCount = Game.Embedded(np).Count;
            NetworkEntity shopEntity = Plat.Get<NetworkEntity>(shop);
            if (shopEntity == null) throw new InvalidOperationException("repair native entity binding failed");
            foreach (PickupDock d in layout.All(nativeCoins))
            {
                NetworkEntity de = Plat.Get<NetworkEntity>(d);
                if (de == null || !have.Contains(Plat.Key(de))) throw new InvalidOperationException("repair native entity binding failed: " + d.name);
            }
        }

        static void NoClimb(List<Transform> racks, List<Transform> docks)
        {
            HashSet<long> done = new HashSet<long>();
            foreach (Transform r in racks)
            {
                foreach (Collider c in Plat.All<Collider>(r, true))
                {
                    if (c == null || c.isTrigger || !done.Add(Plat.Key(c.gameObject))) continue;
                    bool inDock = false;
                    foreach (Transform d in docks)
                    {
                        if (d != null && (Plat.Same(c.transform, d) || c.transform.IsChildOf(d))) { inDock = true; break; }
                    }
                    if (!inDock) Game.NoClimb(c.gameObject);
                }
            }
        }

        static void Attributes(GameObject root)
        {
            int pickups = 0;
            foreach (NetworkPrefab p in Plat.All<NetworkPrefab>(root.transform, true)) Game.PrefabAttributes(p);
            foreach (NetworkEntity e in Plat.All<NetworkEntity>(root.transform, true)) Game.EntityAttributes(e);
            foreach (NetworkEntityBehaviour b in Plat.All<NetworkEntityBehaviour>(root.transform, true))
            {
                NetworkEntity e = Plat.Get<NetworkEntity>(b);
                if (e != null) Game.SetBehaviourEntity(b, e);
            }
            foreach (NetworkRigidbody r in Plat.All<NetworkRigidbody>(root.transform, true)) Game.RigidbodyAttributes(r);
            foreach (DockVisual v in Plat.All<DockVisual>(root.transform, true)) Game.VisualAttributes(v);
            foreach (PickupDock d in Plat.All<PickupDock>(root.transform, true)) Game.DockAttributes(d);
            foreach (Pickup p in Plat.All<Pickup>(root.transform, true)) pickups++;
            if (pickups > 0) throw new InvalidOperationException("the shop template holds " + pickups + " pickups; their attribute pass is not ported");
        }

        static PickupDock Dock(PickupDock donor, Transform anchor, string name, bool coins, List<Item> allowed)
        {
            GameObject g = Plat.Copy(donor.gameObject, anchor);
            g.name = name;
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
            g.transform.localScale = Vector3.one;
            g.SetActive(true);
            Vector3 s = anchor.lossyScale;
            g.transform.localScale = new Vector3(1f / Mathf.Max(0.001f, Mathf.Abs(s.x)), 1f / Mathf.Max(0.001f, Mathf.Abs(s.y)), 1f / Mathf.Max(0.001f, Mathf.Abs(s.z)));
            foreach (NetworkPrefab p in Plat.All<NetworkPrefab>(g.transform, true)) Plat.Kill(p);
            PickupDock old = Plat.Get<PickupDock>(g);
            if (old == null) throw new InvalidOperationException("the donor copy has no PickupDock");
            PickupDock dock = Plat.Add<PickupDock>(g);
            Game.CopyDock(old, dock);
            foreach (DockVisual v in Plat.All<DockVisual>(g.transform, true)) Game.SetVisualDock(v, dock);
            PickupContainer pc = Plat.Get<PickupContainer>(g);
            if (pc == null) throw new InvalidOperationException("native dock withdrawal container missing");
            Game.SetContainer(pc, dock);
            Plat.Kill(old);
            PickupDockSettings settings = Plat.CopyAsset(donor.Settings);
            Game.ClearItems(settings);
            if (coins) settings.IncludedItems.Add(Game.CoinItem());
            else foreach (Item i in allowed) settings.IncludedItems.Add(i);
            Game.ShopSettings(settings, coins);
            Game.SetDockSettings(dock, settings);
            foreach (Collider c in Plat.All<Collider>(g.transform, true))
            {
                c.isTrigger = true;
                c.enabled = false;
            }
            SphereCollider a = Plat.Add<SphereCollider>(g);
            a.radius = 0.17f;
            a.isTrigger = true;
            SphereCollider b = Plat.Add<SphereCollider>(g);
            b.radius = 0.17f;
            b.isTrigger = true;
            b.enabled = dock.QuantityStored > 0;
            Game.Ranges(dock, a, b);
            g.layer = donor.gameObject.layer;
            Game.SetChannel(dock);
            return dock;
        }

        static PickupDock PaymentStorage(Transform shop, Transform box)
        {
            NetworkPrefab native = PrefabManager.GetPrefab(RepairBoxHash);
            if (native == null || Plat.Get<RepairBox>(native) == null) throw new InvalidOperationException("native Repair Box #3458 missing");
            if (box.Find("Dock0") == null && box.Find("CoinDockAnchor") == null) throw new InvalidOperationException("payment anchor missing");
            bool liner = false;
            foreach (MeshFilter m in Plat.All<MeshFilter>(box, true)) if (m.name == "Woven bay liner 1" && m.sharedMesh != null) liner = true;
            if (!liner) throw new InvalidOperationException("payment tray liner missing; cannot align coin capture");
            GameObject g = Plat.Copy(native.gameObject, shop);
            g.name = "Native repair payment storage";
            g.SetActive(true);
            foreach (NetworkPrefab p in Plat.All<NetworkPrefab>(g.transform, true)) Plat.Kill(p);
            RepairBox rb = Plat.Get<RepairBox>(g);
            PickupDock[] inputs = Game.BoxDocks(rb, "dock");
            PickupDock[] costs = Game.BoxDocks(rb, "cost");
            foreach (PickupDock d in inputs) if (d == null || !d.transform.IsChildOf(g.transform)) throw new InvalidOperationException("native payment references escaped clone");
            foreach (PickupDock d in costs) if (d == null || !d.transform.IsChildOf(g.transform)) throw new InvalidOperationException("native payment references escaped clone");
            Item coin = Game.CoinItem();
            Game.PaymentBox(rb, coin);
            foreach (PickupDock d in inputs)
            {
                PickupDockSettings s = Plat.CopyAsset(d.Settings);
                Game.ClearItems(s);
                s.IncludedItems.Add(coin);
                Game.PaySettings(s);
                Game.SetDockSettings(d, s);
            }
            Game.DisableExtras(g);
            foreach (Renderer r in Plat.All<Renderer>(g.transform, true)) r.enabled = false;
            foreach (Collider c in Plat.All<Collider>(g.transform, true)) c.enabled = false;
            Transform anchor = box.Find("Dock0") ?? box.Find("CoinDockAnchor");
            foreach (PickupDock d in inputs)
            {
                d.transform.position = anchor.position;
                d.transform.rotation = anchor.rotation;
            }
            return inputs[0];
        }

        sealed class Layout
        {
            internal Transform Shop;
            internal PickupDock Intake;
            internal PickupDock[] Storage, Collection;

            internal IEnumerable<PickupDock> All(PickupDock coins)
            {
                yield return Intake;
                yield return coins;
                foreach (PickupDock d in Storage) yield return d;
                foreach (PickupDock d in Collection) yield return d;
            }
        }
    }
}
