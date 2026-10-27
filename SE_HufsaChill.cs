using UnityEngine;

namespace Hufsa
{
    /// <summary>
    /// Applied to the local player by HufsaAuraManager and refreshed every tick.
    /// RegenFactor (1 at the edge of the cold, 0 from 20 m) scales stamina regen, and at 0 stamina can't rise at
    /// all; DrainStrength (0-1, within 10 m) drains stamina. Stamina meads lift the regen block while they work
    /// (the drain stays), so drinking one is how you run from it. Slowing is done in Patches (Player speed factors)
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
            if (!meadActive) staminaRegen *= Mathf.Clamp01(RegenFactor);
        }

        // A stamina mead is the way to outrun it: while one works, the cold doesn't stop stamina coming back.
        private bool meadActive;
        private float meadCheck;

        private bool StaminaMeadActive()
        {
            foreach (StatusEffect se in m_character.GetSEMan().GetStatusEffects())
            {
                if (se == this || !(se is SE_Stats s) || se is SE_Rested) continue;
                if (s.m_staminaUpFront > 0f || s.m_staminaOverTime > 0f || s.m_staminaRegenMultiplier > 1f) return true;
            }
            return false;
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            if (!(m_character is Player player) || !player.IsOwner()) return;

            meadCheck -= dt;
            if (meadCheck <= 0f)
            {
                meadActive = StaminaMeadActive();
                meadCheck = 0.25f;
            }

            // Inside the stop radius stamina must not come back at all, unless a stamina mead is working. The
            // mead's first gulp lands before this notices it, so stamina gained on the frame it starts is kept too.
            if (!meadActive && RegenFactor <= 0.001f && lastStamina >= 0f && player.m_stamina > lastStamina)
            {
                meadActive = StaminaMeadActive();
                if (!meadActive) player.m_stamina = lastStamina;
            }

            if (DrainStrength > 0f)
                player.UseStamina(HufsaConfig.StaminaDrain.Value * DrainStrength * ApproachMultiplier * dt);
            lastStamina = player.m_stamina;
        }
    }
}
