using System;
using UnityEngine;

namespace ExtraAttackSystem
{
    /// <summary>
    /// Root motion settlement checks for animation controller reverts.
    /// Currently returns true to avoid blocking revert logic.
    /// Future: Implement proper root motion detection when vanilla behavior is confirmed.
    /// </summary>
    public static class Character_AddRootMotion_Log_Patch
    {
        /// <summary>
        /// Check if root motion has settled for a player.
        /// </summary>
        /// <param name="player">The player to check</param>
        /// <returns>Always true (stub implementation)</returns>
        public static bool IsRootMotionSettled(Player player)
        {
            return true; // Stub: Always settled to avoid blocking reverts
        }

        /// <summary>
        /// Check if root motion has settled for a character.
        /// </summary>
        /// <param name="character">The character to check</param>
        /// <returns>Always true (stub implementation)</returns>
        public static bool IsRootMotionSettled(Character character)
        {
            return true; // Stub: Always settled to avoid blocking reverts
        }
    }
}