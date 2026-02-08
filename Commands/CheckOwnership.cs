using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Newtonsoft.Json;

namespace SteamUtility.Commands
{
    public class CheckOwnership : ICommand
    {
        private const string DEFAULT_GAMES_URL =
            "https://raw.githubusercontent.com/zevnda/steam-game-database/refs/heads/main/games.json";

        public void Execute(string[] args)
        {
            // Expect output file path as first argument
            if (args.Length < 2)
            {
                Console.WriteLine(
                    JsonConvert.SerializeObject(
                        new
                        {
                            success = false,
                            error = "Output file path is required as first argument",
                        },
                        Formatting.None
                    )
                );
                return;
            }

            string outputFilePath = args[1];
            List<uint> appIds = new List<uint>();

            try
            {
                // If no third argument provided, fetch from GitHub
                if (args.Length < 3)
                {
                    appIds = FetchAppIdsFromGitHub();
                }
                // Check if the third argument is a file path
                else if (File.Exists(args[2]))
                {
                    string fileContent = File.ReadAllText(args[2]);
                    appIds = JsonConvert.DeserializeObject<List<uint>>(fileContent);

                    if (appIds == null)
                    {
                        Console.WriteLine(
                            JsonConvert.SerializeObject(
                                new
                                {
                                    success = false,
                                    error = "Failed to parse app IDs from file",
                                },
                                Formatting.None
                            )
                        );
                        return;
                    }
                }
                else
                {
                    // Try to parse as JSON array
                    appIds = JsonConvert.DeserializeObject<List<uint>>(args[2]);

                    if (appIds == null)
                    {
                        Console.WriteLine(
                            JsonConvert.SerializeObject(
                                new
                                {
                                    success = false,
                                    error = "Invalid app IDs format. Expected JSON array or file path",
                                },
                                Formatting.None
                            )
                        );
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    JsonConvert.SerializeObject(
                        new { success = false, error = $"Failed to load app IDs: {ex.Message}" },
                        Formatting.None
                    )
                );
                return;
            }

            if (appIds.Count == 0)
            {
                Console.WriteLine(
                    JsonConvert.SerializeObject(
                        new { success = false, error = "No app IDs provided" },
                        Formatting.None
                    )
                );
                return;
            }

            // Initialize Steam API client
            using (var client = new API.Client())
            {
                try
                {
                    // Initialize without a specific app ID (0 = no specific app)
                    client.Initialize(0);

                    var ownedGames = new List<object>();
                    var notOwnedGames = new List<uint>();

                    foreach (uint appId in appIds)
                    {
                        try
                        {
                            bool isOwned = client.SteamApps008.IsSubscribedApp(appId);

                            if (isOwned)
                            {
                                // Try to get the app name
                                string appName = null;
                                try
                                {
                                    appName = client.SteamApps001.GetAppData(appId, "name");
                                }
                                catch (Exception)
                                {
                                    // If we can't get the name, just ignore it
                                    appName = null;
                                }

                                ownedGames.Add(
                                    new { appid = appId, name = appName ?? $"App {appId}" }
                                );
                            }
                            else
                            {
                                notOwnedGames.Add(appId);
                            }
                        }
                        catch (Exception)
                        {
                            // If there's an error checking a specific game, silently continue
                        }
                    }

                    // Write games to file
                    var gamesData = new { games = ownedGames };
                    string gamesJson = JsonConvert.SerializeObject(gamesData, Formatting.None);

                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(outputFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(outputFilePath, gamesJson);

                    // Output success status
                    var result = new
                    {
                        success = true,
                        totalChecked = appIds.Count,
                        ownedCount = ownedGames.Count,
                        notOwnedCount = notOwnedGames.Count,
                    };

                    string jsonOutput = JsonConvert.SerializeObject(result, Formatting.None);
                    Console.WriteLine(jsonOutput);
                }
                catch (API.ClientInitializeException ex)
                {
                    string errorMessage = "Unknown error";
                    string suggestion = "";

                    switch (ex.FailureReason)
                    {
                        case API.ClientInitializeFailure.InstallPathNotFound:
                            errorMessage = "Could not find Steam installation path";
                            suggestion = "Make sure Steam is installed";
                            break;

                        case API.ClientInitializeFailure.LibraryLoadFailed:
                            errorMessage = "Could not load steamclient.dll";
                            suggestion = "Make sure Steam is installed correctly";
                            break;

                        case API.ClientInitializeFailure.UserConnectionFailed:
                            errorMessage = "Could not connect to Steam user";
                            suggestion = "Make sure Steam is running and you are logged in";
                            break;

                        case API.ClientInitializeFailure.ClientCreationFailed:
                            errorMessage = "Could not create Steam client interface";
                            break;

                        case API.ClientInitializeFailure.PipeCreationFailed:
                            errorMessage = "Could not create Steam pipe";
                            break;

                        default:
                            errorMessage = ex.Message;
                            break;
                    }

                    var errorResult = new
                    {
                        success = false,
                        error = errorMessage,
                        suggestion,
                        failureReason = ex.FailureReason.ToString(),
                    };

                    string jsonError = JsonConvert.SerializeObject(errorResult, Formatting.None);
                    Console.WriteLine(jsonError);
                }
                catch (Exception ex)
                {
                    var errorResult = new
                    {
                        success = false,
                        error = $"Unexpected error: {ex.Message}",
                    };

                    string jsonError = JsonConvert.SerializeObject(errorResult, Formatting.None);
                    Console.WriteLine(jsonError);
                }
            }
        }

        private List<uint> FetchAppIdsFromGitHub()
        {
            using (var webClient = new WebClient())
            {
                string json = webClient.DownloadString(DEFAULT_GAMES_URL);

                // Parse the games.json
                var appIds = JsonConvert.DeserializeObject<List<uint>>(json);

                if (appIds == null || appIds.Count == 0)
                {
                    throw new Exception("No games found in GitHub database");
                }

                return appIds;
            }
        }
    }
}
