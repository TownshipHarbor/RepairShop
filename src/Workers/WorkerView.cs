using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Intelligence;
using Il2CppAlta.Networking;

namespace RepairShopQuest
{
    internal static class WorkerView
    {
        private sealed class State
        {
            internal int Role;

            internal bool Working;

            internal Vector3 Target;

            internal Vector3 Position;

            internal Quaternion Rotation;
        }

        private sealed class WorkState
        {
            internal RepairWorkDescriptor Descriptor;

            internal float Seen;
        }

        private sealed class StationState
        {
            internal RepairStationDescriptor Descriptor;

            internal float Seen;
        }

        private sealed class SequenceState
        {
            internal RepairSequenceStage Stage;

            internal float Seen;
        }

        private static readonly Dictionary<uint, State> states = new Dictionary<uint, State>();

        private static object server;

        private static float next;

        private static readonly Dictionary<uint, WorkState> workStates = new Dictionary<uint, WorkState>();

        private static readonly Dictionary<uint, StationState> stationStates = new Dictionary<uint, StationState>();

        private static readonly Dictionary<uint, SequenceState> sequenceStates = new Dictionary<uint, SequenceState>();

        private static void Clear()
        {
            if (server != JoinedServerInfo.Current)
            {
                server = JoinedServerInfo.Current;
                states.Clear();
                workStates.Clear();
                stationStates.Clear();
                sequenceStates.Clear();
                next = 0f;
                RepairCloneAnimation[] array = Workers.Animations();
                foreach (RepairCloneAnimation obj in array)
                {
                    obj.SetCarryWork(enabled: false);
                    obj.SetForgeWork(heating: false);
                    obj.SetStationWork(RepairWorkMode.Metal, travelling: true);
                    obj.SetRepairWork(null, 0f);
                }
            }
        }

        internal static void MarkWork(string payload)
        {
            Clear();
            if (RepairWorkProtocol.TryParse(payload, out var value))
            {
                workStates[value.WorkerId] = new WorkState
                {
                    Descriptor = value,
                    Seen = Time.unscaledTime
                };
            }
        }

        internal static void MarkStation(string payload)
        {
            Clear();
            if (RepairStationProtocol.TryParse(payload, out var value))
            {
                stationStates[value.WorkerId] = new StationState
                {
                    Descriptor = value,
                    Seen = Time.unscaledTime
                };
            }
        }

        internal static void MarkSequence(string payload)
        {
            Clear();
            if (RepairSequenceProtocol.TryParse(payload, out var worker, out var stage))
            {
                sequenceStates[worker] = new SequenceState
                {
                    Stage = stage,
                    Seen = Time.unscaledTime
                };
            }
        }

        private static RepairStationDescriptor Advance(RepairStationDescriptor source, float age)
        {
            return new RepairStationDescriptor
            {
                WorkerId = source.WorkerId,
                Mode = source.Mode,
                WorkTarget = source.WorkTarget,
                FromPosition = source.FromPosition,
                FromRotation = source.FromRotation,
                ToPosition = source.ToPosition,
                ToRotation = source.ToRotation,
                Elapsed = ((source.Mode == RepairStationMode.Moving) ? Mathf.Min(source.Duration, source.Elapsed + Mathf.Max(0f, age)) : 0f),
                Duration = source.Duration
            };
        }

        private static bool ForgeStationMatches(RepairStationDescriptor station, WorkState work)
        {
            if (station == null || work == null || work.Descriptor == null || !work.Descriptor.Active || Time.unscaledTime - work.Seen >= 12f)
            {
                return false;
            }
            RepairService repairService = Array.Find(Live.Services(), (RepairService s) => s.Entity != null && s.Entity.Identifier == work.Descriptor.ServiceId);
            if (repairService == null || repairService.Shop == null || !repairService.Shop.gameObject.activeInHierarchy || repairService.HeatWorkAnchor == null || repairService.WoodWorkerAnchor == null)
            {
                return false;
            }
            Transform shop = repairService.Shop;
            Vector3 vector = shop.InverseTransformPoint(repairService.HeatWorkAnchor.position);
            float y = shop.InverseTransformPoint(repairService.WoodWorkerAnchor.position).y;
            Vector3 vector2 = shop.TransformPoint(new Vector3(vector.x - 1.35f, y, vector.z - 0.6f));
            Vector3 vector3 = shop.TransformPoint(new Vector3(vector.x - 0.55f, vector.y + 0.1154f, vector.z - 0.25f));
            Vector3 forward = Vector3.ProjectOnPlane(vector3 - vector2, shop.up);
            if (forward.sqrMagnitude < 0.01f)
            {
                return false;
            }
            if ((vector2 - station.ToPosition).sqrMagnitude < 0.0004f && (vector3 - station.WorkTarget).sqrMagnitude < 0.0004f)
            {
                return Quaternion.Angle(Quaternion.LookRotation(forward, shop.up), station.ToRotation) < 1f;
            }
            return false;
        }

