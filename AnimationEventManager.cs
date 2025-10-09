using System.Collections.Generic;
using UnityEngine;

namespace ExtraAttackSystem
{
    public static class AnimationEventManager
    {
        // Add AnimationEvents to clips for attack detection
        public static void AddAnimationEvents(AnimationClip clip)
        {
            if (clip == null)
            {
                ExtraAttackPlugin.LogWarning("System", "Cannot add events: clip is null");
                return;
            }

            float clipLength = clip.length;

            // Resolve YAML config key from ReplacementMap (vanilla name + style suffix), fallback to clip.name
            string configKey = ResolveConfigKeyForClip(clip) ?? clip.name;
            var timing = AnimationTimingConfig.GetTiming(configKey);

            // Calculate timing based on YAML (0.0~1.0 normalized)
            float hitTiming = clipLength * Mathf.Clamp01(timing.HitTiming);
            float trailOnTiming = clipLength * Mathf.Clamp01(timing.TrailOnTiming);
            float trailOffTiming = clipLength * Mathf.Clamp01(timing.TrailOffTiming);

            List<AnimationEvent> events = new List<AnimationEvent>();

            // Add TrailOn event
            AnimationEvent trailOn = new AnimationEvent
            {
                time = trailOnTiming,
                functionName = "TrailOn",
                intParameter = 0,
                floatParameter = 0f,
                stringParameter = ""
            };
            events.Add(trailOn);

            // Add Hit event (attack detection)
            AnimationEvent hit = new AnimationEvent
            {
                time = hitTiming,
                functionName = "OnAttackTrigger",  // Use OnAttackTrigger for compatibility
                intParameter = 0,
                floatParameter = 0f,
                stringParameter = ""
            };
            events.Add(hit);

            // Add TrailOff event
            AnimationEvent trailOff = new AnimationEvent
            {
                time = trailOffTiming,
                functionName = "TrailOff",
                intParameter = 0,
                floatParameter = 0f,
                stringParameter = ""
            };
            events.Add(trailOff);

            // Apply events to clip
            clip.events = events.ToArray();

            ExtraAttackPlugin.LogInfo("System",
                $"Added {events.Count} AnimationEvents to [{clip.name}] using YAML key [{configKey}]: " +
                $"TrailOn={trailOnTiming:F3}s, Hit={hitTiming:F3}s, TrailOff={trailOffTiming:F3}s");
        }

        // Add events to all external animations
        public static void AddEventsToExternalAnimations()
        {
            int count = 0;
            foreach (var kvp in AnimationManager.ExternalAnimations)
            {
                string animName = kvp.Key;
                AnimationClip clip = kvp.Value;
        
                // If YAML key can be resolved, always (re)apply events to match config
                string? configKey = ResolveConfigKeyForClip(clip);
                if (configKey != null)
                {
                    AddAnimationEvents(clip);
                    count++;
                    continue;
                }
        
                // Fallback: if clip already has events, keep them; otherwise add default events
                if (clip.events != null && clip.events.Length > 0)
                {
                    ExtraAttackPlugin.LogInfo("System", $"[{animName}] already has {clip.events.Length} events, keeping existing (no YAML match)");
                    continue;
                }
        
                AddAnimationEvents(clip);
                count++;
            }
        
            ExtraAttackPlugin.LogInfo("System", $"Added/updated AnimationEvents on {count} clips");
        }
        
        // Resolve YAML key from ReplacementMap by reverse lookup of external clip name
        // Returns "VanillaClip_secondary_Q/T/G" when found; otherwise null
        private static string? ResolveConfigKeyForClip(AnimationClip clip)
        {
            if (clip == null) return null;
            string externalName = clip.name;
        
            foreach (var entry in AnimationManager.ReplacementMap)
            {
                string mapKey = entry.Key;
                string? suffix = null;
                if (mapKey.StartsWith("ea_secondary_Q", System.StringComparison.Ordinal)) suffix = "secondary_Q";
                else if (mapKey.StartsWith("ea_secondary_T", System.StringComparison.Ordinal)) suffix = "secondary_T";
                else if (mapKey.StartsWith("ea_secondary_G", System.StringComparison.Ordinal)) suffix = "secondary_G";
                else continue; // ignore legacy style maps here
        
                foreach (var kv in entry.Value)
                {
                    if (kv.Value == externalName)
                    {
                        string vanillaName = kv.Key;
                        string cfg = $"{vanillaName}_{suffix!}";
                        if (AnimationTimingConfig.HasConfig(cfg))
                        {
                            return cfg;
                        }
                    }
                }
            }
        
            return null;
        }
    }
}