using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
using Newtonsoft.Json;
using Steamworks;

namespace SteamUtility.Commands
{
    public class GetAchievementData : ICommand
    {
        private List<AchievementData> _achievementDefinitions = new List<AchievementData>();
        private List<StatData> _statDefinitions = new List<StatData>();
        private static bool statsReceived = false;
        private static bool globalStatsReceived = false;
        private static Callback<UserStatsReceived_t> statsReceivedCallback;
        private static CallResult<GlobalAchievementPercentagesReady_t> globalStatsCallback;

        private enum KeyValueType
        {
            None = 0,
            String = 1,
            Int32 = 2,
            Float32 = 3,
            Pointer = 4,
            WideString = 5,
            Color = 6,
            UInt64 = 7,
            End = 8,
        }

        private class KeyValue
        {
            private static readonly KeyValue _Invalid = new KeyValue();
            public string Name = "<root>";
            public KeyValueType Type = KeyValueType.None;
            public object Value;
            public bool Valid;
            public List<KeyValue> Children = null;

            public KeyValue this[string key]
            {
                get
                {
                    if (this.Children == null)
                        return _Invalid;

                    var child = this.Children.SingleOrDefault(c =>
                        string.Compare(c.Name, key, StringComparison.InvariantCultureIgnoreCase)
                        == 0
                    );

                    return child ?? _Invalid;
                }
            }

            public string AsString(string defaultValue)
            {
                if (!Valid || Value == null)
                    return defaultValue;
                return Value.ToString();
            }

            public int AsInteger(int defaultValue)
            {
                if (!Valid)
                    return defaultValue;

                switch (Type)
                {
                    case KeyValueType.String:
                    case KeyValueType.WideString:
                        return int.TryParse((string)Value, out int value) ? value : defaultValue;
                    case KeyValueType.Int32:
                        return (int)Value;
                    case KeyValueType.Float32:
                        return (int)((float)Value);
                    case KeyValueType.UInt64:
                        return (int)((ulong)Value & 0xFFFFFFFF);
                    default:
                        return defaultValue;
                }
            }

            public bool AsBoolean(bool defaultValue)
            {
                if (!Valid)
                    return defaultValue;

                switch (Type)
                {
                    case KeyValueType.String:
                    case KeyValueType.WideString:
                        return int.TryParse((string)Value, out int value)
                            ? value != 0
                            : defaultValue;
                    case KeyValueType.Int32:
                        return ((int)Value) != 0;
                    case KeyValueType.Float32:
                        return ((int)((float)Value)) != 0;
                    case KeyValueType.UInt64:
                        return ((ulong)Value) != 0;
                    default:
                        return defaultValue;
                }
            }

            public float AsFloat(float defaultValue)
            {
                if (!Valid)
                    return defaultValue;

                switch (Type)
                {
                    case KeyValueType.String:
                    case KeyValueType.WideString:
                        return float.TryParse((string)Value, out float value)
                            ? value
                            : defaultValue;
                    case KeyValueType.Int32:
                        return (float)((int)Value);
                    case KeyValueType.Float32:
                        return (float)Value;
                    case KeyValueType.UInt64:
                        return (float)((ulong)Value);
                    default:
                        return defaultValue;
                }
            }

            public static KeyValue LoadAsBinary(string path)
            {
                if (!File.Exists(path))
                    return null;

                try
                {
                    using (
                        var input = File.Open(
                            path,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite
                        )
                    )
                    {
                        var kv = new KeyValue();
                        return kv.ReadAsBinary(input) ? kv : null;
                    }
                }
                catch
                {
                    return null;
                }
            }

            public bool ReadAsBinary(Stream input)
            {
                Children = new List<KeyValue>();
                try
                {
                    while (true)
                    {
                        var type = (KeyValueType)ReadValueU8(input);
                        if (type == KeyValueType.End)
                            break;

                        var current = new KeyValue { Type = type, Name = ReadStringUnicode(input) };

                        switch (type)
                        {
                            case KeyValueType.None:
                                current.ReadAsBinary(input);
                                break;
                            case KeyValueType.String:
                                current.Valid = true;
                                current.Value = ReadStringUnicode(input);
                                break;
                            case KeyValueType.WideString:
                                throw new FormatException("wstring is unsupported");
                            case KeyValueType.Int32:
                                current.Valid = true;
                                current.Value = ReadValueS32(input);
                                break;
                            case KeyValueType.UInt64:
                                current.Valid = true;
                                current.Value = ReadValueU64(input);
                                break;
                            case KeyValueType.Float32:
                                current.Valid = true;
                                current.Value = ReadValueF32(input);
                                break;
                            case KeyValueType.Color:
                            case KeyValueType.Pointer:
                                current.Valid = true;
                                current.Value = ReadValueU32(input);
                                break;
                            default:
                                throw new FormatException();
                        }

                        if (input.Position >= input.Length)
                            throw new FormatException();

                        Children.Add(current);
                    }

                    Valid = true;
                    return input.Position == input.Length;
                }
                catch
                {
                    return false;
                }
            }

