using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Il2CppAlta.Customization;
using Il2CppAlta.Intelligence;
using Il2CppAlta.Networking;

namespace RepairShopQuest
{
    internal static class Look
    {
        static readonly HashSet<uint> Clones = new HashSet<uint>();
        static readonly Dictionary<uint, List<KeyValuePair<Renderer, Material>>> Originals = new Dictionary<uint, List<KeyValuePair<Renderer, Material>>>();
        static long _server;
        internal static int Restored;

        internal static int Count { get { return Clones.Count; } }

        internal static void Clear()
        {
            Clones.Clear();
            Originals.Clear();
            _server = 0;
        }

        static void Check()
        {
            long s = Plat.Id(JoinedServerInfo.Current);
            if (s == _server) return;
            _server = s;
            Clones.Clear();
            Originals.Clear();
        }

        internal static void Mark(string identifier)
        {
            Check();
            uint id;
            if (!uint.TryParse(identifier, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) || !Clones.Add(id)) return;
            List<KeyValuePair<Renderer, Material>> saved;
            if (!Originals.TryGetValue(id, out saved)) return;
            foreach (KeyValuePair<Renderer, Material> p in saved) if (p.Key != null) p.Key.sharedMaterial = p.Value;
            Originals.Remove(id);
            Restored++;
            Core.Log("restored the outfit colors of clone " + id);
        }

        internal static bool AllowsGhost(PlayerLikeSpectralGhost ghost)
        {
            Check();
            if (ghost == null || ghost.Entity == null) return true;
            uint id = ghost.Entity.Identifier;
            if (Clones.Contains(id)) return false;
            if (!Originals.ContainsKey(id)) Originals[id] = Capture(ghost);
            return true;
        }

        static List<KeyValuePair<Renderer, Material>> Capture(PlayerLikeSpectralGhost ghost)
        {
            List<KeyValuePair<Renderer, Material>> list = new List<KeyValuePair<Renderer, Material>>();
            PlayerCustomizationHandler h = Game.GhostHandler(ghost);
            if (h == null || h.CharacterDummy == null) return list;
            var renderers = h.CharacterDummy.MainRenderers;
            if (renderers == null) return list;
            var en = renderers.GetEnumerator();
            var step = en == null ? null : en.TryCast<Il2CppSystem.Collections.IEnumerator>();
            while (step != null && step.MoveNext())
            {
                Renderer r = en.Current;
                if (r != null && r.sharedMaterial != null) list.Add(new KeyValuePair<Renderer, Material>(r, r.sharedMaterial));
            }
            return list;
        }

        static float _nextScan;

        internal static void Tick()
        {
            Check();
            if (Clones.Count == 0 || Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + 0.5f;
            foreach (PlayerLikeSpectralGhost g in Plat.Scene<PlayerLikeSpectralGhost>())
            {
                if (g == null || g.Entity == null || !Clones.Contains(g.Entity.Identifier)) continue;
                NetworkEntity root = g.Entity.PrefabRoot;
                if (root == null || !root.gameObject.activeInHierarchy) continue;
                Workers.Animation(root.gameObject).Initialize(g);
            }
        }
    }
}
