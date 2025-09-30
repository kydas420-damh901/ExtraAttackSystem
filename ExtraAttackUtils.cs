using System.Collections.Generic;
using UnityEngine;

namespace ExtraAttackSystem
{
    public static class ExtraAttackUtils
    {
        // Attack mode enum
        public enum AttackMode
        {
            Normal,      // Left click (vanilla animation)
            ExtraQ,      // Q key (secondary attack)
            ExtraT,      // T key (primary attack 1)
            ExtraG       // G key (primary attack 2)
        }

        // Attack mode tracking
        private static readonly Dictionary<Player, AttackMode> playerAttackModes = new();
        private static readonly Dictionary<Player, Dictionary<AttackMode, float>> playerCooldowns = new();

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

            // Get button-specific cooldown from config
            float cooldownDuration = ExtraAttackPlugin.GetCooldown(mode);
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

        // Localization helper methods
        public static string GetLocalizedString(string key, params object[] args)
        {
            // TODO: Implement Language folder reference for translation support
            var messages = new Dictionary<string, string>
            {
                { "extra_attack_triggered", "Extra Attack!" },
                { "extra_attack_cooldown", "Extra Attack on cooldown: {0}s" },
                { "extra_attack_no_stamina", "Not enough stamina for Extra Attack" },
                { "extra_attack_no_weapon", "No weapon equipped" },
                { "extra_attack_blocked", "Cannot use Extra Attack right now" }
            };

            if (messages.TryGetValue(key, out string message))
            {
                return args.Length > 0 ? string.Format(message, args) : message;
            }
            return key; // Fallback to key if message not found
        }

        public static void ShowMessage(Player player, string messageKey, params object[] args)
        {
            if (player != null)
            {
                string message = GetLocalizedString(messageKey, args);
                player.Message(MessageHud.MessageType.Center, message);
            }
        }
    }
}