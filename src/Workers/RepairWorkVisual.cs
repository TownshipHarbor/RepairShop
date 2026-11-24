using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Inventory;

namespace RepairShopQuest
{
    internal sealed class RepairWorkVisual : IDisposable
    {
        private sealed class Surface
        {
            internal Renderer Original;

            internal MeshRenderer Copy;

            internal Mesh Mesh;

            internal Pickup Part;

            internal bool Metal;

            internal bool Wood;

            internal bool Cold;

            internal bool CanHeat;

            internal Bounds Bounds;

            internal MaterialPropertyBlock OriginalBlock;

            internal MaterialPropertyBlock HeatBlock;

            internal MaterialPropertyBlock[] OriginalSlotBlocks;

            internal MaterialPropertyBlock[] HeatSlotBlocks;

            internal MaterialState[] Materials;

            internal bool ClearancePrepared;

            internal Vector3[] ClearanceVertices;

            internal int[] ClearanceTriangles;
        }

        private sealed class Grip
        {
            internal GrabPoint Native;

            internal Transform Part;

            internal Pickup OriginalPart;

            internal Vector3 Start;

            internal Vector3 Linear;

            internal float Scale;

            internal bool Wood;
        }

        private sealed class MaterialState
        {
            internal Material Material;

            internal readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>();

            internal readonly Dictionary<string, float> Floats = new Dictionary<string, float>();

            internal bool EmissionKeyword;
        }

        private sealed class LayoutOption
        {
            internal Vector3 Position;

            internal Vector3 Contact;

            internal Vector3 Axis;

            internal Vector3 WorkTop;

            internal Quaternion Rotation;

            internal Quaternion Palm;

            internal Pickup Part;

            internal Grip NativeGrip;

            internal bool AllowsRoll;

            internal float Cost;

            internal int Profile;
        }

        private static readonly Quaternion NativeFromPalm = Quaternion.LookRotation(Vector3.right, Vector3.back);

        private static readonly string[] ColorProperties = new string[1] { "_EmissionColor" };

        private static readonly string[] FloatProperties = new string[2] { "_Glow", "_ForcedTemperature" };

        private const float WoodContactSpacing = 0.0999f;

