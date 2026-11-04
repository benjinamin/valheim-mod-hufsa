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
        public static ConfigEntry<float> NightHuntBase;
        public static ConfigEntry<float> NightHuntPerQuietNight;
        public static ConfigEntry<float> NightHuntNoticedBonus;
        public static ConfigEntry<float> NightHuntCap;
        public static ConfigEntry<int> NightHuntRestNights;
        public static ConfigEntry<float> NightHuntWarning;
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
        public static ConfigEntry<float> WaterSpeedFactor;
        public static ConfigEntry<float> StartEaseSeconds;
        public static ConfigEntry<float> SafeCloseDistance;
        public static ConfigEntry<float> SafeCloseSeconds;
        public static ConfigEntry<bool> WatcherEveryNight;
        public static ConfigEntry<float> WatcherGuaranteeWithin;
        public static ConfigEntry<float> WatcherVanishDense;
        public static ConfigEntry<float> StareCone;
        public static ConfigEntry<float> StareSideRate;
        public static ConfigEntry<float> StareDrainSeconds;
        public static ConfigEntry<float> StareGrace;
        public static ConfigEntry<float> StareNoticeSeconds;
        public static ConfigEntry<float> StareLimitMin;
        public static ConfigEntry<float> StareLimitMax;
        public static ConfigEntry<float> StareShrinkPerHunt;
        public static ConfigEntry<float> StareLimitFloor;
        public static ConfigEntry<float> FirstStareLimit;
        public static ConfigEntry<float> FadeSeconds;
        public static ConfigEntry<float> FadeGlowFirst;
        public static ConfigEntry<float> WindUpMin;
        public static ConfigEntry<float> WindUpMax;
        public static ConfigEntry<float> RetreatApproachSpeed;
        public static ConfigEntry<float> RetreatApproachSeconds;
        public static ConfigEntry<float> RetreatSeconds;
        public static ConfigEntry<float> SightRange;
        public static ConfigEntry<float> SightMinVisible;
        public static ConfigEntry<float> SightMinScreenHeight;
        public static ConfigEntry<float> SightFogContrast;
        public static ConfigEntry<float> CombatLinger;
        public static ConfigEntry<float> OpenSpawnMinDistance;
        public static ConfigEntry<float> OpenSpawnMaxDistance;
        public static ConfigEntry<float> SafeSeconds;
        public static ConfigEntry<bool> FollowThroughPortals;
        public static ConfigEntry<float> FollowDelay;
        public static ConfigEntry<float> HunterTurnSpeed;
        public static ConfigEntry<float> RetargetTurnSpeed;
        public static ConfigEntry<float> RetargetPauseMin;
        public static ConfigEntry<float> RetargetPauseMax;
        public static ConfigEntry<float> StuckRetargetSeconds;
        public static ConfigEntry<float> PassThroughBuildingsAfter;
        public static ConfigEntry<float> MinionDamage;
        public static ConfigEntry<float> MinionDamageRange;

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
        public static ConfigEntry<float> FogGlow;
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
        public static ConfigEntry<bool> FrozenVictims;
        public static ConfigEntry<float> FrozenVictimHealth;
        public static ConfigEntry<float> FrozenVictimAhead;
        public static ConfigEntry<float> FrozenPoseSeconds;
        public static ConfigEntry<float> FrozenTint;
        public static ConfigEntry<int> FrozenSpikes;
        public static ConfigEntry<bool> TrailEnabled;
        public static ConfigEntry<float> TrailLifetime;
        public static ConfigEntry<float> FootprintSize;
        public static ConfigEntry<float> AudioVolume;
        public static ConfigEntry<float> AudioRange;
        public static ConfigEntry<bool> PlayAsMusic;
        public static ConfigEntry<bool> OverrideGameMusic;
        public static ConfigEntry<bool> SoundsEnabled;
        public static ConfigEntry<float> VoiceVolume;
        public static ConfigEntry<float> MoveVolume;
        public static ConfigEntry<float> BreathVolume;
        public static ConfigEntry<float> DespawnVolume;
        public static ConfigEntry<float> DroneVolume;
        public static ConfigEntry<float> StingVolume;
        public static ConfigEntry<float> EyeBrighten;

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
                "Each Watcher a player has noticed since their last hunt (up to 6) makes the next ones this much more likely " +
                "(0.5 = +50% each), sooner and closer.");
            WatcherMinDistance = Synced(cfg, "1 - Spawning", "WatcherSpawnMinDistanceFar", 60f,
                "Closest a Watcher appears (m). Keep it above VanishDistanceOpen.");
            WatcherMaxDistance = Synced(cfg, "1 - Spawning", "WatcherSpawnMaxDistanceFar", 95f, "Furthest a Watcher appears (m) among trees.");
            OpenSpawnMinDistance = Synced(cfg, "1 - Spawning", "OpenSpawnMinDistanceFar", 125f,
                "On open land (no trees within 30 m) it appears this far away (m) or more; scales with how open it is.");
            OpenSpawnMaxDistance = Synced(cfg, "1 - Spawning", "OpenSpawnMaxDistanceFar", 170f,
                "...and at most this far. Each Watcher you notice brings the next one closer, down to about half.");
            HunterEnabled = Synced(cfg, "1 - Spawning", "HunterEnabled", true, "Enable the hunting Hufsa.");
            HunterChance = Synced(cfg, "1 - Spawning", "RandomHuntChance", 0f,
                "Chance per roll of an extra hunt on whoever is most alone, with no warning. 0 = off (night hunts below still come).");
            NightHuntBase = Synced(cfg, "1 - Spawning", "NightHuntChance", 0.05f,
                "Once a player has been hunted, each night there is this chance it comes for them anyway, even if they never " +
                "looked at it...");
            NightHuntPerQuietNight = Synced(cfg, "1 - Spawning", "NightHuntChancePerQuietNight", 0.08f,
                "...plus this much for every night since the last hunt...");
            NightHuntNoticedBonus = Synced(cfg, "1 - Spawning", "NightHuntChanceIfNoticed", 0.15f,
                "...plus this much on a night they noticed a Watcher (and looked away in time)...");
            NightHuntCap = Synced(cfg, "1 - Spawning", "NightHuntChanceMax", 0.5f, "...never more than this.");
            NightHuntRestNights = Synced(cfg, "1 - Spawning", "NightHuntRestNights", 2,
                "After any hunt, this many nights without a night hunt (staring at a Watcher can still bring one).");
            NightHuntWarning = Synced(cfg, "1 - Spawning", "NightHuntWarningSeconds", 20f,
                "The drone and a faint breath fade in this many seconds before a night hunt arrives.");
            HunterCooldownMinutes = Synced(cfg, "1 - Spawning", "HunterCooldownMinutes", 20f, "Minimum real minutes between Hunter spawns.");
            HunterMinDistance = Synced(cfg, "1 - Spawning", "HunterMinDistance", 45f, "");
            HunterMaxDistance = Synced(cfg, "1 - Spawning", "HunterMaxDistance", 65f, "");

            WatcherVanishDistance = Synced(cfg, "2 - Watcher", "VanishDistanceOpen", 35f,
                "It leaves when a player comes this close (m) across open ground (nothing between you).");
            WatcherVanishDense = Synced(cfg, "2 - Watcher", "VanishDistanceDense", 14f,
                "...and this close (m) when trees and rocks stand between you (4 or more). In between, it scales.");
            WatcherLifetime = Synced(cfg, "2 - Watcher", "LifetimeSeconds", 180f, "The Watcher vanishes after this long.");
            WatcherBlinks = Synced(cfg, "2 - Watcher", "Blinks", 3,
                "Times the Watcher moves somewhere else (just outside your view, a little closer) when you look away, before it leaves for good.");
            WatcherLookAwaySeconds = Synced(cfg, "2 - Watcher", "LookAwaySeconds", 1.2f,
                "How long you must look away (after having seen it) before it moves or leaves.");
            WatcherHidesBehindCover = Synced(cfg, "2 - Watcher", "VanishWhenViewBlocked", false,
                "If a tree, rock or building blocks your view of it while you are within 70 m, it is gone when you look again. " +
                "Off by default: it tended to vanish before you could properly look at it. Walking up to it still makes it leave.");
            StareCone = Synced(cfg, "2 - Watcher", "StareConeDegrees", 5f,
                "Looking at it: its face within this many degrees of the middle of your view fills the hidden stare meter at full speed...");
            StareSideRate = Synced(cfg, "2 - Watcher", "StareOffCentreRate", 0.1f,
                "...elsewhere on screen at this share of that speed. While you are busy (fighting, building, swinging, " +
                "inventory or map open) it does not fill at all.");
            StareDrainSeconds = Synced(cfg, "2 - Watcher", "StareDrainSeconds", 30f,
                "Looking away, a full stare meter empties in this many seconds.");
            StareGrace = Synced(cfg, "2 - Watcher", "StareGraceSeconds", 0.5f,
                "Something passing in front of it (a tree as you walk) for less than this does not count as looking away.");
            StareNoticeSeconds = Synced(cfg, "2 - Watcher", "NoticeSeconds", 2f,
                "This much staring (at full speed) and you have noticed it: the next ones come closer, and the night-hunt odds rise that night.");
            StareLimitMin = Synced(cfg, "2 - Watcher", "StareLimitMin", 4f,
                "Stare at it past its limit and it comes for you. Each Watcher's limit is random, between this...");
            StareLimitMax = Synced(cfg, "2 - Watcher", "StareLimitMax", 10f, "...and this many seconds of full-speed staring...");
            StareShrinkPerHunt = Synced(cfg, "2 - Watcher", "StareLimitShrinkPerHunt", 0.15f,
                "...shorter by this share for every hunt you have had...");
            StareLimitFloor = Synced(cfg, "2 - Watcher", "StareLimitFloor", 3f,
                "...but never under this.");
            FirstStareLimit = Synced(cfg, "2 - Watcher", "FirstStareLimit", 3f,
                "The very first Watcher you notice never hunts you: stare at it this long (seconds) and it fades away instead.");
            FadeSeconds = Synced(cfg, "2 - Watcher", "FadeSeconds", 1.2f,
                "How long a Watcher takes to fade away when it leaves quietly (a Watcher backing away takes RetreatSeconds).");
            FadeGlowFirst = cfg.Bind("5 - Visuals", "FadeGlowFirst", 0.35f,
                "Fading away, the glow along its outline goes first, over this share of the fade; the body starts fading " +
                "partway through that and takes the rest.");
            WindUpMin = Synced(cfg, "2 - Watcher", "ChaseWarningMin", 3f,
                "When the limit is reached its eyes flare and the sting plays; the chase starts between this...");
            WindUpMax = Synced(cfg, "2 - Watcher", "ChaseWarningMax", 5f, "...and this many seconds later.");
            RetreatApproachSpeed = Synced(cfg, "2 - Watcher", "RetreatApproachSpeed", 2f,
                "Walking toward it faster than this (m/s)...");
            RetreatApproachSeconds = Synced(cfg, "2 - Watcher", "RetreatApproachSeconds", 1.5f,
                "...for this long makes it back away into the dark...");
            RetreatSeconds = Synced(cfg, "2 - Watcher", "RetreatSeconds", 2.5f, "...fading out over this many seconds.");
            CombatLinger = Synced(cfg, "2 - Watcher", "CombatLingerSeconds", 5f,
                "You count as in combat while an alerted enemy is after you or one hit you, and this many seconds after.");
            SightRange = Synced(cfg, "2 - Watcher", "SightRange", 180f, "Furthest away (m) a sighting can count.");
            SightMinVisible = Synced(cfg, "2 - Watcher", "SightMinVisibleFraction", 0.5f,
                "Share of its upper body (face and the shape below it) that must really show on screen, not hidden by " +
                "leaves, trunks, rocks or the ground; the face on its own must show this much too.");
            SightMinScreenHeight = Synced(cfg, "2 - Watcher", "SightMinScreenHeight", 0.007f,
                "Its upper body must be at least this share of the screen's height (0.007 is about 8 pixels at 1080p, " +
                "roughly 170 m away), so a speck far off doesn't count.");
            SightFogContrast = Synced(cfg, "2 - Watcher", "SightFogContrast", 0f,
                "In fog a sighting only counts as far as this much of it still shows through the fog (0 = fog doesn't matter). " +
                "Off by default: Valheim's fog settings don't match what you see (0.35 cut a blizzard down to 17 m).");
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
            EscapeDistance = Synced(cfg, "3 - Hunter", "WarningDistance", 110f,
                "Players within this distance (m) of a Hunter when it appears get the \"Someone is hunting you!\" warning. " +
                "Distance never ends a hunt: if you outrun it, it comes after you again (FollowDelaySeconds).");
            StartEaseSeconds = Synced(cfg, "3 - Hunter", "StartEaseSeconds", 3f,
                "When it starts to come for you it does not rush: its speed eases in from standing still to its chase " +
                "speed for that distance (fast from far away, down to a walk up close) over this many seconds.");
            WaterSpeedFactor = Synced(cfg, "3 - Hunter", "WaterSpeedFactor", 0.6f,
                "Its speed while gliding across rivers and lakes, as a fraction of its chase speed (the swamp is full of water).");
            ClimbSpeed = Synced(cfg, "3 - Hunter", "ClimbSpeed", 0.7f,
                "How fast (m/s) it climbs up rocks, cliffs and steep slopes after you. Slow, so you have time to think.");
            SafeSeconds = Synced(cfg, "3 - Hunter", "SafePlaceSeconds", 25f,
                "Shelter is the only way out: if everyone is indoors, in bed, in a dungeon, on a boat or at sea for this long, " +
                "it leaves (and goes after another player if there is one).");
            SafeCloseDistance = Synced(cfg, "3 - Hunter", "SafeCloseDistance", 7f,
                "...or sooner: if it has come right up to the shelter (within this many m) and can't get closer...");
            SafeCloseSeconds = Synced(cfg, "3 - Hunter", "SafeCloseSeconds", 5f, "...for this many seconds.");
            FollowThroughPortals = Synced(cfg, "3 - Hunter", "FollowWhenLost", true,
                "If it loses you (you outran it, or used a portal), it comes for you again after FollowDelaySeconds. " +
                "Not once it has killed someone that hunt: then whoever got away is let go.");
            FollowDelay = Synced(cfg, "3 - Hunter", "FollowDelaySeconds", 20f, "");
            HunterTurnSpeed = Synced(cfg, "3 - Hunter", "TurnSpeed", 90f, "How fast (degrees/s) it turns while gliding after you.");
            StuckRetargetSeconds = Synced(cfg, "3 - Hunter", "StuckRetargetSeconds", 15f,
                "It hunts the player it started on until they die, use a portal or hide in shelter, or it has been stuck for " +
                "this long; then it goes after the nearest other player.");
            RetargetTurnSpeed = Synced(cfg, "3 - Hunter", "RetargetTurnSpeed", 480f,
                "Standing still when it picks a new player, it turns to them in jerks this fast (degrees/s)...");
            RetargetPauseMin = Synced(cfg, "3 - Hunter", "RetargetPauseMin", 0.1f, "...stares for between this...");
            RetargetPauseMax = Synced(cfg, "3 - Hunter", "RetargetPauseMax", 0.5f, "...and this many seconds, then comes.");
            PassThroughBuildingsAfter = Synced(cfg, "3 - Hunter", "PassThroughBuildingsAfterSeconds", 8f,
                "Stuck this long behind a building between it and a player out in the open, it passes straight through the walls. " +
                "Never into a shelter its prey is hiding in. 0 = never.");
            MinionDamage = Synced(cfg, "3 - Hunter", "MinionDamagePerSecond", 40f,
                "Tamed creatures and summons that come close are hurt this much per second (it can't be harmed itself).");
            MinionDamageRange = Synced(cfg, "3 - Hunter", "MinionDamageRange", 4f, "How close (m) they must be to be hurt.");
            RetargetAfterDeathRadius = Synced(cfg, "3 - Hunter", "RetargetAfterDeathRadius", 1000f,
                "When its prey dies it turns to the nearest other player it can reach. If it was gone already, the server sends it " +
                "after another player within this distance (m) of where they died; otherwise the hunt ends.");

            ColdRadius = Synced(cfg, "4 - Aura", "ColdRadius", 80f, "Outer cold: frost, slow and reduced stamina regen.");
            StaminaRegenStopRadius = Synced(cfg, "4 - Aura", "StaminaRegenStopRadius", 20f,
                "Stamina regen slows across the cold radius and stops completely within this distance (m).");
            StaminaDrainRadius = Synced(cfg, "4 - Aura", "StaminaDrainRadius", 10f, "Within this distance (m) stamina is drained.");
            CatchRadius = Synced(cfg, "4 - Aura", "CatchRadius", 2.2f,
                "If the Hunter gets this close (m, from its centre) it catches you and you die. It never hurts you otherwise.");
            AuraCurve = Synced(cfg, "4 - Aura", "Curve", 1.5f, "Higher = effects ramp up later / closer.");
            MaxSlow = Synced(cfg, "4 - Aura", "MaxSlow", 0.6f, "Walk, jog and sprint slow at point blank (0.6 = -60%), scaled over the cold radius.");
            SlowCurve = Synced(cfg, "4 - Aura", "SlowCurve", 1f,
                "1 = slows steadily from the edge of the cold to point blank; higher = mostly when close.");
            StaminaDrain = Synced(cfg, "4 - Aura", "StaminaDrainPerSec", 10f, "Stamina drained per second at point blank.");
            ApproachMultiplier = Synced(cfg, "4 - Aura", "ApproachMultiplier", 1.75f, "Stamina drain multiplier while moving toward Hufsa.");
            FireQuenchBeyondCold = Synced(cfg, "4 - Aura", "FireQuenchBeyondCold", 10f,
                "Fires go out (like in rain) everywhere in the cold radius plus this many metres. -1 = off.");

            FogGlow = cfg.Bind("5 - Visuals", "SilhouetteGlowInFog", 0.1f,
                "How much of the silhouette glow is left in misty or foggy weather (1 = all, 0 = none): in fog the glow would give Hufsa away.");
            FoliageFrostEnabled = cfg.Bind("5 - Visuals", "FoliageFrostEnabled", true, "Tint nearby trees, bushes and rocks frosty.");
            WatcherFrostRadius = cfg.Bind("5 - Visuals", "WatcherFrostRadius", 14f, "");
            FrostStrength = cfg.Bind("5 - Visuals", "FrostStrength", 0.75f, "0-1 how strongly vegetation is tinted.");
            FrostColor = cfg.Bind("5 - Visuals", "FrostColor", new Color(0.82f, 0.9f, 1f), "");
            WatcherGlow = cfg.Bind("5 - Visuals", "WatcherSilhouetteGlow", 0.45f,
                "Faint glow along the Watcher's outline, fading inward (0 = off).");
            HunterGlow = cfg.Bind("5 - Visuals", "HunterSilhouetteGlow", 0.45f,
                "Faint glow along the Hunter's outline, fading inward (0 = off).");
            MildBiomes = cfg.Bind("5 - Visuals", "MildBiomes", "Meadows,BlackForest",
                "Biomes where Hufsa brings no snowfall (no falling snow around it) and less frost.");
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
            FrozenVictims = cfg.Bind("5 - Visuals", "FrozenVictims", true,
                "Whoever Hufsa catches is left standing frozen solid, cowering, with ice spikes in their back. The statue " +
                "stays in the world until someone breaks it, and drops ice. Off = a normal death.");
            FrozenVictimHealth = cfg.Bind("5 - Visuals", "FrozenVictimHealth", 60f,
                "How much damage it takes to break a frozen statue. Weak to fire. (Read at startup.)");
            FrozenVictimAhead = cfg.Bind("5 - Visuals", "FrozenVictimAhead", 1.5f,
                "The statue stands this many metres ahead of where the player died, clear of their tombstone.");
            FrozenPoseSeconds = cfg.Bind("5 - Visuals", "FrozenPoseSeconds", 1f,
                "How far into the cower emote (seconds) the statue is frozen.");
            FrozenTint = cfg.Bind("5 - Visuals", "FrozenTint", 0.65f, "How icy-blue the frozen statue is (0 = own colours, 1 = all ice).");
            FrozenSpikes = cfg.Bind("5 - Visuals", "FrozenSpikes", 14, "Ice spikes out of the frozen statue's back.");
            TrailEnabled = cfg.Bind("5 - Visuals", "FrozenTrail", true, "Leave frost patches and footprints behind the Hunter.");
            TrailLifetime = cfg.Bind("5 - Visuals", "FrozenTrailSeconds", 25f, "How long frost patches and footprints last.");
            FootprintSize = cfg.Bind("5 - Visuals", "FootprintLength", 0.45f, "Footprint length in metres.");
            AudioVolume = cfg.Bind("6 - Audio", "Volume", 0.8f, "Volume of Hufsa loop at point blank.");
            AudioRange = cfg.Bind("6 - Audio", "HunterRange", 45f, "Distance (m) at which the Hunter's loop starts fading in. Only the Hunter has music.");
            PlayAsMusic = cfg.Bind("6 - Audio", "LoopOnMusicChannel", true,
                "The Hunter's loop plays as music: on the music channel, following the game's music volume setting. Off = ambient sound, as before.");
            OverrideGameMusic = cfg.Bind("6 - Audio", "LoopOverridesGameMusic", true,
                "While the Hunter's loop plays, the game's own music (the biome's) fades out, and fades back in once the loop has faded away.");
            SoundsEnabled = cfg.Bind("6 - Audio", "Sounds", true,
                "Voice, footsteps and creaks from the 'sounds' folder next to the mod (voice*, step*, creak* files).");
            VoiceVolume = cfg.Bind("6 - Audio", "VoiceVolume", 1f, "");
            MoveVolume = cfg.Bind("6 - Audio", "MovementVolume", 0.8f, "Footsteps and creaks.");
            BreathVolume = cfg.Bind("6 - Audio", "WatcherBreathVolume", 1f,
                "The Watcher's breathing, heard now and then while it stands behind you (breath* files).");
            DespawnVolume = cfg.Bind("6 - Audio", "DespawnVolume", 1f,
                "Hufsa leaving (despawn* files). Heard only within the Watcher's leave distance, up to 40% louder up close.");
            DroneVolume = cfg.Bind("6 - Audio", "DroneVolume", 0.6f,
                "The low drone that rises as you stare at a Watcher, comes before a night hunt and stays through a hunt.");
            StingVolume = cfg.Bind("6 - Audio", "StingVolume", 1f, "The sting when you have stared at a Watcher too long.");
            EyeBrighten = cfg.Bind("5 - Visuals", "EyeBrighten", 0.35f,
                "How much brighter a Watcher's eyes get as you stare at it (0 = not at all).");

            // Defaults that changed: a config file still holding the old default gets the new one, once.
            ConfigEntry<int> defaults = cfg.Bind("9 - Internal", "DefaultsVersion", 0, "Which default changes this file has had. Leave it alone.");
            if (defaults.Value < 1)
            {
                Migrate(MaxSlow, 0.7f); // 0.3.0: the chill was a bit harsh
                Migrate(StaminaDrain, 12f);
                Migrate(ApproachMultiplier, 2f);
                defaults.Value = 1;
            }
            if (defaults.Value < 2)
            {
                Migrate(SafeSeconds, 60f); // 0.4.0: it gives up on shelter sooner
                Migrate(SafeCloseSeconds, 10f);
                defaults.Value = 2;
            }
        }

        private static void Migrate(ConfigEntry<float> entry, float oldDefault)
        {
            if (Mathf.Approximately(entry.Value, oldDefault)) entry.Value = (float)entry.DefaultValue;
        }

        private static ConfigEntry<T> Synced<T>(ConfigFile cfg, string section, string key, T value, string description) =>
            cfg.Bind(section, key, value, new ConfigDescription(description, null,
                new ConfigurationManagerAttributes { IsAdminOnly = true }));
    }
}
