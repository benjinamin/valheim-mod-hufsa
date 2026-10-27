using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Each client publishes its own player's hunt state on the player's ZDO, so the Hunter's owner and the
    /// server can see it for every player:
    /// - refuge: indoors (sheltered), in bed, or in a dungeon. Hufsa waits outside, then gives up.
    /// - unreachable: on a boat, swimming, at sea, or in the Ashlands. Hufsa won't follow, and gives up.
    /// </summary>
    internal class HufsaHuntState : MonoBehaviour
    {
        public const string RefugeKey = "hufsa_refuge";
        public const string UnreachableKey = "hufsa_unreachable";

        private float timer;

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < 0.5f) return;
            timer = 0f;

            Player p = Player.m_localPlayer;
            if (!p || !p.m_nview || !p.m_nview.IsValid()) return;
            ZDO zdo = p.m_nview.GetZDO();
            bool refuge = IsRefuge(p), unreachable = IsUnreachable(p);
            if (zdo.GetBool(RefugeKey, false) != refuge) zdo.Set(RefugeKey, refuge);
            if (zdo.GetBool(UnreachableKey, false) != unreachable) zdo.Set(UnreachableKey, unreachable);
        }

        private static bool IsRefuge(Player p) =>
            p.InShelter() || p.InInterior() || p.InBed() || p.transform.position.y > 3000f;

        private static bool IsUnreachable(Player p)
        {
            if (p.IsAttachedToShip() || p.GetStandingOnShip() || p.IsSwimming()) return true;
            Heightmap.Biome b = HufsaEntity.BiomeAt(p.transform.position);
            return b == Heightmap.Biome.AshLands || b == Heightmap.Biome.Ocean;
        }

        public static bool IsHuntable(ZDO zdo) =>
            zdo != null && !zdo.GetBool(RefugeKey, false) && !zdo.GetBool(UnreachableKey, false);

        public static bool IsHuntable(Player p) =>
            p && !p.IsDead() && p.m_nview && p.m_nview.IsValid() && IsHuntable(p.m_nview.GetZDO());
    }
}
