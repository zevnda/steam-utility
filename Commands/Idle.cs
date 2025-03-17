using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Steamworks;

namespace SteamUtility.Commands
{
    public class Idle : ICommand
    {
        public void Execute(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: SteamUtility.exe idle <app_id> <quiet true|false>");
                Console.WriteLine("Example: SteamUtility.exe idle 440 false");
                return;
            }

            // Validate the AppID
            if (!uint.TryParse(args[1], out uint appId))
            {
                Console.WriteLine("{\"error\":\"Invalid app_id\"}");
                return;
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

            // Determine if quiet mode is enabled
            bool quietMode = args[2].ToLower() == "true";

            // Run the IdleWindow if not in quiet mode
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new IdleWindow(appId, quietMode));

            // Shutdown the Steam API
            SteamAPI.Shutdown();
        }
    }
}
