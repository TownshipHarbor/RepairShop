using System;
using System.Globalization;
using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal static class RepairStationProtocol
    {
        internal const string Prefix = "REPAIR_SHOP_STATION_V1|";

        internal const string Capability = "stations-v1";

        internal const int MaxPayloadLength = 1024;

        internal const float MaxDuration = 30f;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static string Encode(RepairStationDescriptor value)
        {
            if (!IsValid(value))
            {
                throw new InvalidOperationException("Invalid repair worker station descriptor.");
            }
            Vector3 workTarget = value.WorkTarget;
            Vector3 fromPosition = value.FromPosition;
            Vector3 toPosition = value.ToPosition;
            Quaternion fromRotation = value.FromRotation;
            Quaternion toRotation = value.ToRotation;
            float[] array = new float[19]
            {
                workTarget.x, workTarget.y, workTarget.z, fromPosition.x, fromPosition.y, fromPosition.z, fromRotation.x, fromRotation.y, fromRotation.z, fromRotation.w,
                toPosition.x, toPosition.y, toPosition.z, toRotation.x, toRotation.y, toRotation.z, toRotation.w, value.Elapsed, value.Duration
            };
            string[] array2 = new string[21];
            array2[0] = value.WorkerId.ToString(Inv);
            int mode = (int)value.Mode;
            array2[1] = mode.ToString(Inv);
            for (int i = 0; i < array.Length; i++)
            {
                array2[i + 2] = array[i].ToString("R", Inv);
            }
            return string.Join("|", array2);
        }

        internal static bool TryParse(string payload, out RepairStationDescriptor value)
        {
            value = null;
            if (string.IsNullOrEmpty(payload) || payload.Length > 1024)
            {
                return false;
            }
            string[] array = payload.Split('|');
            if (array.Length != 21)
            {
                return false;
            }
            if (!uint.TryParse(array[0], NumberStyles.None, Inv, out var result) || !int.TryParse(array[1], NumberStyles.None, Inv, out var result2))
            {
                return false;
            }
            float[] array2 = new float[19];
            for (int i = 0; i < array2.Length; i++)
            {
                if (array[i + 2].Trim() != array[i + 2] || !float.TryParse(array[i + 2], NumberStyles.Float, Inv, out array2[i]))
                {
                    return false;
                }
            }
            RepairStationDescriptor repairStationDescriptor = new RepairStationDescriptor
            {
                WorkerId = result,
                Mode = (RepairStationMode)result2,
                WorkTarget = new Vector3(array2[0], array2[1], array2[2]),
                FromPosition = new Vector3(array2[3], array2[4], array2[5]),
                FromRotation = new Quaternion(array2[6], array2[7], array2[8], array2[9]),
                ToPosition = new Vector3(array2[10], array2[11], array2[12]),
                ToRotation = new Quaternion(array2[13], array2[14], array2[15], array2[16]),
                Elapsed = array2[17],
                Duration = array2[18]
            };
            if (!IsValid(repairStationDescriptor))
            {
                return false;
            }
            value = repairStationDescriptor;
            return true;
        }

        internal static bool IsValid(RepairStationDescriptor value)
        {
            if (value == null || value.WorkerId == 0 || value.Mode < RepairStationMode.Idle || value.Mode > RepairStationMode.Moving || !Position(value.WorkTarget) || !Position(value.FromPosition) || !Position(value.ToPosition) || !Rotation(value.FromRotation) || !Rotation(value.ToRotation) || !Finite(value.Elapsed) || !Finite(value.Duration) || value.Duration < 0f || value.Duration > 30f || value.Elapsed < 0f || value.Elapsed > value.Duration || (value.ToPosition - value.FromPosition).sqrMagnitude > 4096f)
            {
                return false;
            }
            if (value.Mode == RepairStationMode.Moving)
            {
                return value.Duration > 0f;
            }
            if (value.Elapsed == 0f && value.Duration == 0f && (value.ToPosition - value.FromPosition).sqrMagnitude < 0.0001f)
            {
                return Mathf.Abs(Quaternion.Dot(value.FromRotation, value.ToRotation)) > 0.9999f;
            }
            return false;
        }

        internal static void Sample(RepairStationDescriptor value, float age, out Vector3 position, out Quaternion rotation)
        {
            float num = value.Elapsed + ((Finite(age) && age > 0f) ? age : 0f);
            float num2 = ((value.Duration > 0f) ? Mathf.Clamp01(num / value.Duration) : 1f);
            num2 = num2 * num2 * (3f - 2f * num2);
            position = Vector3.Lerp(value.FromPosition, value.ToPosition, num2);
            rotation = Quaternion.Slerp(value.FromRotation, value.ToRotation, num2);
        }

        private static bool Position(Vector3 value)
        {
            if (Finite(value.x) && Finite(value.y) && Finite(value.z) && Mathf.Abs(value.x) <= 100000f && Mathf.Abs(value.y) <= 100000f)
            {
                return Mathf.Abs(value.z) <= 100000f;
            }
            return false;
        }

        private static bool Rotation(Quaternion value)
        {
            if (!Finite(value.x) || !Finite(value.y) || !Finite(value.z) || !Finite(value.w))
            {
                return false;
            }
            float num = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
            if (num > 0.98f)
            {
                return num < 1.02f;
            }
            return false;
        }

        private static bool Finite(float value)
        {
            if (!float.IsNaN(value))
            {
                return !float.IsInfinity(value);
            }
            return false;
        }
    }
}
