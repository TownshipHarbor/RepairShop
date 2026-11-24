using System;
using UnityEngine;
using Il2Cpp;

namespace RepairShopQuest
{
    internal sealed class RepairWorkerPresence
    {
        private const float Raise = 0.45f;

        private const float Hold = 1.6f;

        private const float Return = 0.55f;

        private const float Cooldown = 18f;

        private uint initialSeed = 1u;

        private uint random = 1u;

        private float elapsed;

        private float idlePhase;

        private float nextGlance;

        private float glanceUntil;

        private float glanceYaw;

        private float glancePitch;

        private float attentionAge;

        private float attentionHold;

        private float waveAge;

        private float returnAge;

        private float returnAmount;

        private float nodAge = 0.8f;

        private float lastGreeting = float.NegativeInfinity;

        private bool initialized;

        private bool waveActive;

        private bool returning;

        private bool requested;

        internal float HeadYaw { get; private set; }

        internal float HeadPitch { get; private set; }

        internal float ChestYaw { get; private set; }

        internal float ChestPitch { get; private set; }

        internal float ChestRoll { get; private set; }

        internal float SideShift { get; private set; }

        internal float WaveEnvelope { get; private set; }

        internal float WavePhase { get; private set; }

        internal float Nod { get; private set; }

        internal string Mode { get; private set; } = "Calm";

        internal void Initialize(uint seed)
        {
            initialSeed = ((seed == 0) ? 1u : seed);
            Reset();
        }

        internal void Reset()
        {
            random = initialSeed;
            elapsed = (waveAge = (returnAge = (returnAmount = (attentionAge = 0f))));
            glanceUntil = (glanceYaw = (glancePitch = 0f));
            waveActive = (returning = (requested = false));
            float num2 = (Nod = 0f);
            float num4 = (WavePhase = num2);
            float num6 = (WaveEnvelope = num4);
            float num8 = (SideShift = num6);
            float num10 = (ChestRoll = num8);
            float num12 = (ChestPitch = num10);
            float num14 = (ChestYaw = num12);
            float headYaw = (HeadPitch = num14);
            HeadYaw = headYaw;
            nodAge = 0.8f;
            lastGreeting = float.NegativeInfinity;
            idlePhase = Next() * (float)Math.PI * 2f;
            nextGlance = 4f + Next() * 4f;
            attentionHold = 3f + Next() * 1.5f;
            Mode = "Calm";
            initialized = true;
        }

        internal void RequestWave()
        {
            requested = true;
        }

