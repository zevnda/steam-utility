## SteamUtility
The workhorse behind [**Steam Game Idler**](https://github.com/zevnda/steam-game-idler) that handles tasks like idling games, managing achievements and updating statistics

## Usage
SteamUtility can be used as a standalone CLI tool in the following way
```
SteamUtility.exe <command> [args...]
SteamUtility.exe [--help | -h]
```

For a detailed overview of a specific command provide the command name with no args
```
SteamUtility.exe <command>
```

| Command                   | Arguments                         | Description                                 |
|---------------------------|-----------------------------------|---------------------------------------------|
| `idle`                    | `<app_id>` `<true\|false>`        | Enter idle state for the specified game     |
| `unlock_achievement`      | `<app_id>` `<achievement_id>`     | Unlock a specific achievement               |
| `lock_achievement`        | `<app_id>` `<achievement_id>`     | Lock a specific achievement                 |
| `toggle_achievement`      | `<app_id>` `<achievement_id>`     | Toggle the state of a specific achievement  |
| `unlock_all_achievements` | `<app_id>` `<achievements_array>` | Bulk unlock multiple achievements           |
| `lock_all_achievements`   | `<app_id>`                        | Lock all achievements                       |
| `update_stats`            | `<app_id>` `<stats_array>`        | Bulk update multiple statistics             |
| `reset_all_stats`         | `<app_id>`                        | Reset all statistics                        |
