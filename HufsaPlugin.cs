using BepInEx;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace Hufsa
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    [SynchronizationMode(AdminOnlyStrictness.IfOnServer)]
    internal class HufsaPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "benjinamin.hufsa";
        public const string PluginName = "Hufsa";
        public const string PluginVersion = "0.1.0";

        internal static BepInEx.Logging.ManualLogSource Log;

        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;
            HufsaConfig.Bind(Config);
            HufsaPrefabs.Register();

            harmony = new Harmony(PluginGUID);
            harmony.PatchAll(typeof(HufsaPlugin).Assembly);

            gameObject.AddComponent<HufsaSpawner>();
            gameObject.AddComponent<HufsaAuraManager>();
            gameObject.AddComponent<FrostVisuals>();
            gameObject.AddComponent<FrostDecals>();
            gameObject.AddComponent<HufsaHuntState>();
            gameObject.AddComponent<HufsaDread>();
            gameObject.AddComponent<HufsaAudio>();

            CommandManager.Instance.AddConsoleCommand(new HufsaCommand());
            Log.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        private void OnDestroy() => harmony?.UnpatchSelf();
    }
}
