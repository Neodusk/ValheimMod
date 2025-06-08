using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
using Mono.Security.Interface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// This file contains special abilities for the Valheim mod

namespace valheimmod
{
    internal partial class valheimmod : BaseUnityPlugin
    {
        /// <summary>
        /// Used to start the Teleport countdown coroutine. Must be in the main valheimmod class
        /// <param name="seconds"></param>
        public void StartTeleportCountdown(int seconds)
        {
            if (teleportCountdownCoroutine != null)
            {
                StopCoroutine(teleportCountdownCoroutine);
            }
            teleportCountdownCoroutine = StartCoroutine(SpecialTeleport.TeleportCountdownCoroutine(seconds));
        }

        /// <summary>
        /// Contains methods for calling special abilities
        /// </summary>
        public class ModAbilities
        {
            public static void CallPendingAbilities(valheimmod Instance)
            {
                SpecialJump.CallPending();
                SpecialTeleport.CallPending(Instance);
            }
            public static void CallSpecialAbilities()
            {
                SpecialJump.Call();

            }
        }

        public class SpecialJump
        {
            public static bool CallPending()
            {
                // If user picks the superjump buff in radial, give them the buff
                RadialAbility radial_ability = GetRadialAbility();
                string ability_name = radial_ability.ToString();
                if (ability_name == RadialAbility.SuperJump.ToString())
                {

                    if (!Player.m_localPlayer.m_seman.HaveStatusEffect(JumpSpecialEffect.StatusEffect.m_nameHash))
                    {
                        Jotunn.Logger.LogInfo("Adding JumpPendingSpecialEffect status effect");
                        Player.m_localPlayer.m_seman.AddStatusEffect(valheimmod.JumpPendingSpecialEffect.StatusEffect, true);
                    }
                    return ZInput.GetButton(ModInput.SpecialRadialButton.Name);
                }
                return false;
            }
            public static void Call()
            {
                // if the player presses the jump button when they have the jump pending buff, give super jump effect

                if (((ZInput.GetButton("Jump") || ZInput.GetButton("JoyJump")) && Player.m_localPlayer.m_seman.HaveStatusEffect(JumpPendingSpecialEffect.StatusEffect.m_nameHash)))
                {
                    Jotunn.Logger.LogInfo("Special jump button is pressed down");
                    Jotunn.Logger.LogInfo($"JumpPendingSpecialEffect StatusEffect Duration: {valheimmod.JumpPendingSpecialEffect.StatusEffect.GetDuration()}");
                    Jotunn.Logger.LogInfo($"JumpPendingSpecialEffect StatusEffect IsDone: {valheimmod.JumpPendingSpecialEffect.StatusEffect.IsDone()}");
                    if (Player.m_localPlayer.m_seman.HaveStatusEffect(JumpPendingSpecialEffect.StatusEffect.m_nameHash) && !Player.m_localPlayer.m_seman.HaveStatusEffect(JumpSpecialEffect.StatusEffect.m_nameHash))
                    {
                        Jotunn.Logger.LogInfo("Removing JumpPendingSpecialEffect status effect and adding JumpSpecialEffect status effect");
                        Player.m_localPlayer.m_seman.RemoveStatusEffect(JumpPendingSpecialEffect.StatusEffect.m_nameHash, false);
                        Player.m_localPlayer.m_seman.AddStatusEffect(valheimmod.JumpSpecialEffect.StatusEffect, false);
                        SpecialJumpTriggered = true;
                        Jotunn.Logger.LogInfo($"SpecialJumpTriggered1 = {SpecialJumpTriggered}");
                    }

                }
            }
            /// <summary>
            /// Sets up the sfx and vfx for the special jump ability.
            /// </summary>
            private static List<EffectList.EffectData> SetupEffectList()
            {
                GameObject frostPrefab = ZNetScene.instance.GetPrefab("vfx_Frost");
                GameObject leafPuffPrefab = ZNetScene.instance.GetPrefab("vfx_bush_leaf_puff");
                GameObject leafPuffHeathPrefab = ZNetScene.instance.GetPrefab("vfx_bush_leaf_puff_heath");
                GameObject iceShardPrefab = ZNetScene.instance.GetPrefab("fx_iceshard_hit");
                GameObject ghostDeathPrefab = ZNetScene.instance.GetPrefab("vfx_ghost_death");
                GameObject soundPrefab = ZNetScene.instance.GetPrefab("sfx_Abomination_Attack2_slam_whoosh");
                var effectList = new List<EffectList.EffectData>();

                if (frostPrefab != null)
                {
                    effectList.Add(new EffectList.EffectData
                    {
                        m_prefab = frostPrefab,
                        m_enabled = true,
                        m_attach = true
                    });
                }
                if (leafPuffPrefab != null)
                {
                    effectList.Add(new EffectList.EffectData
                    {
                        m_prefab = leafPuffPrefab,
                        m_enabled = true,
                        m_attach = true,
                        m_follow = true,
                    });
                }
                if (leafPuffHeathPrefab != null)
                {
                    effectList.Add(new EffectList.EffectData
                    {
                        m_prefab = leafPuffHeathPrefab,
                        m_enabled = true,
                        m_attach = true,
                        m_follow = true,
                    });
                }
                if (ghostDeathPrefab != null)
                {
                    effectList.Add(new EffectList.EffectData
                    {
                        m_prefab = ghostDeathPrefab,
                        m_enabled = true,
                        m_attach = true,
                        m_follow = true,
                    });
                }
                if (iceShardPrefab != null)
                {
                    effectList.Add(new EffectList.EffectData
                    {
                        m_prefab = iceShardPrefab,
                        m_enabled = true,
                        m_attach = true,
                        m_follow = true,
                    });
                }

                if (soundPrefab != null)
                {
                    effectList.Add(new EffectList.EffectData
                    {
                        m_prefab = soundPrefab,
                        m_enabled = true,
                        m_attach = false,
                        m_follow = false,
                    });
                }
                return effectList;
            }

