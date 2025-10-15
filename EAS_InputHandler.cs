using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace ExtraAttackSystem
{
    public static class EAS_InputHandler
    {
        // Attack mode enum
        public enum AttackMode
        {
            Normal,         // Left click (vanilla animation)
            secondary_Q,    // Q key - secondary_Q attack
            secondary_T,    // T key - secondary_T attack
            secondary_G,    // G key - secondary_G attack
        }

        // Attack mode tracking
        private static readonly Dictionary<Player, AttackMode> playerAttackModes = new();
        private static readonly Dictionary<Player, Dictionary<AttackMode, float>> playerCooldowns = new();
        
        // Key press flags to prevent continuous triggering
        private static bool _qKeyPressed = false;
        private static bool _tKeyPressed = false;
        private static bool _gKeyPressed = false;

        // Attack mode management
        public static void SetAttackMode(Player player, AttackMode mode)
        {
            if (player != null)
            {
                playerAttackModes[player] = mode;
            }
        }

        public static AttackMode GetAttackMode(Player player)
        {
            if (player != null && playerAttackModes.TryGetValue(player, out var mode))
            {
                return mode;
            }
            return AttackMode.Normal;
        }

        public static bool IsPlayerInExtraAttack(Player player)
        {
            if (player == null) return false;
            
            var mode = GetAttackMode(player);
            if (mode == AttackMode.Normal) return false;
            
            // 攻撃中でない場合は自動リセット
            if (!player.InAttack())
            {
                ResetAttackMode(player);
                return false;
            }
            
            return true;
        }

        // Cooldown management - per button
        public static bool IsPlayerOnCooldown(Player player, AttackMode mode)
        {
            if (player == null || mode == AttackMode.Normal)
                return false;

            if (playerCooldowns.TryGetValue(player, out var cooldowns))
            {
                if (cooldowns.TryGetValue(mode, out float cooldownTime))
                {
                    return cooldownTime > Time.time;
                }
            }
            return false;
        }

        public static float GetPlayerCooldownRemaining(Player player, AttackMode mode)
        {
            if (player == null || mode == AttackMode.Normal)
                return 0f;

            if (playerCooldowns.TryGetValue(player, out var cooldowns))
            {
                if (cooldowns.TryGetValue(mode, out float cooldownTime))
                {
                    return Mathf.Max(0f, cooldownTime - Time.time);
                }
            }
            return 0f;
        }

        public static void SetPlayerCooldown(Player player, AttackMode mode)
        {
            if (player == null || mode == AttackMode.Normal)
                return;

            if (!playerCooldowns.ContainsKey(player))
            {
                playerCooldowns[player] = new Dictionary<AttackMode, float>();
            }

            // Get cooldown from config
            float cooldownDuration = 0f;
            try
            {
                if (player.GetCurrentWeapon() != null)
                {
                    string weaponType = GetWeaponTypeFromSkill(player.GetCurrentWeapon().m_shared.m_skillType, player.GetCurrentWeapon());
                    string modeString = mode.ToString();
                    
                    float attackCooldown = EAS_CostConfig.GetAttackCooldown(weaponType, modeString);
                    if (attackCooldown > 0f)
                    {
                        cooldownDuration = attackCooldown;
                    }
                }
                
                if (cooldownDuration <= 0f)
                {
                    cooldownDuration = 2.0f; // Default cooldown
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error getting cooldown, using default: {ex.Message}");
                cooldownDuration = 2.0f;
            }
            
            playerCooldowns[player][mode] = Time.time + cooldownDuration;
        }

        public static void CleanupPlayer(Player player)
        {
            if (player != null)
            {
                playerAttackModes.Remove(player);
                playerCooldowns.Remove(player);
            }
        }

        // Reset attack mode to Normal
        public static void ResetAttackMode(Player player)
        {
            if (player != null)
            {
                playerAttackModes[player] = AttackMode.Normal;
            }
        }

        // Check if UI is blocking input
        private static bool IsUIBlocking()
        {
            return (Chat.instance != null && Chat.instance.HasFocus()) ||
                   Console.IsVisible() ||
                   TextInput.IsVisible() ||
                   StoreGui.IsVisible() ||
                   InventoryGui.IsVisible() ||
                   Menu.IsVisible() ||
                   Minimap.IsOpen() ||
                   (TextViewer.instance != null && TextViewer.instance.IsVisible()) ||
                   GameCamera.InFreeFly() ||
                   Hud.InRadial();
        }

        // Weapon type detection
        public static string GetWeaponTypeFromSkill(Skills.SkillType skillType, ItemDrop.ItemData? weapon = null)
        {
            // Check if it's a 2H weapon
            bool is2H = false;
            if (weapon?.m_shared != null)
            {
                var itemType = weapon.m_shared.m_itemType;
                is2H = itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon || 
                       itemType == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft;
            }

            // Convert SkillType to WeaponType
            if (skillType == Skills.SkillType.Axes)
            {
                return is2H ? "Battleaxe" : "Axe";
            }
            else if (skillType == Skills.SkillType.Swords)
            {
                return is2H ? "Greatsword" : "Sword";
            }
            else if (skillType == Skills.SkillType.Clubs)
            {
                return "Club";
            }
            else if (skillType == Skills.SkillType.Spears)
            {
                return "Spear";
            }
            else if (skillType == Skills.SkillType.Knives)
            {
                return "Knife";
            }
            else if (skillType == Skills.SkillType.Unarmed)
            {
                return "Fist";
            }
            else if (skillType == Skills.SkillType.Polearms)
            {
                return "Polearm";
            }
            else
            {
                return "Sword"; // Default fallback
            }
        }

        // Check if player can use extra attack
        public static bool CanUseExtraAttack(Player player, AttackMode mode)
        {
            // TODO: 一時的なデバッグログ - 条件チェック詳細（問題解決後に削除予定）
            if (player == null) 
            {
                ExtraAttackSystemPlugin.LogInfo("System", "CanUseExtraAttack: player is null");
                return false;
            }
            if (mode == AttackMode.Normal) 
            {
                ExtraAttackSystemPlugin.LogInfo("System", "CanUseExtraAttack: mode is Normal");
                return false;
            }

            // Check UI blocking
            if (IsUIBlocking())
            {
                ExtraAttackSystemPlugin.LogInfo("System", "CanUseExtraAttack: UI is blocking");
                return false;
            }

            // Check player state blocking
            if (player.IsDead() || player.InCutscene() || player.IsTeleporting())
            {
                ExtraAttackSystemPlugin.LogInfo("System", "CanUseExtraAttack: player state blocking");
                return false;
            }

            // Check interaction blocking
            if (player.InAttack() || player.InDodge() || player.IsBlocking() || player.InMinorAction())
            {
                ExtraAttackSystemPlugin.LogInfo("System", "CanUseExtraAttack: player in blocking action");
                return false;
            }

            // Check if player is in another extra attack
            if (IsPlayerInExtraAttack(player)) 
            {
                ExtraAttackSystemPlugin.LogInfo("System", "CanUseExtraAttack: player already in extra attack");
                return false;
            }

            // Check cooldown
            if (IsPlayerOnCooldown(player, mode)) 
            {
                ExtraAttackSystemPlugin.LogInfo("System", $"CanUseExtraAttack: player on cooldown for {mode}");
                return false;
            }

            // Check if weapon is equipped
            var weapon = player.GetCurrentWeapon();
            if (weapon == null) 
            {
                ExtraAttackSystemPlugin.LogInfo("System", "CanUseExtraAttack: no weapon equipped");
                return false;
            }

            // Check if weapon has secondary attack
            if (!weapon.HaveSecondaryAttack())
            {
                ExtraAttackSystemPlugin.LogInfo("System", "CanUseExtraAttack: weapon has no secondary attack");
                return false;
            }

            // Check if weapon is excluded
            if (EAS_ExclusionConfig.IsItemExcluded(weapon)) 
            {
                ExtraAttackSystemPlugin.LogInfo("System", "CanUseExtraAttack: weapon is excluded");
                return false;
            }

            // Check stamina
            string weaponType = GetWeaponTypeFromSkill(weapon.m_shared.m_skillType, weapon);
            float staminaCost = EAS_CostConfig.GetStaminaCost(weaponType, mode.ToString());
            if (player.GetStamina() < staminaCost) 
            {
                ExtraAttackSystemPlugin.LogInfo("System", $"CanUseExtraAttack: insufficient stamina ({player.GetStamina()}/{staminaCost})");
                return false;
            }

            // Check eitr
            float eitrCost = EAS_CostConfig.GetEitrCost(weaponType, mode.ToString());
            if (eitrCost > 0f && player.GetEitr() < eitrCost)
            {
                if (ExtraAttackSystemPlugin.IsDebugSystemMessagesEnabled)
                {
                    ExtraAttackSystemPlugin.LogInfo("System", $"CanUseExtraAttack: not enough eitr (need {eitrCost}, have {player.GetEitr()})");
                }
                return false;
            }

            ExtraAttackSystemPlugin.LogInfo("System", "CanUseExtraAttack: all conditions met");
            return true;
        }

        // Handle key input
        public static void HandleKeyInput(Player player)
        {
            if (player == null) return;

            // Q key - with flag to prevent continuous triggering
            if (ExtraAttackSystemPlugin.ExtraAttackKey_Q.Value.IsDown() && !_qKeyPressed)
            {
                // TODO: 一時的なデバッグログ - Qキー処理確認用（問題解決後に削除予定）
                ExtraAttackSystemPlugin.LogInfo("System", "Q key pressed - checking conditions");
                if (CanUseExtraAttack(player, AttackMode.secondary_Q))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "Q key - conditions met, triggering attack");
                    TriggerExtraAttack(player, AttackMode.secondary_Q);
                    _qKeyPressed = true;
                }
                else
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "Q key - conditions not met");
                }
            }
            else if (!ExtraAttackSystemPlugin.ExtraAttackKey_Q.Value.IsDown())
            {
                _qKeyPressed = false;
            }

            // T key - with flag to prevent continuous triggering
            if (ExtraAttackSystemPlugin.EnableTKey.Value && 
                ExtraAttackSystemPlugin.ExtraAttackKey_T.Value.IsDown() && !_tKeyPressed)
            {
                // TODO: 一時的なデバッグログ - Tキー処理確認用（問題解決後に削除予定）
                ExtraAttackSystemPlugin.LogInfo("System", "T key pressed - checking conditions");
                if (CanUseExtraAttack(player, AttackMode.secondary_T))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "T key - conditions met, triggering attack");
                    TriggerExtraAttack(player, AttackMode.secondary_T);
                    _tKeyPressed = true;
                }
                else
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "T key - conditions not met");
                }
            }
            else if (!ExtraAttackSystemPlugin.ExtraAttackKey_T.Value.IsDown())
            {
                _tKeyPressed = false;
            }

            // G key - with flag to prevent continuous triggering
            if (ExtraAttackSystemPlugin.EnableGKey.Value && 
                ExtraAttackSystemPlugin.ExtraAttackKey_G.Value.IsDown() && !_gKeyPressed)
            {
                // TODO: 一時的なデバッグログ - Gキー処理確認用（問題解決後に削除予定）
                ExtraAttackSystemPlugin.LogInfo("System", "G key pressed - checking conditions");
                if (CanUseExtraAttack(player, AttackMode.secondary_G))
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "G key - conditions met, triggering attack");
                    TriggerExtraAttack(player, AttackMode.secondary_G);
                    _gKeyPressed = true;
                }
                else
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "G key - conditions not met");
                }
            }
            else if (!ExtraAttackSystemPlugin.ExtraAttackKey_G.Value.IsDown())
            {
                _gKeyPressed = false;
            }
            
            // Reset attack mode for vanilla secondary attack (right click)
            if (Input.GetMouseButtonDown(1)) // Right mouse button
            {
                SetAttackMode(player, AttackMode.Normal);
                ExtraAttackSystemPlugin.LogInfo("System", "Vanilla secondary attack detected, reset attack mode to Normal");
            }
        }

        // Trigger extra attack
        private static void TriggerExtraAttack(Player player, AttackMode mode)
        {
            try
            {
                // Trigger actual attack - this will call Humanoid.StartAttack which is patched
                // TODO: 一時的なデバッグログ - 攻撃実行確認用（問題解決後に削除予定）
                ExtraAttackSystemPlugin.LogInfo("System", $"Triggering actual attack for {mode}");
                
                // Set attack mode before calling StartAttack
                SetAttackMode(player, mode);
                
                // Update AOC replacementMap and apply timing BEFORE calling StartAttack
                var weapon = player.GetCurrentWeapon();
                if (weapon != null)
                {
                    string weaponType = GetWeaponTypeFromSkill(weapon.m_shared.m_skillType, weapon);
                    
                    // Update AOC replacementMap for this mode (永続AOC)
                    EAS_AnimationManager.UpdateAOCForMode(player, weaponType, mode.ToString());
                    
                    // Apply timing
                    var timing = EAS_AnimationTiming.GetTiming($"{weaponType}_{mode}");
                    ApplyAnimationTiming(player, timing);
                }
                
                // Call StartAttack - this will trigger the Humanoid.StartAttack patch
                // バニラの処理でスタミナ・エイトル消費、攻撃判定などが行われる
                // TODO: 一時的なデバッグログ - StartAttack呼び出し確認用（問題解決後に削除予定）
                ExtraAttackSystemPlugin.LogInfo("System", $"About to call player.StartAttack for {mode}");
                bool attackResult = player.StartAttack(null, true); // true = secondary attack
                // TODO: 一時的なデバッグログ - StartAttack戻り値確認用（問題解決後に削除予定）
                ExtraAttackSystemPlugin.LogInfo("System", $"player.StartAttack returned: {attackResult}");
                
                if (attackResult)
                {
                    ExtraAttackSystemPlugin.LogInfo("System", $"Attack started successfully for {mode}");
                }
                else
                {
                    ExtraAttackSystemPlugin.LogInfo("System", $"Attack failed to start for {mode}");
                }

                // Set cooldown
                SetPlayerCooldown(player, mode);

                // Don't reset attack mode here - let Attack.Stop patch handle it
                // SetAttackMode(player, AttackMode.Normal); // Moved to Attack.Stop patch

                ExtraAttackSystemPlugin.LogInfo("System", $"Extra attack triggered: {mode} for player {player.GetPlayerName()}");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error triggering extra attack: {ex.Message}");
            }
        }

        private static void ApplyAnimationTiming(Player player, EAS_AnimationTiming.AnimationTiming timing)
        {
            try
            {
                // Apply timing to player's attack system using reflection
                var attackField = typeof(Character).GetField("m_attack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (attackField != null)
                {
                    var attack = attackField.GetValue(player);
                    if (attack != null)
                    {
                        // Set timing values using reflection
                        var hitTimingField = attack.GetType().GetField("m_hitTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var trailOnField = attack.GetType().GetField("m_trailOnTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var trailOffField = attack.GetType().GetField("m_trailOffTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (hitTimingField != null)
                            hitTimingField.SetValue(attack, timing.HitTiming);
                        if (trailOnField != null)
                            trailOnField.SetValue(attack, timing.TrailOnTiming);
                        if (trailOffField != null)
                            trailOffField.SetValue(attack, timing.TrailOffTiming);
                    }
                }
                
                if (ExtraAttackSystemPlugin.IsDebugSystemMessagesEnabled)
                {
                    ExtraAttackSystemPlugin.LogInfo("System", $"Applied timing - Hit: {timing.HitTiming:F3}s, TrailOn: {timing.TrailOnTiming:F3}s, TrailOff: {timing.TrailOffTiming:F3}s");
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error applying animation timing: {ex.Message}");
            }
        }

        // Get trigger name for attack mode
        public static string GetTriggerNameForMode(AttackMode mode)
        {
            // Get current weapon to determine the correct trigger name
            var player = Player.m_localPlayer;
            if (player == null) return "";

            var weapon = player.GetCurrentWeapon();
            if (weapon == null) return "";

            string weaponType = GetWeaponTypeFromSkill(weapon.m_shared.m_skillType, weapon);
            
            // Return weapon-specific secondary trigger name
            return weaponType switch
            {
                "Sword" => "sword_secondary",
                "Greatsword" => "greatsword_secondary",
                "Axe" => "axe_secondary",
                "Battleaxe" => "battleaxe_secondary",
                "Club" => "mace_secondary",
                "Spear" => "spear_secondary",
                "Knife" => "knife_secondary",
                "Fist" => "fist_secondary",
                "Shield" => "shield_secondary",
                _ => "sword_secondary" // fallback
            };
        }
    }
}