            private static byte ReadValueU8(Stream input)
            {
                return (byte)input.ReadByte();
            }

            private static short ReadValueS16(Stream input)
            {
                var data = new byte[2];
                input.Read(data, 0, 2);
                return BitConverter.ToInt16(data, 0);
            }

            private static int ReadValueS32(Stream input)
            {
                var data = new byte[4];
                input.Read(data, 0, 4);
                return BitConverter.ToInt32(data, 0);
            }

            private static uint ReadValueU32(Stream input)
            {
                var data = new byte[4];
                input.Read(data, 0, 4);
                return BitConverter.ToUInt32(data, 0);
            }

            private static ulong ReadValueU64(Stream input)
            {
                var data = new byte[8];
                input.Read(data, 0, 8);
                return BitConverter.ToUInt64(data, 0);
            }

            private static float ReadValueF32(Stream input)
            {
                var data = new byte[4];
                input.Read(data, 0, 4);
                return BitConverter.ToSingle(data, 0);
            }

            private static string ReadStringUnicode(Stream input)
            {
                var chars = new List<byte>();
                while (true)
                {
                    var b = input.ReadByte();
                    if (b == 0)
                        break;
                    chars.Add((byte)b);
                }
                return System.Text.Encoding.UTF8.GetString(chars.ToArray());
            }
        }

        private class AchievementData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string IconNormal { get; set; }
            public string IconLocked { get; set; }
            public bool IsHidden { get; set; }
            public int Permission { get; set; }
            public bool Achieved { get; set; }
            public float Percent { get; set; }
        }

