using System;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using Il2CppAlta.Networking;
using GameStream = Il2CppAlta.Serialization.Stream;
using StringEncoding = Il2CppAlta.Serialization.Stream.StringEncoding;

namespace RepairShopQuest
{
    internal static class Sync
    {
        internal const int Message = 18;
        internal const string Key = Shop.Id + " (RepairShop)";
        static SerializeConnectionMethod _handler;
        static string _pending;
        static bool _hasPending, _heard, _warned;
        static float _since;
        static readonly object _lock = new object();

        internal static void Prepare()
        {
            _handler = Plat.ConnectionMethod(Receive);
        }

        internal static void Listen(Connection c)
        {
            if (c == null) return;
            c.SetHandler((MessageType)Message, _handler);
            _heard = false;
            _warned = false;
            _since = Time.unscaledTime;
            Core.Log("new server connection: waiting for the server's SyncLib numbers");
        }

        static void Receive(Connection c, GameStream s)
        {
            string json = null;
            s.SerializeString(ref json, (StringEncoding)0);
            lock (_lock)
            {
                _pending = json;
                _hasPending = true;
            }
            Share(json);
        }

        static void Share(string json)
        {
            try
            {
                AppDomain.CurrentDomain.SetData("SyncLib.Quest.Json", json);
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = a.GetName().Name;
                    if (n != "PortalGunQuest" && n != "PortalGunTwin") continue;
                    Type t = a.GetType("PortalGunQuest.Sync");
                    if (t == null) continue;
                    BindingFlags f = BindingFlags.Static | BindingFlags.NonPublic;
                    FieldInfo gate = t.GetField("_lock", f), text = t.GetField("_pending", f), has = t.GetField("_hasPending", f);
                    if (gate == null || text == null || has == null) continue;
                    lock (gate.GetValue(null))
                    {
                        text.SetValue(null, json);
                        has.SetValue(null, true);
                    }
                }
            }
            catch (Exception) { }
        }

        internal static void Tick()
        {
            string json;
            lock (_lock)
            {
                if (!_hasPending)
                {
                    if (!_heard && !_warned && _since > 0f && Time.unscaledTime - _since > 20f && Plat.InWorld())
                    {
                        _warned = true;
                        Core.Log("no SyncLib numbers from this server after 20 s: it does not run SyncLib, or another headset mod took message 18");
                    }
                    return;
                }
                json = _pending;
                _pending = null;
                _hasPending = false;
            }
            _heard = true;
            Read(json);
        }

        static void Read(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Core.Error("the server sent an empty SyncLib list");
                return;
            }
            Match m = Regex.Match(json, "\"" + Regex.Escape(Key) + "\"\\s*:\\s*(\\d+)");
            uint hash;
            if (!m.Success || !uint.TryParse(m.Groups[1].Value, out hash) || hash == 0)
            {
                Core.Log("this server does not run the repair shop; staying idle");
                if (Shop.Done) Shop.Unregister();
                return;
            }
            if (Shop.Done && Shop.Hash == hash)
            {
                Core.Log("repair shop already registered as " + hash);
                return;
            }
            Live.Clear();
            Shop.Register(hash);
        }
    }
}
