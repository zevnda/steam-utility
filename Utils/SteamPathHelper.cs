using System;
using System.IO;
using Microsoft.Win32;
using Steamworks;

namespace SteamUtility.Utils
{
    public static class SteamPathHelper
    {
        public static string GetSteamInstallPath()
        {
            return (string)
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Valve\Steam", "InstallPath", null);
        }

        public static string GetAchievementDataPath(uint appId, string cacheDir = null)
        {
            string targetDir;
            
            if (!string.IsNullOrEmpty(cacheDir))
            {
                // Use provided cache directory
                CSteamID steamId = SteamUser.GetSteamID();
                targetDir = Path.Combine(
                    cacheDir,
                    steamId.ToString(),
                    "achievement_data"
                );
            }
            else
            {
                // Fallback to original behavior
                string appDataPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData
                );

                CSteamID steamId = SteamUser.GetSteamID();
                targetDir = Path.Combine(
                    appDataPath,
                    "com.zevnda.steam-game-idler",
                    "cache",
                    steamId.ToString(),
                    "achievement_data"
                );
            }

            // Create directory if it doesn't exist
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            return Path.Combine(targetDir, $"{appId}.json");
        }
    }
}
