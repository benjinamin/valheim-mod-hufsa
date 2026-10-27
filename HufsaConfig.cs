using BepInEx.Configuration;
using UnityEngine;

namespace Hufsa
{
    internal static class HufsaConfig
    {
        // Spawning
        public static ConfigEntry<float> SpawnCheckInterval;
        public static ConfigEntry<bool> ForceEveryNight;
        public static ConfigEntry<bool> DeepNorthDaytime;
        public static ConfigEntry<bool> WatcherEnabled;
        public static ConfigEntry<float> WatcherChance;
        public static ConfigEntry<float> WatcherCooldown;
        public static ConfigEntry<float> WatcherEscalation;
        public static ConfigEntry<int> SightingsForHunt;
        public static ConfigEntry<int> DreadDecayDays;
        public static ConfigEntry<float> SightSeconds;
        public static ConfigEntry<bool> DreadMessages;
        public static ConfigEntry<float> WatcherMinDistance;
        public static ConfigEntry<float> WatcherMaxDistance;
        public static ConfigEntry<bool> HunterEnabled;
        public static ConfigEntry<float> HunterChance;
        public static ConfigEntry<float> HunterCooldownMinutes;
        public static ConfigEntry<float> HunterMinDistance;
        public static ConfigEntry<float> HunterMaxDistance;

        // Watcher
        public static ConfigEntry<float> WatcherVanishDistance;
        public static ConfigEntry<float> WatcherLifetime;
        public static ConfigEntry<int> WatcherBlinks;
        public static ConfigEntry<float> WatcherLookAwaySeconds;
        public static ConfigEntry<bool> WatcherHidesBehindCover;
        public static ConfigEntry<string> WatcherDayBiomes;
        public static ConfigEntry<string> WatcherBadWeatherBiomes;
        public static ConfigEntry<string> BadWeatherKeywords;
        public static ConfigEntry<float> MistlandsMinDistance;
        public static ConfigEntry<float> MistlandsMaxDistance;
        public static ConfigEntry<float> MistlandsVanishDistance;
        public static ConfigEntry<float> LookAwayVanishChance;
        public static ConfigEntry<string> WoodedBiomes;
        public static ConfigEntry<string> DenseBiomes;
        public static ConfigEntry<int> DenseCoverBlocks;
        public static ConfigEntry<float> RetargetAfterDeathRadius;
        public static ConfigEntry<float> RespawnAfterVanish;

        // Hunter
        public static ConfigEntry<float> HunterSpeed;
        public static ConfigEntry<float> HunterCloseDistance;
        public static ConfigEntry<float> HunterSpeedScale;
        public static ConfigEntry<float> HunterMaxSpeed;
        public static ConfigEntry<float> EscapeDistance;
        public static ConfigEntry<float> ClimbSpeed;
        public static ConfigEntry<float> LungeSpeed;
        public static ConfigEntry<float> LungeSeconds;
        public static ConfigEntry<float> LungeStopDistance;
        public static ConfigEntry<float> RushFadeSeconds;
        public static ConfigEntry<bool> WatcherEveryNight;
        public static ConfigEntry<float> WatcherGuaranteeWithin;
        public static ConfigEntry<float> WatcherVanishDense;
        public static ConfigEntry<float> SpottedFadeSeconds;
        public static ConfigEntry<float> SafeSeconds;
        public static ConfigEntry<bool> FollowThroughPortals;
        public static ConfigEntry<float> FollowDelay;

        // Aura
        public static ConfigEntry<float> ColdRadius;
        public static ConfigEntry<float> StaminaRegenStopRadius;
        public static ConfigEntry<float> StaminaDrainRadius;
        public static ConfigEntry<float> CatchRadius;
        public static ConfigEntry<float> AuraCurve;
        public static ConfigEntry<float> MaxSlow;
        public static ConfigEntry<float> SlowCurve;
        public static ConfigEntry<float> StaminaDrain;
        public static ConfigEntry<float> ApproachMultiplier;
        public static ConfigEntry<float> FireQuenchBeyondCold;

