## SteamUtility
The workhorse behind [**Steam Game Idler**](https://github.com/zevnda/steam-game-idler) that handles tasks like idling games, managing achievements and updating statistics

## Usage
SteamUtility can be used as a standalone CLI tool in the following way

```
Usage:
    SteamUtility.exe <command> [args...]
    SteamUtility.exe [--help | -h]

Commands:
    idle <app_id> <no-window:bool>                Start idling a specific game
    unlock_achievement <app_id> <ach_id>          Unlock a single achievement
    lock_achievement <app_id> <ach_id>            Lock a single achievement
    toggle_achievement <app_id> <ach_id>          Toggle a single achievement's lock state
    unlock_all_achievements <app_id>              Unlock all achievements
    lock_all_achievements <app_id>                Lock all achievements
    update_stats <app_id> <[stat_objects...]>     Update achievement statistics
    reset_all_stats <app_id>                      Reset all statistics

Examples:
    SteamUtility.exe idle 440 false
    SteamUtility.exe unlock_achievement 440 WIN_100_GAMES
    SteamUtility.exe update_stats 440 ["{name: 'WINS', value: 100}", "{name: 'MONEY', value: 19.50}"]
```