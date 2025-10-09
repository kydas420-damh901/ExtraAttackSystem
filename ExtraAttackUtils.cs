using System.Collections.Generic;
using UnityEngine;
using Jotunn.Managers;
using HarmonyLib;

namespace ExtraAttackSystem
{
    public static class ExtraAttackUtils
    {
        // Attack mode enum
        public enum AttackMode
        {
            Normal,         // Left click (vanilla animation)
            ea_secondary_Q, // Q key - ea_secondary_Q attack
            ea_secondary_T, // T key - ea_secondary_T attack
            ea_secondary_G, // G key - ea_secondary_G attack
        }

        // Attack mode tracking
        private static readonly Dictionary<Player, AttackMode> playerAttackModes = new();
        private static readonly Dictionary<Player, Dictionary<AttackMode, float>> playerCooldowns = new();
        // NEW: Flag to nullify previousAttack on next Attack.Start after exiting Extra mode
        private static readonly Dictionary<Player, bool> playerNullifyNextChain = new();
        // NEW: Pair-specific block flags to prevent combo for the very next attack
        private static readonly Dictionary<Player, bool> playerBlockNextPrimary = new();
        private static readonly Dictionary<Player, bool> playerBlockNextSecondary = new();
        // NEW: Continuous block flag during vanilla chain window after exiting Extra
        private static readonly Dictionary<Player, bool> playerBlockPrimaryDuringChainWindow = new();
        // NEW: Message deduplication per player
        private static readonly Dictionary<Player, string> lastMessageKey = new();
        private static readonly Dictionary<Player, float> lastMessageTime = new();
        private const float MessageRepeatThreshold = 1.0f; // seconds

        // Static message dictionary for localization
        private static readonly Dictionary<string, string> MessageDictionary = new()
        {
            { "extra_attack_triggered", "Extra Attack!" },
            { "extra_attack_cooldown", "Extra Attack on cooldown: {0}s" },
            { "extra_attack_no_stamina", "Not enough stamina for Extra Attack" },
            { "extra_attack_no_weapon", "No weapon equipped" },
            { "extra_attack_blocked", "Cannot use Extra Attack right now" },
            { "extra_attack_no_secondary", "Secondary attack is not defined for the equipped weapon" },
            { "extra_attack_tool_bomb_blocked", "This item type does not support extra attacks" },
            { "extra_attack_ac", "Animator Controller for {0}: {1}" }
        };

        // NEW: Track crouch state before Extra attack to restore/maintain during custom animation
        private static readonly Dictionary<Player, bool> playerWasCrouchingBeforeExtra = new();
        
        // NEW: Bypass flag to allow our next StartAttack call while otherwise blocking vanilla inputs
        private static readonly Dictionary<Player, bool> playerBypassNextStartAttack = new();
        // NEW: PostAttack emote_stop guard window end time per player
        private static readonly Dictionary<Player, float> emoteStopGuardUntil = new();
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
            var mode = GetAttackMode(player);
            return mode != AttackMode.Normal;
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

            // Try to get cooldown from YAML timing based on current animation clip
            float cooldownDuration = 0f;
            try
            {
                if (ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator animator) && animator != null)
                {
                    var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
                    if (clipInfo != null && clipInfo.Length > 0)
                    {
                        string clipName = clipInfo[0].clip.name;
                        int hitIndex = ExtraAttackPatches_Core.GetCurrentHitIndex(animator, clipInfo[0].clip);
                        string configKey = ExtraAttackPatches_Core.BuildConfigKey(player, clipName, hitIndex);
                        
                        var timing = AnimationTimingConfig.GetTiming(configKey);
                        if (timing != null && timing.CooldownSec > 0f)
                        {
                            cooldownDuration = timing.CooldownSec;
                        }
                    }
                }
                
                // Fallback to config if YAML timing not available
                if (cooldownDuration <= 0f)
                {
                    cooldownDuration = ExtraAttackPlugin.GetCooldown(mode);
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting YAML cooldown, falling back to config: {ex.Message}");
                cooldownDuration = ExtraAttackPlugin.GetCooldown(mode);
            }
            
            playerCooldowns[player][mode] = Time.time + cooldownDuration;
        }