        // Frost visuals (client only, not synced)
        public static ConfigEntry<bool> WeatherEnabled;
        public static ConfigEntry<string> WeatherEnvironment;
        public static ConfigEntry<bool> FoliageFrostEnabled;
        public static ConfigEntry<float> WatcherFrostRadius;
        public static ConfigEntry<float> FrostStrength;
        public static ConfigEntry<Color> FrostColor;
        public static ConfigEntry<string> MildBiomes;
        public static ConfigEntry<float> WatcherGlow;
        public static ConfigEntry<float> HunterGlow;
        public static ConfigEntry<float> MildFrostFactor;
        public static ConfigEntry<float> FaceRevealNear;
        public static ConfigEntry<float> FaceRevealFar;
        public static ConfigEntry<float> SmileRevealNear;
        public static ConfigEntry<float> SmileRevealFar;
        public static ConfigEntry<float> HunterSmileRevealNear;
        public static ConfigEntry<float> HunterSmileRevealFar;
        public static ConfigEntry<float> HemSway;
        public static ConfigEntry<bool> TrailEnabled;
        public static ConfigEntry<float> TrailLifetime;
        public static ConfigEntry<float> FootprintSize;
        public static ConfigEntry<float> AudioVolume;
        public static ConfigEntry<float> AudioRange;
        public static ConfigEntry<bool> SoundsEnabled;
        public static ConfigEntry<float> VoiceVolume;
        public static ConfigEntry<float> MoveVolume;