        internal void Step(float dt, float now, RepairWorkerPresenceInput input)
        {
            if (!initialized)
            {
                bool flag = requested;
                Reset();
                requested = flag;
            }
            if (!Finite(dt) || dt <= 0f)
            {
                return;
            }
            dt = Mathf.Min(dt, 0.05f);
            elapsed += dt;
            idlePhase = (idlePhase + dt * 1.25f) % ((float)Math.PI * 20f);
            bool flag2 = input.HasLookTarget && Finite(input.LocalLookDirection.x) && Finite(input.LocalLookDirection.y) && Finite(input.LocalLookDirection.z) && input.LocalLookDirection.z > 0.001f && input.LocalLookDirection.sqrMagnitude > 0.0001f;
            bool flag3 = !input.Working;
            if (waveActive && !flag3)
            {
                waveActive = false;
                returning = WaveEnvelope > 0f;
                returnAge = 0f;
                returnAmount = WaveEnvelope;
            }
            if (Finite(now) && now < lastGreeting)
            {
                lastGreeting = now;
            }
            bool flag4 = input.TargetEntered && flag2 && Finite(input.TargetDistance) && input.TargetDistance >= 0f && input.TargetDistance <= 4f && Finite(now) && now - lastGreeting >= 18f;
            if (!waveActive && !returning && flag3 && (requested || flag4))
            {
                waveActive = true;
                waveAge = 0f;
                nodAge = 0f;
                if (Finite(now))
                {
                    lastGreeting = now;
                }
            }
            requested = false;
            AdvanceWave(dt);
            if (!flag3)
            {
                nodAge = 0.8f;
            }
            nodAge = Mathf.Min(0.8f, nodAge + dt);
            float num = Mathf.Sin((float)Math.PI * nodAge / 0.8f);
            Nod = Mathf.Lerp(Nod, (nodAge < 0.8f) ? (3f * num * num) : 0f, 1f - Mathf.Exp((0f - dt) * 10f));
            bool flag5 = flag3 && flag2 && !waveActive && !returning;
            if (!flag2)
            {
                attentionAge = 0f;
            }
            else if (flag5)
            {
                attentionAge += dt;
                if (attentionAge >= attentionHold + 0.8f)
                {
                    attentionAge -= attentionHold + 0.8f;
                    attentionHold = 3f + Next() * 1.5f;
                }
            }
            bool flag6 = flag5 && attentionAge >= attentionHold;
            float b = 0f;
            float b2 = 0f;
            if (flag3 && !flag2)
            {
                if (elapsed >= nextGlance)
                {
                    glanceYaw = (Next() * 2f - 1f) * 13f;
                    glancePitch = (Next() * 2f - 1f) * 3f;
                    glanceUntil = elapsed + 1.2f + Next() * 0.6f;
                    nextGlance = elapsed + 4f + Next() * 4f;
                }
                if (elapsed < glanceUntil)
                {
                    b = glanceYaw;
                    b2 = glancePitch;
                }
            }
            else
            {
                if (elapsed >= nextGlance)
                {
                    nextGlance = elapsed + 4f + Next() * 4f;
                }
                glanceUntil = 0f;
            }
            if (flag2)
            {
                Vector3 localLookDirection = input.LocalLookDirection;
                b = Mathf.Clamp(Mathf.Atan2(localLookDirection.x, localLookDirection.z) * 57.29578f, input.Working ? (-40f) : (-55f), input.Working ? 40f : 55f);
                b2 = Mathf.Clamp((0f - Mathf.Atan2(localLookDirection.y, Mathf.Sqrt(localLookDirection.x * localLookDirection.x + localLookDirection.z * localLookDirection.z))) * 57.29578f, -25f, 25f);
            }
            if (flag6)
            {
                b = (b2 = 0f);
            }
            float t = 1f - Mathf.Exp((0f - dt) * 3.5f);
            HeadYaw = Mathf.Lerp(HeadYaw, b, t);
            HeadPitch = Mathf.Lerp(HeadPitch, b2, t);
            float t2 = 1f - Mathf.Exp((0f - dt) * 3f);
            float num2 = Mathf.Sin(idlePhase * 0.5f);
            ChestYaw = Mathf.Lerp(ChestYaw, flag3 ? (HeadYaw * 0.025f - WaveEnvelope) : 0f, t2);
            ChestPitch = Mathf.Lerp(ChestPitch, input.Working ? 1.2f : (Mathf.Sin(idlePhase) * 0.4f), t2);
            ChestRoll = Mathf.Lerp(ChestRoll, flag3 ? (num2 * 0.65f) : 0f, t2);
            SideShift = Mathf.Lerp(SideShift, flag3 ? (num2 * 0.0055f) : 0f, t2);
            Mode = (input.Working ? "Repairing" : (waveActive ? "Greeting" : (returning ? "Returning" : (flag6 ? "Gaze break" : (flag2 ? "Attentive" : ((elapsed < glanceUntil) ? "Glancing" : "Calm"))))));
        }

        private void AdvanceWave(float dt)
        {
            if (returning)
            {
                returnAge += dt;
                WaveEnvelope = returnAmount * (1f - Smooth(returnAge / 0.55f));
                if (returnAge >= 0.55f)
                {
                    returning = false;
                }
                return;
            }
            if (!waveActive)
            {
                WaveEnvelope = 0f;
                return;
            }
            float a = waveAge;
            waveAge += dt;
            float num = Mathf.Max(0f, Mathf.Min(waveAge, 2.05f) - Mathf.Max(a, 0.45f));
            WavePhase += num * 9.817477f;
            WaveEnvelope = ((waveAge < 0.45f) ? Smooth(waveAge / 0.45f) : ((waveAge < 2.05f) ? 1f : (1f - Smooth((waveAge - 0.45f - 1.6f) / 0.55f))));
            if (waveAge >= 2.6f)
            {
                waveActive = false;
                WaveEnvelope = 0f;
            }
        }

        private static float Smooth(float v)
        {
            float num = Mathf.Clamp01(v);
            return num * num * (3f - 2f * num);
        }

        private static bool Finite(float v)
        {
            if (!float.IsNaN(v))
            {
                return !float.IsInfinity(v);
            }
            return false;
        }

        private float Next()
        {
            random ^= random << 13;
            random ^= random >> 17;
            random ^= random << 5;
            return (float)(random & 0xFFFFFF) / 16777216f;
        }
    }
}
