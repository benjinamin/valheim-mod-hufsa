using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Client side: the hidden stare meter, and what it leads to.
    /// - It fills while you look at a Watcher: at full speed with its face near the middle of your view
    ///   (StareConeDegrees), slowly with it elsewhere on screen (StareOffCentreRate), not at all while you are busy.
    ///   Looking away it drains (StareDrainSeconds). Nothing shows it: you hear and see it instead, as steady
    ///   breathing rising (the breathloop file) and the Watcher's eyes brightening.
    /// - NoticeSeconds of it and you have noticed that Watcher (HufsaDread).
    /// - Past that Watcher's own random limit it comes for you: its eyes flare, the sting plays, and a few seconds
    ///   later it is the Hunter. Except the very first Watcher you notice, which slips away instead (and so does
    ///   any Watcher when you can't be hunted there and then).
    /// Also plays the drones: from a Watcher (louder closer and the more of it you see), and everywhere before a
    /// night hunt (HufsaDread), as one comes for you, and all through a hunt.
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
        private AudioSource breathLoop;
        private float breathLevel;

        /// <summary>The breathing loop is clearly audible: the Watchers' own breath bursts hold off.</summary>
        public bool BreathingLoud => breathLevel > 0.15f;

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
                if (e.IsHunter || e.Vanishing || e.IsDebug || e.LookRate <= rate) continue;
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
            // Noticed before the limit, however short the limit is.
            if (!best.Noticed && Meter >= Mathf.Min(HufsaConfig.StareNoticeSeconds.Value, best.StareLimit * 0.6f)) best.MarkNoticed();
            if (best.Noticed && Meter >= best.StareLimit) LimitReached(me, best);
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
            bool timeOk = HufsaEntity.IsHufsaTime(w.transform.position);
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

        private AudioSource watcherDrone;
        private float watcherDroneVolume;
        private HufsaEntity droneOn;

        /// <summary>
        /// Two drones. One comes from the Watcher: louder the closer it is, and the more of it you can see. The other
        /// is everywhere: before a night hunt (HufsaDread), as one comes for you, and all through a hunt.
        /// </summary>
        private void UpdateDrone(Player me)
        {
            float target = 0f;
            if (me && !me.IsDead())
            {
                if (WindingUp) target = 1f;
                if (HufsaDread.Instance && HufsaDread.Instance.Warning) target = Mathf.Max(target, HufsaDread.Instance.Warning01);
                if (HuntingNearby(me)) target = 1f;
            }
            droneVolume = Mathf.MoveTowards(droneVolume, target, Time.deltaTime / (target > droneVolume ? 3f : 5f));
            Drive(ref drone, droneVolume, null, HufsaSoundBank.Drones, HufsaConfig.DroneVolume.Value);

            // The Watcher's own: the one that would sound loudest.
            float best = 0f;
            HufsaEntity on = null;
            if (me && !me.IsDead() && !WindingUp)
            {
                foreach (HufsaEntity e in HufsaEntity.Instances)
                {
                    if (e.IsHunter || e.Vanishing) continue;
                    float v = WatcherDroneLevel(me, e);
                    if (v > best)
                    {
                        best = v;
                        on = e;
                    }
                }
            }
            if (on) droneOn = on;
            watcherDroneVolume = Mathf.MoveTowards(watcherDroneVolume, best, Time.deltaTime / (best > watcherDroneVolume ? 1.5f : 3f));
            Drive(ref watcherDrone, watcherDroneVolume, droneOn, HufsaSoundBank.Drones, HufsaConfig.DroneVolume.Value);

            // Steady breathing from the Watcher you stare at: it rises with the stare meter and dies away as it
            // drains; full while its eyes flare.
            float breath = 0f;
            if (me && !me.IsDead() && Target && !Target.Vanishing)
                breath = WindingUp ? 1f : Mathf.Clamp01((Intensity - 0.1f) / 0.9f);
            breathLevel = Mathf.MoveTowards(breathLevel, breath, Time.deltaTime / (breath > breathLevel ? 1f : 2.5f));
            Drive(ref breathLoop, breathLevel, Target ? Target : droneOn, HufsaSoundBank.BreathLoops, HufsaConfig.BreathLoopVolume.Value);
        }

        /// <summary>
        /// 0..1: closer is louder (from SightRange in to its vanish distance), and seeing more of it is louder (a
        /// fifth of that while it is out of sight).
        /// </summary>
        private static float WatcherDroneLevel(Player me, HufsaEntity e)
        {
            float d = Vector3.Distance(me.transform.position, e.transform.position);
            float far = Mathf.Max(60f, HufsaConfig.SightRange.Value);
            float near = HufsaEntity.WatcherVanishDistanceAt(e.transform.position);
            float close = 1f - Mathf.Clamp01((d - near) / Mathf.Max(1f, far - near));
            float seen = Mathf.Clamp01(Mathf.Max(e.SightVisible, e.SightFace));
            return close * close * Mathf.Lerp(0.2f, 1f, seen);
        }

        /// <summary>
        /// Plays the drone at this level (times DroneVolume). From a Watcher: from its direction, a few metres from
        /// you, so where it is can be heard while the loudness stays ours.
        /// </summary>
        private void Drive(ref AudioSource src, float level, HufsaEntity from, System.Collections.Generic.List<AudioClip> clips, float volume)
        {
            if (!src)
            {
                if (level <= 0f) return;
                AudioClip clip = HufsaSoundBank.Pick(clips);
                if (!clip) return;
                var go = new GameObject("HufsaLoop_" + clip.name);
                go.transform.SetParent(transform, false);
                src = go.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = true;
                src.playOnAwake = false;
                src.dopplerLevel = 0f;
                if (AudioMan.instance) src.outputAudioMixerGroup = AudioMan.instance.m_ambientMixer;
            }
            Transform listener = GameCamera.instance ? GameCamera.instance.transform : null;
            if (from && listener)
            {
                Vector3 dir = from.transform.position - listener.position;
                src.transform.position = listener.position + (dir.sqrMagnitude > 1f ? dir.normalized * 5f : Vector3.zero);
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 10f; // the listener is always inside this: full level, direction only
                src.maxDistance = 40f;
            }
            else src.spatialBlend = 0f;
            src.volume = level * Mathf.Max(0f, volume) * (HufsaConfig.SoundsEnabled.Value ? 1f : 0f);
            if (level > 0.001f && !src.isPlaying) src.Play();
            else if (level <= 0.001f && src.isPlaying) src.Stop();
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
