using System.Reflection;

namespace ExtraAttackSystem
{
    public static class VersionInfo
    {
        // セマンティックバージョニング
        public const string Version = "0.7.7";
        public const string Prerelease = "dev";  // リリース版は空、開発中は "dev" など
        public const string Build = "20251012";
        
        // 開発版フラグ（ログで使用）
        public const bool IsDevelopmentVersion = true;
        
        // 完全なバージョン文字列（定数）
        public const string FullVersion = "0.7.7";
        public const string FullVersionWithBuild = "0.7.7.0";
        
        // Git情報（オプション）
        public static string GitCommit => GetGitCommitHash();
        public static string GitBranch => GetGitBranch();
        
        private static string GetGitCommitHash()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var gitHash = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                return gitHash?.Split('+')[1]?.Substring(0, 7) ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }
        
        private static string GetGitBranch()
        {
            try
            {
                // Git情報の取得（実装例）
                return "main"; // デフォルト
            }
            catch
            {
                return "unknown";
            }
        }
    }
}
