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
                //SpectralAxe.CallPending();
            }
            public static void CallSpecialAbilities()
            {
                SpecialJump.Call();
                //SpectralAxe.Call();

            }
        }

        /// <summary>
        /// Handles the special jump ability
        /// </summary>
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
                if (radial_ability != RadialAbility.None)
                {
                    Jotunn.Logger.LogInfo($"Radial ability selected: {ability_name}");
                }
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

        public class SpectralAxe
        {
            private static GameObject heldAxeInstance;
            public static void CallPending()
            {
                // If user picks the spectral axe ability in radial, give them the spectral axe
                RadialAbility radial_ability = GetRadialAbility();
                string ability_name = radial_ability.ToString();
                if (ability_name == RadialAbility.SpectralAxe.ToString())
                {
                    if (Player.m_localPlayer == null || Player.m_localPlayer.m_seman == null ||
                        PendingSpectralAxeEffect == null || PendingSpectralAxeEffect.StatusEffect == null)
                    {
                        return;
                    }
                    if (!Player.m_localPlayer.m_seman.HaveStatusEffect(PendingSpectralAxeEffect.StatusEffect.m_nameHash))
                    {
                        Player.m_localPlayer.m_seman.AddStatusEffect(PendingSpectralAxeEffect.StatusEffect, true);
                        if (heldAxeInstance == null)
                        {
                            GiveSpectralAxe();
                        }
                        //Jotunn.Logger.LogInfo("Adding SpectralAxe status effect");
                        //Player.m_localPlayer.m_seman.AddStatusEffect(SpectralAxeEffect.StatusEffect, true);
                    }
                    return;
                }
            }
            public static void Call()
            {
                if (Player.m_localPlayer == null || Player.m_localPlayer.m_seman == null ||
                    PendingSpectralAxeEffect == null || PendingSpectralAxeEffect.StatusEffect == null)
                {
                    return;
                }

                // If the player presses the attack button while holding the spectral axe, perform a spectral attack
                if (Player.m_localPlayer.m_seman.HaveStatusEffect(PendingSpectralAxeEffect.StatusEffect.m_nameHash) && (ZInput.GetButton("Attack") || ZInput.GetButton("JoyAttack")))
                {
                    //blockAttack = true;
                    if (ZInput.GetButtonDown("Attack") || ZInput.GetButtonDown("JoyAttack"))
                    {
                        Player.m_localPlayer.m_seman.RemoveStatusEffect(PendingSpectralAxeEffect.StatusEffect.m_nameHash, false);
                        Jotunn.Logger.LogInfo("Spectral Axe attack called");
                        ThrowSpectralAxe();
                        //blockAttack = false;
                        //Player.m_localPlayer.UseItem(SpectralAxeItem.ItemData);
                    }
                    Player.m_localPlayer.m_seman.AddStatusEffect(SpectralAxeEffect.StatusEffect, true);
                }
            }

            private static void GiveSpectralAxe()
            {
                // Load the projectile prefab (ensure it's registered in ZNetScene)
                //GameObject prefab = ZNetScene.instance.GetPrefab("SpectralAxeProjectile");
                GameObject prefab = ZNetScene.instance.GetPrefab("AxeBronze");
                if (prefab == null)
                {
                    Jotunn.Logger.LogError("SpectralAxeProjectile prefab not found!");
                    return;
                }

                // Instantiate and parent to player's hand
                Jotunn.Logger.LogInfo("Instantiating Spectral Axe in player's hand");
                Transform hand = Player.m_localPlayer.m_visEquipment.m_rightHand;
                heldAxeInstance = UnityEngine.Object.Instantiate(prefab, hand.position, hand.rotation);
                heldAxeInstance.transform.localPosition = Vector3.zero;
                heldAxeInstance.transform.localRotation = Quaternion.identity;
                heldAxeInstance.name = "SpectralAxe"; // Set a name for easier debugging
                heldAxeInstance.AddComponent<AxeDebug>(); // Add a debug component to track destruction
                Jotunn.Logger.LogInfo("Spectral Axe instantiated and parented to player's hand.");
            }

            private static void ThrowSpectralAxe()
            {
                if (heldAxeInstance == null)
                {
                    Jotunn.Logger.LogError("Held axe instance is null. Cannot throw spectral axe.");
                    return;
                }
                heldAxeInstance.transform.parent = null;
                Rigidbody rb = heldAxeInstance.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = heldAxeInstance.AddComponent<Rigidbody>();
                    Jotunn.Logger.LogInfo("Added Rigidbody to held axe instance.");
                }
                // Force physics state
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.velocity = Player.m_localPlayer.transform.forward * 30f + Vector3.up * 2f;
                Jotunn.Logger.LogInfo($"Set velocity to {rb.velocity}");

                if (heldAxeInstance.GetComponent<Collider>() == null)
                {
                    heldAxeInstance.AddComponent<BoxCollider>();
                    Jotunn.Logger.LogInfo("Added BoxCollider to held axe instance.");
                }

                if (heldAxeInstance.GetComponent<SpectralAxeProjectile>() == null)
                {
                    heldAxeInstance.AddComponent<SpectralAxeProjectile>();
                    Jotunn.Logger.LogInfo("Added SpectralAxeProjectile component to held axe instance.");
                }

                heldAxeInstance = null;
            }

            public static void AddEffects()
            {
                Jotunn.Logger.LogInfo("Adding Spectral Axe effects");
                StatusEffect pendeffect = ScriptableObject.CreateInstance<StatusEffect>();
                pendeffect.name = "PendinnSpectralAxeEffect";
                pendeffect.m_name = "$spectral_axe_effect";
                pendeffect.m_tooltip = "$spectral_axe_effect_tooltip";
                SpectralAxeTexture = SpecialJumpTexture;
                pendeffect.m_icon = Sprite.Create(SpectralAxeTexture, new Rect(0, 0, SpectralAxeTexture.width, SpectralAxeTexture.height), new Vector2(0.5f, 0.5f));
                pendeffect.m_startMessageType = MessageHud.MessageType.Center;
                pendeffect.m_startMessage = "$spectral_axe_effect_start";
                pendeffect.m_stopMessageType = MessageHud.MessageType.Center;
                pendeffect.m_stopMessage = "$spectral_axe_effect_stop";
                pendeffect.m_ttl = 0f; // No TTL for spectral axe
                StatusEffect effect = ScriptableObject.CreateInstance<StatusEffect>();
                effect.name = "SpectralAxeEffect";
                effect.m_name = "$spectral_axe_effect";
                effect.m_tooltip = "$spectral_axe_effect_tooltip";
                effect.m_icon = Sprite.Create(SpectralAxeTexture, new Rect(0, 0, SpectralAxeTexture.width, SpectralAxeTexture.height), new Vector2(0.5f, 0.5f));
                effect.m_startMessageType = MessageHud.MessageType.Center;
                effect.m_startMessage = "$spectral_axe_effect_start";
                effect.m_stopMessageType = MessageHud.MessageType.Center;
                effect.m_stopMessage = "$spectral_axe_effect_stop";
                effect.m_ttl = 0f; // No TTL for spectral axe
                
                //SpectralAxeItem.StatusEffect = new CustomStatusEffect(effect, fixReference: false);
                PendingSpectralAxeEffect = new CustomStatusEffect(pendeffect, fixReference: false);
                SpectralAxeEffect = new CustomStatusEffect(effect, fixReference: false);
                if (PendingSpectralAxeEffect.StatusEffect == null || SpectralAxeEffect.StatusEffect == null)
                {
                    Jotunn.Logger.LogError("Failed to create Spectral Axe status effects. Check the prefab registration.");
                    return;
                }
            }
        }

        public class AxeDebug : MonoBehaviour
        {
            private void OnDestroy()
            {
                Jotunn.Logger.LogWarning("AxeDebug: heldAxeInstance was destroyed!");
            }
        }
        public class SpectralAxeProjectile : MonoBehaviour
        {
            private void OnCollisionEnter(Collision collision)
            {
                // Check if hit object is a tree
                if (collision.gameObject.GetComponent<TreeBase>() != null)
                {
                    // Deal damage or trigger effect
                    var tree = collision.gameObject.GetComponent<TreeBase>();
                    tree.Damage(new HitData
                    {
                        m_damage = new HitData.DamageTypes { m_chop = 100f },
                        m_point = collision.contacts[0].point,
                        m_dir = collision.relativeVelocity.normalized,
                        m_attacker = Player.m_localPlayer.GetZDOID(),
                    });
                }

                // Destroy the projectile after impact
                Destroy(gameObject);
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
                SpectralAxe.AddEffects();
            }
        }
    }
}
