using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using Il2Cpp;
using Il2CppAlta.Intelligence;
using Il2CppAlta.Networking;

[assembly: MelonInfo(typeof(RepairShopQuest.Core), RepairShopQuest.Core.Name, RepairShopQuest.Core.Version, "caleb, ported by Shadow")]
[assembly: MelonGame("Alta", "Att_Quest")]

namespace RepairShopQuest
{
    public sealed class Core : MelonMod
    {
        internal const string Name = "RepairShopQuest";
        internal const string Version = "0.1.1";
        static MelonLogger.Instance _log;
        static bool _running, _reset;
        readonly string[] _last = new string[4];

        internal static void Log(string m) { if (_log != null) _log.Msg(m); }
        internal static void Warn(string m) { if (_log != null) _log.Warning(m); }
        internal static void Error(string m) { if (_log != null) _log.Error(m); }

        public override void OnInitializeMelon()
        {
            _log = LoggerInstance;
            try
            {
                Sync.Prepare();
                HarmonyLib.Harmony h = new HarmonyLib.Harmony("repairshop.quest");
                int patched = 0;
                patched += Patch(h, typeof(JoinServerPipeline), "ConnectionApprovedByServer", null, "Approved", HarmonyLib.Priority.Last);
                patched += Patch(h, typeof(SpawnManager), "SpawnLocal", null, "Spawned", HarmonyLib.Priority.Normal);
                if (patched < 2)
                {
                    Error("the join or spawn hook is missing; the repair shop stays off.");
                    return;
                }
                patched += Patch(h, typeof(PlayerMessageDisplay), "Display", "Display", null, HarmonyLib.Priority.Normal, typeof(string), typeof(float), typeof(DisplayMessageType));
                patched += Patch(h, typeof(PlayerLikeSpectralGhost), "SetupMaterials", "GhostMaterials", null, HarmonyLib.Priority.Normal);
                _running = true;
                Log(Version + " started (" + patched + "/4 hooks), waiting for a server that runs the repair shop");
            }
            catch (Exception e)
            {
                Error("failed to start: " + e);
            }
        }

        int Patch(HarmonyLib.Harmony h, Type type, string method, string prefix, string postfix, int priority, params Type[] args)
        {
            try
            {
                MethodInfo target = args.Length > 0 ? AccessTools.Method(type, method, args) : AccessTools.Method(type, method);
                if (target == null) throw new MissingMethodException(type.Name + "." + method);
                HarmonyMethod pre = prefix == null ? null : new HarmonyMethod(typeof(Core), prefix) { priority = priority };
                HarmonyMethod post = postfix == null ? null : new HarmonyMethod(typeof(Core), postfix) { priority = priority };
                h.Patch(target, pre, post);
                return 1;
            }
            catch (Exception e)
            {
                Error("hook " + type.Name + "." + method + " failed: " + e.Message);
                return 0;
            }
        }

        static void Approved(Connection connection)
        {
            try
            {
                _reset = true;
                Sync.Listen(connection);
            }
            catch (Exception e) { Error("new connection: " + e); }
        }

        static void Spawned(uint prefabHash, NetworkEntity __result)
        {
            try { Live.Spawned(prefabHash, __result); }
            catch (Exception e) { Error("spawn: " + e.Message); }
        }

        static bool Display(string message)
        {
            try { return Messages.Show(message); }
            catch (Exception) { return true; }
        }

        static bool GhostMaterials(PlayerLikeSpectralGhost __instance)
        {
            try { return Look.AllowsGhost(__instance); }
            catch (Exception) { return true; }
        }

        public override void OnUpdate()
        {
            if (!_running) return;
            try
            {
                if (_reset)
                {
                    _reset = false;
                    Live.Clear();
                    Workers.Clear();
                    Look.Clear();
                    Handshake.Clear();
                    Shop.Unregister();
                }
                Sync.Tick();
            }
            catch (Exception e) { Report(0, e); }
            try { Handshake.Tick(); }
            catch (Exception e) { Report(1, e); }
            try { Live.Tick(); }
            catch (Exception e) { Report(2, e); }
            try
            {
                Look.Tick();
                Workers.Tick();
            }
            catch (Exception e) { Report(3, e); }
        }

        public override void OnFixedUpdate()
        {
            if (!_running) return;
            try { Workers.FixedTick(); }
            catch (Exception e) { Report(3, e); }
        }

        public override void OnLateUpdate()
        {
            if (!_running) return;
            try { Live.LateTick(); }
            catch (Exception e) { Report(2, e); }
            try { Workers.LateTick(); }
            catch (Exception e) { Report(3, e); }
        }

        void Report(int slot, Exception e)
        {
            string m = e.GetType().Name + ": " + e.Message;
            if (m == _last[slot]) return;
            _last[slot] = m;
            Error("tick " + slot + " threw: " + e);
        }
    }
}
