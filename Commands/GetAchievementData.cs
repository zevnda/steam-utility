using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using SteamUtility.Models;
using SteamUtility.Services;
using SteamUtility.Utils;
using Steamworks;

namespace SteamUtility.Commands
{
    public class GetAchievementData : ICommand
    {
        private List<AchievementData> _achievementDefinitions = new List<AchievementData>();
        private List<StatData> _statDefinitions = new List<StatData>();
        private static bool statsReceived = false;
        private static bool globalStatsReceived = false;
        private static Callback<UserStatsReceived_t> statsReceivedCallback;
        private static CallResult<GlobalAchievementPercentagesReady_t> globalStatsCallback;

        public void Execute(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine(
                    "Usage: SteamUtility.exe get_permissions <app_id> [achievement_id/stat_id]"
                );
                return;
            }

            uint appId;
            if (!uint.TryParse(args[1], out appId))
            {
                Console.WriteLine("{\"error\":\"Invalid app_id\"}");
                return;
            }

            // Set the Steam App ID environment variable
            Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());

            // Initialize Steam API
            if (!SteamAPI.Init())
            {
                Console.WriteLine(
                    "{\"error\":\"Failed to initialize Steam API. The Steam client must be running\"}"
                );
                return;
            }

            try
            {
                // Setup callback and request stats
                statsReceived = false;
                globalStatsReceived = false;
                statsReceivedCallback = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
                CSteamID steamId = SteamUser.GetSteamID();

                // Request global achievement percentages
                globalStatsCallback = CallResult<GlobalAchievementPercentagesReady_t>.Create(
                    OnGlobalStatsReceived
                );
                SteamAPICall_t hSteamApiCall = SteamUserStats.RequestGlobalAchievementPercentages();
                globalStatsCallback.Set(hSteamApiCall);

                if (SteamAPICall_t.Invalid == SteamUserStats.RequestUserStats(steamId))
                {
                    Console.WriteLine("{\"error\":\"Failed to request stats from Steam\"}");
                    return;
                }

                // Wait for stats to be received
                DateTime startTime = DateTime.Now;
                while (!statsReceived || !globalStatsReceived)
                {
                    SteamAPI.RunCallbacks();
                    if ((DateTime.Now - startTime).TotalSeconds > 10)
                    {
                        Console.WriteLine("{\"error\":\"Stats callback timed out\"}");
                        return;
                    }
                    Thread.Sleep(100);
                }

                var schemaLoader = new StatsSchemaLoader();
                if (
                    !schemaLoader.LoadUserGameStatsSchema(
                        appId,
                        out _achievementDefinitions,
                        out _statDefinitions
                    )
                )
                {
                    Console.WriteLine("{\"error\":\"Failed to load schema\"}");
                    return;
                }

                if (args.Length >= 3)
                {
                    OutputSpecificItem(args[2]);
                }
                else
                {
                    OutputAllItems(appId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{\"error\":\"" + ex.Message + "\"}");
            }
            finally
            {
                SteamAPI.Shutdown();
            }
        }

        private static void OnUserStatsReceived(UserStatsReceived_t pCallback)
        {
            if (pCallback.m_nGameID == SteamUtils.GetAppID().m_AppId)
            {
                if (pCallback.m_eResult == EResult.k_EResultOK)
                {
                    statsReceived = true;
                }
                else if (pCallback.m_eResult == EResult.k_EResultFail)
                {
                    // This is likely a game without any achievements or stats
                    statsReceived = true;
                    Console.WriteLine(
                        "{\"info\":\"This game likely has no achievements or stats\"}"
                    );
                }
                else
                {
                    Console.WriteLine(
                        $"{{\"error\":\"Failed to receive stats from Steam. Error code: {pCallback.m_eResult}\"}}"
                    );
                }
            }
        }

        private static void OnGlobalStatsReceived(
            GlobalAchievementPercentagesReady_t pCallback,
            bool bIOFailure
        )
        {
            if (!bIOFailure && pCallback.m_nGameID == SteamUtils.GetAppID().m_AppId)
            {
                globalStatsReceived = true;
            }
            else
            {
                // We can continue even if this fails, just won't have percentages
                globalStatsReceived = true;
                Console.WriteLine(
                    "{\"warning\":\"Failed to receive global achievement stats from Steam.\"}"
                );
            }
        }

        private void OutputSpecificItem(string id)
        {
            var achievement = _achievementDefinitions.Find(a => a.Id == id);
            var stat = _statDefinitions.Find(s => s.Id == id);

            if (achievement != null)
            {
                var flags = StatFlagHelper.GetFlags(achievement.Permission, false);
                var result = new Dictionary<string, object>
                {
                    { "type", "achievement" },
                    { "id", achievement.Id },
                    { "name", achievement.Name },
                    { "description", achievement.Description },
                    { "iconNormal", achievement.IconNormal },
                    { "iconLocked", achievement.IconLocked },
                    { "permission", achievement.Permission },
                    { "hidden", achievement.IsHidden },
                    { "achieved", achievement.Achieved },
                    { "percent", achievement.Percent },
                    { "protected", (flags & StatFlags.Protected) != 0 },
                    { "flags", flags.ToString() },
                };
                Console.WriteLine(JsonConvert.SerializeObject(result));
            }
            else if (stat != null)
            {
                var flags = StatFlagHelper.GetFlags(stat.Permission, stat.IncrementOnly);
                var result = new Dictionary<string, object>
                {
                    { "type", "stat" },
                    { "id", stat.Id },
                    { "name", stat.Name },
                    { "stat_type", stat.Type },
                    { "permission", stat.Permission },
                    { "min_value", stat.MinValue },
                    { "max_value", stat.MaxValue },
                    { "default_value", stat.DefaultValue },
                    { "value", stat.Value },
                    { "increment_only", stat.IncrementOnly },
                    { "protected", (flags & StatFlags.Protected) != 0 },
                    { "flags", flags.ToString() },
                };
                Console.WriteLine(JsonConvert.SerializeObject(result));
            }
            else
            {
                Console.WriteLine("{\"error\":\"ID not found\"}");
            }
        }

        private void OutputAllItems(uint appId)
        {
            var result = new Dictionary<string, object>
            {
                {
                    "achievements",
                    _achievementDefinitions.Select(a =>
                    {
                        var flags = StatFlagHelper.GetFlags(a.Permission, false);
                        return new
                        {
                            id = a.Id,
                            name = a.Name,
                            description = a.Description,
                            iconNormal = a.IconNormal,
                            iconLocked = a.IconLocked,
                            permission = a.Permission,
                            hidden = a.IsHidden,
                            achieved = a.Achieved,
                            percent = a.Percent,
                            protected_achievement = (flags & StatFlags.Protected) != 0,
                            flags = flags.ToString(),
                        };
                    })
                },
                {
                    "stats",
                    _statDefinitions.Select(s =>
                    {
                        var flags = StatFlagHelper.GetFlags(s.Permission, s.IncrementOnly);
                        return new
                        {
                            id = s.Id,
                            name = s.Name,
                            stat_type = s.Type,
                            permission = s.Permission,
                            value = s.Value,
                            increment_only = s.IncrementOnly,
                            protected_stat = (flags & StatFlags.Protected) != 0,
                            flags = flags.ToString(),
                        };
                    })
                },
            };

            // Save to file
            string jsonContent = JsonConvert.SerializeObject(result, Formatting.Indented);
            string filePath = SteamPathHelper.GetAchievementDataPath(appId);
            File.WriteAllText(filePath, jsonContent);
            Console.WriteLine($"{{\"success\":\"{filePath}\"}}");
        }
    }
}
