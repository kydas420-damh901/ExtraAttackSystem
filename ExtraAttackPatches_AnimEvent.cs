using HarmonyLib;
using UnityEngine;

namespace ExtraAttackSystem
{
    // Animation Event patches (Speed, etc.)
    public static class ExtraAttackPatches_AnimEvent
    {
        /// <summary>
        /// Multiply CharacterAnimEvent.Speed by YAML SpeedMultiplier for current extra-attack animation.
        /// Vanilla sets Animator.speed = speedScale; we adjust speedScale before vanilla runs.
        /// </summary>
        [HarmonyPatch(typeof(CharacterAnimEvent), nameof(CharacterAnimEvent.Speed))]
        [HarmonyPriority(Priority.VeryHigh)]
        public static class CharacterAnimEvent_Speed_Multiplier_Prefix
        {
            public static void Prefix(CharacterAnimEvent __instance, ref float speedScale)
            {
                try
                {
                    // Access private fields from CharacterAnimEvent via Harmony AccessTools
                    var characterField = AccessTools.Field(typeof(CharacterAnimEvent), "m_character");
                    var animatorField = AccessTools.Field(typeof(CharacterAnimEvent), "m_animator");
                    Character? character = characterField.GetValue(__instance) as Character;
                    Animator? animator = animatorField.GetValue(__instance) as Animator;

                    if (character is Player player && player == Player.m_localPlayer && animator != null)
                    {
                        var mode = ExtraAttackUtils.GetAttackMode(player);
                        if (mode != ExtraAttackUtils.AttackMode.Normal)
                        {
                            if (ExtraAttackPatches_Core.TryGetCurrentClipInfo(player, out string clipName, out AnimationClip clip, out int hitIndex))
                            {
                                string configKey = ExtraAttackPatches_Core.BuildConfigKey(player, clipName, hitIndex);
                                var timing = AnimationTimingConfig.GetTiming(configKey);

                                float multiplier = Mathf.Max(0f, timing.SpeedMultiplier);
                                if (Mathf.Abs(multiplier - 1f) > 0.001f)
                                {
                                    speedScale *= multiplier;
                                    ExtraAttackPlugin.LogInfo("AnimEvent", $"Apply SpeedMultiplier: key={configKey} mult={multiplier:F2} newSpeedScale={speedScale:F2}");
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    ExtraAttackPlugin.LogError("System", $"Error in CharacterAnimEvent_Speed_Multiplier_Prefix: {ex.Message}");
                }
            }
        }
    }
}