using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppTMPro;
using Il2Cpp;
using Il2CppAlta.Inventory;
using Il2CppAlta.Networking;
using Il2CppAlta.Networking.Scripts.Player;
using GameStream = Il2CppAlta.Serialization.Stream;
using StringEncoding = Il2CppAlta.Serialization.Stream.StringEncoding;

namespace RepairShopQuest
{
    internal sealed class ShopView
    {
        internal const int ViewMessage = 96;
        internal NetworkEntity Root, Service;
        internal string Name;
        TextMeshPro _quote;
        readonly Transform[] _caps = new Transform[3];
        Transform _anchor;
        readonly List<PickupDock> _inputs = new List<PickupDock>(), _costs = new List<PickupDock>();
        readonly List<Renderer> _hideRenderers = new List<Renderer>();
        readonly List<Collider> _hideColliders = new List<Collider>();
        readonly List<Collider> _bubbleVisuals = new List<Collider>();
        SerializeMethod _handler;
        bool _listening, _ready;
        string _pending, _shown;
        bool _hasPending, _gotDisplay;
        readonly object _lock = new object();
        int _pulse = -1;
        float _pulseUntil;
        internal string Owner, CoinOwner;
        internal readonly RepairService Ref = new RepairService();
        readonly SortedDictionary<int, PickupDock> _storage = new SortedDictionary<int, PickupDock>();
        internal readonly List<KeyValuePair<int, string>> Jobs = new List<KeyValuePair<int, string>>();

        internal ShopView(NetworkEntity root)
        {
            Root = root;
            Name = root.name;
        }

        internal bool Alive { get { return Root != null && !Root.IsBeingDestroyed; } }

        internal bool Tick()
        {
            if (!Alive) return false;
            if (!_ready)
            {
                if (!Root.IsInitialized) return true;
                Setup();
            }
            if (!_listening && Service != null && Service.IsInitialized && Service.MessageHandler != null)
            {
                _handler = Plat.Method(Receive);
                Plat.Listen(Service, (EntityMessageType)ViewMessage, _handler);
                _listening = true;
                Core.Log("listening for the counter board on " + Service.name);
            }
            string text = null;
            lock (_lock)
            {
                if (_hasPending)
                {
                    text = _pending;
                    _hasPending = false;
                }
            }
            if (text != null) Read(text);
            for (int i = 0; i < 3; i++)
            {
                if (_caps[i] == null) continue;
                _caps[i].localPosition = new Vector3(0f, 0f, _pulse == i && Time.unscaledTime < _pulseUntil ? 0.015f : 0f);
            }
            return true;
        }

        internal void LateTick()
        {
            if (!_ready || !Alive) return;
            foreach (Renderer r in _hideRenderers) if (r != null) r.enabled = false;
            foreach (Collider c in _hideColliders) if (c != null) c.enabled = false;
            if (_anchor != null)
            {
                foreach (PickupDock d in _inputs)
                {
                    if (d == null) continue;
                    d.transform.position = _anchor.position;
                    d.transform.rotation = _anchor.rotation;
                }
            }
            foreach (PickupDock d in _costs)
            {
                if (d == null) continue;
                foreach (Renderer r in Plat.All<Renderer>(d.transform, true)) r.enabled = false;
            }
            foreach (Collider c in _bubbleVisuals) if (c != null) c.enabled = false;
        }

