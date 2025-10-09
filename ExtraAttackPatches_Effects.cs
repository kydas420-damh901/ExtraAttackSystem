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
                        var mode = ExtraAttackUtils.GetAttackMode(player);
                        if (mode != ExtraAttackUtils.AttackMode.Normal)
                        {
                            if (ExtraAttackPatches_Core.TryGetCurrentClipInfo(player, out string clipName, out AnimationClip clip, out int hitIndex))
                            {
                                string configKey = ExtraAttackPatches_Core.BuildConfigKey(player, clipName, hitIndex);
                                var timing = AnimationTimingConfig.GetTiming(configKey);

                                if (!timing.EnableVFX)
                                {
                                    ExtraAttackPlugin.LogInfo("VFX",
                                        $"[SKIP] Attack.OnTrailStart: [{configKey}] EnableVFX=false");
                                    return false; // Skip effect spawn entirely
                                }
                            }
                        }
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