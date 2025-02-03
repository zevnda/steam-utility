using System;
using System.Threading;
using System.Windows.Forms;
using Steamworks;

namespace SteamUtility.Commands
{
    public class LockAchievement : ICommand
    {
        static bool statsReceived = false;
        static Callback<UserStatsReceived_t> statsReceivedCallback;

        public void Execute(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine(
                    "Usage: SteamUtility.exe lock_achievement <app_id> <achievement_id>"
                );
                return;
            }

            uint appId;
            string achievementId = args[2];

            // Validate the AppID
            if (!uint.TryParse(args[1], out appId))
            {
                Console.WriteLine("{\"error\":\"Invalid app_id\"}");
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

                // Check if the API call was successful
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

                // Check if the achievement exists and lock it
                if (SteamUserStats.GetAchievement(achievementId, out _))
                {
                    if (SteamUserStats.ClearAchievement(achievementId))
                    {
                        SteamUserStats.StoreStats();
                        Console.WriteLine("{\"success\":\"Successfully locked achievement\"}");
                    }
                    else
                    {
                        Console.WriteLine("{\"error\":\"Failed to lock achievement\"}");
                    }
                }
                else
                {
                    Console.WriteLine(
                        "{\"error\":\"Failed to get achievement data. The achievement might not exists\"}"
                    );
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