        void Setup()
        {
            _ready = true;
            Transform shopNode = null;
            foreach (NetworkEntity e in Plat.All<NetworkEntity>(Root.transform, true))
            {
                string n = e.OriginalName ?? e.name;
                if (n == "Repair Shop") { Service = e; shopNode = e.transform; }
                else if (n == "Native repair payment storage")
                {
                    RepairBox rb = Plat.Get<RepairBox>(e);
                    if (rb != null)
                    {
                        _inputs.AddRange(Game.BoxDocks(rb, "dock"));
                        _costs.AddRange(Game.BoxDocks(rb, "cost"));
                    }
                    _hideRenderers.AddRange(Plat.All<Renderer>(e.transform, true));
                    _hideColliders.AddRange(Plat.All<Collider>(e.transform, true));
                }
                else if (n.StartsWith("Repair storage ", StringComparison.Ordinal) || n == "Repair intake" || n.StartsWith("Repair collection ", StringComparison.Ordinal))
                {
                    PickupDock d = Plat.Get<PickupDock>(e);
                    if (d == null) continue;
                    int slot;
                    if (n.StartsWith("Repair storage ", StringComparison.Ordinal) && int.TryParse(n.Substring(15), out slot)) _storage[slot] = d;
                    Collider keep = Game.PickupRange(d);
                    List<Collider> contents = Game.ContentsRange(d);
                    foreach (Collider c in Plat.All<Collider>(d.transform, true))
                    {
                        if (Plat.Same(c, keep)) continue;
                        bool skip = false;
                        foreach (Collider k in contents) if (Plat.Same(c, k)) skip = true;
                        if (!skip) _bubbleVisuals.Add(c);
                    }
                }
            }
            Ref.Entity = Service;
            Ref.Shop = shopNode;
            if (shopNode != null)
            {
                Ref.KeeperAnchor = shopNode.Find("KeeperCloneAnchor");
                Ref.BlacksmithAnchor = shopNode.Find("BlacksmithCloneAnchor");
                Ref.AnvilAnchor = shopNode.Find("AnvilWorkAnchor");
                Ref.WoodWorkAnchor = shopNode.Find("Wood Repair Area/WoodWorkAnchor");
                Ref.WoodWorkerAnchor = shopNode.Find("Wood Repair Area/WoodWorkerAnchor");
                Ref.HeatWorkAnchor = shopNode.Find("Forge Heating Area/HeatWorkAnchor");
            }
            Ref.Storage = new PickupDock[_storage.Count];
            int slotAt = 0;
            foreach (PickupDock d in _storage.Values) Ref.Storage[slotAt++] = d;
            if (shopNode != null)
            {
                Transform box = shopNode.Find("Repair payment box");
                if (box != null) _anchor = box.Find("Dock0") ?? box.Find("CoinDockAnchor");
                foreach (TextMeshPro t in Plat.All<TextMeshPro>(shopNode, true))
                {
                    if (t.name == "Repair quote") _quote = t;
                }
                foreach (Transform t in Plat.All<Transform>(shopNode, true))
                {
                    for (int i = 0; i < 3; i++) if (t.name == "Repair button " + i) _caps[i] = t.Find("Cap");
                }
            }
            Core.Log("REPAIR_SHOP_SEEN " + Name + ": service " + (Service != null) + ", quote " + (_quote != null) + ", payment docks " + _inputs.Count + ", bubble colliders " + _bubbleVisuals.Count);
            if (shopNode != null)
            {
                string at = "", local = "";
                foreach (string n in new[] { "DropoffAnchor", "PaymentAnchor", "RepairCollection0", "RepairCollection1", "KeeperCloneAnchor", "BlacksmithCloneAnchor", "AnvilWorkAnchor", "Repair payment box/CoinDockAnchor", "Forge Heating Area/HeatWorkAnchor", "Wood Repair Area/WoodWorkAnchor" })
                {
                    Transform t = shopNode.Find(n);
                    if (t == null) continue;
                    string key = n.Substring(n.LastIndexOf('/') + 1);
                    at += " " + key + "=" + V(t.position);
                    local += " " + key + "=" + V(Root.transform.InverseTransformPoint(t.position));
                }
                for (int i = 0; i < 3; i++)
                {
                    if (_caps[i] == null) continue;
                    at += " button" + i + "=" + V(_caps[i].position);
                    local += " button" + i + "=" + V(Root.transform.InverseTransformPoint(_caps[i].position));
                }
                Core.Log("REPAIR_SHOP_ANCHORS" + at);
                Core.Log("REPAIR_SHOP_LOCAL" + local);
            }
        }

        void Receive(IPlayer sender, GameStream s)
        {
            string text = "";
            s.SerializeString(ref text, (StringEncoding)1);
            if (!s.IsReading || text == null || text.Length >= 32768) return;
            lock (_lock)
            {
                _pending = text;
                _hasPending = true;
            }
        }

