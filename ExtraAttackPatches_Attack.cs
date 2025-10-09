using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

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
                            if (ExtraAttackPatches_Core.TryGetCurrentClipInfo(player, out string clipName, out AnimationClip clip, out int hitIndex))
                            {
                                string configKey = ExtraAttackPatches_Core.BuildConfigKey(player, clipName, hitIndex);
                                var timing = AnimationTimingConfig.GetTiming(configKey);

                                if (!timing.EnableHit)
                                {
                                    ExtraAttackPlugin.LogInfo("AOC",
                                        $"Skipped DoMeleeAttack: [{configKey}] EnableHit=false");
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

            // Use centralized GetCurrentHitIndex from ExtraAttackPatches_Core
        }
        [HarmonyPatch(typeof(Attack), "GetAttackStamina")]
        public static class Attack_GetAttackStamina_Postfix
        {
            public static void Postfix(Attack __instance, ref float __result, Humanoid ___m_character, ItemDrop.ItemData ___m_weapon)
            {
                try
                {
                    if (__instance == null || ___m_character == null)
                        return;

                    var player = ___m_character as Player;
                    if (player == null || player != Player.m_localPlayer)
                        return;

                    var mode = ExtraAttackUtils.GetAttackMode(player);
                    if (mode == ExtraAttackUtils.AttackMode.Normal)
                        return;

                    var weapon = ___m_weapon;
                    if (weapon == null)
                        return;

                    float cost = ExtraAttackUtils.GetEffectiveStaminaCost(__instance, player, weapon, mode);
                    __result = cost;
                }
                catch (System.Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in GetAttackStamina Postfix: {ex.Message}");
                }
            }
        }
        [HarmonyPatch(typeof(Attack), nameof(Attack.Stop))]
        public static class Attack_Stop_RevertAOC_Postfix
        {
            public static void Postfix(Attack __instance, Character ___m_character)
            {
                try
                {
                    if (__instance == null || ___m_character == null)
                    {
                        return;
                    }

                    var player = ___m_character as Player;
                    if (player == null || player != Player.m_localPlayer)
                    {
                        return;
                    }

                    // Only handle when in our extra attack modes
                    if (!ExtraAttackUtils.IsPlayerInExtraAttack(player))
                    {
                        return;
                    }

                    // Obtain animator and revert safely when root motion is settled
                    if (ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator animator) && animator != null)
                    {
                        bool settled = true;
                        try { settled = Character_AddRootMotion_Log_Patch.IsRootMotionSettled(player); } catch { }
                        if (!settled)
                        {
                            if (ExtraAttackPlugin.DebugAOCOperations.Value)
                            {
                                ExtraAttackPlugin.LogInfo("AOC", "Attack.Stop: defer revert due to root motion not settled");
                            }
                            return; // Defer to Player.Update revert path
                        }

                        // AOC is now set at equipment change time - no need to revert
                        // ExtraAttackPatches_Animation.RevertStyleAOC(player, animator);
                        
                        // ❌ 削除: Attack.Stopでのモードリセットを削除
                        // 理由: 次の攻撃のモード設定より先に呼ばれるため、カスタムアニメが失われる
                        // ExtraAttackUtils.SetAttackMode(player, ExtraAttackUtils.AttackMode.Normal);
                        // ExtraAttackPlugin.LogInfo("AOC", "Attack.Stop: reset mode to Normal");

                        // Class: ExtraAttackPatches_Attack
                        // 例: 攻撃終了時の emote_stop ガード開始処理の周辺
                        if (!ExtraAttackPlugin.AreGuardsDisabled() && ExtraAttackPlugin.EnablePostAttackEmoteStopGuard.Value)
                        {
                        // 既存のポスト攻撃ガード処理（タイマー開始など）
                            float guardSec = Math.Max(0f, ExtraAttackPlugin.PostAttackEmoteStopGuardSeconds.Value);
                            if (guardSec > 0f)
                            {
                                ExtraAttackUtils.SetEmoteStopGuardWindow(player, guardSec);
                                if (ExtraAttackPlugin.DebugAOCOperations.Value)
                                {
                                    ExtraAttackPlugin.LogInfo("AOC", $"PostAttack emote_stop guard started: {guardSec:F2}s");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Attack.Stop Postfix: {ex.Message}");
                }
            }
        }
    }
}