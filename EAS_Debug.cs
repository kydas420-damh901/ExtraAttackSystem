using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;

namespace ExtraAttackSystem
{
    public static class EAS_Debug
    {
        // Initialize debug configuration (called from ExtraAttackSystemPlugin.cs)
        public static void Initialize()
        {
            ExtraAttackSystemPlugin.LogInfo("System", "EAS_Debug system initialized");
        }
        
        // Public properties for checking debug states (using ExtraAttackSystemPlugin values)
        public static bool IsDebugClipNamesEnabled => ExtraAttackSystemPlugin.IsDebugClipNamesEnabled;
        public static bool IsDebugSystemMessagesEnabled => ExtraAttackSystemPlugin.IsDebugSystemMessagesEnabled;
        public static bool IsDebugAttackTriggersEnabled => ExtraAttackSystemPlugin.IsDebugAttackTriggersEnabled;
        public static bool IsDebugAOCOperationsEnabled => ExtraAttackSystemPlugin.IsDebugAOCOperationsEnabled;
        
        // Log all animation events from all clips
        public static void LogAllAnimationEvents()
        {
            ExtraAttackSystemPlugin.LogInfo("System", "=== DEBUG: ANIMATION EVENTS ===");
            
            try
            {
                var allClips = Resources.FindObjectsOfTypeAll<AnimationClip>();
                ExtraAttackSystemPlugin.LogInfo("System", $"Found {allClips.Length} animation clips");
                
                foreach (var clip in allClips)
                {
                    if (clip.events != null && clip.events.Length > 0)
                    {
                        ExtraAttackSystemPlugin.LogInfo("System", $"Clip: {clip.name} - {clip.events.Length} events");
                        foreach (var evt in clip.events)
                        {
                            ExtraAttackSystemPlugin.LogInfo("System", $"  Event: {evt.functionName} at {evt.time:F3}s");
                        }
                    }
                }
                
                ExtraAttackSystemPlugin.LogInfo("System", "=== END ANIMATION EVENTS ===");
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogInfo("System", $"Error logging animation events: {ex.Message}");
            }
        }
        
        // Log all animation clips
        public static void LogAllAnimationClips()
        {
            ExtraAttackSystemPlugin.LogInfo("System", "LogAllAnimationClips called - executing list output");
            
            ExtraAttackSystemPlugin.LogInfo("System", "=== DEBUG: ANIMATION CLIPS ===");
            
            try
            {
                var allClips = Resources.FindObjectsOfTypeAll<AnimationClip>();
                
                ExtraAttackSystemPlugin.LogInfo("System", $"Found {allClips.Length} animation clips");
                
                foreach (var clip in allClips.OrderBy(c => c.name))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", 
                        $"  Clip: [{clip.name}] - Length: {clip.length:F3}s, FrameRate: {clip.frameRate}, Legacy: {clip.legacy}, Events: {clip.events?.Length ?? 0}");
                }
                