        private class StatData
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public int Permission { get; set; }
            public object MinValue { get; set; }
            public object MaxValue { get; set; }
            public object DefaultValue { get; set; }
            public object Value { get; set; }
            public bool IncrementOnly { get; set; }
        }

        [Flags]
        private enum StatFlags
        {
            None = 0,
            IncrementOnly = 1 << 0,
            Protected = 1 << 1,
            UnknownPermission = 1 << 2,
        }

        private StatFlags GetFlags(int permission, bool isIncrementOnly)
        {
            var flags = StatFlags.None;
            flags |= !isIncrementOnly ? 0 : StatFlags.IncrementOnly;
            flags |= (permission & 2) != 0 ? StatFlags.Protected : 0;
            flags |= (permission & ~2) != 0 ? StatFlags.UnknownPermission : 0;
            return flags;
        }

        private static string GetSteamInstallPath()
        {
            return (string)
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\Software\Valve\Steam", "InstallPath", null);
        }

        private string GetAchievementDataPath(uint appId)
        {
            string appDataPath = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData
            );
            string targetDir = Path.Combine(
                appDataPath,
                "com.zevnda.steam-game-idler",
                "achievement_data"
            );

            // Create directory if it doesn't exist
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            return Path.Combine(targetDir, $"{appId}_achievement_data.json");
        }

        public void Execute(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine(
                    "Usage: SteamUtility.exe get_permissions <app_id> [achievement_id/stat_id]"
                );
                return;
            }

            uint appId;
            if (!uint.TryParse(args[1], out appId))
            {
                Console.WriteLine("{\"error\":\"Invalid app_id\"}");
                return;
            }

            // Set the Steam App ID environment variable
            Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());

            // Initialize Steam API
            if (!SteamAPI.Init())
            {
                Console.WriteLine(
                    "{\"error\":\"Failed to initialize Steam API. The Steam client must be running\"}"
                );
                return;
            }

            try
            {
                // Setup callback and request stats
                statsReceived = false;
                globalStatsReceived = false;
                statsReceivedCallback = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
                CSteamID steamId = SteamUser.GetSteamID();

                // Request global achievement percentages
                globalStatsCallback = CallResult<GlobalAchievementPercentagesReady_t>.Create(
                    OnGlobalStatsReceived
                );
                SteamAPICall_t hSteamApiCall = SteamUserStats.RequestGlobalAchievementPercentages();
                globalStatsCallback.Set(hSteamApiCall);

                if (SteamAPICall_t.Invalid == SteamUserStats.RequestUserStats(steamId))
                {
                    Console.WriteLine("{\"error\":\"Failed to request stats from Steam\"}");
                    return;
                }

                // Wait for stats to be received
                DateTime startTime = DateTime.Now;
                while (!statsReceived || !globalStatsReceived)
                {
                    SteamAPI.RunCallbacks();
                    if ((DateTime.Now - startTime).TotalSeconds > 10)
                    {
                        Console.WriteLine("{\"error\":\"Stats callback timed out\"}");
                        return;
                    }
                    Thread.Sleep(100);
                }

                if (!LoadUserGameStatsSchema(appId))
                {
                    Console.WriteLine("{\"error\":\"Failed to load schema\"}");
                    return;
                }

                if (args.Length >= 3)
                {
                    string id = args[2];
                    var achievement = _achievementDefinitions.Find(a => a.Id == id);
                    var stat = _statDefinitions.Find(s => s.Id == id);

                    if (achievement != null)
                    {
                        var flags = GetFlags(achievement.Permission, false);
                        var result = new Dictionary<string, object>
                        {
                            { "type", "achievement" },
                            { "id", achievement.Id },
                            { "name", achievement.Name },
                            { "description", achievement.Description },
                            { "iconNormal", achievement.IconNormal },
                            { "iconLocked", achievement.IconLocked },
                            { "permission", achievement.Permission },
                            { "hidden", achievement.IsHidden },
                            { "achieved", achievement.Achieved },
                            { "percent", achievement.Percent },
                            { "protected", (flags & StatFlags.Protected) != 0 },
                            { "flags", flags.ToString() },
                        };
                        Console.WriteLine(JsonConvert.SerializeObject(result));
                    }
                    else if (stat != null)
                    {
                        var flags = GetFlags(stat.Permission, stat.IncrementOnly);
                        var result = new Dictionary<string, object>
                        {
                            { "type", "stat" },
                            { "id", stat.Id },
                            { "name", stat.Name },
                            { "stat_type", stat.Type },
                            { "permission", stat.Permission },
                            { "min_value", stat.MinValue },
                            { "max_value", stat.MaxValue },
                            { "default_value", stat.DefaultValue },
                            { "value", stat.Value },
                            { "increment_only", stat.IncrementOnly },
                            { "protected", (flags & StatFlags.Protected) != 0 },
                            { "flags", flags.ToString() },
                        };
                        Console.WriteLine(JsonConvert.SerializeObject(result));
                    }
                    else
                    {
                        Console.WriteLine("{\"error\":\"ID not found\"}");
                    }
                }
                else
                {
                    var result = new Dictionary<string, object>
                    {
                        {
                            "achievements",
                            _achievementDefinitions.Select(a =>
                            {
                                var flags = GetFlags(a.Permission, false);
                                return new
                                {
                                    id = a.Id,
                                    name = a.Name,
                                    description = a.Description,
                                    iconNormal = a.IconNormal,
                                    iconLocked = a.IconLocked,
                                    permission = a.Permission,
                                    hidden = a.IsHidden,
                                    achieved = a.Achieved,
                                    percent = a.Percent,
                                    protected_achievement = (flags & StatFlags.Protected) != 0,
                                    flags = flags.ToString(),
                                };
                            })
                        },
                        {
                            "stats",
                            _statDefinitions.Select(s =>
                            {
                                var flags = GetFlags(s.Permission, s.IncrementOnly);
                                return new
                                {
                                    id = s.Id,
                                    name = s.Name,
                                    stat_type = s.Type,
                                    permission = s.Permission,
                                    value = s.Value,
                                    increment_only = s.IncrementOnly,
                                    protected_stat = (flags & StatFlags.Protected) != 0,
                                    flags = flags.ToString(),
                                };
                            })
                        },
                    };

                    // Save to file
                    string jsonContent = JsonConvert.SerializeObject(result, Formatting.Indented);
                    string filePath = GetAchievementDataPath(appId);
                    File.WriteAllText(filePath, jsonContent);
                    Console.WriteLine($"{{\"success\":\"{filePath}\"}}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("{\"error\":\"" + ex.Message + "\"}");
            }
            finally
            {
                SteamAPI.Shutdown();
            }
        }

        private static void OnUserStatsReceived(UserStatsReceived_t pCallback)
        {
            if (pCallback.m_nGameID == SteamUtils.GetAppID().m_AppId)
            {
                if (pCallback.m_eResult == EResult.k_EResultOK)
                {
                    statsReceived = true;
                }
                else if (pCallback.m_eResult == EResult.k_EResultFail)
                {
                    // This is likely a game without any achievements or stats
                    statsReceived = true;
                    Console.WriteLine(
                        "{\"info\":\"This game likely has no achievements or stats\"}"
                    );
                }
                else
                {
                    Console.WriteLine(
                        $"{{\"error\":\"Failed to receive stats from Steam. Error code: {pCallback.m_eResult}\"}}"
                    );
                }
            }
        }

        private static void OnGlobalStatsReceived(
            GlobalAchievementPercentagesReady_t pCallback,
            bool bIOFailure
        )
        {
            if (!bIOFailure && pCallback.m_nGameID == SteamUtils.GetAppID().m_AppId)
            {
                globalStatsReceived = true;
            }
            else
            {
                // We can continue even if this fails, just won't have percentages
                globalStatsReceived = true;
                Console.WriteLine(
                    "{\"warning\":\"Failed to receive global achievement stats from Steam.\"}"
                );
            }
        }

        private bool LoadUserGameStatsSchema(uint appId)
        {
            string path;
            try
            {
                string fileName = $"UserGameStatsSchema_{appId}.bin";
                path = GetSteamInstallPath();
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

            _achievementDefinitions.Clear();
            _statDefinitions.Clear();

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

                            _statDefinitions.Add(
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

                            _statDefinitions.Add(
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

                                    _achievementDefinitions.Add(
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