            public static void AddEffects()
            {
                StatusEffect effect = ScriptableObject.CreateInstance<StatusEffect>();
                StatusEffect pendeffect = ScriptableObject.CreateInstance<StatusEffect>();
                effect.name = "SpecialJumpEffect";
                effect.m_name = "$special_jumpeffect";
                effect.m_tooltip = "$special_jumpeffect_tooltip";
                effect.m_icon = Sprite.Create(SpecialJumpTexture, new Rect(0, 0, SpecialJumpTexture.width, SpecialJumpTexture.height), new Vector2(0.5f, 0.5f));
                effect.m_startMessageType = MessageHud.MessageType.Center;
                //effect.m_startMessage = "$special_jumpeffect_start";
                effect.m_stopMessageType = MessageHud.MessageType.Center;
                effect.m_ttl = 10f;
                effect.m_cooldownIcon = effect.m_icon;
                JumpSpecialEffect = new CustomStatusEffect(effect, fixReference: false);

                pendeffect.name = "PendingSpecialJumpEffect";
                pendeffect.m_name = "$pending_special_jumpeffect";
                pendeffect.m_tooltip = "$special_jumpeffect_tooltip";
                pendeffect.m_icon = Sprite.Create(SpecialJumpTexture, new Rect(0, 0, SpecialJumpTexture.width, SpecialJumpTexture.height), new Vector2(0.5f, 0.5f));
                pendeffect.m_startMessageType = MessageHud.MessageType.Center;
                pendeffect.m_startMessage = "$pending_special_jumpeffect_start";
                pendeffect.m_stopMessageType = MessageHud.MessageType.Center;
                pendeffect.m_stopMessage = "$pending_special_jumpeffect_stop";
                pendeffect.m_ttl = 0f; // No TTL for pending effect

                var effectList = SetupEffectList();
                pendeffect.m_startEffects = new EffectList
                {
                    m_effectPrefabs = effectList.ToArray()
                };
                PrefabManager.OnPrefabsRegistered -= ModAbilitiesEffects.AddStatusEffects;
                JumpPendingSpecialEffect = new CustomStatusEffect(pendeffect, fixReference: false);
            }
        }
        /// <summary>
        /// Handles the special teleport ability, including adding effects and managing the teleport countdown.
        /// </summary>
        public class SpecialTeleport
        {
            public static bool CallPending(valheimmod Instance)
            {
                // If user picks the teleport home ability in radial, teleport them home
                RadialAbility radial_ability = GetRadialAbility();
                string ability_name = radial_ability.ToString();
                if (ability_name == RadialAbility.TeleportHome.ToString())
                {
                    if (Player.m_localPlayer.m_seman.HaveStatusEffect(TeleportHomeEffect.StatusEffect.m_nameHash))
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$teleporteffect_cd");
                        return false;
                    }
                    if (!Player.m_localPlayer.m_seman.HaveStatusEffect(PendingTeleportHomeEffect.StatusEffect.m_nameHash))
                    {
                        valheimmod.Instance.teleportCancelled = false;
                        valheimmod.Instance.teleportPending = true;
                        Jotunn.Logger.LogInfo("Adding TeleportHomeSpecialEffect status effect");
                        Player.m_localPlayer.m_seman.AddStatusEffect(valheimmod.PendingTeleportHomeEffect.StatusEffect, true);
                        Instance.StartTeleportCountdown(10);

                    }
                    else
                    {
                        Cancel();
                    }
                    return ZInput.GetButton(ModInput.SpecialRadialButton.Name);
                }
                return false;
            }

