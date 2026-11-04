using System.Collections.Generic;
using Jotunn.Entities;

namespace Hufsa
{
    /// <summary>
    /// Console: "hufsa watcher" / "hufsa hunter" spawns one near you, ignoring time of day.
    /// "hufsa weather" shows the current weather and whether a Watcher could appear here now.
    /// "hufsa testmode" / "hufsa availableSpots" toggle the debug modes in HufsaDebug.
    /// </summary>
    internal class HufsaCommand : ConsoleCommand
    {
        public override string Name => "hufsa";
        public override string Help =>
            "hufsa watcher|hunter|weather|dread [hunts quiet rest noticed]|dread night [s]|frozen|fadetest [m] [s]|testmode|availableSpots - spawn a Hufsa near you (ignores time of day), " +
            "check the weather, show/set your dread state or bring a night hunt, preview the frozen statue, or toggle a debug mode";
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            string kind = args.Length > 0 ? args[0].ToLowerInvariant() : "watcher";
            if (kind == "testmode" && HufsaDebug.Instance)
            {
                HufsaDebug.Instance.ToggleTestMode();
                return;
            }
            if (kind == "availablespots" && HufsaDebug.Instance)
            {
                HufsaDebug.Instance.ToggleSpots();
                return;
            }
            if (kind == "probeshot")
            {
                HufsaSight.DumpNextMeasurement();
                Console.instance.Print("The next sight measurement is saved as hufsa_probe_scene/alone.png in the BepInEx folder.");
                return;
            }
            if (kind.StartsWith("fade"))
            {
                float dist = args.Length > 1 && float.TryParse(args[1], out float d) ? d : 20f;
                float secs = args.Length > 2 && float.TryParse(args[2], out float s) ? s : 3f;
                bool ok = HufsaDebug.Instance && HufsaDebug.Instance.FadeTest(dist, secs);
                Console.instance.Print(ok ? $"A Watcher {dist:0} m ahead, fading in {secs:0.#} s (counts for nothing)." : "Couldn't place it.");
                return;
            }
            if (kind == "frozen")
            {
                // A local-only statue of you, just in front of you, without dying.
                bool ok = HufsaFrozenVictim.Preview(Player.m_localPlayer);
                Console.instance.Print(ok ? "A frozen you stands in front of you." : "Couldn't make the statue (see the log).");
                return;
            }
            if (kind.StartsWith("dr") && HufsaDread.Instance)
            {
                // hufsa dread [hunts quiet rest encounters] | hufsa dread night [seconds]
                HufsaDread d = HufsaDread.Instance;
                if (args.Length > 1 && args[1] == "night")
                {
                    float secs = args.Length > 2 && float.TryParse(args[2], out float f) ? f : 25f;
                    d.ScheduleIn(secs);
                    Console.instance.Print($"A night hunt comes in {secs:0} s (only at night, outdoors).");
                    return;
                }
                if (args.Length > 4 && int.TryParse(args[1], out int h) && int.TryParse(args[2], out int q) &&
                    int.TryParse(args[3], out int r) && int.TryParse(args[4], out int enc))
                    d.SetState(h, q, r, enc);
                Console.instance.Print(HufsaDebug.State());
                return;
            }
            if (kind.StartsWith("we"))
            {
                var p = Player.m_localPlayer ? Player.m_localPlayer.transform.position : UnityEngine.Vector3.zero;
                Console.instance.Print($"Weather '{HufsaEntity.CurrentWeather()}' in {HufsaEntity.BiomeAt(p)}: " +
                    $"storm/fog={HufsaEntity.IsBadWeather()}, night={EnvMan.IsNight()}, " +
                    $"Watcher can appear={HufsaEntity.IsWatcherTime(p)}, Hunter can appear={HufsaEntity.IsHufsaTime(p)}");
                return;
            }
            string prefab = kind.StartsWith("h") ? HufsaPrefabs.HunterName : HufsaPrefabs.WatcherName;
            HufsaEntity e = HufsaSpawner.SpawnNearLocalPlayer(prefab, true);
            Console.instance.Print(e ? $"Spawned {prefab}" : $"Could not find a spot for {prefab}");
        }

        public override List<string> CommandOptionList() => new List<string> { "watcher", "hunter", "weather", "dread", "testmode", "availableSpots", "probeshot", "frozen", "fadetest" };
    }
}
