using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using System.Linq;

namespace ExtraAttackSystem
{
    public class CacheMetadata
    {
        public string AssetBundleHash { get; set; } = "";
        public string Version { get; set; } = "";
        public DateTime LastModified { get; set; }
        public Dictionary<string, string> ClipHashes { get; set; } = new Dictionary<string, string>();
    }

    public static class EAS_AnimationCache
    {
        private static readonly string CacheDirectory = Path.Combine(BepInEx.Paths.ConfigPath, "ExtraAttackSystem", "AnimationCache");
        private static readonly string MetadataFile = Path.Combine(CacheDirectory, "metadata.json");
        
        private static CacheMetadata _metadata = new CacheMetadata();
        private static Dictionary<string, AnimationClip> _cachedClips = new Dictionary<string, AnimationClip>();

        public static void Initialize(string assetBundlePath = "")
        {
            try
            {
                ExtraAttackSystemPlugin.LogInfo("System", "EAS_AnimationCache: Initializing animation cache system");
                
                // キャッシュディレクトリ作成
                if (!Directory.Exists(CacheDirectory))
                {
                    Directory.CreateDirectory(CacheDirectory);
                    ExtraAttackSystemPlugin.LogInfo("System", $"Created cache directory: {CacheDirectory}");
                }

                // メタデータ読み込み
                LoadMetadata();

                // アセットバンドルハッシュ計算（ファイル読み込みのみ、AssetBundle読み込みなし）
                string currentAssetBundleHash = CalculateAssetBundleHash(assetBundlePath);
                
                if (string.IsNullOrEmpty(currentAssetBundleHash))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "Could not calculate AssetBundle hash, skipping cache");
                    return;
                }