        public static void CleanupPlayer(Player player)
        {
            if (player != null)
            {
                playerAttackModes.Remove(player);
                playerCooldowns.Remove(player);
                // NEW: cleanup nullify flag
                playerNullifyNextChain.Remove(player);
                // NEW: cleanup pair-specific flags
                playerBlockNextPrimary.Remove(player);
                playerBlockNextSecondary.Remove(player);
                // NEW: cleanup continuous block flag
                playerBlockPrimaryDuringChainWindow.Remove(player);
                // NEW: cleanup last message state
                lastMessageKey.Remove(player);
                lastMessageTime.Remove(player);
                // NEW: cleanup crouch-before-extra flag
                playerWasCrouchingBeforeExtra.Remove(player);
                // NEW: cleanup StartAttack bypass flag
                playerBypassNextStartAttack.Remove(player);
                // NEW: cleanup PostAttack emote_stop guard window
                emoteStopGuardUntil.Remove(player);
            }
        }

        // NEW: Mark that player was crouching before Extra attack
        public static void SetWasCrouchingBeforeExtraAttack(Player player, bool wasCrouching)
        {
            if (player != null)
            {
                playerWasCrouchingBeforeExtra[player] = wasCrouching;
            }
        }

        // NEW: Query whether player was crouching before Extra attack
        public static bool WasCrouchingBeforeExtraAttack(Player player)
        {
            return player != null && playerWasCrouchingBeforeExtra.TryGetValue(player, out bool wasCrouching) && wasCrouching;
        }

        // NEW: Clear stored crouch-before-extra flag
        public static void ClearWasCrouchingBeforeExtraAttack(Player player)
        {
            if (player != null)
            {
                playerWasCrouchingBeforeExtra.Remove(player);
            }
        }

        // NEW: Mark to nullify previousAttack once on next Attack.Start
        public static void MarkNullifyNextChain(Player player)
        {
            if (player != null)
            {
                playerNullifyNextChain[player] = true;
            }
        }

        // NEW: Consume flag; returns true if nullification should occur
        public static bool ConsumeNullifyNextChain(Player player)
        {
            if (player != null && playerNullifyNextChain.TryGetValue(player, out bool flag) && flag)
            {
                playerNullifyNextChain[player] = false;
                return true;
            }
            return false;
        }

        // NEW: Check flag without consuming it
        public static bool HasNullifyNextChain(Player player)
        {
            return player != null && playerNullifyNextChain.TryGetValue(player, out bool flag) && flag;
        }

        // NEW: Pair-specific block helpers
        public static void MarkBlockNextPrimary(Player player)
        {
            if (player != null)
            {
                playerBlockNextPrimary[player] = true;
            }
        }

        public static bool ConsumeBlockNextPrimary(Player player)
        {
            if (player != null && playerBlockNextPrimary.TryGetValue(player, out bool flag) && flag)
            {
                playerBlockNextPrimary[player] = false;
                return true;
            }
            return false;
        }

        public static bool HasBlockNextPrimary(Player player)
        {
            return player != null && playerBlockNextPrimary.TryGetValue(player, out bool flag) && flag;
        }

        public static void MarkBlockNextSecondary(Player player)
        {
            if (player != null)
            {
                playerBlockNextSecondary[player] = true;
            }
        }

        public static bool ConsumeBlockNextSecondary(Player player)
        {
            if (player != null && playerBlockNextSecondary.TryGetValue(player, out bool flag) && flag)
            {
                playerBlockNextSecondary[player] = false;
                return true;
            }
            return false;
        }

        public static bool HasBlockNextSecondary(Player player)
        {
            return player != null && playerBlockNextSecondary.TryGetValue(player, out bool flag) && flag;
        }

        // NEW: Continuous block helpers for LMB chain window
        public static void MarkBlockPrimaryDuringChainWindow(Player player)
        {
            if (player != null)
            {
                playerBlockPrimaryDuringChainWindow[player] = true;
            }
        }

        public static void ClearBlockPrimaryDuringChainWindow(Player player)
        {
            if (player != null)
            {
                playerBlockPrimaryDuringChainWindow.Remove(player);
                // NEW: cleanup last message state
                lastMessageKey.Remove(player);
                lastMessageTime.Remove(player);
                // NEW: cleanup crouch-before-extra flag
                playerWasCrouchingBeforeExtra.Remove(player);
                // NEW: cleanup StartAttack bypass flag
                playerBypassNextStartAttack.Remove(player);
            }
        }

        // NEW: Query continuous LMB chain block window flag
        public static bool HasBlockPrimaryDuringChainWindow(Player player)
        {
            return player != null && playerBlockPrimaryDuringChainWindow.ContainsKey(player);
        }

