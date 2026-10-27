using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Client side: applies the chill to the local player, kills them if the Hunter reaches them, forces
    /// snowy weather near the Hunter and fog while a Watcher is about (once you've seen it often enough).
    /// </summary>
    internal class HufsaAuraManager : MonoBehaviour
    {
        /// <summary>Multiplier on the local player's walk, jog and sprint speed (1 = unaffected).</summary>
        internal static float LocalSpeedFactor = 1f;

        private float timer;
        private string forcedEnv; // the weather we are forcing, if any
        private bool templatePrepared;

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < 0.25f) return;
            timer = 0f;

            Player player = Player.m_localPlayer;
            LocalSpeedFactor = 1f;
            if (!player || player.IsDead())
            {
                SetWeather(null);
                return;
            }

            HufsaEntity nearest = null;
            float nearestDist = float.MaxValue;
            foreach (HufsaEntity g in HufsaEntity.Instances)
            {
                if (!g.IsHunter || g.Vanishing) continue;
                float d = Vector3.Distance(g.transform.position, player.transform.position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearest = g;
                }
            }

            // Safe behind a wall or on a boat even if it's right there.
            if (nearest && nearestDist <= HufsaConfig.CatchRadius.Value && HufsaHuntState.IsHuntable(player))
            {
                Caught(player);
                return;
            }

            float coldRadius = HufsaConfig.ColdRadius.Value;
            float cold = Falloff(nearest, nearestDist, coldRadius);

            // Walk, jog and sprint get steadily slower the closer it is (linear from the edge of the cold).
            if (nearest && nearestDist < coldRadius)
            {
                float closeness = Mathf.Pow(Mathf.Clamp01(1f - nearestDist / coldRadius), HufsaConfig.SlowCurve.Value);
                LocalSpeedFactor = 1f - HufsaConfig.MaxSlow.Value * closeness;
            }

            if (cold > 0f)
            {
                // Stamina regen slows across the cold and stops completely from 20 m; within 10 m it drains.
                float stop = HufsaConfig.StaminaRegenStopRadius.Value;
                float regen = Mathf.Clamp01((nearestDist - stop) / Mathf.Max(coldRadius - stop, 1f));
                float drain = Mathf.Clamp01(1f - nearestDist / Mathf.Max(HufsaConfig.StaminaDrainRadius.Value, 0.1f));
                ApplyChill(player, nearest, cold, regen, drain);
            }

            // No snowy weather in the mild biomes; the cold itself still applies. Otherwise, once you have seen it
            // often enough (WatcherFogFromSightings), a Watcher brings the biome's own fog, where it has one.
            if (HufsaConfig.WeatherEnabled.Value && nearest && !nearest.Mild && nearestDist < coldRadius)
                SetWeather(HufsaConfig.WeatherEnvironment.Value);
            else if (HufsaConfig.WatcherFog.Value && AnyWatcher() && HufsaDread.Instance &&
                     HufsaDread.Instance.Count >= HufsaConfig.WatcherFogFromSightings.Value)
                SetWeather(FogEnvironment(player.transform.position));
            else
                SetWeather(null);
        }

        /// <summary>
        /// How much of the silhouette glow to keep: in fog the glow would make Hufsa stand out far too clearly, so
        /// it fades down to FogGlow there.
        /// </summary>
        internal static float GlowWeatherFactor = 1f;

        private void LateUpdate()
        {
            float target = IsFoggy() ? HufsaConfig.FogGlow.Value : 1f;
            GlowWeatherFactor = Mathf.MoveTowards(GlowWeatherFactor, target, Time.deltaTime / 4f); // like the weather blending
        }

        private static bool IsFoggy()
        {
            string name = HufsaEntity.CurrentWeather().ToLowerInvariant();
            return name.Contains("mist") || name.Contains("fog");
        }

        private static bool AnyWatcher()
        {
            foreach (HufsaEntity g in HufsaEntity.Instances) if (!g.IsHunter && !g.Vanishing) return true;
            return false;
        }

        private readonly System.Collections.Generic.Dictionary<Heightmap.Biome, string> fogByBiome =
            new System.Collections.Generic.Dictionary<Heightmap.Biome, string>();

        /// <summary>
        /// The foggiest of this biome's own weathers with mist or fog in its name (e.g. "Misty"), or null if the
        /// biome has none.
        /// </summary>
        private string FogEnvironment(Vector3 pos)
        {
            Heightmap.Biome biome = HufsaEntity.BiomeAt(pos);
            if (fogByBiome.TryGetValue(biome, out string cached)) return cached;
            string best = null;
            float bestDensity = -1f;
            if (EnvMan.instance)
            {
                foreach (BiomeEnvSetup setup in EnvMan.instance.m_biomes)
                {
                    if (setup.m_biome != biome) continue;
                    foreach (EnvEntry e in setup.m_environments)
                    {
                        string n = (e.m_environment ?? "").ToLowerInvariant();
                        if (e.m_env == null || !(n.Contains("mist") || n.Contains("fog"))) continue;
                        if (e.m_env.m_fogDensityNight > bestDensity)
                        {
                            bestDensity = e.m_env.m_fogDensityNight;
                            best = e.m_environment;
                        }
                    }
                }
            }
            fogByBiome[biome] = best;
            HufsaPlugin.Log.LogInfo($"Hufsa fog for {biome}: {best ?? "none"}");
            return best;
        }

        private static float Falloff(HufsaEntity nearest, float dist, float radius)
        {
            if (!nearest || radius <= 0f) return 0f;
            return Mathf.Pow(Mathf.Clamp01(1f - dist / radius), HufsaConfig.AuraCurve.Value);
        }

        /// <summary>Hufsa has reached you. There is no fighting it.</summary>
        private static void Caught(Player player)
        {
            var hit = new HitData();
            hit.m_damage.m_damage = 1_000_000f;
            hit.m_hitType = HitData.HitType.Freezing;
            hit.m_point = player.GetCenterPoint();
            player.ApplyDamage(hit, true, true, HitData.DamageModifier.Normal);
            HufsaPlugin.Log.LogInfo("Hufsa caught the local player");
        }

        private void ApplyChill(Player player, HufsaEntity hufsa, float cold, float regen, float drain)
        {
            PrepareTemplate();
            SEMan seman = player.GetSEMan();
            var se = seman.GetStatusEffect(SE_HufsaChill.Hash) as SE_HufsaChill;
            if (!se) se = seman.AddStatusEffect(SE_HufsaChill.Hash, true) as SE_HufsaChill;
            if (!se) return;

            Vector3 vel = player.GetVelocity();
            vel.y = 0f;
            Vector3 toHufsa = hufsa.transform.position - player.transform.position;
            toHufsa.y = 0f;
            float approach = vel.magnitude > 0.5f ? Mathf.Max(0f, Vector3.Dot(vel.normalized, toHufsa.normalized)) : 0f;

            se.ColdStrength = cold;
            se.RegenFactor = regen;
            se.DrainStrength = drain;
            se.ApproachMultiplier = 1f + approach * (HufsaConfig.ApproachMultiplier.Value - 1f);
            se.ResetTime();
        }

        /// <summary>Borrow the vanilla Freezing icon and frost breath once ObjectDB is loaded.</summary>
        private void PrepareTemplate()
        {
            if (templatePrepared || !ObjectDB.instance) return;
            StatusEffect template = ObjectDB.instance.GetStatusEffect(SE_HufsaChill.Hash);
            StatusEffect freezing = ObjectDB.instance.GetStatusEffect("Freezing".GetStableHashCode());
            if (template && freezing)
            {
                template.m_icon = freezing.m_icon;
                template.m_startEffects = freezing.m_startEffects;
            }
            templatePrepared = true;
        }

        /// <summary>Forces this weather (null: lets go of ours). Never overrides weather someone else forced.</summary>
        private void SetWeather(string want)
        {
            EnvMan env = EnvMan.instance;
            if (!env || want == forcedEnv) return;
            if (forcedEnv != null && env.m_forceEnv == forcedEnv) env.SetForceEnvironment("");
            forcedEnv = null;
            if (!string.IsNullOrEmpty(want) && string.IsNullOrEmpty(env.m_forceEnv))
            {
                env.SetForceEnvironment(want);
                forcedEnv = want;
            }
        }

        private void OnDestroy() => SetWeather(null);
    }
}