                ExtraAttackSystemPlugin.LogInfo("System", "=== END ANIMATION CLIPS ===");
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogInfo("System", $"Error logging animation clips: {ex.Message}");
            }
        }
        
        // Log clip name during operations
        public static void LogClipName(string operation, string clipName)
        {
            if (!IsDebugClipNamesEnabled) return;
            
            ExtraAttackSystemPlugin.LogInfo("System", $"{operation}: {clipName}");
        }
        
        // Log clip name with additional info
        public static void LogClipName(string operation, string clipName, string additionalInfo)
        {
            if (!IsDebugClipNamesEnabled) return;
            
            ExtraAttackSystemPlugin.LogInfo("System", $"{operation}: {clipName} - {additionalInfo}");
        }

        // Log all animation parameters, events, and attack parameters (one-time output)
        public static void LogAllAnimationParameters()
        {
            ExtraAttackSystemPlugin.LogInfo("System", "=== DEBUG: ANIMATION PARAMETERS ===");
            
            try
            {
                var animators = Resources.FindObjectsOfTypeAll<Animator>();
                ExtraAttackSystemPlugin.LogInfo("System", $"Found {animators.Length} animators");
                
                foreach (var animator in animators)
                {
                    if (animator.runtimeAnimatorController != null)
                    {
                        ExtraAttackSystemPlugin.LogInfo("System", $"Animator: {animator.name}");
                        
                        var controller = animator.runtimeAnimatorController;
                        ExtraAttackSystemPlugin.LogInfo("System", $"  Controller: {controller.name}");
                        
                        // Log parameters - simplified for compatibility
                        ExtraAttackSystemPlugin.LogInfo("System", $"    Controller type: {controller.GetType().Name}");
                    }
                }
                
                ExtraAttackSystemPlugin.LogInfo("System", "=== END ANIMATION PARAMETERS ===");
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogInfo("System", $"Error logging animation parameters: {ex.Message}");
            }
        }

        // Log attack trigger information
        public static void LogAttackTrigger(string triggerName, string playerName = "")
        {
            if (!IsDebugAttackTriggersEnabled) return;
            
            ExtraAttackSystemPlugin.LogInfo("System", $"Attack Trigger: {triggerName} (Player: {playerName})");
        }

        // Log AOC operations
        public static void LogAOCOperation(string operation, string details = "")
        {
            if (!IsDebugAOCOperationsEnabled) return;
            
            ExtraAttackSystemPlugin.LogInfo("System", $"AOC Operation: {operation} - {details}");
        }

        // Log current weapon's complete data
        public static void LogWeaponAttackParameters()
        {
            try
            {
                if (Player.m_localPlayer == null)
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "Player not available");
                    return;
                }

                var weapon = Player.m_localPlayer.GetCurrentWeapon();
                if (weapon == null)
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "No weapon equipped");
                    return;
                }

                LogWeaponData(weapon, "CURRENT WEAPON");
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error logging weapon attack parameters: {ex.Message}");
            }
        }

        // Log complete weapon data
        private static void LogWeaponData(ItemDrop.ItemData weapon, string title)
        {
            ExtraAttackSystemPlugin.LogInfo("System", $"=== {title} ===");
            ExtraAttackSystemPlugin.LogInfo("System", $"Weapon: {weapon.m_shared.m_name}");
            ExtraAttackSystemPlugin.LogInfo("System", $"Skill Type: {weapon.m_shared.m_skillType}");
            ExtraAttackSystemPlugin.LogInfo("System", $"Item Type: {weapon.m_shared.m_itemType}");
            ExtraAttackSystemPlugin.LogInfo("System", $"Weight: {weapon.m_shared.m_weight}");
            ExtraAttackSystemPlugin.LogInfo("System", $"Value: {weapon.m_shared.m_value}");
            
            // Attack Parameters
            if (ExtraAttackSystemPlugin.DebugWeaponAttackParams.Value)
            {
                LogAttackParameters(weapon.m_shared.m_attack, "Primary Attack");
                if (weapon.m_shared.m_secondaryAttack != null)
                {
                    LogAttackParameters(weapon.m_shared.m_secondaryAttack, "Secondary Attack");
                }
            }
            
            // Timing Data
            if (ExtraAttackSystemPlugin.DebugWeaponTimingData.Value)
            {
                LogTimingData(weapon.m_shared.m_attack, "Primary Attack");
                if (weapon.m_shared.m_secondaryAttack != null)
                {
                    LogTimingData(weapon.m_shared.m_secondaryAttack, "Secondary Attack");
                }
            }
            
            // Animation Data
            if (ExtraAttackSystemPlugin.DebugWeaponAnimationData.Value)
            {
                LogAnimationData(weapon.m_shared.m_attack, "Primary Attack");
                if (weapon.m_shared.m_secondaryAttack != null)
                {
                    LogAnimationData(weapon.m_shared.m_secondaryAttack, "Secondary Attack");
                }
            }
            
            // Cost Data
            if (ExtraAttackSystemPlugin.DebugWeaponCostData.Value)
            {
                LogCostData(weapon.m_shared.m_attack, "Primary Attack");
                if (weapon.m_shared.m_secondaryAttack != null)
                {
                    LogCostData(weapon.m_shared.m_secondaryAttack, "Secondary Attack");
                }
            }
            
            // Damage Data
            if (ExtraAttackSystemPlugin.DebugWeaponCostData.Value)
            {
                LogDamageData(weapon.m_shared.m_attack, "Primary Attack");
                if (weapon.m_shared.m_secondaryAttack != null)
                {
                    LogDamageData(weapon.m_shared.m_secondaryAttack, "Secondary Attack");
                }
            }
            
            // Projectile Data
            if (ExtraAttackSystemPlugin.DebugWeaponCostData.Value)
            {
                LogProjectileData(weapon.m_shared.m_attack, "Primary Attack");
                if (weapon.m_shared.m_secondaryAttack != null)
                {
                    LogProjectileData(weapon.m_shared.m_secondaryAttack, "Secondary Attack");
                }
            }
            
            ExtraAttackSystemPlugin.LogInfo("System", $"=== END {title} ===");
        }

        // Log attack parameters
        private static void LogAttackParameters(Attack attack, string attackType)
        {
            ExtraAttackSystemPlugin.LogInfo("System", $"--- {attackType} Parameters ---");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackType: {attack.m_attackType}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackRange: {attack.m_attackRange}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackHeight: {attack.m_attackHeight}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackOffset: {attack.m_attackOffset}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackAngle: {attack.m_attackAngle}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackRayWidth: {attack.m_attackRayWidth}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackRayWidthCharExtra: {attack.m_attackRayWidthCharExtra}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackHeightChar1: {attack.m_attackHeightChar1}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackHeightChar2: {attack.m_attackHeightChar2}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_maxYAngle: {attack.m_maxYAngle}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackOriginJoint: {attack.m_attackOriginJoint}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_lowerDamagePerHit: {attack.m_lowerDamagePerHit}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_hitTerrain: {attack.m_hitTerrain}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_hitFriendly: {attack.m_hitFriendly}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_hitThroughWalls: {attack.m_hitThroughWalls}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_multiHit: {attack.m_multiHit}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_pickaxeSpecial: {attack.m_pickaxeSpecial}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_hitPointtype: {attack.m_hitPointtype}");
        }

        // Log timing data
        private static void LogTimingData(Attack attack, string attackType)
        {
            ExtraAttackSystemPlugin.LogInfo("System", $"--- {attackType} Timing ---");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackAnimation: {attack.m_attackAnimation}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_chargeAnimationBool: {attack.m_chargeAnimationBool}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackRandomAnimations: {attack.m_attackRandomAnimations}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackChainLevels: {attack.m_attackChainLevels}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_loopingAttack: {attack.m_loopingAttack}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackStartNoise: {attack.m_attackStartNoise}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackHitNoise: {attack.m_attackHitNoise}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_speedFactor: {attack.m_speedFactor}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_speedFactorRotation: {attack.m_speedFactorRotation}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_lastChainDamageMultiplier: {attack.m_lastChainDamageMultiplier}");
        }

        // Log animation data
        private static void LogAnimationData(Attack attack, string attackType)
        {
            ExtraAttackSystemPlugin.LogInfo("System", $"--- {attackType} Animation ---");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackAnimation: {attack.m_attackAnimation}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_chargeAnimationBool: {attack.m_chargeAnimationBool}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_drawAnimationState: {attack.m_drawAnimationState}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_drawDurationMin: {attack.m_drawDurationMin}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_bowDraw: {attack.m_bowDraw}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_drawVelocityCurve: {(attack.m_drawVelocityCurve != null ? "Set" : "Null")}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_reloadAnimation: {attack.m_reloadAnimation}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_reloadTime: {attack.m_reloadTime}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_requiresReload: {attack.m_requiresReload}");
        }

        // Log cost data
        private static void LogCostData(Attack attack, string attackType)
        {
            ExtraAttackSystemPlugin.LogInfo("System", $"--- {attackType} Costs ---");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackStamina: {attack.m_attackStamina}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackEitr: {attack.m_attackEitr}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackHealth: {attack.m_attackHealth}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackHealthPercentage: {attack.m_attackHealthPercentage}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackHealthReturnHit: {attack.m_attackHealthReturnHit}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackAdrenaline: {attack.m_attackAdrenaline}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackUseAdrenaline: {attack.m_attackUseAdrenaline}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_drawStaminaDrain: {attack.m_drawStaminaDrain}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_drawEitrDrain: {attack.m_drawEitrDrain}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_reloadStaminaDrain: {attack.m_reloadStaminaDrain}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_reloadEitrDrain: {attack.m_reloadEitrDrain}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_staminaReturnPerMissingHP: {attack.m_staminaReturnPerMissingHP}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_selfDamage: {attack.m_selfDamage}");
        }

        // Log damage data
        private static void LogDamageData(Attack attack, string attackType)
        {
            ExtraAttackSystemPlugin.LogInfo("System", $"--- {attackType} Damage ---");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_damageMultiplier: {attack.m_damageMultiplier}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_damageMultiplierPerMissingHP: {attack.m_damageMultiplierPerMissingHP}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_damageMultiplierByTotalHealthMissing: {attack.m_damageMultiplierByTotalHealthMissing}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_forceMultiplier: {attack.m_forceMultiplier}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_staggerMultiplier: {attack.m_staggerMultiplier}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_recoilPushback: {attack.m_recoilPushback}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_raiseSkillAmount: {attack.m_raiseSkillAmount}");
        }

        // Log projectile data
        private static void LogProjectileData(Attack attack, string attackType)
        {
            ExtraAttackSystemPlugin.LogInfo("System", $"--- {attackType} Projectile ---");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_attackProjectile: {(attack.m_attackProjectile != null ? attack.m_attackProjectile.name : "Null")}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_projectileVel: {attack.m_projectileVel}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_projectileVelMin: {attack.m_projectileVelMin}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_projectileAccuracy: {attack.m_projectileAccuracy}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_projectileAccuracyMin: {attack.m_projectileAccuracyMin}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_projectiles: {attack.m_projectiles}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_projectileBursts: {attack.m_projectileBursts}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_burstInterval: {attack.m_burstInterval}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_launchAngle: {attack.m_launchAngle}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_randomVelocity: {attack.m_randomVelocity}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_skillAccuracy: {attack.m_skillAccuracy}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_useCharacterFacing: {attack.m_useCharacterFacing}");
            ExtraAttackSystemPlugin.LogInfo("System", $"  m_useCharacterFacingYAim: {attack.m_useCharacterFacingYAim}");
        }

        // Log all weapon types' complete data
        public static void LogAllWeaponTypes()
        {
            try
            {
                if (ObjectDB.instance == null)
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "ObjectDB not available");
                    return;
                }

                ExtraAttackSystemPlugin.LogInfo("System", "=== ALL WEAPON TYPES COMPLETE DATA ===");
                
                int weaponCount = 0;
                foreach (var item in ObjectDB.instance.m_items)
                {
                    var itemDrop = item.GetComponent<ItemDrop>();
                    if (itemDrop == null || itemDrop.m_itemData == null)
                        continue;

                    var itemData = itemDrop.m_itemData;
                    
                    // Check if it's a weapon (has attack data)
                    if (itemData.m_shared.m_attack == null || 
                        string.IsNullOrEmpty(itemData.m_shared.m_attack.m_attackAnimation))
                        continue;

                    weaponCount++;
                    LogWeaponData(itemData, $"WEAPON #{weaponCount}: {itemData.m_shared.m_name}");
                }
                
                ExtraAttackSystemPlugin.LogInfo("System", $"\n=== END ALL WEAPON TYPES (Total: {weaponCount}) ===");
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error logging all weapon types: {ex.Message}");
            }
        }
    }
}