        // NEW: Consume continuous LMB chain block window flag (one-shot)
        public static bool ConsumeBlockPrimaryDuringChainWindow(Player player)
        {
            if (player != null && playerBlockPrimaryDuringChainWindow.TryGetValue(player, out bool flag) && flag)
            {
                playerBlockPrimaryDuringChainWindow[player] = false;
                return true;
            }
            return false;
        }

        // NEW: Allow-bypass helpers for our own StartAttack calls
        public static void MarkBypassNextStartAttack(Player player)
        {
            if (player != null)
            {
                playerBypassNextStartAttack[player] = true;
            }
        }

        public static bool ConsumeBypassNextStartAttack(Player player)
        {
            if (player != null && playerBypassNextStartAttack.TryGetValue(player, out bool flag) && flag)
            {
                playerBypassNextStartAttack[player] = false;
                return true;
            }
            return false;
        }

        public static bool HasBypassNextStartAttack(Player player)
        {
            return player != null && playerBypassNextStartAttack.TryGetValue(player, out bool flag) && flag;
        }

        // Localization helper methods
        public static string GetLocalizedString(string key, params object[] args)
        {
            // Try Jotunn localization first; fallback to built-in messages if not found
            string translated = string.Empty;
            try
            {
                translated = LocalizationManager.Instance.TryTranslate(key);
            }
            catch
            {
                translated = string.Empty;
            }

            if (!string.IsNullOrEmpty(translated) && translated != key)
            {
                return args.Length > 0 ? string.Format(translated, args) : translated;
            }

            if (MessageDictionary.TryGetValue(key, out string message))
            {
                return args.Length > 0 ? string.Format(message, args) : message;
            }
            return key; // Fallback to key if message not found
        }

        // NEW: Dedup helper to suppress rapid duplicate messages of the same key per player
        private static bool ShouldSuppressDuplicateMessage(Player player, string key)
        {
            if (player == null)
            {
                return true;
            }
            if (lastMessageKey.TryGetValue(player, out var prevKey) && prevKey == key &&
                lastMessageTime.TryGetValue(player, out var prevTime) && Time.time - prevTime < MessageRepeatThreshold)
            {
                return true;
            }
            lastMessageKey[player] = key;
            lastMessageTime[player] = Time.time;
            return false;
        }

        public static void ShowMessage(Player player, string messageKey, params object[] args)
        {
            if (player != null)
            {
                // Suppress rapid duplicate messages with the same key
                if (ShouldSuppressDuplicateMessage(player, messageKey))
                {
                    return;
                }
                string message = GetLocalizedString(messageKey, args);
                player.Message(MessageHud.MessageType.Center, message);
            }
        }

        public static float GetEffectiveStaminaCost(Player player, ItemDrop.ItemData weapon, AttackMode mode)
        {
            // Compute effective stamina cost for extra attacks before starting Attack; mirrors vanilla modifiers except home-item and missing HP (not available pre-attack)
            if (player == null || weapon == null)
            {
                return 0f;
            }

            float baseCost = 0f;
            
            // Try to get stamina cost from YAML timing based on current animation clip
            try
            {
                if (ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator animator) && animator != null)
                {
                    var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
                    if (clipInfo != null && clipInfo.Length > 0)
                    {
                        string clipName = clipInfo[0].clip.name;
                        int hitIndex = ExtraAttackPatches_Core.GetCurrentHitIndex(animator, clipInfo[0].clip);
                        string configKey = ExtraAttackPatches_Core.BuildConfigKey(player, clipName, hitIndex);
                        
                        var timing = AnimationTimingConfig.GetTiming(configKey);
                        if (timing != null && timing.StaminaCost > 0f)
                        {
                            baseCost = timing.StaminaCost;
                        }
                    }
                }
                
                // Fallback to config if YAML timing not available
                if (baseCost <= 0f)
                {
                    baseCost = ExtraAttackPlugin.GetStaminaCost(weapon.m_shared.m_skillType, mode);
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting YAML stamina cost, falling back to config: {ex.Message}");
                baseCost = ExtraAttackPlugin.GetStaminaCost(weapon.m_shared.m_skillType, mode);
            }
            
            if (baseCost <= 0f)
            {
                return 0f; // allow no-cost extra attack when configured
            }

            float cost = baseCost;
            float skillFactor = player.GetSkillFactor(weapon.m_shared.m_skillType);

            // Apply equipment stamina modifier for weapon attacks (home item flag not known at pre-check time)
            cost *= 1f + player.GetEquipmentAttackStaminaModifier();

            // Apply SEMan stamina usage modifiers
            var seMan = player.GetSEMan();
            if (seMan != null)
            {
                seMan.ModifyAttackStaminaUsage(cost, ref cost, true);
            }

            // Skill factor reduction
            cost -= cost * 0.33f * skillFactor;

            if (cost < 0f) cost = 0f;
            return cost;
        }

