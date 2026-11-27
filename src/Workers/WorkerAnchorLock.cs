using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Intelligence;

namespace RepairShopQuest
{
    internal sealed class WorkerAnchorLock
    {
        internal readonly GameObject gameObject;

        internal readonly Transform transform;

        internal WorkerAnchorLock(GameObject host)
        {
            gameObject = host;
            transform = host.transform;
        }

        private Transform anchor;

        private PlayerLikeSpectralGhost ghost;

        private Vector3 position;

        private Vector3 childPosition;

        private Quaternion rotation;

        private Quaternion childRotation;

        private bool ready;

        private bool moving;

        private Vector3 fromPosition;

        private Vector3 toPosition;

        private Quaternion fromRotation;

        private Quaternion toRotation;

        private float moveBegan;

        private float moveDuration;

        internal void Bind(PlayerLikeSpectralGhost source, Transform target)
        {
            Setup(source);
            anchor = target;
            moving = false;
            position = target.position;
            rotation = target.rotation;
            ready = true;
            Pin();
        }

        internal void Bind(PlayerLikeSpectralGhost source, Vector3 at, Quaternion facing)
        {
            Setup(source);
            anchor = null;
            moving = false;
            position = at;
            rotation = facing;
            ready = true;
            Pin();
        }

        internal void BindMotion(PlayerLikeSpectralGhost source, RepairStationDescriptor value)
        {
            if (RepairStationProtocol.IsValid(value))
            {
                Setup(source);
                if (value.Mode == RepairStationMode.Moving)
                {
                    SetMotion(value.FromPosition, value.FromRotation, value.ToPosition, value.ToRotation, value.Elapsed, value.Duration);
                }
                else
                {
                    SetTarget(value.ToPosition, value.ToRotation);
                }
            }
        }

        internal void SetTarget(Vector3 at, Quaternion facing)
        {
            anchor = null;
            moving = false;
            position = at;
            rotation = facing;
            ready = true;
            Pin();
        }

        internal void SetMotion(Vector3 from, Quaternion fromFacing, Vector3 to, Quaternion toFacing, float elapsed, float duration)
        {
            anchor = null;
            fromPosition = from;
            fromRotation = fromFacing;
            toPosition = to;
            toRotation = toFacing;
            moveDuration = Mathf.Max(0f, duration);
            moveBegan = Time.unscaledTime - Mathf.Clamp(elapsed, 0f, moveDuration);
            moving = moveDuration > 0f;
            ready = true;
            if (!moving)
            {
                position = to;
                rotation = toFacing;
            }
            Pin();
        }

        private void Setup(PlayerLikeSpectralGhost source)
        {
            if (!(ghost == source) || !ready)
            {
                ghost = source;
                if (ghost != null)
                {
                    childPosition = ghost.transform.localPosition;
                    childRotation = ghost.transform.localRotation;
                }
                Rigidbody[] componentsInChildren2 = Plat.All<Rigidbody>(transform, true).ToArray();
                foreach (Rigidbody obj in componentsInChildren2)
                {
                    obj.velocity = Vector3.zero;
                    obj.angularVelocity = Vector3.zero;
                    obj.useGravity = false;
                    obj.isKinematic = true;
                }
            }
        }

        internal void Pin()
        {
            if (!ready)
            {
                return;
            }
            if (anchor != null)
            {
                position = anchor.position;
                rotation = anchor.rotation;
            }
            else if (moving)
            {
                float num = Mathf.Clamp01((Time.unscaledTime - moveBegan) / moveDuration);
                num = num * num * (3f - 2f * num);
                position = Vector3.Lerp(fromPosition, toPosition, num);
                rotation = Quaternion.Slerp(fromRotation, toRotation, num);
                if (num >= 1f)
                {
                    moving = false;
                }
            }
            if (ghost != null)
            {
                Game.DisableAgent(ghost);
            }
            transform.SetPositionAndRotation(position, rotation);
            if (ghost != null && ghost.transform != transform)
            {
                ghost.transform.localPosition = childPosition;
                ghost.transform.localRotation = childRotation;
            }
        }

        internal void FixedUpdate()
        {
            Pin();
        }

        internal void LateUpdate()
        {
            Pin();
        }

        internal static string Wire(Transform anchor)
        {
            Vector3 vector = anchor.position;
            Quaternion quaternion = anchor.rotation;
            return string.Join("|", new float[7] { vector.x, vector.y, vector.z, quaternion.x, quaternion.y, quaternion.z, quaternion.w }.Select((float v) => v.ToString("R", CultureInfo.InvariantCulture)).ToArray());
        }
    }
}
