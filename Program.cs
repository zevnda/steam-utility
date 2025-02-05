﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SteamUtility.Commands;

namespace SteamUtility
{
    static class Program
    {
        static Dictionary<string, ICommand> commands = new Dictionary<string, ICommand>
        {
            { "idle", new Idle() },
            { "unlock_achievement", new UnlockAchievement() },
            { "lock_achievement", new LockAchievement() },
            { "toggle_achievement", new ToggleAchievement() },
            { "unlock_all_achievements", new UnlockAllAchievements() },
            { "lock_all_achievements", new LockAllAchievements() },
            { "update_stats", new UpdateStats() },
            { "reset_all_stats", new ResetAllStats() },
        };

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                ShowUsage();
                return;
            }

            string command = args[0].ToLower();

            if (commands.TryGetValue(command, out ICommand commandHandler))
            {
                commandHandler.Execute(args);
            }
            else if (command == "--help" || command == "-h")
            {
                ShowUsage();
            }
            else
            {
                Console.WriteLine($"Unknown command: {command} \n\nUse --help for help");
            }
        }

        static void ShowUsage()
        {
            var commandUsages = new Dictionary<string, string>
            {
                { "idle <app_id> <no-window:bool>", "Start idling a specific game" },
                { "unlock_achievement <app_id> <ach_id>", "Unlock a single achievement" },
                { "lock_achievement <app_id> <ach_id>", "Lock a single achievement" },
                {
                    "toggle_achievement <app_id> <ach_id>",
                    "Toggle a single achievement's lock state"
                },
                { "unlock_all_achievements <app_id>", "Unlock all achievements" },
                { "lock_all_achievements <app_id>", "Lock all achievements" },
                { "update_stats <app_id> <[stat_objects...]>", "Update achievement statistics" },
                { "reset_all_stats <app_id>", "Reset all statistics" },
            };

            Console.WriteLine("SteamUtility by zevnda");
            Console.WriteLine("\nUsage:");
            Console.WriteLine("    SteamUtility.exe <command> [args...]");
            Console.WriteLine("    SteamUtility.exe [--help | -h]");
            Console.WriteLine("\nCommands:");

            foreach (var cmd in commandUsages)
            {
                Console.WriteLine($"    {cmd.Key, -45} {cmd.Value}");
            }

            Console.WriteLine("\nExamples:");
            Console.WriteLine("    SteamUtility.exe idle 440 false");
            Console.WriteLine("    SteamUtility.exe unlock_achievement 440 WIN_100_GAMES");
            Console.WriteLine(
                "    SteamUtility.exe update_stats 440 [\"{name: 'WINS', value: 100}\", \"{name: 'MONEY', value: 19.50}\", ...]"
            );
        }
    }
}
