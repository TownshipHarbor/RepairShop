using System;
using System.Collections.Generic;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Networking;
using Object = UnityEngine.Object;

namespace RepairShopQuest
{
    internal static class Grips
    {
        internal const uint DonorHash = 36126;
        static GameObject _holder;
        static readonly List<GrabPoint> _pool = new List<GrabPoint>();
        static int _taken;

        internal static Vector3 Position(GrabPoint g)
        {
            return g.position;
        }

        internal static Vector3 Linear(GrabPoint g)
        {
            return g.linear;
        }

        internal static bool IsLocal(GrabPoint g)
        {
            return g.isUsingLocal;
        }

        internal static float Scale(Pickup p)
        {
            return p.grabPointsScale;
        }

        internal static GrabPoint Copy(GrabPoint original, Func<Transform, Transform> map)
        {
            return original;
        }

        internal static GrabPoint FromJson(Dictionary<string, object> d)
        {
            GrabPoint g = Take();
            if (g == null) return null;
            Apply(g, d);
            return g;
        }

        internal static void Apply(GrabPoint g, Dictionary<string, object> d)
        {
            g.isDisabled = Bool(d, "isDisabled");
            g.transform = null;
            g.isUsingLocal = Bool(d, "isUsingLocal");
            g.position = Vec(d, "position");
            g.rotationEuler = Vec(d, "rotationEuler");
            g.linear = Vec(d, "linear");
            g.rotationMode = (RotationMode)Int(d, "rotationMode");
            g.poleForward = Vec(d, "poleForward");
            g.offset = Num(d, "offset");
            g.isSetupWithLeft = Bool(d, "isSetupWithLeft");
            g.dockedRestriction = (PickupFromDockRestriction)Int(d, "dockedRestriction");
            g.nonDockedRestriction = (PickupFromDockRestriction)Int(d, "nonDockedRestriction");
            if (g.enabledFromDocks != null) g.enabledFromDocks.Clear();
            g.isIgnoringAngle = Bool(d, "isIgnoringAngle");
            g.thumbPosition = (ThumbPosition)Int(d, "thumbPosition");
            g.refuseMultipleHands = Bool(d, "refuseMultipleHands");
            g.interactor = null;
        }

        static GrabPoint Take()
        {
            if (_taken < _pool.Count) return _pool[_taken++];
            NetworkPrefab donor = PrefabManager.GetPrefab(DonorHash);
            Pickup dp = donor == null ? null : Plat.Get<Pickup>(donor);
            if (dp == null) return null;
            if (_holder == null)
            {
                _holder = new GameObject("RepairShop grip donors");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }
            GameObject copy = Plat.Copy(donor.gameObject, _holder.transform);
            Pickup p = Plat.Get<Pickup>(copy);
            var points = p == null ? null : p.grabPoints;
            if (points == null || points.Length == 0) return null;
            _pool.Add(points[0]);
            _taken++;
            return points[0];
        }

        static bool Bool(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v is bool && (bool)v;
        }

        static float Num(Dictionary<string, object> d, string k)
        {
            object v;
            return d.TryGetValue(k, out v) && v is double ? (float)(double)v : 0f;
        }

        static int Int(Dictionary<string, object> d, string k)
        {
            return (int)Math.Round(Num(d, k));
        }

        internal static Vector3 Vec(Dictionary<string, object> d, string k)
        {
            object v;
            Dictionary<string, object> o = d.TryGetValue(k, out v) ? v as Dictionary<string, object> : null;
            if (o == null) return Vector3.zero;
            return new Vector3(Num(o, "x"), Num(o, "y"), Num(o, "z"));
        }
    }
}