        private static readonly HashSet<string> Woods = new HashSet<string>(new string[11]
        {
            "oak", "ash", "birch", "walnut", "redwood", "wood", "leather", "fiber", "cloth", "rope",
            "bone"
        }, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<int, float> warnAfter = new Dictionary<int, float>();

        private readonly List<Surface> surfaces = new List<Surface>();

        private readonly List<Grip> grips = new List<Grip>();

        private readonly List<Material> ownedMaterials = new List<Material>();

        private readonly List<Mesh> ownedMeshes = new List<Mesh>();

        private readonly Dictionary<Transform, Transform> transforms = new Dictionary<Transform, Transform>();

        private readonly Dictionary<Renderer, bool> sourceVisibility = new Dictionary<Renderer, bool>();

        private readonly List<LayoutOption> layoutOptions = new List<LayoutOption>();

        private readonly RepairWorkHeatEffect heatEffect = new RepairWorkHeatEffect();

        private Surface workSurface;

        private Surface normalSurface;

        private bool disposed;

        private bool laidOut;

        private bool hidden;

        private bool detachedMetal;

        private Vector3 localWork;

        private Vector3 localWorkTop;

        private Vector3 localGrip;

        private Vector3 localGripAxis;

        private Quaternion localPalm;

        private Pickup gripPart;

        private Grip selectedNativeGrip;

        private GrabPoint nativeCheck;

        private Grip nativeCheckSource;

        private bool gripAllowsRoll;

        private int clearanceTriangleBudget;

        private Collider detachedSupport;

        private Vector3 detachedContact;

        private Vector3 detachedUp;

        private Vector3 detachedSupportAnchor;

        internal Pickup Source { get; private set; }

        internal RepairWorkMode Mode { get; private set; }

        internal Pickup GripPart => gripPart;

        internal bool GripAllowsRoll => gripAllowsRoll;

        internal int LayoutOptionCount => layoutOptions.Count;

        internal int LayoutRevision { get; private set; }

        internal Transform Root { get; private set; }

        internal bool UsableGrip
        {
            get
            {
                if (!disposed && laidOut)
                {
                    return !detachedMetal;
                }
                return false;
            }
        }

        internal bool IsDetachedMetal
        {
            get
            {
                if (!disposed)
                {
                    return detachedMetal;
                }
                return false;
            }
        }

        internal bool IsValid
        {
            get
            {
                if (!disposed && Root != null && Source != null && Source.Entity != null && !Source.Entity.IsBeingDestroyed && Source.IsDocked && !Source.IsInteractedWith && surfaces.All((Surface s) => s.Original != null))
                {
                    if (detachedMetal && laidOut)
                    {
                        return DetachedSupportValid();
                    }
                    return true;
                }
                return false;
            }
        }

        internal Vector3 GripPosition
        {
            get
            {
                if (!(Root != null))
                {
                    return Vector3.zero;
                }
                return Root.TransformPoint(localGrip);
            }
        }

        internal Vector3 GripAxis
        {
            get
            {
                if (!(Root != null))
                {
                    return Vector3.zero;
                }
                return Root.TransformDirection(localGripAxis);
            }
        }

        internal Quaternion PalmRotation
        {
            get
            {
                if (!(Root != null))
                {
                    return Quaternion.identity;
                }
                return Root.rotation * localPalm;
            }
        }

        internal Vector3 WorkPoint
        {
            get
            {
                if (!(Root != null))
                {
                    return Vector3.zero;
                }
                return Root.TransformPoint(localWorkTop);
            }
        }

        internal bool IsSourceHidden => hidden;

        private RepairWorkVisual()
        {
            Mode = RepairWorkMode.Metal;
        }

        internal static bool TryCreate(Pickup source, Transform parent, out RepairWorkVisual visual)
        {
            return TryCreate(source, parent, RepairWorkMode.Metal, out visual);
        }

        internal static bool TryCreate(Pickup source, Transform parent, RepairWorkMode mode, out RepairWorkVisual visual)
        {
            visual = null;
            source = RepairItems.Root(source);
            if (source == null || source.Item == null || parent == null || (mode != RepairWorkMode.Metal && mode != RepairWorkMode.Wood))
            {
                return false;
            }
            RepairWorkVisual repairWorkVisual = new RepairWorkVisual
            {
                Source = source,
                Mode = mode
            };
            try
            {
                repairWorkVisual.Root = new GameObject("Repair item visual (client only)").transform;
                repairWorkVisual.Root.gameObject.hideFlags = HideFlags.DontSave;
                repairWorkVisual.Root.gameObject.SetActive(value: false);
                repairWorkVisual.Root.SetParent(parent, worldPositionStays: false);
                Vector3 lossyScale = parent.lossyScale;
                if (!Finite(lossyScale) || Mathf.Abs(lossyScale.x) < 0.001f || Mathf.Abs(lossyScale.y) < 0.001f || Mathf.Abs(lossyScale.z) < 0.001f || Mathf.Abs(lossyScale.x - lossyScale.y) > 0.01f || Mathf.Abs(lossyScale.y - lossyScale.z) > 0.01f)
                {
                    throw new InvalidOperationException("Unsupported nonuniform worker scale.");
                }
                repairWorkVisual.Root.localScale = new Vector3(1f / lossyScale.x, 1f / lossyScale.y, 1f / lossyScale.z);
                repairWorkVisual.transforms[source.transform] = repairWorkVisual.Root;
                Renderer[] componentsInChildren = Plat.All<Renderer>(source, true).ToArray();
                if (componentsInChildren.Length == 0 || componentsInChildren.Length > 160)
                {
                    throw new InvalidOperationException("Unsupported repair visual size.");
                }
                Renderer[] array = componentsInChildren;
                foreach (Renderer renderer in array)
                {
                    repairWorkVisual.sourceVisibility[renderer] = renderer.enabled;
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    Pickup componentInParent = Plat.InParent<Pickup>(renderer);
                    if (!(componentInParent == null) && !(RepairItems.Root(componentInParent) != source))
                    {
                        PickupDock componentInParent2 = Plat.InParent<PickupDock>(renderer);
                        if ((!(componentInParent2 != null) || !componentInParent2.transform.IsChildOf(source.transform)) && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                        {
                            repairWorkVisual.Copy(renderer, componentInParent);
                        }
                    }
                }
                if (repairWorkVisual.surfaces.Count == 0)
                {
                    throw new InvalidOperationException("Repair visual has no visible geometry.");
                }
                repairWorkVisual.detachedMetal = mode == RepairWorkMode.Metal && RepairItems.IsLooseHead(source) && !repairWorkVisual.surfaces.Any((Surface s) => s.Cold);
                Surface surface = (from s in repairWorkVisual.surfaces
                    where (mode != RepairWorkMode.Wood) ? s.Metal : s.Wood
                    orderby (!(s.Part.DurabilityModule == null)) ? s.Part.DurabilityModule.Integrity : 1f, s.Bounds.size.sqrMagnitude descending
                    select s).FirstOrDefault();
                if (surface == null)
                {
                    throw new InvalidOperationException((mode == RepairWorkMode.Wood) ? ("No recognized wooden work surface. " + repairWorkVisual.WoodDiagnostic()) : "No safe metal work surface.");
                }
                repairWorkVisual.workSurface = surface;
                repairWorkVisual.localWork = surface.Bounds.center;
                repairWorkVisual.normalSurface = ((mode == RepairWorkMode.Wood) ? ((from s in repairWorkVisual.surfaces
                    where s.Metal
                    orderby s.Bounds.size.sqrMagnitude descending
                    select s).FirstOrDefault() ?? surface) : surface);
                if (!repairWorkVisual.detachedMetal)
                {
                    repairWorkVisual.BuildGrips();
                }
                if (!repairWorkVisual.detachedMetal && repairWorkVisual.grips.Count == 0)
                {
                    throw new InvalidOperationException("No native handle grip.");
                }
                Bounds bounds = repairWorkVisual.surfaces[0].Bounds;
                foreach (Surface item in repairWorkVisual.surfaces.Skip(1))
                {
                    bounds.Encapsulate(item.Bounds);
                }
                if (!Finite(bounds.size) || bounds.size.magnitude > 2.4f || bounds.size.magnitude < 0.025f)
                {
                    throw new InvalidOperationException("Repair assembly does not fit the workbench.");
                }
                Material[] array2 = repairWorkVisual.ownedMaterials.ToArray();
                RepairUiStereo.Configure(repairWorkVisual.Root.gameObject);
                repairWorkVisual.ownedMaterials.Clear();
                repairWorkVisual.ownedMaterials.AddRange((from m in repairWorkVisual.surfaces.SelectMany((Surface s) => s.Copy.sharedMaterials)
                    where m != null
                    select m).Distinct());
                Material[] array3 = array2;
                foreach (Material material in array3)
                {
                    if (!repairWorkVisual.ownedMaterials.Contains(material))
                    {
                        Destroy(material);
                    }
                }
                foreach (Surface surface2 in repairWorkVisual.surfaces)
                {
                    surface2.Materials = surface2.Copy.sharedMaterials.Select(CaptureMaterial).ToArray();
                }
                repairWorkVisual.Root.gameObject.SetActive(value: false);
                visual = repairWorkVisual;
                return true;
            }
            catch (Exception ex)
            {
                repairWorkVisual.Dispose();
                int instanceID = source.GetInstanceID();
                if (!warnAfter.TryGetValue(instanceID, out var value) || Time.unscaledTime >= value)
                {
                    if (warnAfter.Count > 64)
                    {
                        warnAfter.Clear();
                    }
                    warnAfter[instanceID] = Time.unscaledTime + 15f;
                    Core.Log("REPAIR_ITEM_VISUAL_WAIT reason=" + ex.Message);
                }
                return false;
            }
        }

        private string WoodDiagnostic()
        {
            return "surfaces=" + surfaces.Count + " [" + string.Join("; ", surfaces.Take(12).Select(delegate(Surface s)
            {
                PhysicalMaterial physicalMaterial = ((s.Part == null || s.Part.PhysicalMaterial == null) ? null : s.Part.PhysicalMaterial.PhysicalMaterial);
                DurabilityModule durabilityModule = ((s.Part == null) ? null : s.Part.DurabilityModule);
                return "renderer=" + ((s.Original == null) ? "missing" : s.Original.name) + ", material=" + ((physicalMaterial == null) ? "missing" : (physicalMaterial.name + "/" + physicalMaterial.Hash)) + ", wood=" + s.Wood + ", wrapping=" + (s.Original != null && Wrapping(s.Original)) + ", integrity=" + ((durabilityModule == null) ? "missing" : durabilityModule.Integrity.ToString("R", CultureInfo.InvariantCulture));
            }).ToArray()) + "]";
        }

        private void Copy(Renderer original, Pickup owner)
        {
            SkinnedMeshRenderer skinnedMeshRenderer = original as SkinnedMeshRenderer;
            Mesh mesh;
            if (skinnedMeshRenderer != null)
            {
                if (skinnedMeshRenderer.sharedMesh == null)
                {
                    return;
                }
                mesh = new Mesh
                {
                    name = skinnedMeshRenderer.sharedMesh.name + " (repair view)"
                };
                skinnedMeshRenderer.BakeMesh(mesh);
                ownedMeshes.Add(mesh);
            }
            else
            {
                MeshFilter component = Plat.Get<MeshFilter>(original);
                if (component == null || component.sharedMesh == null)
                {
                    return;
                }
                mesh = component.sharedMesh;
            }
            if (mesh.vertexCount == 0 || mesh.vertexCount > 350000)
            {
                return;
            }
            Transform parent = CopyTransform(original.transform);
            GameObject gameObject = new GameObject(original.name + " render copy");
            gameObject.transform.SetParent(parent, worldPositionStays: false);
            Plat.Add<MeshFilter>(gameObject).sharedMesh = mesh;
            MeshRenderer meshRenderer = Plat.Add<MeshRenderer>(gameObject);
            Material[] sharedMaterials = original.sharedMaterials;
            if (sharedMaterials.Length == 0 || sharedMaterials.Any((Material m) => m == null))
            {
                throw new InvalidOperationException("Repair item materials are loading.");
            }
            Plat.SetMaterials(meshRenderer, sharedMaterials.Select(delegate(Material m)
            {
                Material material = new Material(m)
                {
                    name = m.name + " (repair view)",
                    enableInstancing = true
                };
                ownedMaterials.Add(material);
                return material;
            }).ToList());
            meshRenderer.shadowCastingMode = original.shadowCastingMode;
            meshRenderer.sortingLayerID = original.sortingLayerID;
            meshRenderer.sortingOrder = original.sortingOrder;
            MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
            original.GetPropertyBlock(materialPropertyBlock);
            meshRenderer.SetPropertyBlock(materialPropertyBlock);
            MaterialPropertyBlock materialPropertyBlock2 = new MaterialPropertyBlock();
            original.GetPropertyBlock(materialPropertyBlock2);
            MaterialPropertyBlock[] array = new MaterialPropertyBlock[sharedMaterials.Length];
            MaterialPropertyBlock[] array2 = new MaterialPropertyBlock[sharedMaterials.Length];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = new MaterialPropertyBlock();
                array2[i] = new MaterialPropertyBlock();
                original.GetPropertyBlock(array[i], i);
                original.GetPropertyBlock(array2[i], i);
                if (!Geo.Empty(array[i]))
                {
                    meshRenderer.SetPropertyBlock(array[i], i);
                }
            }
            bool flag = Cold(owner, original);
            bool flag2 = !flag && Metal(owner, original);
            bool wood = RepairWorkMaterial.IsWood(owner) && !Wrapping(original);
            surfaces.Add(new Surface
            {
                Original = original,
                Copy = meshRenderer,
                Mesh = mesh,
                Part = owner,
                Cold = flag,
                Metal = flag2,
                Wood = wood,
                Bounds = TransformBounds(mesh.bounds, Root.worldToLocalMatrix * gameObject.transform.localToWorldMatrix),
                OriginalBlock = materialPropertyBlock,
                HeatBlock = materialPropertyBlock2,
                OriginalSlotBlocks = array,
                HeatSlotBlocks = array2,
                CanHeat = (Mode == RepairWorkMode.Metal && flag2)
            });
        }

        private Transform CopyTransform(Transform original)
        {
            if (transforms.TryGetValue(original, out var value))
            {
                return value;
            }
            if (original == null || !original.IsChildOf(Source.transform))
            {
                throw new InvalidOperationException("Repair geometry escaped its assembly.");
            }
            Transform parent = CopyTransform(original.parent);
            Transform transform = new GameObject(original.name + " view node").transform;
            transform.SetParent(parent, worldPositionStays: false);
            transform.localPosition = original.localPosition;
            transform.localRotation = original.localRotation;
            transform.localScale = original.localScale;
            transforms[original] = transform;
            return transform;
        }

        private void BuildGrips()
        {
            Pickup[] componentsInChildren = Plat.All<Pickup>(Source, true).ToArray();
            foreach (Pickup pickup in componentsInChildren)
            {
                if (RepairItems.Root(pickup) != Source || pickup.GrabPoints == null)
                {
                    continue;
                }
                PhysicalMaterialPart physicalMaterial = pickup.PhysicalMaterial;
                if (physicalMaterial != null && physicalMaterial.PhysicalMaterial == null)
                {
                    throw new InvalidOperationException("Repair material is loading.");
                }
                Transform transform = CopyTransform(pickup.transform);
                float num = Grips.Scale(pickup);
                if (!Finite(num) || num <= 0f || num > 10f)
                {
                    continue;
                }
                foreach (GrabPoint item in pickup.GrabPoints.Take(32))
                {
                    if (grips.Count >= 64)
                    {
                        return;
                    }
                    if (item != null && !item.IsDisabled && !item.IsOcupied)
                    {
                        GrabPoint grabPoint = CopyGrip(item);
                        Vector3 vector = Grips.Position(grabPoint);
                        Transform transform2 = grabPoint.Transform;
                        if (transform2 != null)
                        {
                            vector = (Grips.IsLocal(grabPoint) ? transform2.localPosition : transform.InverseTransformPoint(transform2.position));
                        }
                        Vector3 vector2 = Grips.Linear(grabPoint);
                        if (Finite(vector) && Finite(vector2))
                        {
                            grips.Add(new Grip
                            {
                                Native = grabPoint,
                                Part = transform,
                                OriginalPart = pickup,
                                Start = vector * num,
                                Linear = vector2 * num,
                                Scale = num,
                                Wood = KnownColdMaterial(pickup)
                            });
                        }
                    }
                }
            }
        }

        private GrabPoint CopyGrip(GrabPoint original)
        {
            return Grips.Copy(original, t => (t == Root || t.IsChildOf(Root)) ? t : CopyTransform(t));
        }

        internal bool Layout(Transform worker, Vector3 anvilTarget)
        {
            if (disposed || detachedMetal || Root == null || worker == null || !Finite(anvilTarget))
            {
                return false;
            }
            if (Mode == RepairWorkMode.Wood)
            {
                anvilTarget -= worker.right * 0.1f;
            }
            laidOut = false;
            layoutOptions.Clear();
            LayoutRevision++;
            List<LayoutOption> list = new List<LayoutOption>();
            Vector3 up = worker.up;
            Vector3 vector = worker.position - worker.right * 0.2f + up * 0.95f;
            float num = ((Mode == RepairWorkMode.Wood) ? 0.0999f : 0.1f);
            foreach (Grip grip in grips)
            {
                float[] array = ((Mode != RepairWorkMode.Wood) ? new float[5] { 0f, 0.25f, 0.5f, 0.75f, 1f } : new float[7] { 0f, 0.125f, 0.25f, 0.5f, 0.75f, 0.875f, 1f });
                foreach (float num2 in array)
                {
                    if (Mode == RepairWorkMode.Wood && grip.OriginalPart != workSurface.Part)
                    {
                        continue;
                    }
                    Vector3 contact = Root.InverseTransformPoint(grip.Part.TransformPoint(grip.Start + grip.Linear * num2));
                    if (!Finite(contact) || !WorkLocation(contact, out var work) || Vector3.Distance(contact, work) < num || (Mode == RepairWorkMode.Wood && !ClearOfMetal(Root.TransformPoint(contact), 0.04f)))
                    {
                        continue;
                    }
                    bool flag = grip.Wood || surfaces.Any((Surface s) => s.Cold && Geo.SqrDistance(s.Bounds, contact) < 0.0064f);
                    if (!flag && surfaces.Any((Surface s) => s.Cold))
                    {
                        continue;
                    }
                    Vector3 vector2 = contact - work;
                    Vector3 vector3 = WorkNormal(vector2);
                    Vector3 normalized = Vector3.ProjectOnPlane(vector2, vector3).normalized;
                    if (normalized.sqrMagnitude < 0.5f)
                    {
                        continue;
                    }
                    Vector3 vector4 = Vector3.ProjectOnPlane(vector - anvilTarget, up).normalized;
                    if (vector4.sqrMagnitude < 0.5f)
                    {
                        vector4 = -worker.forward;
                    }
                    float[] array2 = ((Mode != RepairWorkMode.Wood) ? new float[5] { 0f, -25f, 25f, -50f, 50f } : new float[7] { 0f, -25f, 25f, -50f, 50f, -75f, 75f });
                    for (int j = 0; j < array2.Length; j++)
                    {
                        float[] array3 = ((Mode == RepairWorkMode.Wood) ? new float[1] : new float[3] { 0.15f, 0.08f, 0f });
                        foreach (float num3 in array3)
                        {
                            float num4 = array2[j];
                            Quaternion rotation = Quaternion.LookRotation(Quaternion.AngleAxis(num4, up) * vector4, up) * Quaternion.Inverse(Quaternion.LookRotation(normalized, vector3));
                            Root.rotation = rotation;
                            Vector3 vector5 = Vector3.ProjectOnPlane(worker.position - anvilTarget, up).normalized * num3;
                            Root.position = anvilTarget + vector5 - Root.TransformVector(work);
                            Projection(workSurface, up, out var min, out var _);
                            Root.position += up * (Vector3.Dot(anvilTarget, up) - min + 0.003f);
                            Vector3 vector6 = grip.Part.TransformPoint(grip.Start + grip.Linear * num2);
                            Vector3 vector7 = ((grip.Linear.sqrMagnitude > 1E-05f) ? grip.Part.TransformDirection(grip.Linear).normalized : Vector3.ProjectOnPlane(vector6 - Root.TransformPoint(work), up).normalized);
                            if (vector7.sqrMagnitude < 0.5f)
                            {
                                vector7 = (vector6 - Root.TransformPoint(work)).normalized;
                            }
                            bool flag2 = grip.Native.RotationMode == RotationMode.Pole || grip.Native.RotationMode == RotationMode.Free;
                            for (int l = 0; l < ((!flag2) ? 1 : 2); l++)
                            {
                                Vector3 vector8 = ((l == 0) ? vector7 : (-vector7));
                                Vector3 normalized2 = Vector3.Cross(vector8, -up).normalized;
                                if (normalized2.sqrMagnitude < 0.5f)
                                {
                                    continue;
                                }
                                Quaternion quaternion = Quaternion.LookRotation(normalized2, -up) * NativeFromPalm;
                                GrabPoint grabPoint = CopyGrip(grip.Native);
                                float num5 = grabPoint.CheckScore(grip.Part, grip.Scale, 1f, 0.005f, grip.Part.InverseTransformPoint(vector6), Quaternion.Inverse(grip.Part.rotation) * quaternion, isLeft: true, isDocked: false);
                                if (!Finite(num5) || num5 >= float.MaxValue)
                                {
                                    continue;
                                }
                                Vector3 position = grabPoint.LastPosition + Grips.Linear(grabPoint) * grip.Scale * grabPoint.LastLinearPosition;
                                Vector3 vector9 = grip.Part.TransformPoint(position);
                                if (!Finite(vector9) || Vector3.Distance(vector9, Root.TransformPoint(work)) < num || (Mode == RepairWorkMode.Wood && !ClearOfMetal(vector9, 0.04f)))
                                {
                                    continue;
                                }
                                Quaternion quaternion2 = grip.Part.rotation * grabPoint.LastRotation * Quaternion.Inverse(NativeFromPalm);
                                if (Vector3.Dot(quaternion2 * Vector3.up, -up) < 0.15f)
                                {
                                    continue;
                                }
                                float cost = (vector9 - vector).sqrMagnitude + Mathf.Abs(num4) * 0.0001f + ((!flag) ? 0.2f : 0f);
                                Projection(workSurface, up, out var _, out var max2);
                                Bounds bounds = WorldBounds(workSurface);
                                Vector3 vector10 = ((Mode == RepairWorkMode.Wood) ? Root.TransformPoint(work) : bounds.center);
                                Vector3 vector11 = vector10 + up * (max2 - Vector3.Dot(vector10, up));
                                if (Mode != RepairWorkMode.Wood || ClearOfMetal(vector11, 0.04f))
                                {
                                    Vector3 rhs = vector11 - vector9;
                                    if (!(rhs.sqrMagnitude > 0.0001f) || !(Vector3.Dot(quaternion2 * Vector3.left, rhs) <= 0f))
                                    {
                                        list.Add(new LayoutOption
                                        {
                                            Position = Root.localPosition,
                                            Rotation = Root.localRotation,
                                            Contact = Root.InverseTransformPoint(vector9),
                                            Palm = Quaternion.Inverse(Root.rotation) * quaternion2,
                                            Axis = Root.InverseTransformDirection(vector8),
                                            WorkTop = Root.InverseTransformPoint(vector11),
                                            Part = grip.OriginalPart,
                                            NativeGrip = grip,
                                            AllowsRoll = (grabPoint.RotationMode == RotationMode.Pole || grabPoint.RotationMode == RotationMode.OneWayPole || grabPoint.RotationMode == RotationMode.Free),
                                            Cost = cost,
                                            Profile = j * 2 + l
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            foreach (IGrouping<int, LayoutOption> group in from o in list
                group o by o.Profile into g
                orderby g.Min((LayoutOption o) => o.Cost)
                select g)
            {
                foreach (LayoutOption item in group.OrderBy((LayoutOption o) => o.Cost))
                {
                    if (AddLayout(item) && layoutOptions.Count((LayoutOption o) => o.Profile == group.Key) >= 2)
                    {
                        break;
                    }
                }
            }
            foreach (LayoutOption item2 in list.OrderBy((LayoutOption o) => o.Cost))
            {
                if (layoutOptions.Count >= ((Mode == RepairWorkMode.Wood) ? 32 : 24))
                {
                    break;
                }
                AddLayout(item2);
            }
            if (layoutOptions.Count == 0)
            {
                Root.gameObject.SetActive(value: false);
                return false;
            }
            return SelectLayout(0);
        }

        internal bool TryLayoutDetachedOnAnvil(Transform worker, Vector3 target, Transform anvilRoot)
        {
            if (disposed || !detachedMetal)
            {
                return false;
            }
            SetSourceHidden(value: false);
            laidOut = false;
            detachedSupport = null;
            if (Root != null)
            {
                Root.gameObject.SetActive(value: false);
            }
            if (Root == null || worker == null || anvilRoot == null || !anvilRoot.gameObject.activeInHierarchy || !Finite(target) || !IsValid)
            {
                return false;
            }
            Collider[] array = (from c in Plat.All<Collider>(anvilRoot, true).ToArray()
                where c != null && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy
                select c).ToArray();
            if (array.Length == 0)
            {
                return false;
            }
            Vector3 up = worker.up;
            Vector3 vector = Vector3.zero;
            float num = float.PositiveInfinity;
            Ray ray = new Ray(target + up * 0.25f, -up);
            Collider[] array2 = array;
            for (int j = 0; j < array2.Length; j++)
            {
                if (array2[j].Raycast(ray, out var hitInfo, 0.5f) && !(Vector3.Dot(hitInfo.normal, up) < 0.9f) && !(hitInfo.distance >= num))
                {
                    num = hitInfo.distance;
                    vector = hitInfo.point;
                }
            }
            if (!Finite(num) || Mathf.Abs(Vector3.Dot(vector - target, up)) > 0.12f)
            {
                return false;
            }
            try
            {
                Vector3 size = workSurface.Mesh.bounds.size;
                Vector3[] array3 = new Vector3[3]
                {
                    Vector3.right,
                    Vector3.up,
                    Vector3.forward
                };
                int thin = (from i in Enumerable.Range(0, 3)
                    orderby size[i]
                    select i).First();
                int num2 = (from i in Enumerable.Range(0, 3)
                    where i != thin
                    orderby size[i] descending
                    select i).First();
                Vector3 normalized = Root.InverseTransformDirection(workSurface.Copy.transform.TransformDirection(array3[thin])).normalized;
                Vector3 normalized2 = Root.InverseTransformDirection(workSurface.Copy.transform.TransformDirection(array3[num2])).normalized;
                if (Vector3.Cross(normalized, normalized2).sqrMagnitude < 0.01f)
                {
                    return false;
                }
                Root.rotation = Quaternion.LookRotation(worker.forward, up) * Quaternion.Inverse(Quaternion.LookRotation(normalized2, normalized));
                Root.position = vector - Root.TransformVector(localWork);
                float num3 = float.PositiveInfinity;
                float num4 = float.NegativeInfinity;
                int num5 = 0;
                Vector3 vector2 = Vector3.zero;
                float num6 = float.PositiveInfinity;
                Vector3[] array4;
                foreach (Surface surface in surfaces)
                {
                    Vector3[] vertices = surface.Mesh.vertices;
                    num5 += vertices.Length;
                    if (num5 > 350000)
                    {
                        return false;
                    }
                    Matrix4x4 localToWorldMatrix = surface.Copy.transform.localToWorldMatrix;
                    array4 = vertices;
                    foreach (Vector3 point in array4)
                    {
                        Vector3 vector3 = localToWorldMatrix.MultiplyPoint3x4(point);
                        float num7 = Vector3.Dot(vector3 - vector, up);
                        if (!Finite(vector3) || !Finite(num7))
                        {
                            return false;
                        }
                        if (num7 < num3 - 0.001f)
                        {
                            num3 = num7;
                            vector2 = vector3;
                            num6 = Vector3.ProjectOnPlane(vector3 - vector, up).sqrMagnitude;
                        }
                        else if (num7 <= num3 + 0.001f)
                        {
                            float sqrMagnitude = Vector3.ProjectOnPlane(vector3 - vector, up).sqrMagnitude;
                            if (sqrMagnitude < num6)
                            {
                                vector2 = vector3;
                                num6 = sqrMagnitude;
                            }
                        }
                        num4 = Mathf.Max(num4, num7);
                    }
                }
                if (num5 == 0 || !Finite(num3) || !Finite(num4) || num4 - num3 > 0.5f)
                {
                    return false;
                }
                Root.position += Vector3.ProjectOnPlane(vector - vector2, up) + up * (0.001f - num3);
                Vector3 vector4 = vector2 + Vector3.ProjectOnPlane(vector - vector2, up) + up * (0.001f - num3);
                Collider collider = null;
                array2 = array;
                RaycastHit hitInfo2;
                foreach (Collider collider2 in array2)
                {
                    if (collider2.Raycast(new Ray(vector4 + up * 0.02f, -up), out hitInfo2, 0.04f) && Vector3.Dot(hitInfo2.normal, up) > 0.9f && Vector3.Distance(hitInfo2.point, vector4) < 0.01f)
                    {
                        collider = collider2;
                        break;
                    }
                }
                if (collider == null)
                {
                    return false;
                }
                foreach (Surface surface2 in surfaces)
                {
                    Vector3[] vertices2 = surface2.Mesh.vertices;
                    int num8 = Mathf.Max(1, vertices2.Length / 128);
                    Matrix4x4 localToWorldMatrix2 = surface2.Copy.transform.localToWorldMatrix;
                    for (int k = 0; k < vertices2.Length; k += num8)
                    {
                        Vector3 vector5 = localToWorldMatrix2.MultiplyPoint3x4(vertices2[k]);
                        array2 = array;
                        for (int j = 0; j < array2.Length; j++)
                        {
                            if (array2[j].Raycast(new Ray(vector5 + up * 0.15f, -up), out hitInfo2, 0.3f) && Vector3.Dot(hitInfo2.normal, up) > 0.6f && Vector3.Dot(hitInfo2.point - vector5, up) > 0.005f)
                            {
                                return false;
                            }
                        }
                    }
                }
                bool flag = false;
                array4 = new Vector3[7]
                {
                    Vector3.zero,
                    worker.right * 0.025f,
                    -worker.right * 0.025f,
                    worker.forward * 0.025f,
                    -worker.forward * 0.025f,
                    worker.right * 0.05f,
                    -worker.right * 0.05f
                };
                foreach (Vector3 vector6 in array4)
                {
                    Vector3 origin = vector + vector6 + up * (num4 - num3 + 0.3f);
                    if (SurfaceRay(workSurface, origin, -up, num4 - num3 + 0.35f, out var impact))
                    {
                        localWorkTop = Root.InverseTransformPoint(impact);
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                {
                    return false;
                }
                foreach (Surface surface3 in surfaces)
                {
                    surface3.CanHeat = surface3 == workSurface && surface3.Metal;
                }
                selectedNativeGrip = null;
                gripPart = null;
                gripAllowsRoll = false;
                detachedSupport = collider;
                detachedContact = Root.InverseTransformPoint(vector4);
                detachedUp = collider.transform.InverseTransformDirection(up);
                detachedSupportAnchor = collider.transform.InverseTransformPoint(vector4);
                laidOut = true;
                LayoutRevision++;
                Root.gameObject.SetActive(value: true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool DetachedSupportValid()
        {
            if (detachedSupport == null || !detachedSupport.enabled || detachedSupport.isTrigger || !detachedSupport.gameObject.activeInHierarchy)
            {
                return false;
            }
            Vector3 normalized = detachedSupport.transform.TransformDirection(detachedUp).normalized;
            Vector3 vector = Root.TransformPoint(detachedContact);
            if (Finite(normalized) && Finite(vector) && Vector3.Distance(vector, detachedSupport.transform.TransformPoint(detachedSupportAnchor)) < 0.01f && detachedSupport.Raycast(new Ray(vector + normalized * 0.02f, -normalized), out var hitInfo, 0.04f) && Vector3.Dot(hitInfo.normal, normalized) > 0.9f)
            {
                return Vector3.Distance(hitInfo.point, vector) < 0.01f;
            }
            return false;
        }

        private static bool SurfaceRay(Surface surface, Vector3 origin, Vector3 direction, float limit, out Vector3 impact)
        {
            impact = Vector3.zero;
            if (surface == null || surface.Copy == null || surface.Mesh == null)
            {
                return false;
            }
            Vector3[] vertices = surface.Mesh.vertices;
            int[] triangles = surface.Mesh.triangles;
            if (vertices.Length == 0 || vertices.Length > 350000 || triangles.Length == 0 || triangles.Length > 450000 || triangles.Length % 3 != 0)
            {
                return false;
            }
            Matrix4x4 localToWorldMatrix = surface.Copy.transform.localToWorldMatrix;
            float num = float.PositiveInfinity;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int num2 = triangles[i];
                int num3 = triangles[i + 1];
                int num4 = triangles[i + 2];
                if (num2 < 0 || num3 < 0 || num4 < 0 || num2 >= vertices.Length || num3 >= vertices.Length || num4 >= vertices.Length)
                {
                    return false;
                }
                Vector3 vector = localToWorldMatrix.MultiplyPoint3x4(vertices[num2]);
                Vector3 vector2 = localToWorldMatrix.MultiplyPoint3x4(vertices[num3]);
                Vector3 vector3 = localToWorldMatrix.MultiplyPoint3x4(vertices[num4]);
                Vector3 vector4 = vector2 - vector;
                Vector3 vector5 = vector3 - vector;
                Vector3 rhs = Vector3.Cross(direction, vector5);
                float num5 = Vector3.Dot(vector4, rhs);
                if (Mathf.Abs(num5) < 1E-08f)
                {
                    continue;
                }
                float num6 = 1f / num5;
                Vector3 lhs = origin - vector;
                float num7 = Vector3.Dot(lhs, rhs) * num6;
                if (num7 < 0f || num7 > 1f)
                {
                    continue;
                }
                Vector3 rhs2 = Vector3.Cross(lhs, vector4);
                float num8 = Vector3.Dot(direction, rhs2) * num6;
                if (!(num8 < 0f) && !(num7 + num8 > 1f))
                {
                    float num9 = Vector3.Dot(vector5, rhs2) * num6;
                    if (num9 >= 0f && num9 <= limit && num9 < num)
                    {
                        num = num9;
                    }
                }
            }
            if (!Finite(num))
            {
                return false;
            }
            impact = origin + direction * num;
            return Finite(impact);
        }

        private bool WorkLocation(Vector3 contact, out Vector3 work)
        {
            work = localWork;
            if (Mode != RepairWorkMode.Wood)
            {
                return true;
            }
            Bounds bounds = workSurface.Mesh.bounds;
            int index = ((!(bounds.size.x >= bounds.size.y) || !(bounds.size.x >= bounds.size.z)) ? ((bounds.size.y >= bounds.size.z) ? 1 : 2) : 0);
            Vector3 zero = Vector3.zero;
            zero[index] = bounds.extents[index] * 0.72f;
            Vector3[] array = (from p in new Vector3[5]
                {
                    bounds.center - zero,
                    bounds.center - zero * 0.5f,
                    bounds.center,
                    bounds.center + zero * 0.5f,
                    bounds.center + zero
                }
                select Root.InverseTransformPoint(workSurface.Copy.transform.TransformPoint(p)) into p
                where Finite(p) && (p - contact).sqrMagnitude >= 0.00998001f && ClearOfMetal(Root.TransformPoint(p), 0.04f)
                orderby surfaces.Any((Surface s) => s.Metal && Geo.SqrDistance(s.Bounds, p) < 0.0036f) ? 1 : 0, (p - contact).sqrMagnitude descending
                select p).ToArray();
            if (array.Length == 0)
            {
                return false;
            }
            work = array[0];
            return true;
        }

        internal bool ClearOfMetal(Vector3 world, float radius)
        {
            if (disposed || Root == null || !Finite(world) || !Finite(radius) || radius <= 0f)
            {
                return false;
            }
            Vector3 vector = Root.InverseTransformPoint(world);
            float num = radius * radius;
            foreach (Surface surface in surfaces)
            {
                if (!surface.Metal || Geo.SqrDistance(surface.Bounds, vector) > num)
                {
                    continue;
                }
                if (!surface.ClearancePrepared)
                {
                    surface.ClearancePrepared = true;
                    try
                    {
                        long num2 = 0L;
                        for (int i = 0; i < surface.Mesh.subMeshCount; i++)
                        {
                            num2 += surface.Mesh.GetIndexCount(i);
                        }
                        if (surface.Mesh.isReadable && num2 <= 36000 && clearanceTriangleBudget + num2 / 3 <= 48000)
                        {
                            Matrix4x4 matrix4x = Root.worldToLocalMatrix * surface.Copy.transform.localToWorldMatrix;
                            surface.ClearanceVertices = surface.Mesh.vertices.Select(((Matrix4x4)matrix4x).MultiplyPoint3x4).ToArray();
                            surface.ClearanceTriangles = surface.Mesh.triangles;
                            clearanceTriangleBudget += surface.ClearanceTriangles.Length / 3;
                        }
                    }
                    catch
                    {
                        surface.ClearanceVertices = null;
                        surface.ClearanceTriangles = null;
                    }
                }
                if (surface.ClearanceVertices == null || surface.ClearanceTriangles == null)
                {
                    return false;
                }
                Vector3[] clearanceVertices = surface.ClearanceVertices;
                int[] clearanceTriangles = surface.ClearanceTriangles;
                bool flag = surface.Bounds.Contains(vector);
                int num3 = 0;
                Vector3 normalized = new Vector3(0.897f, 0.331f, 0.293f).normalized;
                for (int j = 0; j + 2 < clearanceTriangles.Length; j += 3)
                {
                    Vector3 a = clearanceVertices[clearanceTriangles[j]];
                    Vector3 b = clearanceVertices[clearanceTriangles[j + 1]];
                    Vector3 c = clearanceVertices[clearanceTriangles[j + 2]];
                    if (TriangleDistanceSquared(vector, a, b, c) <= num)
                    {
                        return false;
                    }
                    if (flag && RayTriangle(vector, normalized, a, b, c))
                    {
                        num3++;
                    }
                }
                if ((num3 & 1) != 0)
                {
                    return false;
                }
            }
            return true;
        }

        private static float TriangleDistanceSquared(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 vector = b - a;
            Vector3 vector2 = c - a;
            Vector3 rhs = p - a;
            float num = Vector3.Dot(vector, rhs);
            float num2 = Vector3.Dot(vector2, rhs);
            if (num <= 0f && num2 <= 0f)
            {
                return rhs.sqrMagnitude;
            }
            Vector3 rhs2 = p - b;
            float num3 = Vector3.Dot(vector, rhs2);
            float num4 = Vector3.Dot(vector2, rhs2);
            if (num3 >= 0f && num4 <= num3)
            {
                return rhs2.sqrMagnitude;
            }
            float num5 = num * num4 - num3 * num2;
            if (num5 <= 0f && num >= 0f && num3 <= 0f)
            {
                return (p - (a + vector * (num / (num - num3)))).sqrMagnitude;
            }
            Vector3 rhs3 = p - c;
            float num6 = Vector3.Dot(vector, rhs3);
            float num7 = Vector3.Dot(vector2, rhs3);
            if (num7 >= 0f && num6 <= num7)
            {
                return rhs3.sqrMagnitude;
            }
            float num8 = num6 * num2 - num * num7;
            if (num8 <= 0f && num2 >= 0f && num7 <= 0f)
            {
                return (p - (a + vector2 * (num2 / (num2 - num7)))).sqrMagnitude;
            }
            float num9 = num3 * num7 - num6 * num4;
            if (num9 <= 0f && num4 - num3 >= 0f && num6 - num7 >= 0f)
            {
                return (p - (b + (c - b) * ((num4 - num3) / (num4 - num3 + (num6 - num7))))).sqrMagnitude;
            }
            float num10 = num9 + num8 + num5;
            if (Mathf.Abs(num10) < 1E-12f)
            {
                return Mathf.Min(rhs.sqrMagnitude, Mathf.Min(rhs2.sqrMagnitude, rhs3.sqrMagnitude));
            }
            return (p - (a + vector * (num8 / num10) + vector2 * (num5 / num10))).sqrMagnitude;
        }

        private static bool RayTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 vector = b - a;
            Vector3 vector2 = c - a;
            Vector3 rhs = Vector3.Cross(direction, vector2);
            float num = Vector3.Dot(vector, rhs);
            if (Mathf.Abs(num) < 1E-08f)
            {
                return false;
            }
            float num2 = 1f / num;
            Vector3 lhs = origin - a;
            float num3 = Vector3.Dot(lhs, rhs) * num2;
            if (num3 < 0f || num3 > 1f)
            {
                return false;
            }
            Vector3 rhs2 = Vector3.Cross(lhs, vector);
            float num4 = Vector3.Dot(direction, rhs2) * num2;
            if (num4 >= 0f && num3 + num4 <= 1f)
            {
                return Vector3.Dot(vector2, rhs2) * num2 > 1E-05f;
            }
            return false;
        }

        private bool AddLayout(LayoutOption option)
        {
            if (layoutOptions.Count >= ((Mode == RepairWorkMode.Wood) ? 32 : 24) || layoutOptions.Any((LayoutOption o) => Vector3.Distance(o.Position, option.Position) < 0.003f && Quaternion.Angle(o.Rotation, option.Rotation) < 1f && Vector3.Distance(o.Contact, option.Contact) < 0.003f && Quaternion.Angle(o.Palm, option.Palm) < 1f))
            {
                return false;
            }
            layoutOptions.Add(option);
            return true;
        }

        internal bool SelectLayout(int index)
        {
            if (disposed || Root == null || index < 0 || index >= layoutOptions.Count)
            {
                return false;
            }
            LayoutOption layoutOption = layoutOptions[index];
            Root.localPosition = layoutOption.Position;
            Root.localRotation = layoutOption.Rotation;
            localGrip = layoutOption.Contact;
            localPalm = layoutOption.Palm;
            localGripAxis = layoutOption.Axis;
            localWorkTop = layoutOption.WorkTop;
            gripPart = layoutOption.Part;
            selectedNativeGrip = layoutOption.NativeGrip;
            gripAllowsRoll = layoutOption.AllowsRoll;
            bool flag = KnownColdMaterial(gripPart) || surfaces.Any((Surface s) => s.Cold && Geo.SqrDistance(s.Bounds, localGrip) < 0.0064f);
            foreach (Surface surface in surfaces)
            {
                surface.CanHeat = Mode == RepairWorkMode.Metal && surface == workSurface && surface.Metal && (flag || Geo.SqrDistance(surface.Bounds, localGrip) > 0.0064f);
            }
            laidOut = true;
            Root.gameObject.SetActive(value: true);
            return true;
        }

        internal bool MatchesNativeGrip(float roll)
        {
            if (selectedNativeGrip == null || selectedNativeGrip.Part == null || (!gripAllowsRoll && Mathf.Abs(roll) > 0.01f))
            {
                return false;
            }
            return MatchesNativePalm(Quaternion.AngleAxis(roll, GripAxis) * PalmRotation);
        }

        internal bool MatchesNativePalm(Quaternion desired)
        {
            return NativePalmError(desired) <= 1f;
        }

        internal float NativePalmError(Quaternion desired)
        {
            if (selectedNativeGrip == null || selectedNativeGrip.Part == null || !Finite(desired.x) || !Finite(desired.y) || !Finite(desired.z) || !Finite(desired.w) || Quaternion.Dot(desired, desired) < 0.0001f)
            {
                return float.PositiveInfinity;
            }
            try
            {
                Grip grip = selectedNativeGrip;
                desired = desired.normalized;
                if (nativeCheck == null || nativeCheckSource != grip)
                {
                    nativeCheck = CopyGrip(grip.Native);
                    nativeCheckSource = grip;
                }
                GrabPoint grabPoint = nativeCheck;
                Quaternion localHandRotation = Quaternion.Inverse(grip.Part.rotation) * desired * NativeFromPalm;
                float num = grabPoint.CheckScore(grip.Part, grip.Scale, 1f, 0.005f, grip.Part.InverseTransformPoint(GripPosition), localHandRotation, isLeft: true, isDocked: false);
                if (!Finite(num) || num >= float.MaxValue)
                {
                    return float.PositiveInfinity;
                }
                Vector3 a = grip.Part.TransformPoint(grabPoint.LastPosition + Grips.Linear(grabPoint) * grip.Scale * grabPoint.LastLinearPosition);
                Quaternion lastRotation = grabPoint.LastRotation;
                if (!Finite(lastRotation.x) || !Finite(lastRotation.y) || !Finite(lastRotation.z) || !Finite(lastRotation.w) || Quaternion.Dot(lastRotation, lastRotation) < 0.0001f)
                {
                    return float.PositiveInfinity;
                }
                Quaternion normalized = (grip.Part.rotation * lastRotation.normalized * Quaternion.Inverse(NativeFromPalm)).normalized;
                return (Vector3.Distance(a, GripPosition) <= 0.005f) ? Quaternion.Angle(desired, normalized) : float.PositiveInfinity;
            }
            catch
            {
                return float.PositiveInfinity;
            }
        }

        private Vector3 WorkNormal(Vector3 stem)
        {
            Surface surface = normalSurface ?? workSurface;
            Vector3 size = surface.Mesh.bounds.size;
            Vector3[] array = new Vector3[3]
            {
                Vector3.right,
                Vector3.up,
                Vector3.forward
            };
            foreach (int item in from i in Enumerable.Range(0, 3)
                orderby size[i]
                select i)
            {
                Vector3 normalized = Root.InverseTransformDirection(surface.Copy.transform.TransformDirection(array[item])).normalized;
                if (Mathf.Abs(Vector3.Dot(normalized, stem.normalized)) < 0.7f)
                {
                    return normalized;
                }
            }
            return Vector3.Cross(stem.normalized, (Mathf.Abs(stem.normalized.y) < 0.8f) ? Vector3.up : Vector3.right).normalized;
        }

        internal void SetSourceHidden(bool value)
        {
            if (value && (!IsValid || !laidOut))
            {
                return;
            }
            foreach (KeyValuePair<Renderer, bool> item in sourceVisibility)
            {
                if (item.Key != null)
                {
                    item.Key.enabled = item.Value && !value;
                }
            }
            hidden = value;
        }

        internal void ApplyHeat(float progress)
        {
            if (disposed || Root == null || Mode == RepairWorkMode.Wood)
            {
                return;
            }
            if (!Finite(progress))
            {
                progress = 0f;
            }
            progress = Mathf.Clamp01(progress);
            float num = HeatEnvelope(progress);
            foreach (Surface surface in surfaces)
            {
                float num2 = (surface.CanHeat ? num : 0f);
                MaterialState[] materials = surface.Materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Heat(materials[i], num2, null);
                }
                if (num2 <= 0.0001f)
                {
                    surface.Copy.SetPropertyBlock(surface.OriginalBlock);
                    for (int j = 0; j < surface.OriginalSlotBlocks.Length; j++)
                    {
                        if (!Geo.Empty(surface.OriginalSlotBlocks[j]))
                        {
                            surface.Copy.SetPropertyBlock(surface.OriginalSlotBlocks[j], j);
                        }
                    }
                    continue;
                }
                if (surface.Materials.Length != 0)
                {
                    Heat(surface.Materials[0], num2, surface.HeatBlock);
                }
                surface.Copy.SetPropertyBlock(surface.HeatBlock);
                for (int k = 0; k < surface.HeatSlotBlocks.Length; k++)
                {
                    if (!Geo.Empty(surface.OriginalSlotBlocks[k]))
                    {
                        Heat(surface.Materials[k], num2, surface.HeatSlotBlocks[k]);
                        surface.Copy.SetPropertyBlock(surface.HeatSlotBlocks[k], k);
                    }
                }
            }
        }

        private static float HeatEnvelope(float progress)
        {
            if (!Finite(progress))
            {
                return 0f;
            }
            progress = Mathf.Clamp01(progress);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.16f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.78f) / 0.2f)));
        }

        internal void UpdateHeatEffects(float progress, bool gripping)
        {
            if (!disposed && !(Root == null) && workSurface != null && Mode != RepairWorkMode.Wood)
            {
                PhysicalMaterial physicalMaterial = ((workSurface.Part == null || workSurface.Part.PhysicalMaterial == null) ? null : workSurface.Part.PhysicalMaterial.PhysicalMaterial);
                float num = (gripping ? HeatEnvelope(progress) : 0f);
                float temperature = 23f + 682.66f * num;
                heatEffect.Update((num > 0.0001f) ? physicalMaterial : null, Root, WorkPoint, temperature);
            }
        }

        private static MaterialState CaptureMaterial(Material material)
        {
            MaterialState materialState = new MaterialState
            {
                Material = material,
                EmissionKeyword = material.IsKeywordEnabled("_EMISSION")
            };
            string[] colorProperties = ColorProperties;
            foreach (string text in colorProperties)
            {
                if (material.HasProperty(text))
                {
                    materialState.Colors[text] = material.GetColor(text);
                }
            }
            colorProperties = FloatProperties;
            foreach (string text2 in colorProperties)
            {
                if (material.HasProperty(text2))
                {
                    materialState.Floats[text2] = material.GetFloat(text2);
                }
            }
            return materialState;
        }

        private static void Heat(MaterialState state, float heat, MaterialPropertyBlock block)
        {
            if (state.Material == null)
            {
                return;
            }
            if (state.Floats.TryGetValue("_ForcedTemperature", out var value))
            {
                float value2 = ((heat <= 0.0001f) ? value : (0.58f * heat));
                state.Material.SetFloat("_ForcedTemperature", value2);
                block?.SetFloat("_ForcedTemperature", value2);
                return;
            }
            if (state.Floats.TryGetValue("_Glow", out value))
            {
                float value3 = Mathf.Lerp(value, Mathf.Max(value, 0.12f), heat);
                state.Material.SetFloat("_Glow", value3);
                block?.SetFloat("_Glow", value3);
            }
            if (state.Colors.TryGetValue("_EmissionColor", out var value4))
            {
                Color value5 = value4 + new Color(0.16f, 0.028f, 0.004f, 0f) * heat;
                state.Material.SetColor("_EmissionColor", value5);
                block?.SetColor("_EmissionColor", value5);
                if (heat > 0.001f || state.EmissionKeyword)
                {
                    state.Material.EnableKeyword("_EMISSION");
                }
                else
                {
                    state.Material.DisableKeyword("_EMISSION");
                }
            }
        }

        internal static float ProjectedExtent(Bounds bounds, Vector3 normal)
        {
            Vector3 extents = bounds.extents;
            return Mathf.Abs(normal.x) * extents.x + Mathf.Abs(normal.y) * extents.y + Mathf.Abs(normal.z) * extents.z;
        }

        private static Bounds WorldBounds(Surface surface)
        {
            return TransformBounds(surface.Mesh.bounds, surface.Copy.transform.localToWorldMatrix);
        }

        private static void Projection(Surface surface, Vector3 normal, out float min, out float max)
        {
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            Bounds bounds = surface.Mesh.bounds;
            Matrix4x4 localToWorldMatrix = surface.Copy.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                float b = Vector3.Dot(localToWorldMatrix.MultiplyPoint3x4(new Vector3(((i & 1) == 0) ? bounds.min.x : bounds.max.x, ((i & 2) == 0) ? bounds.min.y : bounds.max.y, ((i & 4) == 0) ? bounds.min.z : bounds.max.z)), normal);
                min = Mathf.Min(min, b);
                max = Mathf.Max(max, b);
            }
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Bounds result = new Bounds(matrix.MultiplyPoint3x4(bounds.min), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(((i & 1) == 0) ? bounds.min.x : bounds.max.x, ((i & 2) == 0) ? bounds.min.y : bounds.max.y, ((i & 4) == 0) ? bounds.min.z : bounds.max.z)));
            }
            return result;
        }

        private static bool KnownColdMaterial(Pickup part)
        {
            PhysicalMaterial physicalMaterial = ((part.PhysicalMaterial == null) ? null : part.PhysicalMaterial.PhysicalMaterial);
            if (physicalMaterial == null)
            {
                return false;
            }
            string name = physicalMaterial.name.ToLowerInvariant();
            return Woods.Any((string n) => name == n || name.StartsWith(n + " ", StringComparison.Ordinal) || name.EndsWith(" " + n, StringComparison.Ordinal));
        }

        private static bool Cold(Pickup part, Renderer renderer)
        {
            if (KnownColdMaterial(part))
            {
                return true;
            }
            string text = renderer.name.ToLowerInvariant();
            if (!text.Contains("wood") && !text.Contains("wrap") && !text.Contains("cloth"))
            {
                return renderer.sharedMaterials.Any((Material m) => m != null && m.shader != null && (m.shader.name.IndexOf("Wood", StringComparison.OrdinalIgnoreCase) >= 0 || m.shader.name.IndexOf("Leather", StringComparison.OrdinalIgnoreCase) >= 0 || m.shader.name.IndexOf("Cloth", StringComparison.OrdinalIgnoreCase) >= 0));
            }
            return true;
        }

        private static bool Wrapping(Renderer renderer)
        {
            string text = renderer.name.ToLowerInvariant();
            if (!text.Contains("wrap") && !text.Contains("cloth") && !text.Contains("leather") && !text.Contains("rope"))
            {
                return renderer.sharedMaterials.Any((Material m) => m != null && m.shader != null && (m.shader.name.IndexOf("Leather", StringComparison.OrdinalIgnoreCase) >= 0 || m.shader.name.IndexOf("Cloth", StringComparison.OrdinalIgnoreCase) >= 0));
            }
            return true;
        }

        private static bool Metal(Pickup part, Renderer renderer)
        {
            PhysicalMaterial physicalMaterial = ((part.PhysicalMaterial == null) ? null : part.PhysicalMaterial.PhysicalMaterial);
            if (physicalMaterial != null && RepairEconomy.Find(physicalMaterial.name) != null)
            {
                return true;
            }
            if (renderer.sharedMaterials.Any((Material m) => m != null && m.shader != null && (m.shader.name.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0 || (m.HasProperty("_UseMetalMask") && m.GetFloat("_UseMetalMask") > 0.5f))))
            {
                return true;
            }
            if (physicalMaterial != null)
            {
                try
                {
                    Material material = physicalMaterial.GetMaterial(PhysicalMaterialChannel.A);
                    if (material != null && material.shader != null && material.shader.name.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
                catch
                {
                }
            }
            return false;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            heatEffect.Dispose();
            SetSourceHidden(value: false);
            disposed = true;
            foreach (Material ownedMaterial in ownedMaterials)
            {
                if (ownedMaterial != null)
                {
                    RepairUiStereo.UnregisterEffect(ownedMaterial);
                    Destroy(ownedMaterial);
                }
            }
            foreach (Mesh ownedMesh in ownedMeshes)
            {
                if (ownedMesh != null)
                {
                    Destroy(ownedMesh);
                }
            }
            if (Root != null)
            {
                Destroy(Root.gameObject);
            }
            Root = null;
            Source = null;
            surfaces.Clear();
            grips.Clear();
            transforms.Clear();
            sourceVisibility.Clear();
            ownedMaterials.Clear();
            ownedMeshes.Clear();
            layoutOptions.Clear();
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(value);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(value);
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
    }
}
