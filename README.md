# Hufsa (Valheim mod)

BepInEx + Jotunn mod. Adds:

- **Hufsa_Watcher**: appears often at night, 60–100 m away, and stares at you. It vanishes if you come within 25 m.
- **Hufsa_Hunter**: rare, and only one exists in the world at a time. It glides slowly toward you and can't be killed. Its chill aura slows you and drains stamina and HP, and the drain is worse while you move toward it. It leaves at dawn or once you get more than 170 m away.
- Both appear at night only, except in the Deep North, where they can also appear by day.
- A frost bubble surrounds both: forced snow weather, plus frosted trees, bushes and rocks.

## Build
```
%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe build -c Release
```
The build copies `Hufsa.dll` into the Thunderstore "Default" profile at `BepInEx\plugins\Hufsa`.

## Test in-game
1. Launch Valheim modded from Thunderstore Mod Manager.
2. Open the console (F5) and run `devcommands`.
3. Run `hufsa watcher` or `hufsa hunter` to spawn one near you. This ignores the time of day.
4. Run `tod 0.9` for night or `tod 0.5` for day to test despawning.

Config lives in `BepInEx\config\benjinamin.hufsa.cfg`. Spawn and aura values are synced from the server. Visual values are per client.

## Multiplayer
Every client and the server need the mod. The server rolls spawns and enforces the single-Hunter rule. The chosen client then spawns Hufsa near its own player.
