using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Steamworks;

namespace SteamUtility.Commands
{
    public class UpdateStats : ICommand
    {
        static bool statsReceived = false;
        static Callback<UserStatsReceived_t> statsReceivedCallback;

        public void Execute(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine(
                    "Usage: SteamUtility.exe update_stats <app_id> <[stat_objects...]>"
                );
                Console.WriteLine(
                    "Example: SteamUtility.exe update_stats 440 [\"{name: 'WINS', value: 100}\", \"{name: 'MONEY', value: 19.50}\", ...]"
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

            // Parse the JSON array of stats and new values
            StatUpdate[] statUpdates;
            try
            {
                string jsonArray = string.Join(" ", args.Skip(2));
                statUpdates = JsonConvert.DeserializeObject<StatUpdate[]>(jsonArray);
            }
            catch (Exception ex)
            {
                Console.WriteLine("{\"error\":\"Invalid stats format: " + ex.Message + "\"}");
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
                foreach (var statUpdate in statUpdates)
                {
                    // Update the stat with the new value
                    bool success = false;
                    if (int.TryParse(statUpdate.value.ToString(), out int intValue))
                    {
                        success = SteamUserStats.SetStat(statUpdate.name, intValue);
                    }
                    else if (float.TryParse(statUpdate.value.ToString(), out float floatValue))
                    {
                        success = SteamUserStats.SetStat(statUpdate.name, floatValue);
                    }
                    else
                    {
                        allSuccess = false;
                        Console.WriteLine(
                            "{\"error\":\"Invalid integer or float for stat: "
                                + statUpdate.name
                                + "\"}"
                        );
                        continue;
                    }

                    if (!success)
                    {
                        allSuccess = false;
                        Console.WriteLine(
                            "{\"error\":\"Failed to update stat: "
                                + statUpdate.name
                                + ". The stat might not exist\"}"
                        );
                    }
                }

                // Store the updated stats
                if (allSuccess)
                {
                    if (SteamUserStats.StoreStats())
                    {
                        Console.WriteLine("{\"success\":\"Successfully updated all stats\"}");
                    }
                    else
                    {
                        Console.WriteLine("{\"error\":\"Failed to store updated stats\"}");
                    }
                }
                else
                {
                    Console.WriteLine("{\"error\":\"One or more stats failed to update\"}");
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

        // Class to represent stat updates
        private class StatUpdate
        {
            public string name { get; set; }
            public object value { get; set; }
        }
    }
}
