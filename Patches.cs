using HarmonyLib;

namespace Hufsa
{
    [HarmonyPatch]
    internal static class Patches
    {
        [HarmonyPatch(typeof(Game), nameof(Game.Start)), HarmonyPostfix]
        private static void Game_Start()
        {
            HufsaSpawner.RegisterRpcs();
            HufsaDread.RegisterRpcs();
        }

        // HunterBehaviour drives movement; keep MonsterAI's component (pathfinding) but skip its brain.
        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI)), HarmonyPrefix]
        private static bool MonsterAI_UpdateAI(MonsterAI __instance, ref bool __result)
        {
            if (!__instance.GetComponent<HunterBehaviour>()) return true;
            __result = true;
            return false;
        }

        // While the Hunter climbs it moves itself; the normal character physics (gravity, ground) would fight it.
        [HarmonyPatch(typeof(Character), nameof(Character.CustomFixedUpdate)), HarmonyPrefix]
        private static bool Character_CustomFixedUpdate(Character __instance)
        {
            var hunter = __instance.GetComponent<HunterBehaviour>();
            return !hunter || !hunter.Climbing;
        }

        // Unkillable.
        [HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage)), HarmonyPrefix]
        private static bool Character_RPC_Damage(Character __instance) => !__instance.GetComponent<HunterBehaviour>();

        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage)), HarmonyPrefix]
        private static bool Character_ApplyDamage(Character __instance) => !__instance.GetComponent<HunterBehaviour>();

        // The Hunter's cold puts fires out, the same way rain does, until it has gone again.
        [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.CheckWet)), HarmonyPostfix]
        private static void Fireplace_CheckWet(Fireplace __instance)
        {
            float extra = HufsaConfig.FireQuenchBeyondCold.Value;
            if (extra < 0f) return;
            float r = HufsaConfig.ColdRadius.Value + extra;
            if (HufsaEntity.AnyHunterWithin(__instance.transform.position, r)) __instance.m_wet = true;
        }

        // Hufsa's cold slows the local player's jog/walk and sprint, more the closer it is.
        [HarmonyPatch(typeof(Player), nameof(Player.GetJogSpeedFactor)), HarmonyPostfix]
        private static void Player_GetJogSpeedFactor(Player __instance, ref float __result)
        {
            if (__instance == Player.m_localPlayer) __result *= HufsaAuraManager.LocalSpeedFactor;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.GetRunSpeedFactor)), HarmonyPostfix]
        private static void Player_GetRunSpeedFactor(Player __instance, ref float __result)
        {
            if (__instance == Player.m_localPlayer) __result *= HufsaAuraManager.LocalSpeedFactor;
        }

        // Dying during a hunt sends it after someone else nearby (server decides).
        [HarmonyPatch(typeof(Player), nameof(Player.OnDeath)), HarmonyPostfix]
        private static void Player_OnDeath(Player __instance)
        {
            if (__instance != Player.m_localPlayer) return;
            if (HufsaEntity.AnyHunterWithin(__instance.transform.position, 300f))
                HufsaSpawner.ReportDeath(__instance.transform.position);
        }

        // No sleeping the hunt away: the bed treats Hufsa as an enemy nearby.
        [HarmonyPatch(typeof(Bed), nameof(Bed.CheckEnemies)), HarmonyPrefix]
        private static bool Bed_CheckEnemies(Player human, ref bool __result)
        {
            if (!human || !HufsaEntity.AnyHunterWithin(human.transform.position, 300f)) return true;
            human.Message(MessageHud.MessageType.Center, "<color=#E0301E>You can't sleep. Something is out there.</color>");
            __result = false;
            return false;
        }

        // No name or health bar: you shouldn't know what it is.
        [HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.ShowHud)), HarmonyPrefix]
        private static bool EnemyHud_ShowHud(Character c) => !c || !c.GetComponent<HunterBehaviour>();
    }
}
