using System;
using System.Threading;
using System.Windows.Forms;
using Steamworks;

namespace SteamUtility.Commands
{
    public class UpdateStat : ICommand
    {
        static bool statsReceived = false;
        static Callback<UserStatsReceived_t> statsReceivedCallback;

        public void Execute(string[] args)
        {
            if (args.Length < 4)
            {
                Console.WriteLine(
                    "Usage: SteamUtility.exe update_stat <app_id> <stat_name> <new_value>"
                );
                return;
            }

            // Validate the AppID
            uint appId;
            if (!uint.TryParse(args[1], out appId))
            {
                Console.WriteLine("{\"error\":\"Invalid app_id\"}");
                return;
            }

            string statName = args[2];
            string newValue = args[3];

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

                // Update the stat with the new value
                bool success = false;
                if (int.TryParse(newValue, out int intValue))
                {
                    success = SteamUserStats.SetStat(statName, intValue);
                }
                else if (float.TryParse(newValue, out float floatValue))
                {
                    success = SteamUserStats.SetStat(statName, floatValue);
                }
                else
                {
                    Console.WriteLine("{\"error\":\"Invalid integer or float\"}");
                    return;
                }

                // Store the updated stats
                if (success)
                {
                    if (SteamUserStats.StoreStats())
                    {
                        Console.WriteLine("{\"success\":\"Successfully updated stat\"}");
                    }
                    else
                    {
                        Console.WriteLine("{\"error\":\"Failed to update stat\"}");
                    }
                }
                else
                {
                    Console.WriteLine(
                        "{\"error\":\"Failed to update stat. The stat might not exists\"}"
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
