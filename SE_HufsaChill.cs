using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Applied to the local player by HufsaAuraManager and refreshed every tick.
    /// RegenFactor (1 at the edge of the cold, 0 from 20 m) scales stamina regen, and at 0 stamina can't rise at
    /// all; DrainStrength (0-1, within 10 m) drains stamina. Slowing is done in Patches (Player speed factors)
    /// from HufsaAuraManager.LocalSpeedFactor. It never hurts you: being caught is what kills.
    /// </summary>
    internal class SE_HufsaChill : SE_Stats
    {
        public const string SEName = "SE_HufsaChill";
        public static readonly int Hash = SEName.GetStableHashCode();

        public float ColdStrength;
        public float RegenFactor = 1f;
        public float DrainStrength;
        public float ApproachMultiplier = 1f;

        private float lastStamina = -1f;

        public override void ModifyStaminaRegen(ref float staminaRegen)
        {
            base.ModifyStaminaRegen(ref staminaRegen);
            staminaRegen *= Mathf.Clamp01(RegenFactor);
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (!(m_character is Player player) || !player.IsOwner()) return;

            // Inside the stop radius stamina must not come back at all, whatever else tries to restore it.
            if (RegenFactor <= 0.001f && lastStamina >= 0f && player.m_stamina > lastStamina)
                player.m_stamina = lastStamina;

            if (DrainStrength > 0f)
                player.UseStamina(HufsaConfig.StaminaDrain.Value * DrainStrength * ApproachMultiplier * dt);
            lastStamina = player.m_stamina;
        }
    }
}
