using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using Il2CppAlta.Character;
using Il2CppAlta.Customization;
using Il2CppAlta.Intelligence;
using Il2CppAlta.Inventory;

namespace RepairShopQuest
{
    internal sealed class RepairCloneAnimation
    {
        internal readonly GameObject gameObject;

        internal readonly Transform transform;

        internal RepairCloneAnimation(GameObject host)
        {
            gameObject = host;
            transform = host.transform;
            Awake();
            OnEnable();
        }

        private sealed class SkinIdentity
        {
            internal SkinnedMeshRenderer Renderer;

            internal Mesh Mesh;

            internal Transform[] Bones;
        }

        private PlayerLikeSpectralGhost ghost;

        private CharacterDummy dummy;

        private Transform bodyRoot;

        private Transform skeletonRoot;

        private Transform chest;

        private Transform head;

        private Transform leftArm;

        private Transform rightArm;

        private Transform leftElbow;

        private Transform rightElbow;

        private Transform leftHand;

        private Transform rightHand;

        private Quaternion leftRest;

        private Quaternion rightRest;

        private Quaternion leftElbowRest;

        private Quaternion rightElbowRest;

        private Quaternion leftHandRest;

        private Quaternion rightHandRest;

        private Quaternion chestRest;

        private Quaternion headRest;

        private Vector3 headPosition;

        private Vector3 skeletonPosition;

        private Animator ownedAnimator;

        private Animator bodyPoseAnimator;

        private RuntimeAnimatorController bodyPoseController;

        private int bodyPoseIndex = int.MinValue;

        private bool animatorWasEnabled;

        private bool hasPose;

        private bool warnedMissingRig;

        private bool warnedBodyPose;

        private int shopRole;

        private bool shopWorking;

        private RepairWorkMode stationMode = RepairWorkMode.Metal;

        private bool stationTravelling;

        private bool carryWork;

        private Transform carryShop;

        private readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider> carryHits = Geo.Colliders(128);

        private bool forgeWork;

        private float forgeLower = 0.3f;

        private float forgeForward;

        private float forgePitch = 20f;

        private float forgeTilt = 25f;

        private Vector3 shopTarget;

        private float nextBindAt;

        private float nextHammerScan;

        private float idlePhase;

        private float workPhase;

        private float workBlend;

        private float toolBlend;

        private float inspectWait;

        private float inspectRemaining;

        private readonly RepairWorkerPresence presence = new RepairWorkerPresence();

        private readonly RepairWorkerWaveArm waveArm = new RepairWorkerWaveArm();

        private readonly RepairWorkerWaveSkin waveSkin = new RepairWorkerWaveSkin();

        private readonly RepairWorkerWorkArm leftWorkArm = new RepairWorkerWorkArm();

        private readonly RepairWorkerWorkArm rightWorkArm = new RepairWorkerWorkArm();

        private readonly RepairWorkerHand leftFingers = new RepairWorkerHand();

        private readonly RepairWorkerHand rightFingers = new RepairWorkerHand();

        private readonly RepairWorkerEyePose eyePose = new RepairWorkerEyePose();

        private readonly RepairWorkerGrip hammerGrip = new RepairWorkerGrip();

        private readonly Dictionary<int, float> greetedPlayers = new Dictionary<int, float>();

        private PlayerController attentionPlayer;

        private float nextAttentionScan;

        private float keepAttentionUntil;

        private static PlayerController[] cachedPlayers;

        private static float nextPlayerRefresh;

        private static float lastPlayerClock = float.NegativeInfinity;

        private static readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<RaycastHit> sightHits = Geo.Hits(24);

        private bool previewAttention;

        private bool greetingRequested;

        private Vector3 previewPoint;

        private Transform shopHammer;

        private Transform hammerParent;

        private bool ownsHammer;

        private Vector3 hammerPosition;

        private Vector3 hammerScale;

        private Quaternion hammerRotation;

        private Vector3 hammerFacePoint;

        private bool hammerFaceReady;

        private RepairWorkDescriptor workDescriptor;

        private RepairWorkVisual workVisual;

        private float workDescriptorAt;

        private float nextWorkScan;

        private float workProgress;

        private float workGrip;

        private float workApproachTime;

        private float nextWoodDiagnosticAt;

        private bool workFallback;

        private bool workLayoutChosen;

        private bool receivedWorkView;

        private int workLayoutIndex;

        private int workLayoutRevision = -1;

        private float workPalmRoll;

        private Transform woodBenchRoot;

        private Collider[] woodBenchSolids;

        private MeshCollider woodTabletop;

        private Transform[] woodHandBones;

        private Transform woodIndex;

        private Transform woodLittle;

        private Transform woodThumb;

        private Transform forgeRoot;

        private Transform forgeEnvironment;

        private Transform[] forgeHandBones;

        private int woodBenchLayers;

        private readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider> benchHits = Geo.Colliders(128);

        private SoundEffectSet workStrikeSound;

        private float nextSoundLookup;

        private readonly List<SkinIdentity> skinIdentity = new List<SkinIdentity>();

        internal float GreetingAmount => presence.WaveEnvelope;

        internal float WorkBlend => workBlend;

        internal string PresenceMode => presence.Mode;

        internal void RequestGreeting()
        {
            greetingRequested = true;
        }

        internal void PreviewAttention(Vector3 worldPoint, bool enabled)
        {
            previewPoint = worldPoint;
            previewAttention = enabled;
        }

        internal void SetCarryWork(bool enabled)
        {
            if (carryWork != enabled)
            {
                carryWork = enabled;
                ClearWorkVisual();
                workFallback = false;
                nextWorkScan = 0f;
                workPhase = (workBlend = (inspectWait = (inspectRemaining = 0f)));
            }
        }

        internal void SetForgeWork(bool heating)
        {
            if (forgeWork != heating)
            {
                forgeWork = heating;
                ClearWorkVisual();
                workFallback = false;
                nextWorkScan = 0f;
                workPhase = (workBlend = (inspectWait = (inspectRemaining = 0f)));
            }
        }

        internal void SetStationWork(RepairWorkMode mode, bool travelling)
        {
            if (mode != RepairWorkMode.Metal && mode != RepairWorkMode.Wood)
            {
                mode = RepairWorkMode.Metal;
            }
            bool num = stationMode != mode || stationTravelling != travelling;
            stationMode = mode;
            stationTravelling = travelling;
            if (num)
            {
                ClearWorkVisual();
                workFallback = false;
                nextWorkScan = 0f;
                workPhase = (workBlend = (inspectWait = (inspectRemaining = 0f)));
            }
            if (travelling && !carryWork)
            {
                ClearWorkVisual();
                workBlend = (workGrip = 0f);
            }
        }

        internal void SetShopWork(int role, bool working, Vector3 target)
        {
            if (role == 1 || role == 2)
            {
                if (shopRole != role)
                {
                    ClearHammer();
                    ClearWorkVisual();
                }
                shopRole = role;
                shopWorking = role == 2 && working;
                shopTarget = target;
                if (!shopWorking && (!carryWork || !stationTravelling))
                {
                    ClearWorkVisual();
                }
            }
        }

        internal void SetRepairWork(RepairWorkDescriptor descriptor, float sampledAt)
        {
            if (workDescriptor == null || descriptor == null || workDescriptor.JobId != descriptor.JobId || workDescriptor.ItemId != descriptor.ItemId || workDescriptor.ServiceId != descriptor.ServiceId || workDescriptor.Slot != descriptor.Slot)
            {
                ClearWorkVisual();
                workFallback = false;
                nextWorkScan = 0f;
                inspectWait = (inspectRemaining = 0f);
            }
            workDescriptor = descriptor;
            workDescriptorAt = sampledAt;
            receivedWorkView = descriptor != null;
            if (descriptor == null || !descriptor.Active)
            {
                ClearWorkVisual();
            }
        }

        internal void SetWorkVisual(RepairWorkVisual visual)
        {
            if (workVisual != visual)
            {
                ClearWorkVisual();
                workVisual = visual;
                workApproachTime = 0f;
                workGrip = 0f;
                if (forgeWork && workVisual != null)
                {
                    ConfigureForgePose(workVisual.Source);
                }
                if ((stationMode == RepairWorkMode.Wood || forgeWork || carryWork) && workVisual != null && workVisual.Root != null)
                {
                    workVisual.Root.gameObject.SetActive(value: false);
                }
            }
        }

