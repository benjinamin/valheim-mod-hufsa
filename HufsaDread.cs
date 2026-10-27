using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Client side: the local player's "dread", the number of times they have really spotted a Watcher.
    /// Saved with the character. Each sighting shows a quiet line of text that grows more ominous; once it
    /// reaches SightingsForHunt the server starts a hunt for this player on a night when they can be hunted.
    /// It drops by one for every DreadDecayDays in-game days without a sighting, and goes back to zero when
    /// the hunt comes.
    /// </summary>
    internal class HufsaDread : MonoBehaviour
    {
        public const string RpcDread = "Hufsa_Dread";
        public const string RpcDreadReset = "Hufsa_DreadReset";
        private const string CountKey = "hufsa_dread";
        private const string DayKey = "hufsa_dread_day";

        internal static HufsaDread Instance;

        public int Count { get; private set; }

        private Player loadedFor;
        private int lastDay;
        private int lastReported = -1;
        private float reportTimer;

        private void Awake() => Instance = this;

        internal static void RegisterRpcs()
        {
            ZRoutedRpc.instance.Register(RpcDreadReset, sender => Instance?.ResetAfterHunt());
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
            Decay();

            reportTimer -= Time.deltaTime;
            if (Count != lastReported || reportTimer <= 0f) Report();
        }

        private void Load(Player p)
        {
            loadedFor = p;
            // Characters saved before the rename still carry the old "groke_" keys: move them over.
            foreach (string key in new[] { CountKey, DayKey })
            {
                string old = key.Replace("hufsa", "groke");
                if (!p.m_customData.ContainsKey(key) && p.m_customData.TryGetValue(old, out string v)) p.m_customData[key] = v;
                p.m_customData.Remove(old);
            }
            Count = p.m_customData.TryGetValue(CountKey, out string c) && int.TryParse(c, out int n) ? n : 0;
            lastDay = p.m_customData.TryGetValue(DayKey, out string d) && int.TryParse(d, out int day) ? day : Today();
            lastReported = -1;
        }

        private void Save()
        {
            if (!loadedFor) return;
            loadedFor.m_customData[CountKey] = Count.ToString();
            loadedFor.m_customData[DayKey] = lastDay.ToString();
        }

        private static int Today() => EnvMan.instance ? EnvMan.instance.GetDay() : 0;

        /// <summary>It lets go slowly: one sighting forgotten per DreadDecayDays quiet days.</summary>
        private void Decay()
        {
            if (Count <= 0 || !EnvMan.instance) return;
            int days = Mathf.Max(1, HufsaConfig.DreadDecayDays.Value);
            if (Today() - lastDay < days) return;
            Count--;
            lastDay = Today();
            Save();
        }

        /// <summary>A Watcher was really seen: in the middle of the view, nothing in the way, for a moment.</summary>
        public void AddSighting()
        {
            Count++;
            lastDay = Today();
            Save();
            HufsaPlugin.Log.LogInfo($"Hufsa sighted ({Count}/{HufsaConfig.SightingsForHunt.Value})");
            ShowMessage();
        }

        /// <summary>Console testing: set the count directly.</summary>
        public void SetCount(int n)
        {
            Count = Mathf.Max(0, n);
            lastDay = Today();
            Save();
        }

        public void ResetAfterHunt()
        {
            Count = 0;
            lastDay = Today();
            Save();
        }

        private void Report()
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.instance.GetServerPeerID(), RpcDread, Count);
            lastReported = Count;
            reportTimer = 60f;
        }

        private void ShowMessage()
        {
            if (!HufsaConfig.DreadMessages.Value || !MessageHud.instance) return;
            int need = Mathf.Max(1, HufsaConfig.SightingsForHunt.Value);
            string text = Count >= need ? "It knows where you are."
                : Count == need - 1 ? "It is getting closer."
                : Count == 1 ? "You feel watched."
                : "Something is watching you.";
            MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, $"<color=#B9A6D9>{text}</color>");
        }
    }
}