                if (_metadata.AssetBundleHash != currentAssetBundleHash)
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "AssetBundle changed, rebuilding animation cache");
                    RebuildAllCaches(currentAssetBundleHash);
                }
                else
                {
                    ExtraAttackSystemPlugin.LogInfo("System", "AssetBundle unchanged, loading animation cache");
                    LoadCachedClips();
                }
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error initializing animation cache: {ex.Message}");
            }
        }

        private static void LoadMetadata()
        {
            try
            {
                if (File.Exists(MetadataFile))
                {
                    var lines = File.ReadAllLines(MetadataFile);
                    _metadata = new CacheMetadata();
                    
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("Hash:"))
                        {
                            _metadata.AssetBundleHash = line.Substring(5).Trim();
                        }
                        else if (line.StartsWith("Version:"))
                        {
                            _metadata.Version = line.Substring(8).Trim();
                        }
                        else if (line.StartsWith("Clip:"))
                        {
                            var parts = line.Substring(5).Split('=');
                            if (parts.Length == 2)
                            {
                                _metadata.ClipHashes[parts[0].Trim()] = parts[1].Trim();
                            }
                        }
                    }
                    
                    ExtraAttackSystemPlugin.LogInfo("System", $"Loaded cache metadata: Hash={_metadata.AssetBundleHash?.Substring(0, 8)}..., Clips={_metadata.ClipHashes.Count}");
                }
                else
                {
                    _metadata = new CacheMetadata();
                    ExtraAttackSystemPlugin.LogInfo("System", "No cache metadata found, will create new cache");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error loading cache metadata: {ex.Message}");
                _metadata = new CacheMetadata();
            }
        }

        private static void SaveMetadata()
        {
            try
            {
                var lines = new List<string>();
                lines.Add($"Hash:{_metadata.AssetBundleHash}");
                lines.Add($"Version:{_metadata.Version}");
                lines.Add($"LastModified:{_metadata.LastModified:yyyy-MM-dd HH:mm:ss}");
                
                foreach (var kvp in _metadata.ClipHashes)
                {
                    lines.Add($"Clip:{kvp.Key}={kvp.Value}");
                }
                
                File.WriteAllLines(MetadataFile, lines);
                ExtraAttackSystemPlugin.LogInfo("System", "Saved cache metadata");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error saving cache metadata: {ex.Message}");
            }
        }

        private static string CalculateAssetBundleHash(string assetBundlePath)
        {
            try
            {
                if (string.IsNullOrEmpty(assetBundlePath) || !File.Exists(assetBundlePath))
                {
                    ExtraAttackSystemPlugin.LogWarning("System", "AssetBundle file not found");
                    return "";
                }

                byte[] fileBytes = File.ReadAllBytes(assetBundlePath);
                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(fileBytes);
                    string hash = BitConverter.ToString(hashBytes).Replace("-", "");
                    return hash;
                }
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error calculating AssetBundle hash: {ex.Message}");
                return "";
            }
        }

        private static void RebuildAllCaches(string assetBundleHash)
        {
            try
            {
                ExtraAttackSystemPlugin.LogInfo("System", "Starting cache rebuild...");
                
                // 既存キャッシュクリア
                ClearCache();

                _metadata.AssetBundleHash = assetBundleHash;
                _metadata.Version = "0.8.7"; // MOD version
                _metadata.LastModified = DateTime.Now;
                _metadata.ClipHashes.Clear();

                // 全武器タイプ×全モードでキャッシュ作成
                var weaponTypes = new[] { "Sword", "Greatsword", "Axe", "Club", "Spear", "Knife", "Battleaxe", "Polearm", "Fist" };
                var modes = new[] { "secondary_Q", "secondary_T", "secondary_G" };

                int successCount = 0;
                foreach (var weaponType in weaponTypes)
                {
                    foreach (var mode in modes)
                    {
                        if (CreateCacheForMode(weaponType, mode))
                        {
                            successCount++;
                        }
                    }
                }

                SaveMetadata();
                ExtraAttackSystemPlugin.LogInfo("System", $"Animation cache rebuilt successfully: {successCount} clips cached");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error rebuilding cache: {ex.Message}");
            }
        }

        private static bool CreateCacheForMode(string weaponType, string mode)
        {
            try
            {
                // カスタムアニメーション名取得
                string? customAnimName = EAS_AnimationManager.GetCustomAnimationName(weaponType, mode);
                if (string.IsNullOrEmpty(customAnimName))
                {
                    // アニメーションが設定されていない場合はスキップ
                    return false;
                }

                // 元のクリップ取得
                var originalClip = EAS_AnimationManager.GetAnimationClip(customAnimName!);
                if (originalClip == null)
                {
                    ExtraAttackSystemPlugin.LogWarning("System", $"Original clip not found for {weaponType}_{mode}: {customAnimName}");
                    return false;
                }

                // タイミング取得
                var timing = EAS_AnimationTiming.GetTiming($"{weaponType}_{mode}");

                // タイミング付きクリップ作成
                var clipWithTiming = CreateClipWithTiming(originalClip, timing);

                // メモリキャッシュに保存
                string cacheKey = $"{weaponType}_{mode}";
                _cachedClips[cacheKey] = clipWithTiming;

                // ハッシュ記録
                string clipHash = CalculateClipHash(originalClip);
                _metadata.ClipHashes[cacheKey] = clipHash;

                return true;
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error creating cache for {weaponType}_{mode}: {ex.Message}");
                return false;
            }
        }

        private static AnimationClip CreateClipWithTiming(AnimationClip originalClip, EAS_AnimationTiming.AnimationTiming timing)
        {
            // クリップを複製
            var clipWithTiming = UnityEngine.Object.Instantiate(originalClip);
            clipWithTiming.name = originalClip.name + "_WithTiming";

            // タイミングイベント追加
            var events = new List<AnimationEvent>();

            // Hitイベント
            if (timing.HitTiming > 0)
            {
                events.Add(new AnimationEvent
                {
                    time = timing.HitTiming,
                    functionName = "Hit",
                    stringParameter = "Hit"
                });
            }

            // TrailOnイベント
            if (timing.TrailOnTiming > 0)
            {
                events.Add(new AnimationEvent
                {
                    time = timing.TrailOnTiming,
                    functionName = "TrailOn",
                    stringParameter = "TrailOn"
                });
            }

            // TrailOffイベント
            if (timing.TrailOffTiming > 0)
            {
                events.Add(new AnimationEvent
                {
                    time = timing.TrailOffTiming,
                    functionName = "TrailOff",
                    stringParameter = "TrailOff"
                });
            }

            clipWithTiming.events = events.ToArray();
            return clipWithTiming;
        }

        private static void LoadCachedClips()
        {
            try
            {
                // メモリキャッシュは既に作成済みのため、再作成が必要
                ExtraAttackSystemPlugin.LogInfo("System", "Loading cached clips from memory...");
                
                // 実際には、前回のキャッシュが有効なので再作成
                var weaponTypes = new[] { "Sword", "Greatsword", "Axe", "Club", "Spear", "Knife", "Battleaxe", "Polearm", "Fist" };
                var modes = new[] { "secondary_Q", "secondary_T", "secondary_G" };

                int loadCount = 0;
                foreach (var weaponType in weaponTypes)
                {
                    foreach (var mode in modes)
                    {
                        string cacheKey = $"{weaponType}_{mode}";
                        if (_metadata.ClipHashes.ContainsKey(cacheKey))
                        {
                            // キャッシュが存在する場合は再作成
                            if (CreateCacheForMode(weaponType, mode))
                            {
                                loadCount++;
                            }
                        }
                    }
                }
                
                ExtraAttackSystemPlugin.LogInfo("System", $"Loaded {loadCount} cached clips");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error loading cached clips: {ex.Message}");
            }
        }

        private static string CalculateClipHash(AnimationClip clip)
        {
            try
            {
                string clipData = $"{clip.name}_{clip.length}_{clip.events.Length}_{clip.frameRate}";
                using (var sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(clipData));
                    return BitConverter.ToString(hashBytes).Replace("-", "");
                }
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error calculating clip hash: {ex.Message}");
                return "";
            }
        }

        public static AnimationClip? GetCachedClip(string weaponType, string mode)
        {
            string cacheKey = $"{weaponType}_{mode}";
            return _cachedClips.TryGetValue(cacheKey, out var clip) ? clip : null;
        }

        public static void ClearCache()
        {
            try
            {
                _cachedClips.Clear();
                _metadata.ClipHashes.Clear();
                ExtraAttackSystemPlugin.LogInfo("System", "Animation cache cleared");
            }
            catch (Exception ex)
            {
                ExtraAttackSystemPlugin.LogError("System", $"Error clearing cache: {ex.Message}");
            }
        }
    }
}