        void Read(string text)
        {
            Dictionary<string, object> d;
            try { d = Json.Parse(text) as Dictionary<string, object>; }
            catch (Exception e)
            {
                Core.Warn("repair view rejected: " + e.Message);
                return;
            }
            if (d == null) return;
            string display = Json.Text(d, "text") ?? "";
            if (!_gotDisplay && display.Length > 0)
            {
                _gotDisplay = true;
                Core.Log("REPAIR_DISPLAY_RECEIVED entity=" + (Service == null ? 0 : Service.Identifier));
            }
            Owner = Json.Text(d, "owner");
            CoinOwner = Json.Text(d, "coinOwner");
            Jobs.Clear();
            Ref.State.jobs.Clear();
            object jobs;
            if (d.TryGetValue("jobs", out jobs) && jobs is List<object>)
            {
                foreach (object j in (List<object>)jobs)
                {
                    Dictionary<string, object> jd = j as Dictionary<string, object>;
                    if (jd == null) continue;
                    Jobs.Add(new KeyValuePair<int, string>(Json.Int(jd, "slot", -1), Json.Text(jd, "owner") + "|" + Json.Text(jd, "state")));
                    Ref.State.jobs.Add(new ServiceJob { slot = Json.Int(jd, "slot", -1), owner = Json.Text(jd, "owner"), state = Json.Text(jd, "state") });
                }
            }
            int pulse = Json.Int(d, "pulse", -1);
            if (pulse >= 0)
            {
                _pulse = pulse;
                _pulseUntil = Time.unscaledTime + 0.3f;
            }
            if (_quote != null && display != _shown)
            {
                _shown = display;
                _quote.text = Wrap(display);
                Core.Log("counter board: " + display.Replace('\n', '|'));
            }
        }

        internal string QuoteText { get { return _quote == null ? null : _quote.text; } }

        static string V(Vector3 v)
        {
            return v.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "," + v.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "," + v.z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static string Wrap(string text)
        {
            List<string> lines = new List<string>();
            foreach (string para in (text ?? "").Split('\n'))
            {
                string line = "";
                foreach (string word in para.Split(' '))
                {
                    if (line.Length + word.Length + 1 > 27 && line.Length > 0)
                    {
                        lines.Add(line);
                        line = "";
                    }
                    line = line + (line.Length == 0 ? "" : " ") + word;
                }
                lines.Add(line);
            }
            if (lines.Count > 8) lines.RemoveRange(8, lines.Count - 8);
            return string.Join("\n", lines.ToArray());
        }
    }

    internal static class Live
    {
        static readonly List<NetworkEntity> Pending = new List<NetworkEntity>();
        static readonly Dictionary<long, ShopView> Shops = new Dictionary<long, ShopView>();

        internal static IEnumerable<ShopView> All { get { return Shops.Values; } }

        internal static RepairService[] Services()
        {
            List<RepairService> list = new List<RepairService>();
            foreach (ShopView s in Shops.Values) if (s.Alive && s.Ref.Entity != null) list.Add(s.Ref);
            return list.ToArray();
        }

        internal static void Spawned(uint hash, NetworkEntity e)
        {
            if (e == null || !Shop.Is(hash)) return;
            Pending.Add(e);
        }

        internal static void Clear()
        {
            Pending.Clear();
            Shops.Clear();
        }

        internal static void Tick()
        {
            if (Pending.Count > 0)
            {
                NetworkEntity[] list = Pending.ToArray();
                Pending.Clear();
                foreach (NetworkEntity e in list)
                {
                    if (e == null) continue;
                    long key = Plat.Key(e);
                    if (Shops.ContainsKey(key)) continue;
                    Shops[key] = new ShopView(e);
                    Core.Log("repair shop spawned: " + e.name);
                }
            }
            if (Shops.Count == 0) return;
            List<long> dead = null;
            foreach (KeyValuePair<long, ShopView> s in Shops)
            {
                if (s.Value.Tick()) continue;
                if (dead == null) dead = new List<long>();
                dead.Add(s.Key);
            }
            if (dead == null) return;
            foreach (long k in dead)
            {
                Core.Log("repair shop gone: " + Shops[k].Name);
                Shops.Remove(k);
            }
        }

        internal static void LateTick()
        {
            foreach (ShopView s in Shops.Values) s.LateTick();
        }
    }
}
