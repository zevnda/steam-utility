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

                // Steam changed format - type_int field no longer exists in new schema files
                // Determine type by structure:
                // - Has "bits" child = Achievement (type 4) or GroupAchievement (type 5)
                // - Has "type" or "type_int" field = use that value
                // - Else determine by value type or default to Integer stat (type 1)

                int rawType = 0;

                // First try the old format fields
                if (stat["type_int"].Valid)
                {
                    rawType = stat["type_int"].AsInteger(0);
                }
                else if (stat["type"].Valid)
                {
                    // Check if tpye is a string (new format) or integer (old format)
                    if (stat["type"].Type == KeyValueType.String)
                    {
                        string typeStr = stat["type"].AsString("");

                        if (typeStr.Equals("INT", StringComparison.OrdinalIgnoreCase))
                            rawType = 1;
                        else if (typeStr.Equals("FLOAT", StringComparison.OrdinalIgnoreCase))
                            rawType = 2;
                        else if (typeStr.Equals("AVGRATE", StringComparison.OrdinalIgnoreCase))
                            rawType = 3;
                        else if (
                            typeStr.Equals("ACHIEVEMENT", StringComparison.OrdinalIgnoreCase)
                            || typeStr.Equals("ACHIEVEMENTS", StringComparison.OrdinalIgnoreCase)
                        )
                            rawType = 4;
                    }
                    else
                    {
                        // Old format with integer type
                        rawType = stat["type"].AsInteger(0);
                    }
                }
                // New format, detect by structure
                else
                {
                    // Check if this is an achievement by looking for "bits" child
                    bool hasBits =
                        stat.Children != null
                        && stat.Children.Any(c =>
                            string.Compare(
                                c.Name,
                                "bits",
                                StringComparison.InvariantCultureIgnoreCase
                            ) == 0
                        );

                    if (hasBits)
                    {
                        // Is an achievement
                        rawType = 4;
                    }
                    else
                    {
                        // It's a stat, determine type by checking for min/max/default values
                        // If any are floats, it's a float stat (type 2) otherwise integer (type 1)
                        bool hasFloatValue = false;

                        if (stat["min"].Valid && stat["min"].Type == KeyValueType.Float32)
                            hasFloatValue = true;
                        if (stat["max"].Valid && stat["max"].Type == KeyValueType.Float32)
                            hasFloatValue = true;
                        if (stat["default"].Valid && stat["default"].Type == KeyValueType.Float32)
                            hasFloatValue = true;

                        // Check for window field which indicates avgrate (type 3)
                        bool hasWindow = stat["window"].Valid;

                        if (hasWindow)
                            rawType = 3; // AverageRate
                        else if (hasFloatValue)
                            rawType = 2; // Float
                        else
                            rawType = 1; // Integer (default)
                    }
                }

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
