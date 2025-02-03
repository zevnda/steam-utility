using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Steamworks;

namespace SteamUtility.Commands
{
    public class UnlockAllAchievements : ICommand
    {
        static bool statsReceived = false;

        // Callback for when user stats are received
        static Callback<UserStatsReceived_t> statsReceivedCallback;

        public void Execute(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine(
                    "Usage: SteamUtility.exe unlock_all_achievements <app_id> [\"achievement_id1\", \"achievement_id2\", ...]"
                );
                return;
            }

            uint appId;
            string[] achievementIds;

            // Validate the AppID
            if (!uint.TryParse(args[1], out appId))
            {
                Console.WriteLine("{\"error\":\"Invalid app_id\"}");
                return;
            }

            // Parse the JSON array of achievement IDs
            try
            {
                string jsonArray = string.Join(" ", args.Skip(2));
                achievementIds = JsonConvert.DeserializeObject<string[]>(jsonArray);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    "{\"error\":\"Invalid achievement_ids format: " + ex.Message + "\"}"
                );
                return;
            }

            // Set the Steam App ID environment variable
            Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());

            // Initialize the Steam API
            if (!SteamAPI.Init())
            {
                Console.WriteLine("{\"fail\":\"Failed to initialize Steam API\"}");
                return;
            }

            try
            {
                // Get the Steam user ID and create a callback for receiving user stats
                CSteamID steamId = SteamUser.GetSteamID();
                statsReceivedCallback = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
                // Request user stats from Steam
                SteamAPICall_t apiCall = SteamUserStats.RequestUserStats(steamId);

                // Check if the API call is valid
                if (apiCall == SteamAPICall_t.Invalid)
                {
                    Console.WriteLine("{\"error\":\"Failed to requests stats from Steam\"}");
                    return;
                }

                // Wait for the stats to be received
                DateTime startTime = DateTime.Now;
                while (!statsReceived)
                {
                    SteamAPI.RunCallbacks();
                    if ((DateTime.Now - startTime).TotalSeconds > 10)
                    {
                        Console.WriteLine("{\"error\":\"Callback timed out\"}");
                        return;
                    }
                    Thread.Sleep(100);
                }

                bool allSuccess = true;
                foreach (string achievementId in achievementIds)
                {
                    // Check if the achievement exists and unlock it
                    if (SteamUserStats.GetAchievement(achievementId, out _))
                    {
                        if (!SteamUserStats.SetAchievement(achievementId))
                        {
                            allSuccess = false;
                            Console.WriteLine(
                                "{\"error\":\"Failed to unlock achievement: "
                                    + achievementId
                                    + "\"}"
                            );
                        }
                    }
                    else
                    {
                        allSuccess = false;
                        Console.WriteLine(
                            "{\"error\":\"Failed to get achievement data for: "
                                + achievementId
                                + ". The achievement might not exist\"}"
                        );
                    }
                }

                if (allSuccess)
                {
                    SteamUserStats.StoreStats();
                    Console.WriteLine("{\"success\":\"Successfully unlocked all achievements\"}");
                }
                else
                {
                    Console.WriteLine("{\"error\":\"One or more achievements failed to unlock\"}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{\"error\":\"An error occurred: " + ex.Message + "\"}");
            }
            finally
            {
                // Shutdown the Steam API
                SteamAPI.Shutdown();
            }
        }

        // Callback method for when user stats are received
        static void OnUserStatsReceived(UserStatsReceived_t pCallback)
        {
            if (pCallback.m_nGameID == SteamUtils.GetAppID().m_AppId)
            {
                if (pCallback.m_eResult == EResult.k_EResultOK)
                {
                    statsReceived = true;
                }
                else
                {
                    Console.WriteLine(
                        "{\"error\":\"Failed to receive stats from Steam. Error code: "
                            + pCallback.m_eResult
                            + "\"}"
                    );
                }
            }
        }
    }
}
