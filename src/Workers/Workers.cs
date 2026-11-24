using System;
using System.Collections.Generic;
using UnityEngine;
using Il2CppInterop.Runtime;
using UnityEngine.Events;

namespace RepairShopQuest
{
    internal static class Workers
    {
        sealed class Entry
        {
            internal GameObject Host;
            internal RepairCloneAnimation Anim;
            internal WorkerAnchorLock Lock;
            internal bool Active = true;
        }

        static readonly Dictionary<long, Entry> All = new Dictionary<long, Entry>();
        static bool _hooked;

        static Entry For(GameObject host)
        {
            long key = Plat.Key(host);
            Entry e;
            if (!All.TryGetValue(key, out e))
            {
                e = new Entry { Host = host };
                All[key] = e;
                Hook();
            }
            return e;
        }

        internal static RepairCloneAnimation Animation(GameObject host)
        {
            Entry e = For(host);
            if (e.Anim == null) e.Anim = new RepairCloneAnimation(host);
            return e.Anim;
        }

        internal static WorkerAnchorLock Lock(GameObject host)
        {
            Entry e = For(host);
            if (e.Lock == null) e.Lock = new WorkerAnchorLock(host);
            return e.Lock;
        }

        internal static RepairCloneAnimation[] Animations()
        {
            List<RepairCloneAnimation> list = new List<RepairCloneAnimation>();
            foreach (Entry e in All.Values) if (e.Anim != null) list.Add(e.Anim);
            return list.ToArray();
        }

        internal static void Clear()
        {
            foreach (Entry e in All.Values)
            {
                if (e.Anim == null) continue;
                try { e.Anim.OnDestroy(); }
                catch (Exception) { }
            }
            All.Clear();
        }

        internal static void Tick()
        {
            if (!Handshake.Available) return;
            WorkerView.Update();
        }

        internal static void LateTick()
        {
            if (All.Count == 0) return;
            List<long> dead = null;
            foreach (KeyValuePair<long, Entry> p in All)
            {
                Entry e = p.Value;
                if (e.Host == null)
                {
                    if (e.Anim != null) e.Anim.OnDestroy();
                    if (dead == null) dead = new List<long>();
                    dead.Add(p.Key);
                    continue;
                }
                bool active = e.Host.activeInHierarchy;
                if (active != e.Active)
                {
                    e.Active = active;
                    if (e.Anim != null)
                    {
                        if (active) e.Anim.OnEnable();
                        else e.Anim.OnDisable();
                    }
                }
                if (!active) continue;
                if (e.Anim != null) e.Anim.LateUpdate();
                if (e.Lock != null) e.Lock.LateUpdate();
            }
            if (dead != null) foreach (long k in dead) All.Remove(k);
        }

        internal static void FixedTick()
        {
            foreach (Entry e in All.Values) if (e.Lock != null && e.Host != null && e.Host.activeInHierarchy) e.Lock.FixedUpdate();
        }

        static void BeforeRender()
        {
            try
            {
                foreach (Entry e in All.Values) if (e.Anim != null && e.Host != null && e.Host.activeInHierarchy) e.Anim.RenderPose();
            }
            catch (Exception) { }
        }

        static void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            try
            {
                Application.add_onBeforeRender(DelegateSupport.ConvertDelegate<UnityAction>(new Action(BeforeRender)));
                Core.Log("worker pose hook installed");
            }
            catch (Exception ex)
            {
                Core.Warn("worker pose hook failed, poses come from LateUpdate only: " + ex.Message);
            }
        }
    }
}