        public static void Bind(ConfigFile cfg)
        {
            SpawnCheckInterval = Synced(cfg, "1 - Spawning", "CheckInterval", 30f, "Seconds between server spawn rolls per player.");
            ForceEveryNight = Synced(cfg, "1 - Spawning", "TestForceEveryNight", false,
                "TESTING: when night falls, always start a hunt (or Watchers if hunts are off), ignoring chances and cooldowns.");
            DeepNorthDaytime = Synced(cfg, "1 - Spawning", "DeepNorthDaytime", true, "Allow Hufsa to appear during the day in the Deep North.");
            WatcherEnabled = Synced(cfg, "1 - Spawning", "WatcherEnabled", true, "Enable the distant staring Hufsa.");
            WatcherChance = Synced(cfg, "1 - Spawning", "WatcherChanceEachRoll", 0.08f,
                "Chance per roll (every CheckInterval) that a Watcher appears, on top of the one guaranteed per night.");
            WatcherEveryNight = Synced(cfg, "1 - Spawning", "WatcherEveryNight", true,
                "Every player gets at least one Watcher each night (where a Watcher can appear), at a random time early in the night.");
            WatcherGuaranteeWithin = Synced(cfg, "1 - Spawning", "WatcherGuaranteeWithinSeconds", 240f,
                "The guaranteed Watcher comes at a random moment within this many seconds after nightfall.");
            WatcherCooldown = Synced(cfg, "1 - Spawning", "WatcherMinSecondsBetween", 300f, "Minimum seconds between Watchers for the same player.");
            WatcherEscalation = Synced(cfg, "1 - Spawning", "WatcherEscalation", 0.5f,
                "Each sighting a player has makes Watchers this much more likely (0.5 = +50% per sighting), sooner and closer.");
            SightingsForHunt = Synced(cfg, "1 - Spawning", "SightingsBeforeHunt", 6,
                "After this many real sightings of a Watcher, Hufsa hunts that player on the next night they can be hunted.");
            DreadDecayDays = Synced(cfg, "1 - Spawning", "SightingDecayDays", 2,
                "One sighting is forgotten after this many in-game days without seeing one.");
            WatcherMinDistance = Synced(cfg, "1 - Spawning", "WatcherSpawnMinDistance", 45f,
                "Closest a Watcher appears (m). Keep it above VanishDistanceOpen.");
            WatcherMaxDistance = Synced(cfg, "1 - Spawning", "WatcherSpawnMaxDistance", 75f, "Furthest a Watcher appears (m).");
            HunterEnabled = Synced(cfg, "1 - Spawning", "HunterEnabled", true, "Enable the hunting Hufsa.");
            HunterChance = Synced(cfg, "1 - Spawning", "RandomHuntChance", 0f,
                "Chance per roll of a hunt with no warning. 0 = hunts only come after enough Watcher sightings.");
            HunterCooldownMinutes = Synced(cfg, "1 - Spawning", "HunterCooldownMinutes", 20f, "Minimum real minutes between Hunter spawns.");
            HunterMinDistance = Synced(cfg, "1 - Spawning", "HunterMinDistance", 45f, "");
            HunterMaxDistance = Synced(cfg, "1 - Spawning", "HunterMaxDistance", 65f, "");

            WatcherVanishDistance = Synced(cfg, "2 - Watcher", "VanishDistanceOpen", 35f,
                "It leaves when a player comes this close (m) across open ground (nothing between you).");
            WatcherVanishDense = Synced(cfg, "2 - Watcher", "VanishDistanceDense", 14f,
                "...and this close (m) when trees and rocks stand between you (4 or more). In between, it scales.");
            SpottedFadeSeconds = Synced(cfg, "2 - Watcher", "FadeAfterSpottedSeconds", 5f,
                "Once you have really spotted it, it fades away this many seconds later (0 = stays).");
            WatcherLifetime = Synced(cfg, "2 - Watcher", "LifetimeSeconds", 180f, "The Watcher vanishes after this long.");
            WatcherBlinks = Synced(cfg, "2 - Watcher", "Blinks", 3,
                "Times the Watcher moves somewhere else (just outside your view, a little closer) when you look away, before it leaves for good.");
            WatcherLookAwaySeconds = Synced(cfg, "2 - Watcher", "LookAwaySeconds", 1.2f,
                "How long you must look away (after having seen it) before it moves or leaves.");
            WatcherHidesBehindCover = Synced(cfg, "2 - Watcher", "VanishWhenViewBlocked", false,
                "If a tree, rock or building blocks your view of it while you are within 70 m, it is gone when you look again. " +
                "Off by default: it tended to vanish before you could properly look at it. Walking up to it still makes it leave.");
            SightSeconds = Synced(cfg, "2 - Watcher", "SightSeconds", 1f,
                "How long it must be near the middle of your view, with nothing in the way, to count as a sighting.");
            DreadMessages = cfg.Bind("2 - Watcher", "SightingMessages", true,
                "Show a quiet line of text (top left) when you spot a Watcher, growing more ominous as a hunt nears.");
            WatcherDayBiomes = Synced(cfg, "2 - Watcher", "DayBiomesAnyWeather", "Swamp,Mountain,Mistlands,DeepNorth",
                "Biomes where the Watcher may also appear by day, in any weather. It never appears in the Ashlands.");
            WatcherBadWeatherBiomes = Synced(cfg, "2 - Watcher", "DayBiomesBadWeather", "Meadows,BlackForest,Plains",
                "Biomes where the Watcher may appear by day only in storm or fog.");
            BadWeatherKeywords = Synced(cfg, "2 - Watcher", "BadWeatherKeywords", "storm,thunder,mist,fog",
                "A weather counts as storm or fog when its name contains one of these (console: 'hufsa weather' shows the current one).");
            MistlandsMinDistance = Synced(cfg, "2 - Watcher", "LowVisibilityMinDistance", 40f,
                "In low visibility (the Mistlands, or daytime storm/fog anywhere) it stands a bit closer than usual.");
            MistlandsMaxDistance = Synced(cfg, "2 - Watcher", "LowVisibilityMaxDistance", 60f, "");
            MistlandsVanishDistance = Synced(cfg, "2 - Watcher", "LowVisibilityVanishDistance", 18f,
                "In low visibility you can get this close before it leaves.");
            WoodedBiomes = Synced(cfg, "2 - Watcher", "WoodedBiomes", "Meadows,BlackForest,Swamp,Mistlands",
                "Biomes where the Watcher only stands between trees or on a hilltop. Elsewhere any spot you can see it from will do.");
            DenseBiomes = Synced(cfg, "2 - Watcher", "DenseBiomes", "Swamp,Mistlands",
                "Biomes so full of trees and roots that cover blocks your view all the time.");
            DenseCoverBlocks = Synced(cfg, "2 - Watcher", "DenseCoverBlocks", 4,
                "In dense biomes it only vanishes after cover has blocked your view this many times (elsewhere: once). Looking away still counts as normal.");
            LookAwayVanishChance = Synced(cfg, "2 - Watcher", "LookAwayVanishChance", 0.5f,
                "Each time you look away, the chance it is simply gone instead of moving somewhere else.");
            RespawnAfterVanish = Synced(cfg, "2 - Watcher", "RespawnAfterVanishSeconds", 30f,
                "When it vanishes because you looked away or hid behind cover, a new Watcher comes back for you after this long (if it's still Watcher time).");

            HunterSpeed = Synced(cfg, "3 - Hunter", "CloseSpeed", 1.9f, "Glide speed when close (player walk is ~1.6, run ~7).");
            HunterCloseDistance = Synced(cfg, "3 - Hunter", "CloseDistance", 15f, "Within this distance (m) it glides at the slow Speed.");
            HunterSpeedScale = Synced(cfg, "3 - Hunter", "SpeedScale", 40f,
                "Beyond CloseDistance its speed grows exponentially: Speed x e^((distance - CloseDistance) / SpeedScale). Lower = faster catch-up.");
            HunterMaxSpeed = Synced(cfg, "3 - Hunter", "MaxSpeed", 5.5f,
                "Top speed (m/s). A fresh player runs ~7, so you can pull away while your stamina lasts, but not by walking.");
            EscapeDistance = Synced(cfg, "3 - Hunter", "EscapeDistance", 110f,
                "Get further than this (m) from it and you have escaped; it leaves. Keep it under ~120 m (Valheim unloads things beyond that).");
            LungeSpeed = Synced(cfg, "3 - Hunter", "RushSpeed", 11f,
                "When the Watcher you kept seeing turns on you, it bursts at you this fast (m/s; a running player does ~7).");
            LungeSeconds = Synced(cfg, "3 - Hunter", "RushSeconds", 1.2f, "How long the burst lasts at full speed.");
            RushFadeSeconds = Synced(cfg, "3 - Hunter", "RushFadeSeconds", 1.5f, "How quickly it then fades back to the normal chase.");
            LungeStopDistance = Synced(cfg, "3 - Hunter", "RushStopDistance", 25f,
                "It stops rushing once this close (m), so the burst can't carry it all the way to you.");
            ClimbSpeed = Synced(cfg, "3 - Hunter", "ClimbSpeed", 0.7f,
                "How fast (m/s) it climbs up rocks, cliffs and steep slopes after you. Slow, so you have time to think.");
            SafeSeconds = Synced(cfg, "3 - Hunter", "SafeSpaceSeconds", 10f,
                "If everyone near it is indoors, in bed, in a dungeon, on a boat, swimming or at sea for this long, it leaves (and goes after another player if there is one).");
            FollowThroughPortals = Synced(cfg, "3 - Hunter", "FollowThroughPortals", true,
                "If the hunted player teleports away (portal), Hufsa comes for them again after FollowDelay.");
            FollowDelay = Synced(cfg, "3 - Hunter", "FollowDelaySeconds", 20f, "");
            RetargetAfterDeathRadius = Synced(cfg, "3 - Hunter", "RetargetAfterDeathRadius", 1000f,
                "When its prey dies, it goes after another player within this distance (m) of where they died; otherwise the hunt ends.");

            ColdRadius = Synced(cfg, "4 - Aura", "ColdRadius", 80f, "Outer cold: snow weather, frost, slow and reduced stamina regen.");
            StaminaRegenStopRadius = Synced(cfg, "4 - Aura", "StaminaRegenStopRadius", 20f,
                "Stamina regen slows across the cold radius and stops completely within this distance (m).");
            StaminaDrainRadius = Synced(cfg, "4 - Aura", "StaminaDrainRadius", 10f, "Within this distance (m) stamina is drained.");
            CatchRadius = Synced(cfg, "4 - Aura", "CatchRadius", 2.2f,
                "If the Hunter gets this close (m, from its centre) it catches you and you die. It never hurts you otherwise.");
            AuraCurve = Synced(cfg, "4 - Aura", "Curve", 1.5f, "Higher = effects ramp up later / closer.");
            MaxSlow = Synced(cfg, "4 - Aura", "MaxSlow", 0.7f, "Walk, jog and sprint slow at point blank (0.7 = -70%), scaled over the cold radius.");
            SlowCurve = Synced(cfg, "4 - Aura", "SlowCurve", 1f,
                "1 = slows steadily from the edge of the cold to point blank; higher = mostly when close.");
            StaminaDrain = Synced(cfg, "4 - Aura", "StaminaDrainPerSec", 12f, "Stamina drained per second at point blank.");
            ApproachMultiplier = Synced(cfg, "4 - Aura", "ApproachMultiplier", 2f, "Stamina drain multiplier while moving toward Hufsa.");
            FireQuenchBeyondCold = Synced(cfg, "4 - Aura", "FireQuenchBeyondCold", 10f,
                "Fires go out (like in rain) everywhere in the cold radius plus this many metres. -1 = off.");

            WeatherEnabled = cfg.Bind("5 - Visuals", "WeatherEnabled", true, "Force snowy weather while inside the Hunter's cold radius.");
            WeatherEnvironment = cfg.Bind("5 - Visuals", "WeatherEnvironment", "Snow", "Environment name to force (e.g. Snow, SnowStorm, Twilight_Snow).");
            FoliageFrostEnabled = cfg.Bind("5 - Visuals", "FoliageFrostEnabled", true, "Tint nearby trees, bushes and rocks frosty.");
            WatcherFrostRadius = cfg.Bind("5 - Visuals", "WatcherFrostRadius", 14f, "");
            FrostStrength = cfg.Bind("5 - Visuals", "FrostStrength", 0.75f, "0-1 how strongly vegetation is tinted.");
            FrostColor = cfg.Bind("5 - Visuals", "FrostColor", new Color(0.82f, 0.9f, 1f), "");
            WatcherGlow = cfg.Bind("5 - Visuals", "WatcherSilhouetteGlow", 0.45f,
                "Faint glow along the Watcher's outline, fading inward (0 = off).");
            HunterGlow = cfg.Bind("5 - Visuals", "HunterSilhouetteGlow", 0.45f,
                "Faint glow along the Hunter's outline, fading inward (0 = off).");
            MildBiomes = cfg.Bind("5 - Visuals", "MildBiomes", "Meadows,BlackForest",
                "Biomes where Hufsa brings no snowfall (no falling snow around it, no snowy weather) and less frost.");
            MildFrostFactor = cfg.Bind("5 - Visuals", "MildFrostFactor", 0.4f,
                "How strong the frost on plants and the frozen trail is in the mild biomes (1 = same as elsewhere).");
            SmileRevealFar = cfg.Bind("5 - Visuals", "WatcherSmileRevealFar", 80f,
                "Watcher: distance (m) where the grin starts fading in. It stands 60-100 m away and leaves at ~25 m.");
            SmileRevealNear = cfg.Bind("5 - Visuals", "WatcherSmileRevealNear", 40f, "Watcher: distance (m) where the grin is fully visible.");
            HunterSmileRevealFar = cfg.Bind("5 - Visuals", "HunterSmileRevealFar", 60f, "Hunter: distance (m) where the grin starts fading in.");
            HunterSmileRevealNear = cfg.Bind("5 - Visuals", "HunterSmileRevealNear", 25f, "Hunter: distance (m) where the grin is fully visible.");
            FaceRevealFar = cfg.Bind("5 - Visuals", "FaceRevealFar", 22f, "Distance (m) where the pupils and nose start to appear.");
            FaceRevealNear = cfg.Bind("5 - Visuals", "FaceRevealNear", 6f, "Distance (m) where the pupils and nose are fully visible.");
            HemSway = cfg.Bind("5 - Visuals", "HemMovement", 1f, "How much the hem moves while the Hunter glides (0 = stiff).");
            TrailEnabled = cfg.Bind("5 - Visuals", "FrozenTrail", true, "Leave frost patches and footprints behind the Hunter.");
            TrailLifetime = cfg.Bind("5 - Visuals", "FrozenTrailSeconds", 25f, "How long frost patches and footprints last.");
            FootprintSize = cfg.Bind("5 - Visuals", "FootprintLength", 0.45f, "Footprint length in metres.");
            AudioVolume = cfg.Bind("6 - Audio", "Volume", 0.8f, "Volume of Hufsa loop at point blank.");
            AudioRange = cfg.Bind("6 - Audio", "HunterRange", 45f, "Distance (m) at which the Hunter's loop starts fading in. Only the Hunter has music.");
            SoundsEnabled = cfg.Bind("6 - Audio", "Sounds", true,
                "Voice, footsteps and creaks from the 'sounds' folder next to the mod (voice*, step*, creak* files).");
            VoiceVolume = cfg.Bind("6 - Audio", "VoiceVolume", 1f, "");
            MoveVolume = cfg.Bind("6 - Audio", "MovementVolume", 0.8f, "Footsteps and creaks.");
        }

        private static ConfigEntry<T> Synced<T>(ConfigFile cfg, string section, string key, T value, string description) =>
            cfg.Bind(section, key, value, new ConfigDescription(description, null,
                new ConfigurationManagerAttributes { IsAdminOnly = true }));
    }
}
