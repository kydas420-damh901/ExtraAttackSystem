using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace ExtraAttackSystem
{
    // Store original attack parameters for restoration
    public class AttackParams
    {
        public float m_attackRange;
        public float m_attackHeight;
        public float m_attackAngle;
        public float m_attackRayWidth;
        public float m_attackRayWidthCharExtra;
        public float m_attackHeightChar1;
        public float m_attackHeightChar2;
        public float m_maxYAngle;
    }

    public static class EAS_PlayerPatches
    {
        // Store original attack parameters
        private static readonly Dictionary<Attack, AttackParams> _originalAttackParams = new();

        // Store original attack parameters for restoration
        public static void StoreOriginalAttackParams(Attack attack)
        {
            if (attack != null && !_originalAttackParams.ContainsKey(attack))
            {
                _originalAttackParams[attack] = new AttackParams
                {
                    m_attackRange = attack.m_attackRange,
                    m_attackHeight = attack.m_attackHeight,
                    m_attackAngle = attack.m_attackAngle,
                    m_attackRayWidth = attack.m_attackRayWidth,
                    m_attackRayWidthCharExtra = attack.m_attackRayWidthCharExtra,
                    m_attackHeightChar1 = attack.m_attackHeightChar1,
                    m_attackHeightChar2 = attack.m_attackHeightChar2,
                    m_maxYAngle = attack.m_maxYAngle
                };
            }
        }

        // Restore original attack parameters
        public static void RestoreOriginalAttackParams(Attack attack)
        {
            if (attack != null && _originalAttackParams.TryGetValue(attack, out var originalParams))
            {
                attack.m_attackRange = originalParams.m_attackRange;
                attack.m_attackHeight = originalParams.m_attackHeight;
                attack.m_attackAngle = originalParams.m_attackAngle;
                attack.m_attackRayWidth = originalParams.m_attackRayWidth;
                attack.m_attackRayWidthCharExtra = originalParams.m_attackRayWidthCharExtra;
                attack.m_attackHeightChar1 = originalParams.m_attackHeightChar1;
                attack.m_attackHeightChar2 = originalParams.m_attackHeightChar2;
                attack.m_maxYAngle = originalParams.m_maxYAngle;
                
                _originalAttackParams.Remove(attack);
            }
        }
    }

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
                        // Apply YAML values to weapon's m_secondaryAttack before vanilla StartAttack
                        var weapon = player.GetCurrentWeapon();
                        if (weapon != null && weapon.m_shared.m_secondaryAttack != null)
                        {
                            string weaponType = EAS_InputHandler.GetWeaponTypeFromSkill(weapon.m_shared.m_skillType, weapon);
                            var timing = EAS_AnimationTiming.GetTimingDirect(weaponType, attackMode.ToString());
                            
                            if (timing != null)
                            {
                                // TODO: 一時的なデバッグログ - 武器値適用確認用（問題解決後に削除予定）
                                // Debug: Log original values
                                ExtraAttackSystemPlugin.LogInfo("System", $"Original weapon values - Range: {weapon.m_shared.m_secondaryAttack.m_attackRange}, Height: {weapon.m_shared.m_secondaryAttack.m_attackHeight}, Angle: {weapon.m_shared.m_secondaryAttack.m_attackAngle}");
                                
                                // Debug: Log YAML values
                                ExtraAttackSystemPlugin.LogInfo("System", $"YAML values - Range: {timing.AttackRange}, Height: {timing.AttackHeight}, Angle: {timing.AttackAngle}");
                                
                                // Store original values for restoration
                                EAS_PlayerPatches.StoreOriginalAttackParams(weapon.m_shared.m_secondaryAttack);
                                
                                // Apply YAML values to weapon's secondary attack
                                weapon.m_shared.m_secondaryAttack.m_attackRange = timing.AttackRange;
                                weapon.m_shared.m_secondaryAttack.m_attackHeight = timing.AttackHeight;
                                weapon.m_shared.m_secondaryAttack.m_attackAngle = timing.AttackAngle;
                                weapon.m_shared.m_secondaryAttack.m_attackRayWidth = timing.AttackRayWidth;
                                weapon.m_shared.m_secondaryAttack.m_attackRayWidthCharExtra = timing.AttackRayWidthCharExtra;
                                weapon.m_shared.m_secondaryAttack.m_attackHeightChar1 = timing.AttackHeightChar1;
                                weapon.m_shared.m_secondaryAttack.m_attackHeightChar2 = timing.AttackHeightChar2;
                                weapon.m_shared.m_secondaryAttack.m_maxYAngle = timing.MaxYAngle;
                                
                                // Debug: Log applied values
                                ExtraAttackSystemPlugin.LogInfo("System", $"Applied weapon values - Range: {weapon.m_shared.m_secondaryAttack.m_attackRange}, Height: {weapon.m_shared.m_secondaryAttack.m_attackHeight}, Angle: {weapon.m_shared.m_secondaryAttack.m_attackAngle}");
                                
                                ExtraAttackSystemPlugin.LogInfo("System", $"Applied YAML values to weapon's m_secondaryAttack for {attackMode}");
                            }
                            else
                            {
                                // TODO: 一時的なデバッグログ - YAML設定未発見確認用（問題解決後に削除予定）
                                ExtraAttackSystemPlugin.LogWarning("System", $"No YAML config found for {weaponType}_{attackMode}");
                            }
                        }
                        
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
                    // Restore original weapon parameters after attack
                    var weapon = player.GetCurrentWeapon();
                    if (weapon != null && weapon.m_shared.m_secondaryAttack != null)
                    {
                        EAS_PlayerPatches.RestoreOriginalAttackParams(weapon.m_shared.m_secondaryAttack);
                        ExtraAttackSystemPlugin.LogInfo("System", $"Restored original weapon parameters for {attackMode}");
                    }
                    
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
        static void Prefix(Attack __instance, Character ___m_character)
        {
            try
            {
                Character character = ___m_character;
                if (character is Player player && player == Player.m_localPlayer)
                {
                    var attackMode = EAS_InputHandler.GetAttackMode(player);
                    if (attackMode != EAS_InputHandler.AttackMode.Normal)
                    {
                        // TODO: 一時的なデバッグログ - DoMeleeAttack値確認用（問題解決後に削除予定）
                        // Debug: Log actual values used in DoMeleeAttack
                        ExtraAttackSystemPlugin.LogInfo("System", $"DoMeleeAttack values - Range: {__instance.m_attackRange}, Height: {__instance.m_attackHeight}, Angle: {__instance.m_attackAngle}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error in Attack_DoMeleeAttack_Patch: {ex.Message}");
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
