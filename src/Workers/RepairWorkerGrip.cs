using System;
using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal sealed class RepairWorkerGrip
    {
        private Transform item;

        private Transform nativeFrame;

        private RepairHammerGripData source;

        private Vector3 point;

        private Quaternion rotation = Quaternion.identity;

        private float warnAfter;

        internal bool IsBound => item != null;

        internal Quaternion ModelRotationInPalm => Quaternion.Inverse(rotation);

        internal Vector3 ContactPoint => point;

        internal void Place(Transform target, Vector3 palmPosition, Quaternion palmRotation, RepairHammerGripData data)
        {
            if (target == null || data == null)
            {
                Clear();
            }
            else
            {
                if (!Finite(palmPosition) || !Finite(palmRotation))
                {
                    return;
                }
                if (item != target || source != data)
                {
                    item = null;
                    source = null;
                    if (!Bind(target, palmPosition, palmRotation, data))
                    {
                        return;
                    }
                }
                target.rotation = palmRotation * ModelRotationInPalm;
                target.position = palmPosition - target.TransformVector(point);
            }
        }

        private bool Bind(Transform target, Vector3 palmPosition, Quaternion palmRotation, RepairHammerGripData data)
        {
            Transform parent = target.parent;
            Vector3 localScale = target.localScale;
            Vector3 position = target.position;
            Quaternion quaternion = target.rotation;
            bool flag = false;
            string text = "No available native hammer grab point.";
            try
            {
                if (data.grips == null || data.grips.Length == 0 || !Finite(data.scale) || data.scale <= 0f)
                {
                    throw new InvalidOperationException("Invalid native hammer grip data.");
                }
                if (!Finite(data.holdPoint) || !Finite(data.axis) || !Finite(data.strikeAxis) || data.axis.sqrMagnitude < 0.0001f || Vector3.Cross(data.axis, data.strikeAxis).sqrMagnitude < 0.0001f)
                {
                    throw new InvalidOperationException("Invalid native hammer working axes.");
                }
                if (nativeFrame == null)
                {
                    nativeFrame = new GameObject("Repair worker native grip frame").transform;
                    nativeFrame.gameObject.hideFlags = HideFlags.HideAndDontSave;
                }
                nativeFrame.localScale = Vector3.one;
                Vector3 vector = palmRotation * Vector3.right;
                Vector3 vector2 = palmRotation * Vector3.forward;
                nativeFrame.SetPositionAndRotation(palmPosition, Quaternion.LookRotation(vector, -vector2));
                Quaternion quaternion2 = Quaternion.LookRotation(data.strikeAxis, data.axis);
                target.rotation = Quaternion.LookRotation(vector2, vector) * Quaternion.Inverse(quaternion2);
                target.position = palmPosition - target.TransformVector(data.holdPoint);
                GrabPoint[] grips = data.Clone().grips;
                Vector3 localHandPosition = target.InverseTransformPoint(nativeFrame.position);
                Quaternion localHandRotation = Quaternion.Inverse(target.rotation) * nativeFrame.rotation;
                GrabPoint grabPoint = null;
                float num = float.MaxValue;
                GrabPoint[] array = grips;
                foreach (GrabPoint grabPoint2 in array)
                {
                    if (grabPoint2 != null && !grabPoint2.IsDisabled && !grabPoint2.IsOcupied)
                    {
                        float num2 = grabPoint2.CheckScore(target, data.scale, 1f, 0.005f, localHandPosition, localHandRotation, isLeft: false, isDocked: false);
                        if (Finite(num2) && num2 < num)
                        {
                            num = num2;
                            grabPoint = grabPoint2;
                        }
                    }
                }
                if (grabPoint != null)
                {
                    bool isOcupied = grabPoint.IsOcupied;
                    try
                    {
                        grabPoint.ApplyTo(data.scale, nativeFrame, target);
                    }
                    finally
                    {
                        grabPoint.IsOcupied = isOcupied;
                    }
                    RestoreAttachment(target, parent, localScale);
                    point = target.InverseTransformPoint(palmPosition);
                    rotation = Quaternion.Inverse(target.rotation) * palmRotation;
                    if (!Finite(point) || !Finite(rotation))
                    {
                        throw new InvalidOperationException("Nonfinite native hammer grip.");
                    }
                    flag = true;
                }
            }
            catch (Exception ex)
            {
                text = ex.Message;
            }
            finally
            {
                if (target != null)
                {
                    RestoreAttachment(target, parent, localScale);
                    if (!flag)
                    {
                        target.SetPositionAndRotation(position, quaternion);
                    }
                }
            }
            if (!flag)
            {
                if (Time.unscaledTime >= warnAfter)
                {
                    warnAfter = Time.unscaledTime + 15f;
                    Core.Warn("REPAIR_WORKER_GRIP_WAIT reason=" + text);
                }
                return false;
            }
            item = target;
            source = data;
            return true;
        }

        private static void RestoreAttachment(Transform target, Transform parent, Vector3 scale)
        {
            if (target.parent != parent)
            {
                target.SetParent(parent, worldPositionStays: true);
            }
            target.localScale = scale;
        }

        internal void Clear()
        {
            item = null;
            source = null;
            point = Vector3.zero;
            rotation = Quaternion.identity;
            warnAfter = 0f;
            if (!(nativeFrame == null))
            {
                GameObject gameObject = nativeFrame.gameObject;
                nativeFrame = null;
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(gameObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }
        }

        private static bool Finite(float value)
        {
            if (!float.IsNaN(value))
            {
                return !float.IsInfinity(value);
            }
            return false;
        }

        private static bool Finite(Vector3 value)
        {
            if (Finite(value.x) && Finite(value.y))
            {
                return Finite(value.z);
            }
            return false;
        }

        private static bool Finite(Quaternion value)
        {
            if (Finite(value.x) && Finite(value.y) && Finite(value.z))
            {
                return Finite(value.w);
            }
            return false;
        }
    }
}