        internal static void Mark(string payload)
        {
            Clear();
            string[] array = payload.Split('|');
            if (array.Length != 13 || !uint.TryParse(array[0], out var result) || result == 0 || !int.TryParse(array[1], out var result2) || result2 < 1 || result2 > 2 || !int.TryParse(array[2], out var result3) || (result3 != 0 && result3 != 1))
            {
                return;
            }
            float[] array2 = new float[10];
            for (int i = 0; i < array2.Length; i++)
            {
                if (!float.TryParse(array[i + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out array2[i]) || float.IsNaN(array2[i]) || float.IsInfinity(array2[i]))
                {
                    return;
                }
            }
            Quaternion quaternion = new Quaternion(array2[6], array2[7], array2[8], array2[9]);
            float num = Quaternion.Dot(quaternion, quaternion);
            if (!(num < 0.5f) && !(num > 1.5f))
            {
                states[result] = new State
                {
                    Role = result2,
                    Working = (result3 == 1),
                    Target = new Vector3(array2[0], array2[1], array2[2]),
                    Position = new Vector3(array2[3], array2[4], array2[5]),
                    Rotation = quaternion.normalized
                };
            }
        }

        internal static void Update()
        {
            Clear();
            if (Time.unscaledTime < next)
            {
                return;
            }
            next = Time.unscaledTime + 0.5f;
            PlayerLikeSpectralGhost[] array = Plat.Scene<PlayerLikeSpectralGhost>();
            foreach (PlayerLikeSpectralGhost playerLikeSpectralGhost in array)
            {
                if (playerLikeSpectralGhost.Entity == null || !states.TryGetValue(playerLikeSpectralGhost.Entity.Identifier, out var value))
                {
                    continue;
                }
                NetworkEntity prefabRoot = playerLikeSpectralGhost.Entity.PrefabRoot;
                if (!(prefabRoot == null) && prefabRoot.gameObject.activeInHierarchy)
                {
                    WorkerAnchorLock workerAnchorLock = Workers.Lock(prefabRoot.gameObject);
                    RepairCloneAnimation repairCloneAnimation = Workers.Animation(prefabRoot.gameObject);
                    repairCloneAnimation.Initialize(playerLikeSpectralGhost);
                    StationState value2;
                    bool flag = stationStates.TryGetValue(playerLikeSpectralGhost.Entity.Identifier, out value2);
                    bool num = value.Role == 2 && flag && Time.unscaledTime - value2.Seen < 12f;
                    SequenceState value3;
                    bool flag2 = sequenceStates.TryGetValue(playerLikeSpectralGhost.Entity.Identifier, out value3);
                    bool flag3 = flag2 && Time.unscaledTime - value3.Seen < 4f;
                    if (num)
                    {
                        RepairStationDescriptor repairStationDescriptor = Advance(value2.Descriptor, Time.unscaledTime - value2.Seen);
                        workerAnchorLock.BindMotion(playerLikeSpectralGhost, repairStationDescriptor);
                        bool flag4 = repairStationDescriptor.Mode == RepairStationMode.Moving;
                        bool carryWork = flag3 && value3.Stage == RepairSequenceStage.Moving && flag4;
                        workStates.TryGetValue(playerLikeSpectralGhost.Entity.Identifier, out var value4);
                        bool flag5 = flag3 && value3.Stage == RepairSequenceStage.Heating && repairStationDescriptor.Mode == RepairStationMode.Metal && ForgeStationMatches(repairStationDescriptor, value4);
                        bool flag6 = !flag2 || (flag3 && ((value3.Stage == RepairSequenceStage.Metal && repairStationDescriptor.Mode == RepairStationMode.Metal) || (value3.Stage == RepairSequenceStage.Wood && repairStationDescriptor.Mode == RepairStationMode.Wood) || flag5));
                        repairCloneAnimation.SetCarryWork(carryWork);
                        repairCloneAnimation.SetForgeWork(flag5);
                        repairCloneAnimation.SetStationWork((repairStationDescriptor.Mode != RepairStationMode.Wood) ? RepairWorkMode.Metal : RepairWorkMode.Wood, flag4);
                        repairCloneAnimation.SetShopWork(value.Role, value.Working && flag6 && (repairStationDescriptor.Mode == RepairStationMode.Wood || repairStationDescriptor.Mode == RepairStationMode.Metal), repairStationDescriptor.WorkTarget);
                    }
                    else
                    {
                        repairCloneAnimation.SetCarryWork(enabled: false);
                        repairCloneAnimation.SetForgeWork(heating: false);
                        workerAnchorLock.Bind(playerLikeSpectralGhost, value.Position, value.Rotation);
                        repairCloneAnimation.SetStationWork(RepairWorkMode.Metal, flag);
                        repairCloneAnimation.SetShopWork(value.Role, value.Working && !flag, value.Target);
                    }
                    repairCloneAnimation.SetRepairWork((workStates.TryGetValue(playerLikeSpectralGhost.Entity.Identifier, out var value5) && Time.unscaledTime - value5.Seen < 12f) ? value5.Descriptor : null, value5?.Seen ?? 0f);
                }
            }
        }
    }
}
