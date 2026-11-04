using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Client side: the hidden stare meter, and what it leads to.
    /// - It fills while you look at a Watcher: at full speed with its face near the middle of your view
    ///   (StareConeDegrees), slowly with it elsewhere on screen (StareOffCentreRate), not at all while you are busy.
    ///   Looking away it drains (StareDrainSeconds). Nothing shows it: you hear and see it instead, as breathing,
    ///   the drone rising, and the Watcher's eyes brightening.
    /// - NoticeSeconds of it and you have noticed that Watcher (HufsaDread).
    /// - Past that Watcher's own random limit it comes for you: its eyes flare, the sting plays, and a few seconds
    ///   later it is the Hunter. Except the very first Watcher you notice, which slips away instead (and so does
    ///   any Watcher when you can't be hunted there and then).
    /// Also plays the drone: while staring, before a night hunt (HufsaDread) and all through a hunt.
    /// </summary>
    internal class HufsaStare : MonoBehaviour
    {
        internal static HufsaStare Instance;

        /// <summary>The meter, in seconds of full-speed staring.</summary>
        public float Meter { get; private set; }
        public float Rate { get; private set; }
        public bool Busy { get; private set; }
        /// <summary>The Watcher being looked at (or last looked at).</summary>
        public HufsaEntity Target { get; private set; }

        /// <summary>The Watcher whose eyes are flaring before it comes for you.</summary>
        internal static HufsaEntity WindingUp;
        private float windUpEnds = -1f;
        private Vector3 windUpPos;

        private AudioSource drone;
        private float droneVolume;
        private float breathTimer = 4f;

        private void Awake() => Instance = this;

        /// <summary>0..1: how close the meter is to the limit of the Watcher being looked at.</summary>
        public float Intensity => Target ? Mathf.Clamp01(Meter / Mathf.Max(0.1f, Target.StareLimit)) : 0f;

        /// <summary>How bright this Watcher's eyes are for the local player (1 = as made).</summary>
        public static float EyeGlow(HufsaEntity e)
        {
            if (!Instance || !e) return 1f;
            if (e == WindingUp) return 4f;
            if (e != Instance.Target) return 1f;
            return 1f + Mathf.Max(0f, HufsaConfig.EyeBrighten.Value) * Instance.Intensity;
        }

        private void Update()
        {
            Player me = Player.m_localPlayer;
            UpdateDrone(me);
            if (!me || me.IsDead())
            {
                Meter = 0f;
                Target = null;
                return;
            }
            if (windUpEnds > 0f)
            {
                if (Time.time >= windUpEnds) EndWindUp(me);
                return;
            }

            Busy = IsBusy(me);
            HufsaEntity best = null;
            float rate = 0f;
            foreach (HufsaEntity e in HufsaEntity.Instances)
            {
                if (e.IsHunter || e.Vanishing || e.LookRate <= rate) continue;
                rate = e.LookRate;
                best = e;
            }
            if (Busy) rate = 0f;
            Rate = rate;
            if (best) Target = best;
            else if (Target && (Target.Vanishing || !Target.isActiveAndEnabled)) Target = null;

            float full = Mathf.Max(1f, HufsaConfig.StareLimitMax.Value);
            if (rate > 0f) Meter += rate * Time.deltaTime;
            else Meter = Mathf.Max(0f, Meter - Time.deltaTime * full / Mathf.Max(1f, HufsaConfig.StareDrainSeconds.Value));

            if (!best || rate <= 0f) return;
            if (!best.Noticed && Meter >= HufsaConfig.StareNoticeSeconds.Value) best.MarkNoticed();
            if (best.Noticed && Meter >= best.StareLimit) LimitReached(me, best);

            // Breathing: closer together and louder the nearer the limit.
            float k = Intensity;
            if (k < 0.35f) return;
            breathTimer -= Time.deltaTime;
            if (breathTimer > 0f) return;
            HufsaSoundBank.PlayFrom(best.transform.position, HufsaSoundBank.Pick(HufsaSoundBank.Breaths),
                HufsaConfig.BreathVolume.Value * Mathf.Lerp(0.35f, 0.8f, k), Random.Range(0.94f, 1.04f));
            breathTimer = Mathf.Lerp(6f, 2.5f, k) * Random.Range(0.85f, 1.15f);
        }

        /// <summary>Fighting, building, swinging, or in a menu: you don't really take it in.</summary>
        private static bool IsBusy(Player me) =>
            HufsaEntity.LocalInCombat(me) || me.InPlaceMode() || me.InAttack() ||
            InventoryGui.IsVisible() || Minimap.IsOpen() || Menu.IsVisible() || StoreGui.IsVisible() ||
            TextInput.IsVisible() || (Console.instance && Console.IsVisible());

        private void LimitReached(Player me, HufsaEntity w)
        {
            Meter = 0f;
            w.OverStared = true;
            bool timeOk = HufsaEntity.IsHufsaTime(w.transform.position) || HufsaDebug.TestMode;
            bool canHunt = !w.HarmlessLesson && HufsaConfig.HunterEnabled.Value && timeOk &&
                           HufsaHuntState.IsHuntable(me) && !HufsaEntity.AnyHunterLoaded();
            if (!canHunt)
            {
                HufsaPlugin.Log.LogInfo(w.HarmlessLesson ? "Stared at the first Watcher too long: it slips away" :
                    "Stared at a Watcher too long, but it can't hunt you here and now: it slips away");
                w.SlipAway();
                return;
            }
            // Its eyes flare, the sting, and a few seconds later it comes.
            WindingUp = w;
            windUpPos = w.transform.position;
            windUpEnds = Time.time + Random.Range(HufsaConfig.WindUpMin.Value, Mathf.Max(HufsaConfig.WindUpMin.Value, HufsaConfig.WindUpMax.Value));
            HufsaSoundBank.PlayFrom(w.transform.position, HufsaSoundBank.Pick(HufsaSoundBank.Stings), HufsaConfig.StingVolume.Value);
            HufsaPlugin.Log.LogInfo("Stared at a Watcher too long: it is coming");
        }

        private void EndWindUp(Player me)
        {
            HufsaEntity w = WindingUp;
            WindingUp = null;
            windUpEnds = -1f;
            HufsaEntity.BecomeHunter(w, w ? w.transform.position : windUpPos, me);
        }

        // ---------------------------------------------------------------- the drone

        private void UpdateDrone(Player me)
        {
            float target = 0f;
            if (me && !me.IsDead())
            {
                if (Target && !Target.Vanishing && Meter > 0f) target = Mathf.Pow(Intensity, 1.5f);
                if (WindingUp) target = 1f;
                if (HufsaDread.Instance && HufsaDread.Instance.Warning) target = Mathf.Max(target, HufsaDread.Instance.Warning01);
                if (HuntingNearby(me)) target = 1f;
            }
            float fade = target > droneVolume ? 3f : 5f;
            droneVolume = Mathf.MoveTowards(droneVolume, target, Time.deltaTime / fade);

            if (!drone)
            {
                if (droneVolume <= 0f) return;
                AudioClip clip = HufsaSoundBank.Pick(HufsaSoundBank.Drones);
                if (!clip) return;
                drone = gameObject.AddComponent<AudioSource>();
                drone.clip = clip;
                drone.loop = true;
                drone.playOnAwake = false;
                drone.spatialBlend = 0f;
                if (AudioMan.instance) drone.outputAudioMixerGroup = AudioMan.instance.m_ambientMixer;
            }
            drone.volume = droneVolume * Mathf.Max(0f, HufsaConfig.DroneVolume.Value) * (HufsaConfig.SoundsEnabled.Value ? 1f : 0f);
            if (droneVolume > 0.001f && !drone.isPlaying) drone.Play();
            else if (droneVolume <= 0.001f && drone.isPlaying) drone.Stop();
        }

        private static bool HuntingNearby(Player me)
        {
            float range = HufsaConfig.EscapeDistance.Value;
            foreach (HufsaEntity g in HufsaEntity.Instances)
                if (g.IsHunter && !g.Vanishing && (g.transform.position - me.transform.position).sqrMagnitude < range * range)
                    return true;
            return false;
        }
    }
}
