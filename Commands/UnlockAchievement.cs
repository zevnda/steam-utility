using System;
using System.Threading;
using System.Windows.Forms;
using Steamworks;

namespace SteamUtility.Commands
{
    public class UnlockAchievement : ICommand
    {
        static bool statsReceived = false;

        // Callback for when user stats are received
        static Callback<UserStatsReceived_t> statsReceivedCallback;

        public void Execute(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine(
                    "Usage: SteamUtility.exe unlock_achievement <app_id> <achievement_id>"
                );
                Console.WriteLine("Example: SteamUtility.exe unlock_achievement 440 WIN_100_GAMES");
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
                Console.WriteLine(
                    "{\"fail\":\"Failed to initialize Steam API. The Steam client must be running\"}"
                );
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

                // Check if the achievement exists and unlock it
                if (SteamUserStats.GetAchievement(achievementId, out _))
                {
                    if (SteamUserStats.SetAchievement(achievementId))
                    {
                        SteamUserStats.StoreStats();
                        Console.WriteLine("{\"success\":\"Successfully unlocked achievement\"}");
                    }
                    else
                    {
                        Console.WriteLine("{\"error\":\"Failed to unlock achievement\"}");
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