            public static void Cancel()
            {
                if (valheimmod.Instance.teleportCountdownCoroutine != null)
                {
                    valheimmod.Instance.StopCoroutine(valheimmod.Instance.teleportCountdownCoroutine);
                    valheimmod.Instance.teleportCountdownCoroutine = null;
                    if (Player.m_localPlayer != null)
                    {
                        valheimmod.Instance.teleportCancelled = true;
                        valheimmod.Instance.teleportPending = false;
                        // Remove the pending teleport status effect if you want:
                        if (Player.m_localPlayer != null)
                        {
                            Player.m_localPlayer.m_seman.RemoveStatusEffect(PendingTeleportHomeEffect.StatusEffect.m_nameHash, false);
                        }
                    }
                }
            }
            public static void AddEffects()
            {
                StatusEffect pendteleporteffect = ScriptableObject.CreateInstance<StatusEffect>();
                StatusEffect teleporteffect = ScriptableObject.CreateInstance<StatusEffect>();
                pendteleporteffect.name = "PendingTeleportEffect";
                pendteleporteffect.m_name = "$pending_teleport_effect";
                pendteleporteffect.m_tooltip = "$special_teleport_tooltip";
                pendteleporteffect.m_icon = Sprite.Create(TeleportTexture, new Rect(0, 0, TeleportTexture.width, TeleportTexture.height), new Vector2(0.5f, 0.5f));
                pendteleporteffect.m_startMessageType = MessageHud.MessageType.Center;
                pendteleporteffect.m_startMessage = "$pending_teleporteffect_start";
                pendteleporteffect.m_stopMessageType = MessageHud.MessageType.Center;
                pendteleporteffect.m_ttl = 0f; // No TTL for pending effect
                teleporteffect.name = "TeleportEffect";
                teleporteffect.m_name = "$teleport_effect";
                teleporteffect.m_tooltip = "$special_teleport_cd_tooltip";
                teleporteffect.m_icon = Sprite.Create(TeleportTexture, new Rect(0, 0, TeleportTexture.width, TeleportTexture.height), new Vector2(0.5f, 0.5f));
                teleporteffect.m_startMessageType = MessageHud.MessageType.Center;
                teleporteffect.m_startMessage = "$teleporteffect_start";
                teleporteffect.m_stopMessageType = MessageHud.MessageType.Center;
                teleporteffect.m_stopMessage = "$teleporteffect_stop";
                teleporteffect.m_ttl = 0f;
                teleporteffect.m_cooldownIcon = teleporteffect.m_icon;

                PendingTeleportHomeEffect = new CustomStatusEffect(pendteleporteffect, fixReference: false);
                TeleportHomeEffect = new CustomStatusEffect(teleporteffect, fixReference: false);
            }

            public static System.Collections.IEnumerator TeleportCountdownCoroutine(int seconds)
            {
                for (int i = seconds; i > 0; i--)
                {
                    if (Player.m_localPlayer != null)
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Teleporting home in {i}...");
                    }
                    yield return new WaitForSeconds(1f);

                    // Optional: If teleport was cancelled during countdown, exit early
                    if (valheimmod.Instance.teleportCancelled || !valheimmod.Instance.teleportPending)
                    {
                    valheimmod.Instance.teleportCountdownCoroutine = null;
                        yield break;
                    }
                }
                valheimmod.Instance.teleportCountdownCoroutine = null;