        private void ConfigureForgePose(Pickup source)
        {
            forgeLower = 0.3f;
            forgeForward = 0f;
            forgePitch = 20f;
            forgeTilt = 25f;
            if (source == null)
            {
                return;
            }
            Vector3 lossyScale = source.transform.lossyScale;
            float num = Mathf.Max(Mathf.Abs(lossyScale.x), Mathf.Max(Mathf.Abs(lossyScale.y), Mathf.Abs(lossyScale.z)));
            if (num < 0.001f || float.IsNaN(num) || float.IsInfinity(num))
            {
                return;
            }
            Renderer[] componentsInChildren = Plat.All<Renderer>(source, true).ToArray();
            Bounds bounds = default(Bounds);
            bool flag = false;
            Renderer[] array = componentsInChildren;
            foreach (Renderer renderer in array)
            {
                if (!(renderer == null) && renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    if (!flag)
                    {
                        bounds = renderer.bounds;
                        flag = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }
            if (flag && bounds.size.magnitude / num > 1.15f)
            {
                forgeLower = 0.1f;
                forgePitch = 20f;
                forgeTilt = 20f;
            }
        }

        private void ClearWorkVisual()
        {
            if (workVisual != null)
            {
                workVisual.Dispose();
            }
            workVisual = null;
            woodBenchRoot = null;
            woodBenchSolids = null;
            woodTabletop = null;
            woodHandBones = null;
            woodIndex = (woodLittle = (woodThumb = null));
            woodBenchLayers = 0;
            forgeRoot = (forgeEnvironment = null);
            forgeHandBones = null;
            workGrip = 0f;
            workApproachTime = 0f;
            workLayoutChosen = false;
            workLayoutIndex = 0;
            workLayoutRevision = -1;
            workPalmRoll = 0f;
        }

        private void WoodVisualWait(string reason)
        {
            if (stationMode == RepairWorkMode.Wood && !forgeWork && !stationTravelling && !(Time.unscaledTime < nextWoodDiagnosticAt))
            {
                nextWoodDiagnosticAt = Time.unscaledTime + 15f;
                Core.Log("REPAIR_WOOD_VISUAL_WAIT item=" + ((workDescriptor != null) ? workDescriptor.ItemId : 0u) + " reason=" + reason);
            }
        }

        private void UpdateRepairWork(float now)
        {
            RepairWorkDescriptor descriptor = workDescriptor;
            bool flag = carryWork && stationTravelling;
            if (shopRole != 2 || (!flag && (!shopWorking || stationTravelling)) || descriptor == null || !descriptor.Active || now - workDescriptorAt > 12f)
            {
                ClearWorkVisual();
                return;
            }
            workProgress = Mathf.Clamp01(descriptor.Progress + Mathf.Max(0f, now - workDescriptorAt) / descriptor.Duration);
            if (workProgress >= 1f)
            {
                ClearWorkVisual();
                return;
            }
            if (workVisual != null && !workVisual.IsValid)
            {
                ClearWorkVisual();
            }
            if (now < nextWorkScan)
            {
                return;
            }
            nextWorkScan = now + 0.25f;
            RepairService repairService = Array.Find(Live.Services(), (RepairService s) => s.Entity != null && s.Entity.Identifier == descriptor.ServiceId);
            if (repairService == null || repairService.Restoring || repairService.Faulted || descriptor.Slot < 0 || descriptor.Slot >= repairService.Storage.Length || !repairService.State.jobs.Exists((ServiceJob j) => j.slot == descriptor.Slot && j.state == "paid"))
            {
                ClearWorkVisual();
                return;
            }
            PickupDock pickupDock = repairService.Storage[descriptor.Slot];
            Pickup pickup = ((pickupDock == null) ? null : RepairItems.Root(pickupDock.DockedPickup));
            if (pickup == null || pickup.Entity == null || pickup.Entity.Identifier != descriptor.ItemId || !pickup.IsDocked || pickup.IsInteractedWith)
            {
                ClearWorkVisual();
                return;
            }
            carryShop = repairService.Shop;
            if (!flag && stationMode == RepairWorkMode.Wood)
            {
                Transform transform = ((repairService.WoodWorkAnchor == null) ? null : repairService.WoodWorkAnchor.parent);
                if (transform == null || !transform.gameObject.activeInHierarchy)
                {
                    WoodVisualWait("wood bench unavailable");
                    ClearWorkVisual();
                    workFallback = false;
                    return;
                }
                if (workVisual != null && (woodBenchRoot != transform || !WoodBenchReady()))
                {
                    ClearWorkVisual();
                    workFallback = false;
                }
            }
            if (workVisual != null || workFallback)
            {
                return;
            }
            bool isDetachedMetal;
            bool num;
            if (RepairWorkVisual.TryCreate(pickup, bodyRoot, flag ? RepairWorkMode.Metal : stationMode, out var visual) || (flag && RepairWorkVisual.TryCreate(pickup, bodyRoot, RepairWorkMode.Wood, out visual)))
            {
                Vector3 vector = (flag ? bodyRoot.TransformPoint(new Vector3(0.15f, 0.85f, 0.65f)) : shopTarget);
                isDetachedMetal = visual.IsDetachedMetal;
                Transform anvilRoot = ((repairService.Shop == null) ? null : repairService.Shop.Find("ATT working anvil"));
                if (!isDetachedMetal)
                {
                    if (visual.Layout(bodyRoot, vector))
                    {
                        if (flag)
                        {
                            num = CarryGeometryClear(visual);
                            goto IL_0362;
                        }
                        goto IL_0364;
                    }
                }
                else if (!flag && !forgeWork && stationMode == RepairWorkMode.Metal)
                {
                    num = visual.TryLayoutDetachedOnAnvil(bodyRoot, vector, anvilRoot);
                    goto IL_0362;
                }
                goto IL_03d6;
            }
            goto IL_03e8;
            IL_0362:
            if (num)
            {
                goto IL_0364;
            }
            goto IL_03d6;
            IL_0364:
            SetWorkVisual(visual);
            if (isDetachedMetal)
            {
                visual.SetSourceHidden(value: true);
            }
            if (!flag && stationMode == RepairWorkMode.Wood && !ConfigureWoodBench(repairService.WoodWorkAnchor.parent))
            {
                WoodVisualWait("wood bench colliders unavailable");
                ClearWorkVisual();
                workFallback = false;
            }
            else if (forgeWork && !ConfigureForge(repairService.Shop))
            {
                ClearWorkVisual();
                workFallback = false;
            }
            goto IL_03e8;
            IL_03e8:
            nextWorkScan = now + 1f;
            return;
            IL_03d6:
            WoodVisualWait("no compatible workpiece layout");
            visual.Dispose();
            goto IL_03e8;
        }

        internal void SetHammerModel(Transform model)
        {
            if (!(shopHammer == model))
            {
                ClearHammer();
                if (!(model == null))
                {
                    shopHammer = model;
                    ownsHammer = false;
                    hammerParent = model.parent;
                    hammerPosition = model.localPosition;
                    hammerRotation = model.localRotation;
                    hammerScale = model.localScale;
                    CacheHammerFace();
                }
            }
        }

        private void Awake()
        {
            ghost = Plat.Get<PlayerLikeSpectralGhost>(gameObject);
            bodyRoot = RootOf(ghost) ?? transform;
        }

        private static Transform RootOf(PlayerLikeSpectralGhost source)
        {
            if (!(source != null) || !(source.Entity != null) || !(source.Entity.PrefabRoot != null))
            {
                return null;
            }
            return source.Entity.PrefabRoot.transform;
        }

        internal void Initialize(PlayerLikeSpectralGhost source)
        {
            if (!(ghost == source))
            {
                RestorePose();
                ghost = source;
                bodyRoot = RootOf(source) ?? transform;
                nextBindAt = 0f;
            }
        }

        private bool BindRig()
        {
            if (ghost == null)
            {
                return false;
            }
            PlayerCustomizationHandler playerCustomizationHandler = Game.GhostHandler(ghost);
            if (playerCustomizationHandler == null || playerCustomizationHandler.CharacterDummy == null)
            {
                return false;
            }
            CharacterDummy characterDummy = playerCustomizationHandler.CharacterDummy;
            int num = RepairWorkerBodyPose.Requested(characterDummy);
            if (characterDummy == dummy && hasPose && skeletonRoot != null && characterDummy.Root == skeletonRoot && leftArm != null && rightArm != null && leftElbow != null && rightElbow != null && bodyPoseAnimator == characterDummy.Animator && bodyPoseController == ((characterDummy.Animator == null) ? null : characterDummy.Animator.runtimeAnimatorController) && bodyPoseIndex == num && SkinsMatch(characterDummy.transform))
            {
                return true;
            }
            RestorePose();
            dummy = characterDummy;
            if (!RepairWorkerBodyPose.TryApply(characterDummy.Animator, num))
            {
                if (!warnedBodyPose && true)
                {
                    warnedBodyPose = true;
                    Core.Warn("Repair worker waiting for native body pose " + num + ".");
                }
                return false;
            }
            warnedBodyPose = false;
            skeletonRoot = characterDummy.Root;
            chest = (head = (leftArm = (rightArm = (leftElbow = (rightElbow = (leftHand = (rightHand = null)))))));
            if (skeletonRoot == null)
            {
                return false;
            }
            eyePose.Bind(skeletonRoot);
            Dictionary<string, Transform> dictionary = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            if (characterDummy.RigMap != null)
            {
                foreach (var bone in characterDummy.RigMap.Bones)
                {
                    if (bone.Key != null && bone.Value != null)
                    {
                        dictionary[bone.Key.name] = bone.Value;
                    }
                }
            }
            Transform[] componentsInChildren = Plat.All<Transform>(skeletonRoot, true).ToArray();
            foreach (Transform transform in componentsInChildren)
            {
                string key = RepairWorkerBones.Name(transform);
                if (!dictionary.ContainsKey(key))
                {
                    dictionary[key] = transform;
                }
            }
            chest = RepairWorkerBones.Find(dictionary, "chest_jnt");
            head = RepairWorkerBones.Find(dictionary, "head_jnt");
            leftArm = RepairWorkerBones.Find(dictionary, "shoulder_L_jnt");
            rightArm = RepairWorkerBones.Find(dictionary, "shoulder_R_jnt");
            leftElbow = RepairWorkerBones.Find(dictionary, "elbow_L_jnt");
            rightElbow = RepairWorkerBones.Find(dictionary, "elbow_R_jnt");
            leftHand = RepairWorkerBones.Find(dictionary, "wrist_L_jnt") ?? RepairWorkerBones.Find(dictionary, "hand_L_jnt");
            rightHand = RepairWorkerBones.Find(dictionary, "wrist_R_jnt") ?? RepairWorkerBones.Find(dictionary, "hand_R_jnt");
            if (characterDummy.Animator != null && characterDummy.Animator.isHuman)
            {
                leftArm = leftArm ?? characterDummy.Animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightArm = rightArm ?? characterDummy.Animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                leftElbow = leftElbow ?? characterDummy.Animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightElbow = rightElbow ?? characterDummy.Animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                leftHand = leftHand ?? characterDummy.Animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rightHand = rightHand ?? characterDummy.Animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            if (leftArm == null || rightArm == null || leftElbow == null || rightElbow == null)
            {
                if (!warnedMissingRig)
                {
                    warnedMissingRig = true;
                    Core.Warn("Repair worker animation waiting for active native arm joints on " + ghost.Entity.Identifier + ".");
                }
                return false;
            }
            warnedMissingRig = false;
            leftRest = leftArm.localRotation;
            rightRest = rightArm.localRotation;
            leftElbowRest = leftElbow.localRotation;
            rightElbowRest = rightElbow.localRotation;
            if (leftHand != null)
            {
                leftHandRest = leftHand.localRotation;
            }
            if (rightHand != null)
            {
                rightHandRest = rightHand.localRotation;
            }
            if (chest != null)
            {
                chestRest = chest.localRotation;
            }
            if (head != null)
            {
                headRest = head.localRotation;
                headPosition = head.localPosition;
            }
            skeletonPosition = skeletonRoot.localPosition;
            leftFingers.Bind(leftHand, dictionary, characterDummy.transform, rightHand: false);
            rightFingers.Bind(rightHand, dictionary, characterDummy.transform, rightHand: true);
            waveArm.Bind(leftArm, leftElbow, leftHand, leftElbowRest, dictionary);
            waveSkin.Bind(characterDummy.transform, leftArm, leftElbow, leftHand);
            leftWorkArm.Bind(leftArm, leftElbow, leftHand, leftRest, leftElbowRest, leftHandRest);
            rightWorkArm.Bind(rightArm, rightElbow, rightHand, rightRest, rightElbowRest, rightHandRest);
            CaptureSkins(characterDummy.transform);
            ownedAnimator = characterDummy.Animator;
            if (ownedAnimator != null)
            {
                animatorWasEnabled = ownedAnimator.enabled;
                ownedAnimator.enabled = false;
            }
            bodyPoseAnimator = characterDummy.Animator;
            bodyPoseController = ((characterDummy.Animator == null) ? null : characterDummy.Animator.runtimeAnimatorController);
            bodyPoseIndex = num;
            uint num2 = ((ghost.Entity != null) ? ghost.Entity.Identifier : ((uint)gameObject.GetInstanceID()));
            presence.Initialize(num2);
            workPhase = (workBlend = (toolBlend = 0f));
            idlePhase = (float)(num2 % 701) * 0.017f;
            attentionPlayer = null;
            nextAttentionScan = (nextHammerScan = 0f);
            greetedPlayers.Clear();
            hasPose = true;
            Core.Log("REPAIR_WORKER_ANIMATION_BOUND id=" + num2 + " head=" + (head != null) + " hands=" + (leftHand != null && rightHand != null) + " bodyType=" + num);
            return true;
        }

        private void CaptureSkins(Transform root)
        {
            skinIdentity.Clear();
            SkinnedMeshRenderer[] componentsInChildren = Plat.All<SkinnedMeshRenderer>(root, true).ToArray();
            foreach (SkinnedMeshRenderer skinnedMeshRenderer in componentsInChildren)
            {
                skinIdentity.Add(new SkinIdentity
                {
                    Renderer = skinnedMeshRenderer,
                    Mesh = skinnedMeshRenderer.sharedMesh,
                    Bones = skinnedMeshRenderer.bones
                });
            }
        }

        private bool SkinsMatch(Transform root)
        {
            SkinnedMeshRenderer[] componentsInChildren = Plat.All<SkinnedMeshRenderer>(root, true).ToArray();
            if (componentsInChildren.Length != skinIdentity.Count)
            {
                return false;
            }
            foreach (SkinIdentity item in skinIdentity)
            {
                if (item.Renderer == null || item.Renderer.sharedMesh != item.Mesh)
                {
                    return false;
                }
                bool flag = false;
                SkinnedMeshRenderer[] array = componentsInChildren;
                for (int i = 0; i < array.Length; i++)
                {
                    if (array[i] == item.Renderer)
                    {
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                {
                    return false;
                }
                Transform[] bones = item.Renderer.bones;
                if (bones.Length != item.Bones.Length)
                {
                    return false;
                }
                for (int j = 0; j < bones.Length; j++)
                {
                    if (bones[j] != item.Bones[j])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        internal void LateUpdate()
        {
            AdvanceAnimation(Time.deltaTime, Time.unscaledTime);
        }

        internal void AdvanceAnimation(float dt, float now)
        {
            bodyRoot = RootOf(ghost) ?? bodyRoot;
            if (bodyRoot == null || float.IsNaN(dt) || float.IsInfinity(dt))
            {
                return;
            }
            if (!hasPose || now >= nextBindAt)
            {
                nextBindAt = now + 0.5f;
                if (!BindRig())
                {
                    return;
                }
            }
            if (greetingRequested)
            {
                presence.RequestWave();
                greetingRequested = false;
            }
            dt = Mathf.Clamp(dt, 0f, 0.05f);
            if (dt > 0f)
            {
                idlePhase = (idlePhase + dt * 1.25f) % ((float)Math.PI * 20f);
                UpdateAttention(now, dt);
                UpdateRepairWork(now);
                bool flag = shopRole == 2 && ((shopWorking && !stationTravelling) || (carryWork && stationTravelling)) && ((stationMode != RepairWorkMode.Wood) ? (!receivedWorkView || (workVisual != null && !workFallback)) : (workVisual != null && !workFallback));
                workBlend = Mathf.MoveTowards(workBlend, flag ? 1 : 0, dt * 2f);
                toolBlend = Mathf.MoveTowards(toolBlend, (shopRole == 2) ? 1 : 0, dt * 2.5f);
                float num = workPhase;
                if (!stationTravelling && workVisual != null && workBlend > 0.95f && (workGrip > 0.9f || workVisual.IsDetachedMetal))
                {
                    if (inspectRemaining > 0f)
                    {
                        inspectRemaining = Mathf.Max(0f, inspectRemaining - dt);
                    }
                    else
                    {
                        inspectWait += dt;
                        workPhase = (workPhase + dt / ((stationMode == RepairWorkMode.Wood) ? 1.7f : 1.35f)) % 1f;
                        if (inspectWait >= 14f && workPhase < num)
                        {
                            inspectWait = 0f;
                            inspectRemaining = 1.25f;
                            workPhase = 0f;
                        }
                    }
                }
                else if (!stationTravelling && workBlend > 0.001f && (stationMode != RepairWorkMode.Wood || workGrip > 0.9f))
                {
                    workPhase = (workPhase + dt / ((stationMode == RepairWorkMode.Wood) ? 1.7f : 1.35f)) % 1f;
                }
                if (shopRole == 2 && now >= nextHammerScan)
                {
                    nextHammerScan = now + 1f;
                    ResolveHammer();
                }
                if (!forgeWork && num < 0.88f && workPhase >= 0.88f && workVisual != null && (workGrip > 0.9f || workVisual.IsDetachedMetal) && workBlend > 0.95f)
                {
                    PlayWorkStrike(now);
                }
            }
            ApplyPose();
            if (dt > 0f && carryWork && stationTravelling && workVisual != null && workLayoutChosen && workGrip > 0.9f && (!CarryHandClear() || !CarryGeometryClear(workVisual)))
            {
                ClearWorkVisual();
                workFallback = true;
            }
            if (dt > 0f && !stationTravelling && stationMode == RepairWorkMode.Wood && workVisual != null && workLayoutChosen && workGrip > 0.9f)
            {
                if (!WoodBenchReady())
                {
                    ClearWorkVisual();
                    workFallback = false;
                }
                else if (!WoodHandClear() || workVisual.NativePalmError(leftFingers.PalmRotation) > 10f || !WoodWorkSupported(workVisual.WorkPoint))
                {
                    WoodVisualWait("hand, native grip or bench support safety check");
                    ClearWorkVisual();
                    workFallback = true;
                }
            }
            if (dt > 0f && forgeWork && workVisual != null && workLayoutChosen && workGrip > 0.9f && (!ForgeHandClear() || !ForgeBodyClear() || workVisual.NativePalmError(leftFingers.PalmRotation) > 10f))
            {
                ClearWorkVisual();
                workFallback = true;
            }
            if (dt > 0f && workVisual != null)
            {
                workVisual.UpdateHeatEffects(workProgress, !stationTravelling && workBlend > 0.95f && (workGrip > 0.9f || workVisual.IsDetachedMetal));
                if (workBlend > 0.95f && workGrip < 0.9f && !workVisual.IsDetachedMetal)
                {
                    workApproachTime += dt;
                }
                else
                {
                    workApproachTime = 0f;
                }
                if (workApproachTime > 0.8f)
                {
                    WoodVisualWait("hand did not reach grip");
                    ClearWorkVisual();
                    workFallback = true;
                }
            }
        }

        private void UpdateAttention(float now, float dt)
        {
            Vector3 vector = ((head != null) ? head.position : (bodyRoot.position + bodyRoot.up * 1.5f));
            if (attentionPlayer != null && !Eligible(attentionPlayer, vector, 8f))
            {
                attentionPlayer = null;
            }
            if (!previewAttention && now >= nextAttentionScan)
            {
                nextAttentionScan = now + 0.75f;
                PlayerController playerController = attentionPlayer;
                float num = ((playerController != null && Visible(playerController, vector)) ? (Vector3.Distance(vector, PlayerHead(playerController)) - 0.8f) : float.PositiveInfinity);
                if (float.IsInfinity(num))
                {
                    playerController = null;
                }
                PlayerController[] array = Players(now);
                foreach (PlayerController playerController2 in array)
                {
                    if (Eligible(playerController2, vector, 6f) && Visible(playerController2, vector) && (!(playerController != null) || !(now < keepAttentionUntil) || !(playerController2 != playerController)))
                    {
                        float num2 = Vector3.Distance(vector, PlayerHead(playerController2));
                        if (num2 < num)
                        {
                            playerController = playerController2;
                            num = num2;
                        }
                    }
                }
                if (playerController != attentionPlayer)
                {
                    attentionPlayer = playerController;
                    keepAttentionUntil = now + 2.5f;
                }
                if (greetedPlayers.Count > 128)
                {
                    greetedPlayers.Clear();
                }
            }
            bool flag = shopRole == 2 && !stationTravelling && (shopWorking || workBlend > 0.02f);
            bool flag2 = previewAttention || attentionPlayer != null;
            Vector3 vector2 = (previewAttention ? previewPoint : ((attentionPlayer != null) ? PlayerHead(attentionPlayer) : (vector + bodyRoot.forward * 3f)));
            if (flag)
            {
                vector2 = shopTarget + bodyRoot.up * 0.1f;
            }
            float value;
            bool flag3 = !previewAttention && attentionPlayer != null && (!greetedPlayers.TryGetValue(attentionPlayer.GetInstanceID(), out value) || now - value > 60f);
            presence.Step(dt, now, new RepairWorkerPresenceInput
            {
                Working = (flag || shopRole == 0),
                HasLookTarget = (flag || flag2),
                LocalLookDirection = bodyRoot.InverseTransformDirection(vector2 - vector),
                TargetDistance = (flag2 ? Vector3.Distance(bodyRoot.position, previewAttention ? previewPoint : attentionPlayer.PlayerFeetPosition) : float.PositiveInfinity),
                TargetEntered = flag3
            });
            if (flag3 && presence.WaveEnvelope > 0f)
            {
                greetedPlayers[attentionPlayer.GetInstanceID()] = now;
            }
        }

        private static PlayerController[] Players(float now)
        {
            if (cachedPlayers == null || now >= nextPlayerRefresh || now < lastPlayerClock)
            {
                cachedPlayers = Plat.Scene<PlayerController>();
                nextPlayerRefresh = now + 0.8f;
            }
            lastPlayerClock = now;
            return cachedPlayers;
        }

        private static Vector3 PlayerHead(PlayerController player)
        {
            if (!(player.Head != null))
            {
                return player.PlayerFeetPosition + Vector3.up * 1.5f;
            }
            return player.Head.position;
        }

        private bool Eligible(PlayerController player, Vector3 origin, float radius)
        {
            if (player == null || !player.isActiveAndEnabled || player.transform == bodyRoot || player.transform.IsChildOf(bodyRoot))
            {
                return false;
            }
            Vector3 vector = PlayerHead(player) - origin;
            if (vector.sqrMagnitude > 0.01f && vector.sqrMagnitude <= radius * radius)
            {
                return Vector3.Dot(vector.normalized, bodyRoot.forward) > 0.05f;
            }
            return false;
        }

        private bool Visible(PlayerController player, Vector3 origin)
        {
            Vector3 vector = PlayerHead(player);
            Vector3 vector2 = vector - origin;
            int num = Physics.RaycastNonAlloc(origin, vector2.normalized, sightHits, vector2.magnitude, -5, QueryTriggerInteraction.Ignore);
            if (num == sightHits.Length)
            {
                return false;
            }
            for (int i = 0; i < num; i++)
            {
                RaycastHit raycastHit = sightHits[i];
                Transform transform = raycastHit.transform;
                if (!(transform == null) && !(transform == bodyRoot) && !transform.IsChildOf(bodyRoot) && !(transform == player.transform) && !transform.IsChildOf(player.transform) && !(Vector3.Distance(raycastHit.point, vector) < 0.2f))
                {
                    return false;
                }
            }
            return true;
        }

        private void ApplyPose()
        {
            if (hasPose && !(bodyRoot == null) && !(skeletonRoot == null) && !(dummy == null) && !(dummy.Root != skeletonRoot))
            {
                waveArm.RestoreForearm();
                leftWorkArm.RestoreForearm();
                rightWorkArm.RestoreForearm();
                if (ownedAnimator != null)
                {
                    ownedAnimator.enabled = false;
                }
                leftArm.localRotation = leftRest;
                rightArm.localRotation = rightRest;
                leftElbow.localRotation = leftElbowRest;
                rightElbow.localRotation = rightElbowRest;
                if (leftHand != null)
                {
                    leftHand.localRotation = leftHandRest;
                }
                if (rightHand != null)
                {
                    rightHand.localRotation = rightHandRest;
                }
                if (workVisual != null && !workVisual.IsValid)
                {
                    ClearWorkVisual();
                }
                Vector3 vector = bodyRoot.right * presence.SideShift;
                if (workVisual != null)
                {
                    vector += (bodyRoot.forward * 0.2f - bodyRoot.up * 0.04f) * workBlend;
                }
                if (forgeWork && workVisual != null)
                {
                    vector += (bodyRoot.forward * forgeForward - bodyRoot.up * forgeLower) * workBlend;
                }
                skeletonRoot.localPosition = skeletonPosition + ((skeletonRoot.parent != null) ? skeletonRoot.parent.InverseTransformVector(vector) : vector);
                float num = ((stationMode == RepairWorkMode.Wood && workVisual != null) ? (-15f * workBlend) : 0f);
                CloneMotionMath.Pose(chest, chestRest, bodyRoot, presence.ChestPitch + (forgeWork ? (forgePitch * workBlend) : 0f), presence.ChestYaw + num, presence.ChestRoll);
                float num2 = ((inspectRemaining <= 0f) ? 0f : Mathf.Clamp01(Mathf.Min((1.25f - inspectRemaining) / 0.25f, inspectRemaining / 0.25f)));
                if (head != null)
                {
                    head.localPosition = headPosition;
                    CloneMotionMath.Pose(head, headRest, bodyRoot, presence.HeadPitch - presence.ChestPitch + presence.Nod + num2 * 5f, presence.HeadYaw - presence.ChestYaw - num, 0f);
                }
                PoseArm(leftArm, leftElbow, leftHand, leftRest, -1f, 0f);
                PoseArm(rightArm, rightElbow, rightHand, rightRest, 1f, toolBlend);
                if (workVisual != null)
                {
                    PlaceHammer();
                }
                ApplyWorkPose();
                leftFingers.Apply(Mathf.Lerp(1f, 0.12f, presence.WaveEnvelope), workGrip);
                rightFingers.Apply(1f, toolBlend);
                if (workVisual == null)
                {
                    ApplyGreeting();
                }
                else
                {
                    waveSkin.Apply(0f);
                }
                PlaceHammer();
                eyePose.Apply();
            }
        }

        private void PoseArm(Transform arm, Transform elbow, Transform wrist, Quaternion rest, float side, float carry)
        {
            if (!(arm == null) && !(elbow == null) && !(arm.parent == null))
            {
                float num = Mathf.Sin(idlePhase) * 0.007f * (1f - workBlend);
                Vector3 direction = -bodyRoot.up + bodyRoot.right * (side * (0.07f + carry * 0.18f)) + bodyRoot.forward * (num + carry * 0.16f);
                arm.localRotation = CloneMotionMath.Aim(rest, elbow.localPosition, arm.parent.InverseTransformDirection(direction));
                if (!(wrist == null))
                {
                    float num2 = Vector3.Distance(arm.position, elbow.position) + Vector3.Distance(elbow.position, wrist.position);
                    CloneMotionMath.Reach(arm, elbow, wrist, arm.position + direction.normalized * (num2 * Mathf.Lerp(0.96f, 0.87f, carry)), -bodyRoot.forward + bodyRoot.right * side * 0.2f);
                }
            }
        }

        private void ApplyWorkPose()
        {
            if (shopRole != 2 || workBlend <= 0.0001f || rightHand == null)
            {
                return;
            }
            if (stationTravelling)
            {
                if (carryWork && workVisual != null && workVisual.IsValid)
                {
                    ApplyRepairGrip();
                }
                return;
            }
            float num = ((workPhase < 0.72f) ? Mathf.SmoothStep(0f, 1f, workPhase / 0.72f) : (1f - Mathf.SmoothStep(0f, 1f, (workPhase - 0.72f) / 0.16f)));
            if (workVisual != null && workVisual.IsValid)
            {
                if (workVisual.IsDetachedMetal)
                {
                    if (hammerGrip.IsBound && hammerFaceReady)
                    {
                        ApplyRepairHammer(num);
                    }
                    workVisual.ApplyHeat(workProgress);
                    return;
                }
                ApplyRepairGrip();
                if (workVisual == null)
                {
                    return;
                }
                if (forgeWork)
                {
                    workVisual.ApplyHeat(workProgress);
                }
                else if (stationMode != RepairWorkMode.Wood || !(workGrip < 0.9f))
                {
                    if (hammerGrip.IsBound && hammerFaceReady)
                    {
                        ApplyRepairHammer(num);
                    }
                    workVisual.ApplyHeat(workProgress);
                }
                return;
            }
            workGrip = 0f;
            if (forgeWork || stationMode == RepairWorkMode.Wood)
            {
                return;
            }
            Vector3 target = shopTarget - bodyRoot.forward * 0.38f + bodyRoot.right * 0.025f + bodyRoot.up * (0.35f + num * 0.22f);
            Vector3 target2 = shopTarget - bodyRoot.right * 0.2f - bodyRoot.forward * 0.43f + bodyRoot.up * 0.37f;
            rightWorkArm.Reach(target, bodyRoot.right - bodyRoot.forward * 0.3f, workBlend);
            if (leftHand != null)
            {
                leftWorkArm.Reach(target2, -bodyRoot.right - bodyRoot.forward * 0.3f, workBlend * (1f - presence.WaveEnvelope));
            }
            Vector3 vector = shopTarget + bodyRoot.up * (num * 0.6f) - rightFingers.PalmPosition;
            if (vector.sqrMagnitude > 0.0001f && hammerGrip.IsBound && RepairWorkerProps.HammerGrip != null)
            {
                RepairHammerGripData repairHammerGripData = RepairWorkerProps.HammerGrip;
                Vector3 vector2 = Vector3.ProjectOnPlane(bodyRoot.up, vector.normalized);
                if (vector2.sqrMagnitude < 0.0001f)
                {
                    vector2 = Vector3.ProjectOnPlane(bodyRoot.forward, vector.normalized);
                }
                Quaternion rotation = Quaternion.LookRotation(repairHammerGripData.strikeAxis, repairHammerGripData.axis);
                Quaternion desiredWrist = Quaternion.LookRotation(vector.normalized, vector2.normalized) * Quaternion.Inverse(rotation) * Quaternion.Inverse(hammerGrip.ModelRotationInPalm) * Quaternion.Inverse(rightFingers.PalmFrame);
                rightWorkArm.AimTool(desiredWrist, workBlend);
            }
        }

        private void ApplyRepairGrip()
        {
            workGrip = 0f;
            if (workVisual == null || !workVisual.UsableGrip || leftHand == null)
            {
                return;
            }
            if (workLayoutRevision != workVisual.LayoutRevision)
            {
                workLayoutChosen = false;
            }
            bool flag = stationMode == RepairWorkMode.Wood || stationTravelling || forgeWork;
            if (!workLayoutChosen && workBlend > (flag ? 0.999f : 0.95f))
            {
                ChooseWorkLayout();
            }
            if (workVisual == null || (flag && !workLayoutChosen))
            {
                return;
            }
            Vector3 gripPosition = workVisual.GripPosition;
            Quaternion quaternion = Quaternion.AngleAxis(workPalmRoll, workVisual.GripAxis) * workVisual.PalmRotation;
            Quaternion wrist = quaternion * Quaternion.Inverse(leftFingers.PalmFrame);
            float amount = Mathf.Clamp01(workBlend * 3f);
            SolveWorkGrip(gripPosition, wrist, amount);
            float num = Vector3.Distance(leftFingers.PalmPosition, gripPosition);
            float num2 = Quaternion.Angle(leftFingers.PalmRotation, quaternion);
            bool flag2 = !forgeWork || (workLayoutChosen && num <= 0.025f && workVisual.NativePalmError(leftFingers.PalmRotation) <= 10f && ForgeHandClear() && ForgeBodyClear());
            if (num <= 0.03f && num2 <= 35f && flag2)
            {
                workGrip = amount;
                workVisual.SetSourceHidden(value: true);
                if (flag)
                {
                    workVisual.Root.gameObject.SetActive(value: true);
                }
            }
            else if (flag)
            {
                workVisual.SetSourceHidden(value: false);
                workVisual.Root.gameObject.SetActive(value: false);
            }
        }

        private void SolveWorkGrip(Vector3 target, Quaternion wrist, float amount)
        {
            for (int i = 0; i < 6; i++)
            {
                leftWorkArm.Reach(target - leftHand.TransformVector(leftFingers.PalmOffset), -bodyRoot.right - bodyRoot.forward * 0.3f, amount);
                leftWorkArm.AimTool(wrist, amount);
            }
        }

        private void ChooseWorkLayout()
        {
            Quaternion localRotation = leftArm.localRotation;
            Quaternion localRotation2 = leftElbow.localRotation;
            Quaternion localRotation3 = leftHand.localRotation;
            Transform transform = ((leftHand.parent != leftElbow) ? leftHand.parent : null);
            Quaternion quaternion = ((transform != null) ? transform.localRotation : Quaternion.identity);
            bool flag = carryWork && stationTravelling;
            float num = float.PositiveInfinity;
            int num2 = ((stationMode == RepairWorkMode.Wood || flag || forgeWork) ? (-1) : 0);
            float num3 = 0f;
            for (int i = 0; i < workVisual.LayoutOptionCount; i++)
            {
                if (!SelectWorkLayout(i) || (flag && (!CarryBodyClear(workVisual) || !CarryGeometryClear(workVisual))) || (!flag && stationMode == RepairWorkMode.Wood && !WoodWorkSupported(workVisual.WorkPoint)))
                {
                    continue;
                }
                float[] array = ((!workVisual.GripAllowsRoll) ? new float[1] : ((stationMode != RepairWorkMode.Wood) ? new float[5] { 0f, -30f, 30f, -60f, 60f } : new float[7] { 0f, -30f, 30f, -60f, 60f, -90f, 90f }));
                foreach (float num4 in array)
                {
                    if ((stationMode == RepairWorkMode.Wood || flag || forgeWork) && !workVisual.MatchesNativeGrip(num4))
                    {
                        continue;
                    }
                    leftArm.localRotation = localRotation;
                    leftElbow.localRotation = localRotation2;
                    if (transform != null)
                    {
                        transform.localRotation = quaternion;
                    }
                    leftHand.localRotation = localRotation3;
                    Quaternion quaternion2 = Quaternion.AngleAxis(num4, workVisual.GripAxis) * workVisual.PalmRotation;
                    SolveWorkGrip(workVisual.GripPosition, quaternion2 * Quaternion.Inverse(leftFingers.PalmFrame), 1f);
                    float num5 = Vector3.Distance(leftFingers.PalmPosition, workVisual.GripPosition);
                    float num6 = Quaternion.Angle(leftFingers.PalmRotation, quaternion2);
                    if ((stationMode == RepairWorkMode.Wood || flag || forgeWork) && (num5 > 0.025f || num6 > 25f))
                    {
                        continue;
                    }
                    if (flag)
                    {
                        leftFingers.Apply(1f, 1f);
                        if (workVisual.NativePalmError(leftFingers.PalmRotation) > 10f || !CarryHandClear())
                        {
                            continue;
                        }
                    }
                    else if (stationMode == RepairWorkMode.Wood)
                    {
                        leftFingers.Apply(1f, 1f);
                        if (workVisual.NativePalmError(leftFingers.PalmRotation) > 10f || !WoodHandClear())
                        {
                            continue;
                        }
                    }
                    else if (forgeWork)
                    {
                        leftFingers.Apply(1f, 1f);
                        if (workVisual.NativePalmError(leftFingers.PalmRotation) > 10f || !ForgeHandClear() || !ForgeBodyClear())
                        {
                            continue;
                        }
                    }
                    float num7 = num5 + num6 * 0.0004f + Mathf.Abs(num4) * 1E-05f;
                    if (stationMode == RepairWorkMode.Wood)
                    {
                        float num8 = Mathf.Abs(Vector3.Dot(leftFingers.PalmRotation * Vector3.up, bodyRoot.up));
                        if (num8 > 0.75f)
                        {
                            continue;
                        }
                        float num9 = ((transform == null) ? 0f : Quaternion.Angle(quaternion, transform.localRotation));
                        num7 += num8 * 0.03f + Mathf.Max(0f, num9 - 55f) * 0.0005f;
                    }
                    if (num5 > 0.025f || num6 > 25f)
                    {
                        num7 += 1f;
                    }
                    if (num7 < num)
                    {
                        num = num7;
                        num2 = i;
                        num3 = num4;
                    }
                }
            }
            leftArm.localRotation = localRotation;
            leftElbow.localRotation = localRotation2;
            if (transform != null)
            {
                transform.localRotation = quaternion;
            }
            leftHand.localRotation = localRotation3;
            if (stationMode == RepairWorkMode.Wood || flag || forgeWork)
            {
                leftFingers.Restore();
            }
            if (num2 < 0)
            {
                WoodVisualWait("no supported reachable native grip pose");
                ClearWorkVisual();
                workFallback = true;
            }
            else
            {
                SelectWorkLayout(num2);
                workLayoutIndex = num2;
                workLayoutRevision = workVisual.LayoutRevision;
                workPalmRoll = num3;
                workLayoutChosen = true;
            }
        }

        private bool SelectWorkLayout(int index)
        {
            if (workVisual == null || !workVisual.SelectLayout(index))
            {
                return false;
            }
            if (forgeWork)
            {
                Vector3 workPoint = workVisual.WorkPoint;
                Vector3 vector = Vector3.ProjectOnPlane(workVisual.GripPosition - workPoint, bodyRoot.up);
                if (vector.sqrMagnitude < 0.0001f)
                {
                    return false;
                }
                workVisual.Root.RotateAround(workPoint, Vector3.Cross(vector.normalized, bodyRoot.up), forgeTilt);
            }
            return true;
        }

        private bool ConfigureForge(Transform shop)
        {
            forgeRoot = ((shop == null) ? null : shop.Find("Forge Heating Area"));
            forgeEnvironment = ((shop == null) ? null : shop.root);
            forgeHandBones = null;
            if (forgeRoot != null && forgeRoot.gameObject.activeInHierarchy && forgeEnvironment != null)
            {
                return Plat.All<Collider>(forgeRoot, true).ToArray().Length != 0;
            }
            return false;
        }

        private bool ForgeOverlap(Vector3 a, Vector3 b, float radius)
        {
            if (forgeRoot == null || forgeEnvironment == null || !forgeRoot.gameObject.activeInHierarchy)
            {
                return true;
            }
            PhysicsScene physicsScene = forgeRoot.gameObject.scene.GetPhysicsScene();
            if (!physicsScene.IsValid())
            {
                return true;
            }
            int num = physicsScene.OverlapCapsule(a, b, radius, benchHits, -1, QueryTriggerInteraction.Ignore);
            if (num >= benchHits.Length)
            {
                return true;
            }
            for (int i = 0; i < num; i++)
            {
                if (benchHits[i] != null && benchHits[i].enabled && benchHits[i].gameObject.activeInHierarchy && benchHits[i].transform.IsChildOf(forgeEnvironment))
                {
                    return true;
                }
            }
            return false;
        }

        private bool ForgeBodyClear()
        {
            if (forgeRoot == null || bodyRoot == null || skeletonRoot == null || head == null)
            {
                return false;
            }
            Vector3 vector = skeletonRoot.position - bodyRoot.up * 0.42f;
            Vector3 position = head.position;
            if (Vector3.Dot(vector - forgeRoot.parent.position, bodyRoot.up) < 0.18f)
            {
                return false;
            }
            return !ForgeOverlap(vector, position, 0.19f);
        }

        private bool ForgeHandClear()
        {
            if (workVisual == null || leftHand == null || forgeRoot == null)
            {
                return false;
            }
            Transform[] componentsInChildren;
            if (forgeHandBones == null)
            {
                List<Transform> list = new List<Transform>();
                componentsInChildren = Plat.All<Transform>(leftHand, true).ToArray();
                foreach (Transform transform in componentsInChildren)
                {
                    string text = RepairWorkerBones.Name(transform);
                    if (text.EndsWith("_L_jnt", StringComparison.OrdinalIgnoreCase) && (text.StartsWith("thumb", StringComparison.OrdinalIgnoreCase) || text.StartsWith("index", StringComparison.OrdinalIgnoreCase) || text.StartsWith("middle", StringComparison.OrdinalIgnoreCase) || text.StartsWith("ring", StringComparison.OrdinalIgnoreCase) || text.StartsWith("little", StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(transform);
                    }
                }
                forgeHandBones = list.ToArray();
            }
            if (forgeHandBones.Length == 0)
            {
                return false;
            }
            componentsInChildren = forgeHandBones;
            foreach (Transform transform2 in componentsInChildren)
            {
                if (transform2 == null || !workVisual.ClearOfMetal(transform2.position, 0.012f))
                {
                    return false;
                }
                Vector3 b = ((transform2.parent != null && transform2.parent != leftHand) ? transform2.parent.position : transform2.position);
                for (int j = 1; j <= 3; j++)
                {
                    if (!workVisual.ClearOfMetal(Vector3.Lerp(transform2.position, b, (float)j * 0.25f), 0.012f))
                    {
                        return false;
                    }
                }
                if (ForgeOverlap(transform2.position, b, 0.01f))
                {
                    return false;
                }
            }
            Vector3 vector = leftFingers.PalmPosition - leftFingers.PalmRotation * Vector3.up * 0.025f;
            if (workVisual.ClearOfMetal(vector, 0.025f))
            {
                return !ForgeOverlap(vector, vector, 0.025f);
            }
            return false;
        }

        private bool WoodHandClear()
        {
            return WorkHandClear(requireBench: true);
        }

        private bool CarryHandClear()
        {
            return WorkHandClear(requireBench: false);
        }

        private bool WorkHandClear(bool requireBench)
        {
            if (workVisual == null || leftHand == null || (requireBench && (woodBenchRoot == null || !woodBenchRoot.gameObject.activeInHierarchy)))
            {
                return false;
            }
            Transform[] componentsInChildren;
            if (woodHandBones == null)
            {
                List<Transform> list = new List<Transform>();
                componentsInChildren = Plat.All<Transform>(leftHand, true).ToArray();
                foreach (Transform transform in componentsInChildren)
                {
                    string text = RepairWorkerBones.Name(transform);
                    if (text.Equals("indexRoot_L_jnt", StringComparison.OrdinalIgnoreCase))
                    {
                        woodIndex = transform;
                    }
                    else if (text.Equals("littleRoot_L_jnt", StringComparison.OrdinalIgnoreCase))
                    {
                        woodLittle = transform;
                    }
                    else if (text.Equals("thumbEnd_L_jnt", StringComparison.OrdinalIgnoreCase))
                    {
                        woodThumb = transform;
                    }
                    if (text.EndsWith("_L_jnt", StringComparison.OrdinalIgnoreCase) && (text.StartsWith("thumb", StringComparison.OrdinalIgnoreCase) || text.StartsWith("index", StringComparison.OrdinalIgnoreCase) || text.StartsWith("middle", StringComparison.OrdinalIgnoreCase) || text.StartsWith("ring", StringComparison.OrdinalIgnoreCase) || text.StartsWith("little", StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(transform);
                    }
                }
                woodHandBones = list.ToArray();
            }
            componentsInChildren = woodHandBones;
            foreach (Transform transform2 in componentsInChildren)
            {
                if (transform2 == null)
                {
                    return false;
                }
                if (!workVisual.ClearOfMetal(transform2.position, 0.012f))
                {
                    return false;
                }
                if (transform2.parent != null && transform2.parent != leftHand)
                {
                    for (int j = 1; j <= 3; j++)
                    {
                        if (!workVisual.ClearOfMetal(Vector3.Lerp(transform2.position, transform2.parent.position, (float)j * 0.25f), 0.012f))
                        {
                            return false;
                        }
                    }
                }
                if (requireBench && BenchOverlap(transform2.position, (transform2.parent != null) ? transform2.parent.position : transform2.position, 0.01f))
                {
                    return false;
                }
            }
            Vector3 vector = leftFingers.PalmPosition - leftFingers.PalmRotation * Vector3.up * 0.025f;
            if (!workVisual.ClearOfMetal(vector, 0.025f) || (requireBench && BenchOverlap(vector, vector, 0.025f)) || woodIndex == null || woodLittle == null || woodThumb == null)
            {
                return false;
            }
            Vector3 rhs = workVisual.WorkPoint - workVisual.GripPosition;
            if (rhs.sqrMagnitude < 0.0001f)
            {
                return false;
            }
            rhs.Normalize();
            if (Vector3.Dot((woodIndex.position - woodLittle.position).normalized, rhs) > 0.5f)
            {
                return Vector3.Dot(woodThumb.position - workVisual.GripPosition, rhs) > 0.01f;
            }
            return false;
        }

        private bool CarryGeometryClear(RepairWorkVisual visual)
        {
            if (visual == null || visual.Root == null || visual.Source == null || carryShop == null || bodyRoot == null)
            {
                return false;
            }
            PhysicsScene physicsScene = bodyRoot.gameObject.scene.GetPhysicsScene();
            if (!physicsScene.IsValid())
            {
                return false;
            }
            Renderer[] componentsInChildren = Plat.All<Renderer>(visual.Root, true).ToArray();
            foreach (Renderer renderer in componentsInChildren)
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }
                Bounds bounds = renderer.bounds;
                if (bounds.extents.sqrMagnitude < 1E-07f)
                {
                    continue;
                }
                int num = Geo.OverlapAabb(physicsScene, bounds.center, bounds.extents + Vector3.one * 0.005f, carryHits, -1, QueryTriggerInteraction.Ignore);
                if (num >= carryHits.Length)
                {
                    return false;
                }
                for (int j = 0; j < num; j++)
                {
                    Collider collider = carryHits[j];
                    if (!(collider == null) && collider.enabled && !collider.isTrigger)
                    {
                        Transform transform = collider.transform;
                        if (!(transform == bodyRoot) && !transform.IsChildOf(bodyRoot) && !(transform == visual.Source.transform) && !transform.IsChildOf(visual.Source.transform))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private bool CarryBodyClear(RepairWorkVisual visual)
        {
            if (visual == null || visual.Root == null || bodyRoot == null)
            {
                return false;
            }
            int num = 0;
            try
            {
                MeshFilter[] componentsInChildren = Plat.All<MeshFilter>(visual.Root, true).ToArray();
                foreach (MeshFilter meshFilter in componentsInChildren)
                {
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                    {
                        return false;
                    }
                    Vector3[] vertices = meshFilter.sharedMesh.vertices;
                    num += vertices.Length;
                    if (num > 350000)
                    {
                        return false;
                    }
                    Matrix4x4 matrix4x = bodyRoot.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;
                    Vector3[] array = vertices;
                    foreach (Vector3 point in array)
                    {
                        Vector3 vector = matrix4x.MultiplyPoint3x4(point);
                        if (vector.y > 0.62f && vector.y < 1.5f && vector.x * vector.x + vector.z * vector.z < 0.0361f)
                        {
                            return false;
                        }
                    }
                }
                return num > 0;
            }
            catch
            {
                return false;
            }
        }

        private bool BenchOverlap(Vector3 a, Vector3 b, float radius)
        {
            PhysicsScene physicsScene = woodBenchRoot.gameObject.scene.GetPhysicsScene();
            if (!physicsScene.IsValid())
            {
                return true;
            }
            if (woodBenchSolids == null)
            {
                return true;
            }
            Collider[] array = woodBenchSolids;
            for (int i = 0; i < array.Length; i++)
            {
                MeshCollider meshCollider = array[i] as MeshCollider;
                if (meshCollider != null && !meshCollider.convex && meshCollider.enabled && meshCollider.gameObject.activeInHierarchy)
                {
                    if (meshCollider.sharedMesh == null)
                    {
                        return true;
                    }
                    Bounds bounds = meshCollider.sharedMesh.bounds;
                    Transform transform = meshCollider.transform;
                    if (bounds.Contains(transform.InverseTransformPoint(a)) || bounds.Contains(transform.InverseTransformPoint(b)) || bounds.Contains(transform.InverseTransformPoint((a + b) * 0.5f)))
                    {
                        return true;
                    }
                }
            }
            int num = physicsScene.OverlapCapsule(a, b, radius, benchHits, woodBenchLayers, QueryTriggerInteraction.Ignore);
            if (num >= benchHits.Length)
            {
                return true;
            }
            for (int j = 0; j < num; j++)
            {
                if (benchHits[j] != null && benchHits[j].enabled && benchHits[j].transform.IsChildOf(woodBenchRoot))
                {
                    return true;
                }
            }
            return false;
        }

        private bool ConfigureWoodBench(Transform root)
        {
            woodBenchRoot = root;
            woodBenchLayers = 0;
            woodTabletop = null;
            if (root == null || !root.gameObject.activeInHierarchy)
            {
                woodBenchSolids = null;
                return false;
            }
            List<Collider> list = new List<Collider>();
            Collider[] componentsInChildren = Plat.All<Collider>(root, true).ToArray();
            foreach (Collider collider in componentsInChildren)
            {
                if (collider != null && !collider.isTrigger)
                {
                    list.Add(collider);
                    woodBenchLayers |= 1 << collider.gameObject.layer;
                    MeshCollider meshCollider = collider as MeshCollider;
                    if (meshCollider != null && !meshCollider.convex && meshCollider.sharedMesh != null)
                    {
                        woodTabletop = meshCollider;
                    }
                }
            }
            woodBenchSolids = list.ToArray();
            return WoodBenchReady();
        }

        private bool WoodBenchReady()
        {
            if (woodBenchRoot == null || !woodBenchRoot.gameObject.activeInHierarchy || woodBenchSolids == null || woodTabletop == null || !woodTabletop.enabled || woodTabletop.isTrigger || !woodTabletop.gameObject.activeInHierarchy || woodTabletop.sharedMesh == null || !woodTabletop.transform.IsChildOf(woodBenchRoot))
            {
                return false;
            }
            if (woodBenchSolids.Length == 0)
            {
                return false;
            }
            Collider[] array = woodBenchSolids;
            foreach (Collider collider in array)
            {
                if (collider == null || !collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy || !collider.transform.IsChildOf(woodBenchRoot))
                {
                    return false;
                }
            }
            return true;
        }

        private bool WoodWorkSupported(Vector3 point)
        {
            if (!WoodBenchReady())
            {
                return false;
            }
            Vector3 up = bodyRoot.up;
            Ray ray = new Ray(point + up * 0.12f, -up);
            Collider[] array = woodBenchSolids;
            foreach (Collider collider in array)
            {
                if (!(collider == null) && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy && collider.transform.IsChildOf(woodBenchRoot) && collider.Raycast(ray, out var hitInfo, 0.32f) && Vector3.Dot(hitInfo.normal, up) > 0.6f && Mathf.Abs(Vector3.Dot(hitInfo.point - shopTarget, up)) < 0.06f)
                {
                    return true;
                }
            }
            return false;
        }

        private void PlayWorkStrike(float now)
        {
            if (stationMode == RepairWorkMode.Wood || stationTravelling)
            {
                return;
            }
            try
            {
                if (workStrikeSound == null && now >= nextSoundLookup)
                {
                    nextSoundLookup = now + 10f;
                    AudioManager audioManager = Plat.First<AudioManager>();
                    if ((UnityEngine.Object)(object)audioManager != null)
                    {
                        workStrikeSound = audioManager.Resolve(null, SoundEffect.ForgingSuccessfulStrike, gameObject);
                    }
                }
                if (workStrikeSound != null)
                {
                    workStrikeSound.PlayAtPosition(workVisual.WorkPoint);
                }
            }
            catch
            {
                workStrikeSound = null;
                nextSoundLookup = now + 30f;
            }
        }

        private void ApplyRepairHammer(float lift)
        {
            RepairHammerGripData repairHammerGripData = RepairWorkerProps.HammerGrip;
            if (repairHammerGripData != null)
            {
                float num = ((stationMode == RepairWorkMode.Wood) ? 0.16f : 0.22f);
                float num2 = ((stationMode == RepairWorkMode.Wood) ? 0.035f : 0.08f);
                Vector3 vector = workVisual.WorkPoint + bodyRoot.up * (0.012f + lift * num) - bodyRoot.forward * (lift * num2);
                Quaternion desiredWrist = Quaternion.LookRotation(-bodyRoot.up, bodyRoot.forward) * Quaternion.Inverse(Quaternion.LookRotation(repairHammerGripData.strikeAxis, repairHammerGripData.axis)) * Quaternion.Inverse(hammerGrip.ModelRotationInPalm) * Quaternion.Inverse(rightFingers.PalmFrame);
                int num3 = ((workVisual != null && workVisual.IsDetachedMetal) ? 21 : ((stationMode == RepairWorkMode.Wood) ? 18 : 3));
                for (int i = 0; i < num3; i++)
                {
                    Vector3 vector2 = rightFingers.PalmRotation * hammerGrip.ModelRotationInPalm * Vector3.Scale(hammerFacePoint - hammerGrip.ContactPoint, shopHammer.lossyScale);
                    Vector3 vector3 = vector - vector2;
                    rightWorkArm.Reach(vector3 - rightHand.TransformVector(rightFingers.PalmOffset), bodyRoot.right - bodyRoot.forward * 0.3f, workBlend);
                    rightWorkArm.AimTool(desiredWrist, workBlend);
                }
                if (rightArm != null)
                {
                    rightArm.rotation = Quaternion.AngleAxis(((stationMode == RepairWorkMode.Wood) ? (-35f) : (-25f)) * lift * workBlend, bodyRoot.right) * rightArm.rotation;
                }
            }
        }

        private void CacheHammerFace()
        {
            hammerFaceReady = false;
            if (shopHammer == null || RepairWorkerProps.HammerGrip == null)
            {
                return;
            }
            MeshFilter[] componentsInChildren = Plat.All<MeshFilter>(shopHammer, true).ToArray();
            foreach (MeshFilter meshFilter in componentsInChildren)
            {
                if (meshFilter.sharedMesh == null || meshFilter.name.IndexOf("metal_hammer", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                Bounds bounds = meshFilter.sharedMesh.bounds;
                Vector3 normalized = RepairWorkerProps.HammerGrip.strikeAxis.normalized;
                Vector3 vector = shopHammer.InverseTransformPoint(meshFilter.transform.TransformPoint(bounds.center));
                float num = 0f;
                for (int j = -1; j <= 1; j += 2)
                {
                    for (int k = -1; k <= 1; k += 2)
                    {
                        for (int l = -1; l <= 1; l += 2)
                        {
                            Vector3 position = bounds.center + Vector3.Scale(bounds.extents, new Vector3(j, k, l));
                            num = Mathf.Max(num, Vector3.Dot(shopHammer.InverseTransformPoint(meshFilter.transform.TransformPoint(position)) - vector, normalized));
                        }
                    }
                }
                hammerFacePoint = vector + normalized * num;
                hammerFaceReady = true;
                break;
            }
        }

        private void ApplyGreeting()
        {
            float waveEnvelope = presence.WaveEnvelope;
            if (waveEnvelope <= 0.0001f || leftHand == null)
            {
                waveSkin.Apply(0f);
                return;
            }
            float wavePhase = presence.WavePhase;
            Vector3 upperDirection = bodyRoot.TransformDirection(new Vector3(-0.65f, -0.3f, 0.65f));
            upperDirection += bodyRoot.right * (Mathf.Sin(wavePhase) * 0.02f) + bodyRoot.forward * (Mathf.Cos(wavePhase) * 0.008f);
            if (!waveArm.ApplyGreeting(upperDirection, 105f + Mathf.Sin(wavePhase) * 2f, 35f, waveEnvelope))
            {
                waveSkin.Apply(0f);
                return;
            }
            waveArm.ApplyPalmFacing(bodyRoot.forward, waveEnvelope);
            leftHand.rotation = Quaternion.AngleAxis(Mathf.Sin(wavePhase) * 8f * waveEnvelope, bodyRoot.forward) * leftHand.rotation;
            waveSkin.Apply(waveEnvelope);
        }

        private void ResolveHammer()
        {
            if (!(shopHammer != null) && !(bodyRoot == null))
            {
                shopHammer = RepairWorkerProps.CreateHammer(bodyRoot);
                ownsHammer = shopHammer != null;
                CacheHammerFace();
            }
        }

        private void PlaceHammer()
        {
            if (shopRole == 2 && !(shopHammer == null) && !(rightHand == null))
            {
                hammerGrip.Place(shopHammer, rightFingers.PalmPosition, rightFingers.PalmRotation, RepairWorkerProps.HammerGrip);
            }
        }

        private void ClearHammer()
        {
            hammerGrip.Clear();
            hammerFaceReady = false;
            if (shopHammer != null)
            {
                if (ownsHammer)
                {
                    RepairWorkerProps.DestroyHammer(shopHammer);
                }
                else
                {
                    shopHammer.SetParent(hammerParent, worldPositionStays: false);
                    shopHammer.localPosition = hammerPosition;
                    shopHammer.localRotation = hammerRotation;
                    shopHammer.localScale = hammerScale;
                }
            }
            shopHammer = null;
            ownsHammer = false;
            hammerParent = null;
        }

        internal void RenderPose()
        {
            ApplyPose();
        }

        private void RestorePose()
        {
            ClearWorkVisual();
            waveArm.RestoreForearm();
            leftWorkArm.RestoreForearm();
            rightWorkArm.RestoreForearm();
            waveSkin.Restore();
            leftFingers.Restore();
            rightFingers.Restore();
            eyePose.Clear();
            ClearHammer();
            skinIdentity.Clear();
            if (ownedAnimator != null)
            {
                ownedAnimator.enabled = animatorWasEnabled;
                ownedAnimator = null;
            }
            bodyPoseAnimator = null;
            bodyPoseController = null;
            bodyPoseIndex = int.MinValue;
            if (hasPose)
            {
                if (leftHand != null)
                {
                    leftHand.localRotation = leftHandRest;
                }
                if (rightHand != null)
                {
                    rightHand.localRotation = rightHandRest;
                }
                if (leftElbow != null)
                {
                    leftElbow.localRotation = leftElbowRest;
                }
                if (rightElbow != null)
                {
                    rightElbow.localRotation = rightElbowRest;
                }
                if (leftArm != null)
                {
                    leftArm.localRotation = leftRest;
                }
                if (rightArm != null)
                {
                    rightArm.localRotation = rightRest;
                }
                if (chest != null)
                {
                    chest.localRotation = chestRest;
                }
                if (head != null)
                {
                    head.localRotation = headRest;
                    head.localPosition = headPosition;
                }
                if (skeletonRoot != null)
                {
                    skeletonRoot.localPosition = skeletonPosition;
                }
            }
            hasPose = false;
            attentionPlayer = null;
            presence.Reset();
        }

        internal void OnEnable()
        {
            nextBindAt = 0f;
        }

        internal void OnDisable()
        {
            RestorePose();
            greetingRequested = false;
            nextBindAt = 0f;
        }

        internal void OnDestroy()
        {
            RestorePose();
        }
    }
}
