using HarmonyLib;
using System;
using UnityEngine;

namespace ExtraAttackSystem
{
    // Combo prevention patches - FINAL FIX
    public static class ExtraAttackPatches_Combo
    {
        /// <summary>
        /// Force m_previousAttack to null AFTER Humanoid.StartAttack completes
        /// This prevents combo chaining from T/G → left-click
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        [HarmonyPriority(Priority.Last)]  // Execute after all other patches
        public static class Humanoid_StartAttack_ForceNullPrevious_Patch
        {
            public static void Postfix(Humanoid __instance, bool __result)
            {
                try
                {
                    if (!__result) return; // Attack didn't start, skip

                    if (__instance is Player player && player == Player.m_localPlayer)
                    {
                        var attackMode = ExtraAttackUtils.GetAttackMode(player);

                        // For extra attack modes, ALWAYS clear m_previousAttack
                        if (attackMode != ExtraAttackUtils.AttackMode.Normal)
                        {
                            var traverse = Traverse.Create(__instance);
                            Attack m_previousAttack = traverse.Field("m_previousAttack").GetValue<Attack>();
                            Attack m_currentAttack = traverse.Field("m_currentAttack").GetValue<Attack>();

                            string prevInfo = m_previousAttack != null ? $"EXISTS ({m_previousAttack.m_attackAnimation})" : "NULL";
                            string currInfo = m_currentAttack != null ? $"EXISTS ({m_currentAttack.m_attackAnimation})" : "NULL";

                            ExtraAttackPlugin.LogInfo("COMBO",
                                $"[POSTFIX] Humanoid.StartAttack: mode={attackMode}, prev={prevInfo}, curr={currInfo}");

                            if (m_previousAttack != null)
                            {
                                ExtraAttackPlugin.LogInfo("COMBO",
                                    $"!!! FORCE NULLIFYING m_previousAttack: {m_previousAttack.m_attackAnimation} !!!");

                                // CRITICAL: Set the actual field, not the parameter
                                traverse.Field("m_previousAttack").SetValue(null);

                                ExtraAttackPlugin.LogInfo("COMBO", "!!! m_previousAttack NOW NULL !!!");
                            }
                            else
                            {
                                ExtraAttackPlugin.LogInfo("COMBO", "m_previousAttack was already NULL");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System",
                        $"Error in Humanoid_StartAttack_ForceNullPrevious_Patch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// DEBUG: Log Humanoid.StartAttack calls (Prefix for visibility)
        /// </summary>
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        [HarmonyPriority(Priority.First)]
        public static class Humanoid_StartAttack_DebugLog_Patch
        {
            public static void Prefix(Humanoid __instance, bool secondaryAttack)
            {
                try
                {
                    if (__instance is Player player && player == Player.m_localPlayer)
                    {
                        var currentMode = ExtraAttackUtils.GetAttackMode(player);

                        var traverse = Traverse.Create(__instance);
                        Attack m_previousAttack = traverse.Field("m_previousAttack").GetValue<Attack>();
                        Attack m_currentAttack = traverse.Field("m_currentAttack").GetValue<Attack>();

                        string prevInfo = m_previousAttack != null ? $"EXISTS ({m_previousAttack.m_attackAnimation})" : "NULL";
                        string currInfo = m_currentAttack != null ? $"EXISTS ({m_currentAttack.m_attackAnimation})" : "NULL";

                        ExtraAttackPlugin.LogInfo("COMBO",
                            $"[PREFIX] Humanoid.StartAttack: mode={currentMode}, sec={secondaryAttack}, " +
                            $"prev={prevInfo}, curr={currInfo}");
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System",
                        $"Error in Humanoid_StartAttack_DebugLog_Patch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Skip Attack.OnAttackTrigger() when YAML has Range=0 or Angle=0
        /// This prevents BOTH sound and damage
        /// </summary>
        [HarmonyPatch(typeof(Attack), "OnAttackTrigger")]
        [HarmonyPriority(Priority.VeryHigh)]
        public static class Attack_OnAttackTrigger_SkipWhenDisabled_Patch
        {
            public static bool Prefix(Attack __instance)
            {
                try
                {
                    var traverse = Traverse.Create(__instance);
                    var character = traverse.Field("m_character").GetValue<Character>();

                    if (character is Player player && player == Player.m_localPlayer)
                    {
                        var attackMode = ExtraAttackUtils.GetAttackMode(player);

                        if (attackMode != ExtraAttackUtils.AttackMode.Normal)
                        {
                            if (ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator animator))
                            {
                                AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);
                                if (clipInfos.Length > 0)
                                {
                                    string clipName = clipInfos[0].clip.name;
                                    AnimationClip clip = clipInfos[0].clip;

                                    int hitIndex = ExtraAttackPatches_Core.GetCurrentHitIndex(animator, clip);
                                    string configKey = ExtraAttackPatches_Core.BuildConfigKey(player, clipName, hitIndex);
                                    var timing = AnimationTimingConfig.GetTiming(configKey);

                                    if (timing.AttackRange <= 0f || timing.AttackAngle <= 0f)
                                    {
                                        ExtraAttackPlugin.LogInfo("SOUND",
                                            $"[SKIP] Attack.OnAttackTrigger: [{configKey}] Range={timing.AttackRange:F2}, Angle={timing.AttackAngle:F1} - Sound & Damage DISABLED");

                                        return false; // Skip entire OnAttackTrigger
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Attack_OnAttackTrigger_SkipWhenDisabled_Patch: {ex.Message}");
                }

                return true;
            }
        }
    }
}