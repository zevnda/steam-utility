using System;
using System.Threading;
using Steamworks;

namespace SteamUtility.Commands
{
    public class Idle : ICommand
    {
        public void Execute(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: SteamUtility.exe idle <app_id>");
                Console.WriteLine("Example: SteamUtility.exe idle 440");
                return;
            }

            // Validate the AppID
            if (!uint.TryParse(args[1], out uint appId))
            {
                Console.WriteLine("{\"error\":\"Invalid app_id\"}");
                return;
            }

            // Get the optional app name if provided
            string appName = "Idling";
            if (args.Length >= 3)
            {
                appName = args[2];
            }

            // Set the SteamAppId environment variable
            Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());

            // Initialize the Steam API
            if (!SteamAPI.Init())
            {
                Console.WriteLine(
                    "{\"fail\":\"Failed to initialize Steam API. The Steam client must be running\"}"
                );
                return;
            }
            else
            {
                Console.WriteLine("{\"success\":\"Steam API initialized\"}");
            }

            // Initialize Steam API shutdown when application exits
            AppDomain.CurrentDomain.ProcessExit += (s, e) => SteamAPI.Shutdown();

            try
            {
                // Create the IdleWindow with the app name
                using (var window = new IdleWindow(appId, appName))
                {
                    // Keep the application running
                    Console.WriteLine("Press Ctrl+C to exit...");

                    // This will keep the application running until it's manually terminated
                    while (true)
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
            finally
            {
                // Make sure to shut down the Steam API in case of exceptions
                SteamAPI.Shutdown();
            }
        }
    }
}