        public static float GetEffectiveStaminaCost(Attack attack, Player player, ItemDrop.ItemData weapon, AttackMode mode)
        {
            // Compute effective stamina cost during Attack.GetAttackStamina; mirrors vanilla logic including home-item and missing HP
            if (attack == null || player == null || weapon == null)
            {
                return 0f;
            }

            float baseCost = 0f;
            
            // Try to get stamina cost from YAML timing based on current animation clip
            try
            {
                if (ExtraAttackPatches_Core.TryGetPlayerAnimator(player, out Animator animator) && animator != null)
                {
                    var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
                    if (clipInfo != null && clipInfo.Length > 0)
                    {
                        string clipName = clipInfo[0].clip.name;
                        int hitIndex = ExtraAttackPatches_Core.GetCurrentHitIndex(animator, clipInfo[0].clip);
                        string configKey = ExtraAttackPatches_Core.BuildConfigKey(player, clipName, hitIndex);
                        
                        var timing = AnimationTimingConfig.GetTiming(configKey);
                        if (timing != null && timing.StaminaCost > 0f)
                        {
                            baseCost = timing.StaminaCost;
                        }
                    }
                }
                
                // Fallback to config if YAML timing not available
                if (baseCost <= 0f)
                {
                    baseCost = ExtraAttackPlugin.GetStaminaCost(weapon.m_shared.m_skillType, mode);
                }
            }
            catch (System.Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error getting YAML stamina cost, falling back to config: {ex.Message}");
                baseCost = ExtraAttackPlugin.GetStaminaCost(weapon.m_shared.m_skillType, mode);
            }

            if (baseCost <= 0f)
            {
                return 0f;
            }

            float cost = baseCost;
            float skillFactor = player.GetSkillFactor(weapon.m_shared.m_skillType);

            // Use Traverse to avoid FieldAccessException on private fields
            var traverse = Traverse.Create(attack);
            bool isHomeItem = false;
            float staminaReturnPerMissingHP = 0f;
            Character? charRef = null;
            try { isHomeItem = traverse.Field("m_isHomeItem").GetValue<bool>(); } catch { /* fallback false */ }
            try { staminaReturnPerMissingHP = traverse.Field("m_staminaReturnPerMissingHP").GetValue<float>(); } catch { /* fallback 0 */ }
            try { charRef = traverse.Field("m_character").GetValue<Character>(); } catch { /* fallback null */ }

            // Home item vs attack stamina modifier
            if (isHomeItem)
            {
                cost *= 1f + player.GetEquipmentHomeItemModifier();
            }
            else
            {
                cost *= 1f + player.GetEquipmentAttackStaminaModifier();
            }

            // SEMan modifier (prefer player; use character if available)
            var seMan = (charRef != null ? charRef.GetSEMan() : player.GetSEMan());
            if (seMan != null)
            {
                seMan.ModifyAttackStaminaUsage(cost, ref cost, true);
            }

            // Skill factor reduction
            cost -= cost * 0.33f * skillFactor;

            // Stamina return per missing HP
            if (staminaReturnPerMissingHP > 0f)
            {
                cost -= (player.GetMaxHealth() - player.GetHealth()) * staminaReturnPerMissingHP;
            }

            if (cost < 0f) cost = 0f;
            return cost;
        }

        // NEW: Set PostAttack emote_stop guard window (seconds)
        public static void SetEmoteStopGuardWindow(Player player, float seconds)
        {
            if (player == null || seconds <= 0f)
            {
                return;
            }
            emoteStopGuardUntil[player] = Time.time + seconds;
        }

        // NEW: Check if player is currently within emote_stop guard window
        public static bool IsInEmoteStopGuardWindow(Player player)
        {
            if (player == null) return false;
            if (emoteStopGuardUntil.TryGetValue(player, out float until))
            {
                return Time.time < until;
            }
            return false;
        }

        // NEW: Clear guard window
        public static void ClearEmoteStopGuardWindow(Player player)
        {
            if (player != null)
            {
                emoteStopGuardUntil.Remove(player);
            }
        }

    }
}