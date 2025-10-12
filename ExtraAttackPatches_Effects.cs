using HarmonyLib;
using UnityEngine;

namespace ExtraAttackSystem
{
    // Effect-related patches: gate trail start effects via YAML EnableVFX
    public static class ExtraAttackPatches_Effects
    {
        /// <summary>
        /// Skip Attack.OnTrailStart when YAML has EnableVFX=false for the current animation.
        /// Prevents trail start visual/audio effects from spawning in Extra modes.
        /// </summary>
        [HarmonyPatch(typeof(Attack), nameof(Attack.OnTrailStart))]
        [HarmonyPriority(Priority.VeryHigh)]
        public static class Attack_OnTrailStart_SkipVFX_Prefix
        {
            public static bool Prefix(Attack __instance)
            {
                try
                {
                    var traverse = Traverse.Create(__instance);
                    var character = traverse.Field("m_character").GetValue<Character>();

                    if (character is Player player && player == Player.m_localPlayer)
                    {
                        // VFX is always enabled (EnableVFX removed)
                    }
                }
                catch (System.Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in Attack_OnTrailStart_SkipVFX_Prefix: {ex.Message}");
                }

                return true;
            }
        }
    }
}