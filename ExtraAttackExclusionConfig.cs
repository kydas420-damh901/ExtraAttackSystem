using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ExtraAttackSystem
{
    public static class ExtraAttackExclusionConfig
    {
        private static string ConfigFolderPath => Path.Combine(BepInEx.Paths.ConfigPath, "ExtraAttackSystem");
        private static string ConfigFilePath => Path.Combine(ConfigFolderPath, "ExtraAttackExclusion.yaml");

        public class ExclusionConfig
        {
            public List<string> ExcludedItemNames { get; set; } = new List<string>();
            public List<string> ExcludedItemTypes { get; set; } = new List<string>();
            public List<string> ExcludedPrefabNames { get; set; } = new List<string>();
        }

        private static ExclusionConfig current = new ExclusionConfig();

        public static void Initialize()
        {
            try
            {
                if (!Directory.Exists(ConfigFolderPath))
                {
                    Directory.CreateDirectory(ConfigFolderPath);
                }

                if (ShouldCreateOrRegenerateExclusionConfig())
                {
                    CreateDefault();
                    ExtraAttackPlugin.LogInfo("System", "Created/regenerated ExtraAttackExclusion.yaml");
                }
                else
                {
                    Load();
                    ExtraAttackPlugin.LogInfo("System", $"Loaded ExtraAttackExclusion.yaml: names={current.ExcludedItemNames.Count}, types={current.ExcludedItemTypes.Count}, prefabs={current.ExcludedPrefabNames.Count}");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error initializing ExtraAttackExclusionConfig: {ex.Message}");
            }
        }

        // Check if exclusion config should be created or regenerated
        private static bool ShouldCreateOrRegenerateExclusionConfig()
        {
            if (!File.Exists(ConfigFilePath))
            {
                ExtraAttackPlugin.LogInfo("Config", "ExtraAttackExclusion.yaml not found, will create");
                return true;
            }

            // Check if file is empty or has no content
            try
            {
                string content = File.ReadAllText(ConfigFilePath, Encoding.UTF8).Trim();
                if (string.IsNullOrEmpty(content))
                {
                    ExtraAttackPlugin.LogInfo("Config", "ExtraAttackExclusion.yaml is empty, will regenerate");
                    return true;
                }

                // Check if file has actual exclusion data
                if (!content.Contains("ExcludedItemNames:") && !content.Contains("ExcludedItemTypes:") && !content.Contains("ExcludedPrefabNames:"))
                {
                    ExtraAttackPlugin.LogInfo("Config", "ExtraAttackExclusion.yaml has no exclusion data, will regenerate");
                    return true;
                }

                ExtraAttackPlugin.LogInfo("Config", "ExtraAttackExclusion.yaml exists and has content, skipping generation");
                return false;
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error checking ExtraAttackExclusion.yaml: {ex.Message}");
                return true; // Regenerate on error
            }
        }

        private static void CreateDefault()
        {
            current = new ExclusionConfig();

            // Default: block all Tool types (Hoe/Hammer/Pickaxe etc.)
            current.ExcludedItemTypes.Add(nameof(ItemDrop.ItemData.ItemType.Tool));

            // Default: explicit prefabs for common tools and tankards (verified by decompiled prefabs)
            current.ExcludedPrefabNames.AddRange(new[]
            {
                // Tools
                "Hammer",
                "Hoe",
                "PickaxeAntler",
                "PickaxeBronze",
                "PickaxeIron",
                "PickaxeStone",
                // Scythe (vanilla)
                "Scythe",
                // Tankards
                "Tankard",
                "TankardOdin",
                // Anniversary & Dvergr tankards (vanilla)
                "TankardAnniversary",
                "Tankard_dvergr",
                // Bombs (prefab)
                "BombOoze"
            });

            // Also include known item name tokens from vanilla for tools and bombs
            current.ExcludedItemNames.AddRange(new[]
            {
                // Tools
                "$item_hammer",
                "$item_hoe",
                "$item_pickaxe_antler",
                // Scythe (vanilla)
                "$item_scythe",
                // Tankards (vanilla)
                "$item_tankard",
                "$item_tankard_odin",
                "$item_tankard_anniversary",
                "$item_dvergrtankard",
                // Bombs (known tokens)
                "$item_bilebomb",
                "$item_bombblob_frost",
                "$item_bombblob_lava",
                "$item_bombblob_poison",
                "$item_bombblob_poisonelite",
                "$item_bombblob_tar",
                "$item_lavabomb",
                "$item_oozebomb",
                "$item_smokebomb",
                "$item_catapult_ammo"
            });

            SaveWithComments();
        }

        private static void SaveWithComments()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# ============================================================================");
                sb.AppendLine("# Extra Attack System - Equip Exclusions Configuration");
                sb.AppendLine("# Define equipment to block Q/T/G inputs. You can add or remove entries freely.");
                sb.AppendLine("# ============================================================================");
                sb.AppendLine();
                sb.AppendLine("# Item display tokens or names (SharedData.m_name). Example: $item_hammer");
                sb.AppendLine("excludedItemNames:");
                foreach (var name in current.ExcludedItemNames.Distinct())
                {
                    sb.AppendLine($"  - {name}");
                }
                sb.AppendLine();
                sb.AppendLine("# Item types as strings (e.g., Tool, OneHandedWeapon, TwoHandedWeapon, Shield, Torch, Consumable, Ammo, Material)");
                sb.AppendLine("excludedItemTypes:");
                foreach (var type in current.ExcludedItemTypes.Distinct())
                {
                    sb.AppendLine($"  - {type}");
                }
                sb.AppendLine();
                sb.AppendLine("# Prefab names (GameObject.name in Prefabs). Example: Hammer, Tankard, BombOoze");
                sb.AppendLine("excludedPrefabNames:");
                foreach (var prefab in current.ExcludedPrefabNames.Distinct())
                {
                    sb.AppendLine($"  - {prefab}");
                }

                File.WriteAllText(ConfigFilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error saving ExtraAttackExclusion.yaml: {ex.Message}");
            }
        }

        private static void Load()
        {
            try
            {
                string yaml = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                current = deserializer.Deserialize<ExclusionConfig>(yaml) ?? new ExclusionConfig();
            }
            catch (Exception ex)
            {
                ExtraAttackPlugin.LogError("System", $"Error loading ExtraAttackExclusion.yaml, using defaults: {ex.Message}");
                current = new ExclusionConfig();
            }
        }

        public static void Reload()
        {
            ExtraAttackPlugin.LogInfo("System", "F6: Starting ExtraAttackExclusion reload");
            if (File.Exists(ConfigFilePath))
            {
                Load();
                ExtraAttackPlugin.LogInfo("System", "Reloaded ExtraAttackExclusion.yaml");
            }
            else
            {
                ExtraAttackPlugin.LogWarning("System", "F6: ExtraAttackExclusion.yaml not found, skipping reload");
            }
        }

        public static bool ShouldBlockExtraForItem(ItemDrop.ItemData item)
        {
            if (item == null || item.m_shared == null)
            {
                return false;
            }

            // Check by item type
            string itemTypeName = item.m_shared.m_itemType.ToString();
            if (current.ExcludedItemTypes.Contains(itemTypeName))
            {
                ExtraAttackPlugin.LogInfo("Exclusion", $"Blocked by type: {itemTypeName} item={(item.m_shared.m_name ?? "null")} prefab={(item.m_dropPrefab?.name ?? "null")}");
                return true;
            }

            // Check by item name token/display name
            string name = item.m_shared.m_name ?? string.Empty;
            if (!string.IsNullOrEmpty(name))
            {
                if (current.ExcludedItemNames.Contains(name))
                {
                    ExtraAttackPlugin.LogInfo("Exclusion", $"Blocked by name: {name} type={item.m_shared.m_itemType} prefab={(item.m_dropPrefab?.name ?? "null")} ");
                    return true;
                }
            }

            // Optional: check prefab name if available
            string prefabName = item.m_dropPrefab != null ? item.m_dropPrefab.name : string.Empty;
            if (!string.IsNullOrEmpty(prefabName))
            {
                if (current.ExcludedPrefabNames.Contains(prefabName))
                {
                    ExtraAttackPlugin.LogInfo("Exclusion", $"Blocked by prefab: {prefabName} item={(item.m_shared.m_name ?? "null")} type={item.m_shared.m_itemType}");
                    return true;
                }
            }

            return false;
        }
    }
}