                // Only run this if teleport wasn't cancelled
                if (!valheimmod.Instance.teleportCancelled && valheimmod.Instance.teleportPending)
                {
                    // Place your post-countdown logic here
                    Player.m_localPlayer.m_seman.AddStatusEffect(TeleportHomeEffect.StatusEffect, true);
                    PlayerProfile profile = Game.instance.GetPlayerProfile();
                    Vector3 homepoint = profile.GetCustomSpawnPoint(); // Get the player's home point
                    if (homepoint == Vector3.zero)
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You don't have a bed. Teleporting to Sacrificial Stones");
                        homepoint = profile.GetHomePoint(); // Fallback to the default home point
                    }
                    Player.m_localPlayer.TeleportTo(homepoint, Quaternion.identity, true); // TelepoSetCustomSpawnPointrt the player to their home point
                    Player.m_localPlayer.m_seman.RemoveStatusEffect(PendingTeleportHomeEffect.StatusEffect.m_nameHash, false); // Remove the pending teleport effect
                    valheimmod.Instance.teleportPending = false;
                }
            }
        }

        public class SpectralArrow
        {
            public static Dictionary<Player, int> ShotsFired = new Dictionary<Player, int>();
            public static Dictionary<Player, float> PreviousSkill = new Dictionary<Player, float>();
            public static void Call()
            {
                if (Player.m_localPlayer != null && Player.m_localPlayer.m_seman.HaveStatusEffect(PendingSpectralArrowEffect.StatusEffect.m_nameHash))
                {
                    if (Player.m_localPlayer.m_seman.HaveStatusEffect(PendingSpectralArrowEffect.StatusEffect.m_nameHash) && !Player.m_localPlayer.m_seman.HaveStatusEffect(SpectralArrowEffect.StatusEffect.m_nameHash))
                    {
                        if (ZInput.GetButtonDown("Attack") || ZInput.GetButtonDown("JoyAttack"))
                        {
                            Jotunn.Logger.LogInfo("Special spectral arrow button is pressed down");
                            Jotunn.Logger.LogInfo("Removing PendingSpectralArrowEffect status effect and adding SpectralArrowEffect status effect");
                            Player.m_localPlayer.m_seman.RemoveStatusEffect(PendingSpectralArrowEffect.StatusEffect.m_nameHash, false);
                            Player.m_localPlayer.m_seman.AddStatusEffect(valheimmod.SpectralArrowEffect.StatusEffect, false);
                        }
                    }

                }
            }
            public static bool CallPending()
            {
                return false;
            }
            public static void Cancel()
            {
                RadialAbility radial_ability = GetRadialAbility();
                string ability_name = radial_ability.ToString();
                // player cancelled the spectral arrow ability by clicking the radial button again
                if (RadialMenuIsOpen && radial_ability == RadialAbility.SpectralArrow)
                {
                    if (Player.m_localPlayer != null)
                    {
                        if (Player.m_localPlayer.m_seman.HaveStatusEffect(PendingSpectralArrowEffect.StatusEffect.m_nameHash))
                        {
                            Player.m_localPlayer.m_seman.RemoveStatusEffect(PendingSpectralArrowEffect.StatusEffect.m_nameHash, false);
                        }

                    }
                }
            }
            public static void AddEffects()
            {
                StatusEffect effect = ScriptableObject.CreateInstance<StatusEffect>();
                StatusEffect pendeffect = ScriptableObject.CreateInstance<StatusEffect>();
                pendeffect.name = "PendingSpectralArrowEffect";
                pendeffect.m_name = "$pending_spectralarrow_effect";
                pendeffect.m_tooltip = "$special_spectralarrow_tooltip";
                pendeffect.m_icon = Sprite.Create(SpectralArrowTexture, new Rect(0, 0, SpectralArrowTexture.width, SpectralArrowTexture.height), new Vector2(0.5f, 0.5f));
                pendeffect.m_startMessageType = MessageHud.MessageType.Center;
                pendeffect.m_startMessage = "$pending_spectralarrow_start";
                pendeffect.m_stopMessageType = MessageHud.MessageType.Center;
                pendeffect.m_ttl = 0f; // No TTL for pending effect
                effect.name = "SpectralArrowEffect";
                effect.m_name = "$spectral_arrow_effect";
                effect.m_tooltip = "$special_arrow_cd_tooltip";
                effect.m_icon = Sprite.Create(SpectralArrowTexture, new Rect(0, 0, SpectralArrowTexture.width, SpectralArrowTexture.height), new Vector2(0.5f, 0.5f));
                effect.m_startMessageType = MessageHud.MessageType.Center;
                effect.m_startMessage = "$spectrl_arrow_start";
                effect.m_stopMessageType = MessageHud.MessageType.Center;
                effect.m_stopMessage = "$spectrl_arrow_stop";
                effect.m_ttl = 0f;
                effect.m_cooldownIcon = effect.m_icon;

                PendingSpectralArrowEffect = new CustomStatusEffect(pendeffect, fixReference: false);
                SpectralArrowEffect = new CustomStatusEffect(effect, fixReference: false);
            }
        }
        public class ModAbilitiesEffects
        {

            /// <summary>
            /// Adds status effects for special abilities.
            /// </summary>
            public static void AddStatusEffects()
            {
                SpecialJump.AddEffects();
                SpecialTeleport.AddEffects();
            }
        }
    }
}
