using HarmonyLib;
using System;
using System.Collections.Generic;

namespace ExtraAttackSystem
{
    // Attack-related patches
    public static class ExtraAttackPatches_Attack
    {
        [HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
        public static class Attack_DoMeleeAttack_OverrideParams_Patch
        {
            private static Dictionary<Attack, AttackParams> originalParams = new Dictionary<Attack, AttackParams>();

            private class AttackParams
            {
                public float attackRange;
                public float attackHeight;
                public float attackOffset;
                public float attackAngle;
                public float attackRayWidth;
                public float attackRayWidthCharExtra;
                public float attackHeightChar1;
                public float attackHeightChar2;
                public float maxYAngle;
            }

            public static bool Prefix(Attack __instance, Character ___m_character)
            {
                try
                {
                    Character character = ___m_character;
                    if (character is Player player && player == Player.m_localPlayer)
                    {
                        var attackMode = ExtraAttackUtils.GetAttackMode(player);

                        if (attackMode != ExtraAttackUtils.AttackMode.Normal)
                        {
                            if (ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out UnityEngine.Animator animator))
                            {
                                UnityEngine.AnimatorClipInfo[] clipInfos = animator.GetCurrentAnimatorClipInfo(0);
                                if (clipInfos.Length > 0)
                                {
                                    string clipName = clipInfos[0].clip.name;
                                    UnityEngine.AnimationClip clip = clipInfos[0].clip;

                                    int hitIndex = GetCurrentHitIndex(animator, clip);
                                    string configKey = BuildConfigKey(player, clipName, hitIndex);
                                    var timing = AnimationTimingConfig.GetTiming(configKey);

                                    if (timing.AttackRange <= 0f || timing.AttackAngle <= 0f)
                                    {
                                        ExtraAttackPlugin.LogInfo("AOC",
                                            $"Skipped DoMeleeAttack: [{configKey}] has Range={timing.AttackRange:F2} or Angle={timing.AttackAngle:F1}");
                                        return false;
                                    }

                                    originalParams[__instance] = new AttackParams
                                    {
                                        attackRange = __instance.m_attackRange,
                                        attackHeight = __instance.m_attackHeight,
                                        attackOffset = __instance.m_attackOffset,
                                        attackAngle = __instance.m_attackAngle,
                                        attackRayWidth = __instance.m_attackRayWidth,
                                        attackRayWidthCharExtra = __instance.m_attackRayWidthCharExtra,
                                        attackHeightChar1 = __instance.m_attackHeightChar1,
                                        attackHeightChar2 = __instance.m_attackHeightChar2,
                                        maxYAngle = __instance.m_maxYAngle
                                    };

                                    __instance.m_attackRange = timing.AttackRange;
                                    __instance.m_attackHeight = timing.AttackHeight;
                                    __instance.m_attackOffset = timing.AttackOffset;
                                    __instance.m_attackAngle = timing.AttackAngle;
                                    __instance.m_attackRayWidth = timing.AttackRayWidth;
                                    __instance.m_attackRayWidthCharExtra = timing.AttackRayWidthCharExtra;
                                    __instance.m_attackHeightChar1 = timing.AttackHeightChar1;
                                    __instance.m_attackHeightChar2 = timing.AttackHeightChar2;
                                    __instance.m_maxYAngle = timing.MaxYAngle;

                                    ExtraAttackPlugin.LogInfo("AOC",
                                        $"Applied attack params from YAML: [{configKey}] Range={timing.AttackRange:F2}, Height={timing.AttackHeight:F2}, Angle={timing.AttackAngle:F1}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in DoMeleeAttack_OverrideParams Prefix: {ex.Message}");
                }

                return true;
            }

            public static void Postfix(Attack __instance, Character ___m_character)
            {
                try
                {
                    if (originalParams.TryGetValue(__instance, out var original))
                    {
                        __instance.m_attackRange = original.attackRange;
                        __instance.m_attackHeight = original.attackHeight;
                        __instance.m_attackOffset = original.attackOffset;
                        __instance.m_attackAngle = original.attackAngle;
                        __instance.m_attackRayWidth = original.attackRayWidth;
                        __instance.m_attackRayWidthCharExtra = original.attackRayWidthCharExtra;
                        __instance.m_attackHeightChar1 = original.attackHeightChar1;
                        __instance.m_attackHeightChar2 = original.attackHeightChar2;
                        __instance.m_maxYAngle = original.maxYAngle;

                        originalParams.Remove(__instance);

                        ExtraAttackPlugin.LogInfo("AOC", "Restored original attack params");
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in DoMeleeAttack_OverrideParams Postfix: {ex.Message}");
                }
            }

            private static string BuildConfigKey(Player player, string clipName, int hitIndex)
            {
                try
                {
                    var attackMode = ExtraAttackUtils.GetAttackMode(player);
                    string modeSuffix = attackMode switch
                    {
                        ExtraAttackUtils.AttackMode.ExtraQ => "_Q",
                        ExtraAttackUtils.AttackMode.ExtraT => "_T",
                        ExtraAttackUtils.AttackMode.ExtraG => "_G",
                        _ => ""
                    };

                    string key1 = $"{clipName}{modeSuffix}_hit{hitIndex}";
                    string key2 = $"{clipName}{modeSuffix}";
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
                    ExtraAttackPlugin.LogError("System", $"Error in BuildConfigKey: {ex.Message}");
                    return clipName;
                }
            }

            private static int GetCurrentHitIndex(UnityEngine.Animator animator, UnityEngine.AnimationClip clip)
            {
                try
                {
                    var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    float normalizedTime = stateInfo.normalizedTime % 1f;
                    float currentTime = normalizedTime * clip.length;

                    float closestTimeDiff = float.MaxValue;
                    int hitIndex = 0;

                    foreach (var evt in clip.events)
                    {
                        if (evt.functionName == "OnAttackTrigger")
                        {
                            float timeDiff = UnityEngine.Mathf.Abs(evt.time - currentTime);

                            if (timeDiff < closestTimeDiff && timeDiff < 0.1f)
                            {
                                closestTimeDiff = timeDiff;
                                hitIndex = evt.intParameter;
                            }
                        }
                    }

                    return hitIndex;
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in GetCurrentHitIndex: {ex.Message}");
                    return 0;
                }
            }
        }
    }
}