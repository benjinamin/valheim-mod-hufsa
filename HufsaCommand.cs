using System.Collections.Generic;
using Jotunn.Entities;

namespace Hufsa
{
    /// <summary>
    /// Console: "hufsa watcher" / "hufsa hunter" spawns one near you, ignoring time of day.
    /// "hufsa weather" shows the current weather and whether a Watcher could appear here now.
    /// </summary>
    internal class HufsaCommand : ConsoleCommand
    {
        public override string Name => "hufsa";
        public override string Help =>
            "hufsa watcher|hunter|weather|dread [n] - spawn a Hufsa near you (ignores time of day), check the weather, or show/set your Watcher sightings";
        public override bool IsCheat => true;

        public override void Run(string[] args)
        {
            string kind = args.Length > 0 ? args[0].ToLowerInvariant() : "watcher";
            if (kind.StartsWith("dr") && HufsaDread.Instance)
            {
                if (args.Length > 1 && int.TryParse(args[1], out int n)) HufsaDread.Instance.SetCount(n);
                Console.instance.Print($"Watcher sightings: {HufsaDread.Instance.Count}/{HufsaConfig.SightingsForHunt.Value}" +
                                       " (a hunt starts on the next night you can be hunted once it's full)");
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

        public override List<string> CommandOptionList() => new List<string> { "watcher", "hunter", "weather", "dread" };
    }
}
