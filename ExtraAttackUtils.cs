using System.Collections.Generic;
using UnityEngine;

namespace ExtraAttackSystem
{
    public static class ExtraAttackUtils
    {
        // Extra attack state tracking
        private static readonly Dictionary<Player, bool> playerExtraAttackStates = new();
        private static readonly Dictionary<Player, float> playerCooldowns = new();

        // State management methods
        public static void SetExtraAttackState(Player player, bool isExtraAttack)
        {
            if (player != null)
            {
                playerExtraAttackStates[player] = isExtraAttack;
            }
        }

        public static bool IsPlayerInExtraAttack(Player player)
        {
            return player != null && playerExtraAttackStates.ContainsKey(player) && playerExtraAttackStates[player];
        }

        public static bool IsPlayerOnCooldown(Player player)
        {
            return player != null && playerCooldowns.ContainsKey(player) && playerCooldowns[player] > Time.time;
        }

        public static float GetPlayerCooldownRemaining(Player player)
        {
            if (player != null && playerCooldowns.ContainsKey(player))
            {
                return Mathf.Max(0f, playerCooldowns[player] - Time.time);
            }
            return 0f;
        }

        public static void SetPlayerCooldown(Player player)
        {
            if (player != null)
            {
                playerCooldowns[player] = Time.time + ExtraAttackPlugin.ExtraAttackCooldown.Value;
            }
        }

        public static void CleanupPlayer(Player player)
        {
            if (player != null)
            {
                playerExtraAttackStates.Remove(player);
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