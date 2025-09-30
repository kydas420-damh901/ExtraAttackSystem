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

            // Calculate timing based on clip length
            // Hit should occur around 40-50% through the animation
            float hitTiming = clipLength * 0.45f;
            float trailOnTiming = clipLength * 0.35f;
            float trailOffTiming = clipLength * 0.70f;

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
                $"Added {events.Count} AnimationEvents to [{clip.name}]: " +
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

                // Check if clip already has events
                if (clip.events != null && clip.events.Length > 0)
                {
                    ExtraAttackPlugin.LogInfo("System", $"[{animName}] already has {clip.events.Length} events, skipping");
                    continue;
                }

                AddAnimationEvents(clip);
                count++;
            }

            ExtraAttackPlugin.LogInfo("System", $"Added AnimationEvents to {count} clips");
        }
    }
}