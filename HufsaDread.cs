using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Client side: what this player's character has been through with Hufsa, saved with the character, and the
    /// night hunts that come of it.
    /// - Count: Watchers noticed since the last hunt (up to FamiliarityCap): the next ones come sooner and closer.
    /// - Encounters: Watchers ever noticed. The very first one never hunts you (HufsaStare).
    /// - Hunts: hunts you have had. Each makes the stare limit shorter, and the first unlocks the night hunts.
    /// - Night hunts: once hunted, every night it may come anyway, even if you never looked: NightHuntChance plus
    ///   NightHuntChancePerQuietNight for each night since the last hunt, plus NightHuntChanceIfNoticed on a night
    ///   you noticed a Watcher, at most NightHuntChanceMax. After any hunt, NightHuntRestNights without one. The
    ///   drone and a faint breath come NightHuntWarningSeconds before it does.
    /// </summary>
    internal class HufsaDread : MonoBehaviour
    {
        public const string RpcDread = "Hufsa_Dread";
        public const string RpcDreadReset = "Hufsa_DreadReset";
        public const string RpcNightHunt = "Hufsa_NightHunt";
        public const int FamiliarityCap = 6;

        private const string CountKey = "hufsa_dread";
        private const string EncountersKey = "hufsa_encounters";
        private const string HuntsKey = "hufsa_hunts";
        private const string HuntDayKey = "hufsa_hunt_day";
        private const string QuietKey = "hufsa_quiet_nights";
        private const string RestKey = "hufsa_rest_nights";
        private const string NightKey = "hufsa_last_night";

        internal static HufsaDread Instance;

        /// <summary>Watchers noticed since the last hunt, at most FamiliarityCap.</summary>
        public int Count { get; private set; }
        public int Encounters { get; private set; }
        public int Hunts { get; private set; }
        public int QuietNights { get; private set; }
        public int RestNights { get; private set; }

        /// <summary>0..1: how far along the next Watchers are in coming sooner and closer.</summary>
        public float Familiarity => Mathf.Clamp01(Count / (float)FamiliarityCap);

        // Tonight (not saved).
        public bool NoticedTonight { get; private set; }
        public float TonightChance { get; private set; }
        /// <summary>When tonight's hunt comes (Time.time), or -1.</summary>
        public float HuntAt { get; private set; } = -1f;
        public bool Warning => HuntAt > 0f && Time.time >= HuntAt - HufsaConfig.NightHuntWarning.Value;
        /// <summary>0..1 over the warning before a night hunt.</summary>
        public float Warning01 => HuntAt > 0f
            ? Mathf.Clamp01(1f - (HuntAt - Time.time) / Mathf.Max(1f, HufsaConfig.NightHuntWarning.Value)) : 0f;

        private Player loadedFor;
        private int huntDay = -1;
        private int lastNight = -1;
        private bool wasNight;
        private bool bonusRolled;
        private bool warnedBreath;
        private int lastReported = -1;
        private float reportTimer;

        private void Awake() => Instance = this;

        internal static void RegisterRpcs()
        {
            ZRoutedRpc.instance.Register(RpcDreadReset, sender => Instance?.OnHunted());
        }

        private void Update()
        {
            Player p = Player.m_localPlayer;
            if (!p)
            {
                loadedFor = null;
                return;
            }
            if (p != loadedFor) Load(p);

            UpdateNight(p);

            reportTimer -= Time.deltaTime;
            if (Count != lastReported || reportTimer <= 0f) Report();
        }

        private void Load(Player p)
        {
            loadedFor = p;
            // Characters saved before the rename still carry the old "groke_" key: move it over.
            string old = CountKey.Replace("hufsa", "groke");
            if (!p.m_customData.ContainsKey(CountKey) && p.m_customData.TryGetValue(old, out string v)) p.m_customData[CountKey] = v;
            p.m_customData.Remove(old);
            p.m_customData.Remove("hufsa_dread_day"); // the old decay

            Count = Mathf.Min(GetInt(p, CountKey, 0), FamiliarityCap);
            Encounters = Mathf.Max(GetInt(p, EncountersKey, 0), Count);
            Hunts = GetInt(p, HuntsKey, 0);
            huntDay = GetInt(p, HuntDayKey, -1);
            QuietNights = GetInt(p, QuietKey, 0);
            RestNights = GetInt(p, RestKey, 0);
            lastNight = GetInt(p, NightKey, -1);
            wasNight = false;
            HuntAt = -1f;
            NoticedTonight = bonusRolled = false;
            lastReported = -1;
        }

        private static int GetInt(Player p, string key, int fallback) =>
            p.m_customData.TryGetValue(key, out string s) && int.TryParse(s, out int n) ? n : fallback;

        private void Save()
        {
            if (!loadedFor) return;
            var d = loadedFor.m_customData;
            d[CountKey] = Count.ToString();
            d[EncountersKey] = Encounters.ToString();
            d[HuntsKey] = Hunts.ToString();
            d[HuntDayKey] = huntDay.ToString();
            d[QuietKey] = QuietNights.ToString();
            d[RestKey] = RestNights.ToString();
            d[NightKey] = lastNight.ToString();
        }

        private static int Today() => EnvMan.instance ? EnvMan.instance.GetDay() : 0;

        /// <summary>A Watcher was noticed (looked at for NoticeSeconds). Returns whether it was the very first one.</summary>
        public bool Notice()
        {
            bool first = Encounters == 0;
            Encounters++;
            Count = Mathf.Min(Count + 1, FamiliarityCap);
            if (EnvMan.IsNight()) NoticedTonight = true;
            Save();
            HufsaPlugin.Log.LogInfo($"Watcher noticed ({Encounters} so far{(first ? ", the first: it won't hunt you" : "")})");
            return first;
        }

        /// <summary>
        /// A hunt has come for this player (from staring, at night, or the server turning it on them). Counted once
        /// per day: following you through a portal is the same hunt.
        /// </summary>
        public void OnHunted()
        {
            HuntAt = -1f;
            Count = 0;
            QuietNights = 0;
            RestNights = Mathf.Max(0, HufsaConfig.NightHuntRestNights.Value);
            if (huntDay != Today())
            {
                Hunts++;
                huntDay = Today();
            }
            Save();
        }

        /// <summary>Console testing.</summary>
        public void SetState(int hunts, int quiet, int rest, int encounters)
        {
            Hunts = Mathf.Max(0, hunts);
            QuietNights = Mathf.Max(0, quiet);
            RestNights = Mathf.Max(0, rest);
            Encounters = Mathf.Max(0, encounters);
            Save();
        }

        /// <summary>Console testing: tonight's hunt comes in this many seconds.</summary>
        public void ScheduleIn(float seconds) => HuntAt = Time.time + Mathf.Max(0f, seconds);

        private float Chance(bool noticed) =>
            Mathf.Min(HufsaConfig.NightHuntCap.Value,
                HufsaConfig.NightHuntBase.Value + QuietNights * HufsaConfig.NightHuntPerQuietNight.Value +
                (noticed ? HufsaConfig.NightHuntNoticedBonus.Value : 0f));

        public bool NightHuntsUnlocked => Hunts > 0;

        private void UpdateNight(Player p)
        {
            if (!EnvMan.instance) return;
            bool night = EnvMan.IsNight();
            if (night && !wasNight) OnNightfall();
            if (!night && wasNight)
            {
                HuntAt = -1f; // dawn: whatever didn't come tonight, didn't
                NoticedTonight = bonusRolled = false;
            }
            wasNight = night;

            if (HuntAt < 0f || !night) return;
            if (Warning && !warnedBreath)
            {
                warnedBreath = true;
                // A faint breath from somewhere behind you.
                Transform cam = GameCamera.instance ? GameCamera.instance.transform : null;
                Vector3 behind = cam ? cam.position - cam.forward * 12f : p.transform.position;
                HufsaSoundBank.PlayFrom(behind, HufsaSoundBank.Pick(HufsaSoundBank.Breaths),
                    HufsaConfig.BreathVolume.Value * 0.45f, Random.Range(0.9f, 0.98f));
            }
            if (Time.time < HuntAt) return;

            if (!HufsaHuntState.IsHuntable(p) || p.IsDead() || HufsaEntity.AnyHunterLoaded())
            {
                // Inside, or something else is going on: it waits, and tries again a little later.
                HuntAt = Time.time + 60f;
                warnedBreath = false;
                return;
            }
            HuntAt = -1f;
            warnedBreath = false;
            if (ZRoutedRpc.instance != null)
                ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RpcNightHunt);
            HufsaPlugin.Log.LogInfo("Night hunt: it comes for you");
            OnHunted(); // used up whether it comes or another player's hunt already took the night
        }

        private void OnNightfall()
        {
            int night = Today();
            if (night == lastNight) return; // reloaded mid-night: already rolled
            lastNight = night;
            NoticedTonight = bonusRolled = false;
            HuntAt = -1f;
            TonightChance = 0f;
            if (!NightHuntsUnlocked || !HufsaConfig.HunterEnabled.Value)
            {
                Save();
                return;
            }
            if (RestNights > 0)
            {
                RestNights--;
                HufsaPlugin.Log.LogInfo($"Night {night}: resting from it ({RestNights} more)");
                Save();
                return;
            }
            TonightChance = Chance(false);
            bool comes = Random.value < TonightChance;
            HufsaPlugin.Log.LogInfo($"Night {night}: {TonightChance:P0} it comes ({QuietNights} quiet nights): {(comes ? "it will" : "not tonight")}");
            QuietNights++; // counts as quiet unless it comes (OnHunted resets it)
            if (comes) HuntAt = Time.time + Random.Range(30f, NightSeconds() * 0.6f);
            Save();
        }

        private static float NightSeconds() =>
            EnvMan.instance ? Mathf.Max(120f, EnvMan.instance.m_dayLengthSec * 0.3f) : 400f;

        /// <summary>
        /// A noticed Watcher left (and you looked away in time). The night's odds go up by the bonus once, and if it
        /// is coming, it comes 1-5 minutes from now.
        /// </summary>
        public void WatcherLeftNoticed()
        {
            if (!EnvMan.IsNight() || !NightHuntsUnlocked || RestNights > 0 || lastNight != Today()) return;
            if (!bonusRolled)
            {
                bonusRolled = true;
                float before = HuntAt > 0f ? 1f : TonightChance;
                float after = Mathf.Min(HufsaConfig.NightHuntCap.Value, TonightChance + HufsaConfig.NightHuntNoticedBonus.Value);
                // The extra roll that lifts tonight's odds from 'before' to 'after'.
                float extra = before >= 1f ? 0f : Mathf.Clamp01((after - before) / (1f - before));
                TonightChance = Mathf.Max(TonightChance, after);
                if (HuntAt < 0f && Random.value < extra) HuntAt = 1f; // scheduled just below
            }
            if (HuntAt > 0f) HuntAt = Time.time + Random.Range(60f, 300f);
        }

        private void Report()
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RpcDread, Count);
            lastReported = Count;
            reportTimer = 60f;
        }
    }
}
