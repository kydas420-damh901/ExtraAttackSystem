using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExtraAttackSystem
{
    // =========================
    // Utils (one-shot flag)
    // =========================
    internal static class EAS_UtilsExt
    {
        private static readonly HashSet<Player> s_clearPrevOnNextNormalStart = new();

        public static void MarkClearPrevOnNextNormalStart(Player p)
        {
            if (p != null) s_clearPrevOnNextNormalStart.Add(p);
        }

        public static bool ConsumeClearPrevOnNextNormalStart(Player p)
        {
            return p != null && s_clearPrevOnNextNormalStart.Remove(p);
        }

        public static void Cleanup(Player p)
        {
            s_clearPrevOnNextNormalStart.Remove(p);
        }
    }

    // =========================
    // Core helpers (AOC-based key)
    // =========================
    internal static class EAS_CoreExt
    {
        public static ExtraAttackUtils.AttackMode DetectModeFromAnimator(Player player, Animator animator)
        {
            try
            {
                var rac = animator.runtimeAnimatorController;
                foreach (var kv in AnimationManager.CustomRuntimeControllers)
                {
                    if (kv.Value == rac)
                    {
                        switch (kv.Key)
                        {
                            case "ExtraAttack_Q": return ExtraAttackUtils.AttackMode.ExtraQ;
                            case "ExtraAttack_T_Swords":
                            case "ExtraAttack_T_Clubs":
                                return ExtraAttackUtils.AttackMode.ExtraT;
                            case "ExtraAttack_G_Swords":
                            case "ExtraAttack_G_Clubs":
                                return ExtraAttackUtils.AttackMode.ExtraG;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"DetectModeFromAnimator error: {ex.Message}");
            }
            return ExtraAttackUtils.AttackMode.Normal;
        }

        public static string BuildConfigKeyFromAnimator(Player player, Animator animator, string clipName, int hitIndex)
        {
            try
            {
                var mode = DetectModeFromAnimator(player, animator);
                string suffix = mode switch
                {
                    ExtraAttackUtils.AttackMode.ExtraQ => "_Q",
                    ExtraAttackUtils.AttackMode.ExtraT => "_T",
                    ExtraAttackUtils.AttackMode.ExtraG => "_G",
                    _ => string.Empty
                };

                string key1 = $"{clipName}{suffix}_hit{hitIndex}";
                string key2 = $"{clipName}{suffix}";
                string key3 = $"{clipName}_hit{hitIndex}";
                string key4 = clipName;

                if (AnimationTimingConfig.HasConfig(key1)) return key1;
                if (AnimationTimingConfig.HasConfig(key2)) return key2;
                if (AnimationTimingConfig.HasConfig(key3)) return key3;
                if (AnimationTimingConfig.HasConfig(key4)) return key4;
                return clipName;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"BuildConfigKeyFromAnimator error: {ex.Message}");
                return clipName;
            }
        }
    }

    // =========================
    // Patches
    // =========================

    // (A) Mark flag when Extra attack STARTS successfully
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    internal static class EAS_StartAttack_MarkFlag_Postfix
    {
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(Humanoid __instance, bool secondaryAttack, bool __result)
        {
            try
            {
                if (!__result) return;
                if (__instance is Player p && p == Player.m_localPlayer)
                {
                    var mode = ExtraAttackUtils.GetAttackMode(p);
                    if (mode != ExtraAttackUtils.AttackMode.Normal)
                    {
                        EAS_UtilsExt.MarkClearPrevOnNextNormalStart(p);
                        ExtraAttackPlugin.LogInfo("COMBO", "[POSTFIX] Extra attack started -> mark clear-prev for next normal");
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"EAS_StartAttack_MarkFlag_Postfix: {ex.Message}");
            }
        }
    }

    // (B) Clear previous just BEFORE the next normal StartAttack runs
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
    internal static class EAS_StartAttack_ClearPrevOnNormal_Prefix
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Humanoid __instance, bool secondaryAttack)
        {
            try
            {
                if (__instance is Player p && p == Player.m_localPlayer && !secondaryAttack)
                {
                    if (EAS_UtilsExt.ConsumeClearPrevOnNextNormalStart(p))
                    {
                        var t = Traverse.Create(__instance);
                        var prev = t.Field("m_previousAttack").GetValue<Attack>();
                        if (prev != null)
                        {
                            t.Field("m_previousAttack").SetValue(null);
                            ExtraAttackPlugin.LogInfo("COMBO", "[PREFIX] Cleared m_previousAttack before normal attack");
                        }

                        // also clear queued second-attack timer (defensive)
                        var field = typeof(Player).GetField("m_queuedSecondAttackTimer",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null)
                        {
                            field.SetValue(p, 0f);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"EAS_StartAttack_ClearPrevOnNormal_Prefix: {ex.Message}");
            }
        }
    }

    // (C) Skip sound & damage when YAML=0 by looking at active AOC (not attack mode)
    [HarmonyPatch(typeof(Attack), "OnAttackTrigger")]
    internal static class EAS_Attack_OnAttackTrigger_SkipByAOC_Prefix
    {
        [HarmonyPriority(Priority.VeryHigh)]
        private static bool Prefix(Attack __instance)
        {
            try
            {
                var ch = Traverse.Create(__instance).Field("m_character").GetValue<Character>();
                if (ch is Player p && p == Player.m_localPlayer)
                {
                    if (ExtraAttackPatches_Core.TryGetPlayerAnimator(p, out Animator animator))
                    {
                        var infos = animator.GetCurrentAnimatorClipInfo(0);
                        if (infos != null && infos.Length > 0)
                        {
                            string clipName = infos[0].clip.name;
                            var clip = infos[0].clip;
                            int hitIndex = ExtraAttackPatches_Core.GetCurrentHitIndex(animator, clip);
                            string key = EAS_CoreExt.BuildConfigKeyFromAnimator(p, animator, clipName, hitIndex);
                            var timing = AnimationTimingConfig.GetTiming(key);

                            if (timing.AttackRange <= 0f || timing.AttackAngle <= 0f)
                            {
                                ExtraAttackPlugin.LogInfo("SOUND", $"[SKIP] Attack.OnAttackTrigger: [{key}] Range={timing.AttackRange:F2}, Angle={timing.AttackAngle:F1}");
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"EAS_Attack_OnAttackTrigger_SkipByAOC_Prefix: {ex.Message}");
            }
            return true;
        }
    }

    // Cleanup our one-shot flags on player destroy
    [HarmonyPatch(typeof(Player), nameof(Player.OnDestroy))]
    internal static class EAS_Player_OnDestroy_CleanupFlag_Patch
    {
        private static void Prefix(Player __instance)
        {
            EAS_UtilsExt.Cleanup(__instance);
        }
    }
}