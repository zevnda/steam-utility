﻿﻿using System;
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
            else
            {
                ShowUsage();
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine("Version 1.0.0 by zevnda");
            Console.WriteLine("\nUsage:");
            Console.WriteLine("      SteamUtility.exe <command> [args...]");
            Console.WriteLine("      SteamUtility.exe [--help | -h]");
            Console.WriteLine("\nCommands:                         Usage:");

            var commandUsages = new Dictionary<string, string>
            {
                { "idle", "idle <app_id> <quiet true|false>" },
                { "unlock_achievement", "unlock_achievement <app_id> <achievement_id>" },
                { "lock_achievement", "lock_achievement <app_id> <achievement_id>" },
                { "toggle_achievement", "toggle_achievement <app_id> <achievement_id>" },
                {
                    "unlock_all_achievements",
                    "unlock_all_achievements <app_id> [\"achievement_id1\", \"achievement_id2\", ...]"
                },
                { "lock_all_achievements", "lock_all_achievements <app_id>" },
                {
                    "update_stats",
                    "update_stats <app_id> [\"{statName: 'stat_one', newValue: 10}\", \"{statName: 'stat_two', newValue: 1.5}\", ...]"
                },
                { "reset_all_stats", "reset_all_stats <app_id>" },
            };

            foreach (var cmd in commandUsages)
            {
                Console.WriteLine($"      {cmd.Key, -30} SteamUtility.exe {cmd.Value}");
            }
        }
    }
}
