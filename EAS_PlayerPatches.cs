using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace ExtraAttackSystem
{
    [HarmonyPatch(typeof(Player), "Update")]
    public static class Player_Update_Patch
    {
        private static ItemDrop.ItemData? _lastWeapon = null;

        static void Postfix(Player __instance)
        {
            if (__instance == null) return;
            if (!__instance.IsOwner()) return;

            try
            {
                // Detect weapon change and apply AOC
                var currentWeapon = __instance.GetCurrentWeapon();
                if (currentWeapon != _lastWeapon)
                {
                    _lastWeapon = currentWeapon;
                    
                    if (currentWeapon != null)
                    {
                        string weaponType = EAS_InputHandler.GetWeaponTypeFromSkill(
                            currentWeapon.m_shared.m_skillType, currentWeapon);
                        
                        // Apply AOC (weapon change only)
                        EAS_AnimationManager.ApplyAOCForWeapon(__instance, weaponType);
                        ExtraAttackSystemPlugin.LogInfo("System", $"Weapon changed to {weaponType}, AOC applied");
                    }
                }

                // Handle extra attack key input
                EAS_InputHandler.HandleKeyInput(__instance);
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error in Player_Update_Patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Player), "OnDestroy")]
    public static class Player_OnDestroy_Patch
    {
        static void Prefix(Player __instance)
        {
            if (__instance == null) return;

            try
            {
                // Cleanup player data
                EAS_InputHandler.CleanupPlayer(__instance);
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error in Player_OnDestroy_Patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Humanoid), "StartAttack")]
    public static class Humanoid_StartAttack_Patch
    {
        static bool Prefix(Humanoid __instance, Character target, bool secondaryAttack, ref bool __result)
        {
            if (__instance == null)
            {
                __result = false;
                return false;
            }

            try
            {
                // Check if this is a player and handle extra attack mode
                if (__instance is Player player)
                {
                    var attackMode = EAS_InputHandler.GetAttackMode(player);
                    
                    // Only intercept QTG attacks (secondary_Q, secondary_T, secondary_G)
                    if (attackMode == EAS_InputHandler.AttackMode.secondary_Q || 
                        attackMode == EAS_InputHandler.AttackMode.secondary_T || 
                        attackMode == EAS_InputHandler.AttackMode.secondary_G)
                    {
                        // TODO: 一時的なデバッグログ - QTG攻撃検出確認用（問題解決後に削除予定）
                        ExtraAttackSystemPlugin.LogInfo("System", $"QTG attack detected: {attackMode}, continuing with vanilla StartAttack");
                        // Let vanilla method handle the rest (attack detection, damage, etc.)
                        return true; // Continue with original method - バニラの攻撃判定も実行
                    }
                }

                return true; // Continue with original method
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error in Humanoid_StartAttack_Patch: {ex.Message}");
                return true;
            }
        }

        static void Postfix(Humanoid __instance, Character target, bool secondaryAttack, bool __result)
        {
            if (__instance is Player player && player == Player.m_localPlayer)
            {
                var attackMode = EAS_InputHandler.GetAttackMode(player);
                if (attackMode == EAS_InputHandler.AttackMode.secondary_Q || 
                    attackMode == EAS_InputHandler.AttackMode.secondary_T || 
                    attackMode == EAS_InputHandler.AttackMode.secondary_G)
                {
                    // TODO: 一時的なデバッグログ - バニラStartAttack完了確認用（問題解決後に削除予定）
                    ExtraAttackSystemPlugin.LogInfo("System", $"Vanilla StartAttack completed for {attackMode}, result: {__result}");
                }
            }
        }
    }

    [HarmonyPatch(typeof(Attack), "Start")]
    public static class Attack_Start_Patch
    {
        static void Postfix(Attack __instance, Humanoid character, ItemDrop.ItemData weapon, bool __result)
        {
            try
            {
                if (__result && character is Player player && player == Player.m_localPlayer)
                {
                    var attackMode = EAS_InputHandler.GetAttackMode(player);
                    if (attackMode != EAS_InputHandler.AttackMode.Normal)
                    {
                        ExtraAttackSystemPlugin.LogInfo("System", $"Attack.Start - attackMode: {attackMode}, attackType: {__instance.m_attackType}, attackAnimation: {__instance.m_attackAnimation}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error in Attack_Start_Patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
    public static class Attack_DoMeleeAttack_Patch
    {
        private static Dictionary<Attack, AttackParams> originalParams = new Dictionary<Attack, AttackParams>();
        
        static Attack_DoMeleeAttack_Patch()
        {
            ExtraAttackSystemPlugin.LogInfo("System", "Attack_DoMeleeAttack_Patch constructor called - patch registered");
        }
        
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
        
        static bool Prefix(Attack __instance, Character ___m_character)
        {
            try
            {
                Character character = ___m_character;
                if (character is Player player && player == Player.m_localPlayer)
                {
                    var attackMode = EAS_InputHandler.GetAttackMode(player);
                    ExtraAttackSystemPlugin.LogInfo("System", $"Attack.DoMeleeAttack called - attackMode: {attackMode}");

                    if (attackMode != EAS_InputHandler.AttackMode.Normal)
                    {
                        // Get weapon type and mode for timing configuration
                        var weapon = player.GetCurrentWeapon();
                        if (weapon != null)
                        {
                            string weaponType = EAS_InputHandler.GetWeaponTypeFromSkill(
                                weapon.m_shared.m_skillType, weapon);
                            
                            // Get timing configuration
                            var timing = EAS_AnimationTiming.GetTiming($"{weaponType}_{attackMode}");

                            // Check if hit is enabled
                            if (!timing.EnableHit)
                            {
                                ExtraAttackSystemPlugin.LogInfo("System", 
                                    $"Skipped DoMeleeAttack: [{weaponType}_{attackMode}] EnableHit=false");
                                return false; // Skip DoMeleeAttack
                            }

                            // Store original parameters
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

                            // Apply YAML attack parameters
                            __instance.m_attackRange = timing.AttackRange;
                            __instance.m_attackHeight = timing.AttackHeight;
                            __instance.m_attackOffset = timing.AttackOffset;
                            __instance.m_attackAngle = timing.AttackAngle;
                            __instance.m_attackRayWidth = timing.AttackRayWidth;
                            __instance.m_attackRayWidthCharExtra = timing.AttackRayWidthCharExtra;
                            __instance.m_attackHeightChar1 = timing.AttackHeightChar1;
                            __instance.m_attackHeightChar2 = timing.AttackHeightChar2;
                            __instance.m_maxYAngle = timing.MaxYAngle;

                            ExtraAttackSystemPlugin.LogInfo("System", 
                                $"Applied custom attack parameters for {weaponType}_{attackMode}: " +
                                $"Range={timing.AttackRange}, Height={timing.AttackHeight}, Angle={timing.AttackAngle}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error in Attack_DoMeleeAttack_Patch Prefix: {ex.Message}");
            }

            return true; // Continue with original method
        }

        static void Postfix(Attack __instance)
        {
            try
            {
                // Restore original parameters
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
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error in Attack_DoMeleeAttack_Patch Postfix: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Attack), "Stop")]
    public static class Attack_Stop_Patch
    {
        static void Postfix(Attack __instance, Character ___m_character)
        {
            try
            {
                Character character = ___m_character;
                if (character is Player player && player == Player.m_localPlayer)
                {
                    var attackMode = EAS_InputHandler.GetAttackMode(player);
                    
                    // Reset attack mode after attack stops
                    if (attackMode != EAS_InputHandler.AttackMode.Normal)
                    {
                        ExtraAttackSystemPlugin.LogInfo("System", $"Resetting attack mode from {attackMode} to Normal after attack stop");
                        EAS_InputHandler.SetAttackMode(player, EAS_InputHandler.AttackMode.Normal);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error in Attack_Stop_Patch: {ex.Message}");
            }
        }
    }
}
