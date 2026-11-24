using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal sealed class RepairHammerGripData
    {
        public GrabPoint[] grips;

        public float scale = 1f;

        public Vector3 axis = Vector3.forward;

        public Vector3 holdPoint;

        public Vector3 strikeAxis = Vector3.forward;

        List<Dictionary<string, object>> _source;

        internal static RepairHammerGripData Parse(string json)
        {
            Dictionary<string, object> o = Json.Parse(json) as Dictionary<string, object>;
            object raw;
            List<object> list = o != null && o.TryGetValue("grips", out raw) ? raw as List<object> : null;
            if (list == null || list.Count == 0 || list.Count > 32 || !o.ContainsKey("scale") || !o.ContainsKey("axis") || !o.ContainsKey("holdPoint") || !o.ContainsKey("strikeAxis"))
            {
                throw new InvalidOperationException("Invalid native hammer metadata.");
            }
            RepairHammerGripData d = new RepairHammerGripData
            {
                scale = Number(o["scale"]),
                axis = Grips.Vec(o, "axis"),
                holdPoint = Grips.Vec(o, "holdPoint"),
                strikeAxis = Grips.Vec(o, "strikeAxis"),
                _source = new List<Dictionary<string, object>>()
            };
            foreach (object g in list)
            {
                Dictionary<string, object> gd = g as Dictionary<string, object>;
                if (gd == null) throw new InvalidOperationException("Invalid native hammer grab point.");
                d._source.Add(gd);
            }
            d.grips = new GrabPoint[d._source.Count];
            for (int i = 0; i < d.grips.Length; i++)
            {
                d.grips[i] = Grips.FromJson(d._source[i]);
                if (d.grips[i] == null) throw new InvalidOperationException("No native grab point to carry the hammer grip.");
            }
            return d;
        }

        static float Number(object v)
        {
            if (!(v is double)) throw new InvalidOperationException("Invalid native hammer coordinate.");
            float f = (float)(double)v;
            if (float.IsNaN(f) || float.IsInfinity(f)) throw new InvalidOperationException("Non-finite native hammer coordinate.");
            return f;
        }

        internal RepairHammerGripData Clone()
        {
            RepairHammerGripData d = new RepairHammerGripData { scale = scale, axis = axis, holdPoint = holdPoint, strikeAxis = strikeAxis, _source = _source };
            if (grips == null) return d;
            d.grips = new GrabPoint[grips.Length];
            for (int i = 0; i < grips.Length; i++)
            {
                if (grips[i] != null) Grips.Apply(grips[i], _source[i]);
                d.grips[i] = grips[i];
            }
            return d;
        }
    }
}
