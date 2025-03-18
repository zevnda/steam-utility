using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SteamUtility.Models;
using SteamUtility.Utils;
using Steamworks;

namespace SteamUtility.Services
{
    public class StatsSchemaLoader
    {
        public bool LoadUserGameStatsSchema(
            uint appId,
            out List<AchievementData> achievementDefinitions,
            out List<StatData> statDefinitions
        )
        {
            achievementDefinitions = new List<AchievementData>();
            statDefinitions = new List<StatData>();

            string path;
            try
            {
                string fileName = $"UserGameStatsSchema_{appId}.bin";
                path = SteamPathHelper.GetSteamInstallPath();
                path = Path.Combine(path, "appcache", "stats", fileName);
                if (!File.Exists(path))
                    return false;
            }
            catch (Exception)
            {
                return false;
            }

            var kv = KeyValue.LoadAsBinary(path);
            if (kv == null)
                return false;

            var stats = kv[appId.ToString(CultureInfo.InvariantCulture)]["stats"];
            if (!stats.Valid || stats.Children == null)
                return false;

            foreach (var stat in stats.Children)
            {
                if (!stat.Valid)
                    continue;

                var rawType = stat["type_int"].Valid
                    ? stat["type_int"].AsInteger(0)
                    : stat["type"].AsInteger(0);

                switch (rawType)
                {
                    case 1: // Integer
                        {
                            string statId = stat["name"].AsString("");
                            int statValue = 0;
                            bool success = SteamUserStats.GetStat(statId, out statValue);

                            statDefinitions.Add(
                                new StatData
                                {
                                    Id = statId,
                                    Name = stat["display"]
                                        ["name"]
                                        .AsString(stat["name"].AsString("")),
                                    Type = "integer",
                                    MinValue = stat["min"].AsInteger(int.MinValue),
                                    MaxValue = stat["max"].AsInteger(int.MaxValue),
                                    DefaultValue = stat["default"].AsInteger(0),
                                    Value = success ? statValue : 0,
                                    IncrementOnly = stat["incrementonly"].AsBoolean(false),
                                    Permission = stat["permission"].AsInteger(0),
                                }
                            );
                        }
                        break;

                    case 2: // Float
                    case 3: // AverageRate
                        {
                            string statId = stat["name"].AsString("");
                            float statValue = 0.0f;
                            bool success = SteamUserStats.GetStat(statId, out statValue);

                            statDefinitions.Add(
                                new StatData
                                {
                                    Id = statId,
                                    Name = stat["display"]
                                        ["name"]
                                        .AsString(stat["name"].AsString("")),
                                    Type = rawType == 2 ? "float" : "avgrate",
                                    MinValue = stat["min"].AsFloat(float.MinValue),
                                    MaxValue = stat["max"].AsFloat(float.MaxValue),
                                    DefaultValue = stat["default"].AsFloat(0.0f),
                                    Value = success ? statValue : 0,
                                    IncrementOnly = stat["incrementonly"].AsBoolean(false),
                                    Permission = stat["permission"].AsInteger(0),
                                }
                            );
                        }
                        break;

                    case 4: // Achievement
                    case 5: // GroupAchievement
                        if (stat.Children != null)
                        {
                            foreach (
                                var bits in stat.Children.Where(b =>
                                    string.Compare(
                                        b.Name,
                                        "bits",
                                        StringComparison.InvariantCultureIgnoreCase
                                    ) == 0
                                )
                            )
                            {
                                if (!bits.Valid || bits.Children == null)
                                    continue;

                                foreach (var bit in bits.Children)
                                {
                                    string achievementId = bit["name"].AsString("");
                                    // Use SteamUserStats.GetAchievementDisplayAttribute to get localized name and description
                                    string name = SteamUserStats.GetAchievementDisplayAttribute(
                                        achievementId,
                                        "name"
                                    );
                                    string description =
                                        SteamUserStats.GetAchievementDisplayAttribute(
                                            achievementId,
                                            "desc"
                                        );

                                    // Fall back to values from schema if the API returns empty strings
                                    if (string.IsNullOrEmpty(name))
                                        name = bit["display"]["name"].AsString("");
                                    if (string.IsNullOrEmpty(description))
                                        description = bit["display"]["desc"].AsString("");

                                    // Check if the achievement is achieved
                                    bool achieved = false;
                                    SteamUserStats.GetAchievement(achievementId, out achieved);

                                    // Get achievement global percentage
                                    float percent = 0.0f;
                                    SteamUserStats.GetAchievementAchievedPercent(
                                        achievementId,
                                        out percent
                                    );

                                    achievementDefinitions.Add(
                                        new AchievementData()
                                        {
                                            Id = achievementId,
                                            Name = name,
                                            Description = description,
                                            IconNormal = bit["display"]["icon"].AsString(""),
                                            IconLocked = bit["display"]["icon_gray"].AsString(""),
                                            IsHidden = bit["display"]["hidden"].AsBoolean(false),
                                            Permission = bit["permission"].AsInteger(0),
                                            Achieved = achieved,
                                            Percent = percent,
                                        }
                                    );
                                }
                            }
                        }
                        break;
                }
            }

            return true;
        }
    }
}
