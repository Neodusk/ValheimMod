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
        public Coroutine teleportCountdownCoroutine;
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
                SpectralArrow.CallPending();
                ValhallaDome.CallPending();
            }
            public static void CallSpecialAbilities()
            {
                SpecialJump.Call();
                SpectralArrow.Call();

            }
        }

        public class SpecialJump
        {
            public static Texture2D texture;
            public static bool Triggered = false; // Flag to indicate if the special jump key is pressed down
            public static int specialForce = 15; // Set the jump force for the special jump
            public static int defaultForce = 8; // Set the default jump force
            public static CustomStatusEffect SpecialEffect; // Custom status effect for the special jump
            public static CustomStatusEffect PendingSpecialEffect; // Custom status effect for the special jump
            public static bool CallPending()
            {
                // If user picks the superjump buff in radial, give them the buff
                RadialAbility radial_ability = GetRadialAbility();
                string ability_name = radial_ability.ToString();
                if (ability_name == RadialAbility.SuperJump.ToString())
                {

                    if (!Player.m_localPlayer.m_seman.HaveStatusEffect(SpecialEffect.StatusEffect.m_nameHash))
                    {
                        Jotunn.Logger.LogInfo("Adding SpecialJump.PendingSpecialEffect status effect");
                        Player.m_localPlayer.m_seman.AddStatusEffect(PendingSpecialEffect.StatusEffect, true);
                    }
                    return ZInput.GetButton(ModInput.SpecialRadialButton.Name);
                }
                return false;
            }
            public static void Call()
            {
                // if the player presses the jump button when they have the jump pending buff, give super jump effect

                if (((ZInput.GetButton("Jump") || ZInput.GetButton("JoyJump")) && Player.m_localPlayer.m_seman.HaveStatusEffect(PendingSpecialEffect.StatusEffect.m_nameHash)))
                {
                    Jotunn.Logger.LogInfo("Special jump button is pressed down");
                    Jotunn.Logger.LogInfo($"SpecialJump.PendingSpecialEffect StatusEffect Duration: {PendingSpecialEffect.StatusEffect.GetDuration()}");
                    Jotunn.Logger.LogInfo($"SpecialJump.PendingSpecialEffect StatusEffect IsDone: {PendingSpecialEffect.StatusEffect.IsDone()}");
                    if (Player.m_localPlayer.m_seman.HaveStatusEffect(PendingSpecialEffect.StatusEffect.m_nameHash) && !Player.m_localPlayer.m_seman.HaveStatusEffect(SpecialEffect.StatusEffect.m_nameHash))
                    {
                        Jotunn.Logger.LogInfo("Removing SpecialJump.PendingSpecialEffect status effect and adding JumpSpecialEffect status effect");
                        Player.m_localPlayer.m_seman.RemoveStatusEffect(PendingSpecialEffect.StatusEffect.m_nameHash, false);
                        Player.m_localPlayer.m_seman.AddStatusEffect(SpecialEffect.StatusEffect, false);
                        Triggered = true;
                        Jotunn.Logger.LogInfo($"SpecialJumpTriggered1 = {Triggered}");
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
                effect.m_icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                effect.m_startMessageType = MessageHud.MessageType.Center;
                //effect.m_startMessage = "$special_jumpeffect_start";
                effect.m_stopMessageType = MessageHud.MessageType.Center;
                effect.m_ttl = 10f;
                effect.m_cooldownIcon = effect.m_icon;
                SpecialEffect = new CustomStatusEffect(effect, fixReference: false);

                pendeffect.name = "PendingSpecialJumpEffect";
                pendeffect.m_name = "$pending_special_jumpeffect";
                pendeffect.m_tooltip = "$special_jumpeffect_tooltip";
                pendeffect.m_icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
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
                PendingSpecialEffect = new CustomStatusEffect(pendeffect, fixReference: false);
            }
        }
        /// <summary>
        /// Handles the special teleport ability, including adding effects and managing the teleport countdown.
        /// </summary>
        public class SpecialTeleport
        {
            public static Texture2D texture;
            public static CustomStatusEffect PendingSpecialEffect; // Custom status effect for the teleport home pending state
            public static CustomStatusEffect SpecialEffect; // Custom status effect for the teleport home
            public static bool teleportCancelled = false;
            public static bool teleportPending = false;
            public static string teleportEndingMsg = "Traveling...";


            public static bool CallPending(valheimmod Instance)
            {
                // If user picks the teleport home ability in radial, teleport them home
                RadialAbility radial_ability = GetRadialAbility();
                string ability_name = radial_ability.ToString();
                if (ability_name == RadialAbility.TeleportHome.ToString())
                {
                    if (Player.m_localPlayer.m_seman.HaveStatusEffect(SpecialEffect.StatusEffect.m_nameHash))
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$teleporteffect_cd");
                        return false;
                    }
                    if (!Player.m_localPlayer.m_seman.HaveStatusEffect(PendingSpecialEffect.StatusEffect.m_nameHash))
                    {
                        teleportCancelled = false;
                        teleportPending = true;
                        Jotunn.Logger.LogInfo("Adding TeleportHomeSpecialEffect status effect");
                        Player.m_localPlayer.m_seman.AddStatusEffect(PendingSpecialEffect.StatusEffect, true);
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
                        teleportCancelled = true;
                        teleportPending = false;
                        // Remove the pending teleport status effect if you want:
                        if (Player.m_localPlayer != null)
                        {
                            Player.m_localPlayer.m_seman.RemoveStatusEffect(PendingSpecialEffect.StatusEffect.m_nameHash, false);
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
                pendteleporteffect.m_icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                pendteleporteffect.m_startMessageType = MessageHud.MessageType.Center;
                pendteleporteffect.m_startMessage = "$pending_teleporteffect_start";
                pendteleporteffect.m_stopMessageType = MessageHud.MessageType.Center;
                pendteleporteffect.m_ttl = 0f; // No TTL for pending effect
                teleporteffect.name = "TeleportEffect";
                teleporteffect.m_name = "$teleport_effect";
                teleporteffect.m_tooltip = "$special_teleport_cd_tooltip";
                teleporteffect.m_icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                teleporteffect.m_startMessageType = MessageHud.MessageType.Center;
                teleporteffect.m_startMessage = "$teleporteffect_start";
                teleporteffect.m_stopMessageType = MessageHud.MessageType.Center;
                teleporteffect.m_stopMessage = "$teleporteffect_stop";
                teleporteffect.m_ttl = 0f;
                teleporteffect.m_cooldownIcon = teleporteffect.m_icon;

                PendingSpecialEffect = new CustomStatusEffect(pendteleporteffect, fixReference: false);
                SpecialEffect = new CustomStatusEffect(teleporteffect, fixReference: false);
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
                    if (teleportCancelled || !teleportPending)
                    {
                        valheimmod.Instance.teleportCountdownCoroutine = null;
                        yield break;
                    }
                }
                valheimmod.Instance.teleportCountdownCoroutine = null;

                // Only run this if teleport wasn't cancelled
                if (!teleportCancelled && teleportPending)
                {
                    // Place your post-countdown logic here
                    Player.m_localPlayer.m_seman.AddStatusEffect(SpecialEffect.StatusEffect, true);
                    PlayerProfile profile = Game.instance.GetPlayerProfile();
                    Vector3 homepoint = profile.GetCustomSpawnPoint(); // Get the player's home point
                    if (homepoint == Vector3.zero)
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You don't have a bed. Teleporting to Sacrificial Stones");
                        homepoint = profile.GetHomePoint(); // Fallback to the default home point
                    }
                    Player.m_localPlayer.TeleportTo(homepoint, Quaternion.identity, true); // TelepoSetCustomSpawnPointrt the player to their home point
                    Player.m_localPlayer.m_seman.RemoveStatusEffect(PendingSpecialEffect.StatusEffect.m_nameHash, false); // Remove the pending teleport effect
                    teleportPending = false;
                }
            }
        }

        public class SpectralArrow
        {
            public static Texture2D texture;
            public static Sprite[] textures = new Sprite[3];
            public static CustomStatusEffect SpecialEffect;
            public static CustomStatusEffect SpecialCDEffect;
            public static Dictionary<Player, int> ShotsFired = new Dictionary<Player, int>();
            public static Dictionary<Player, float> PreviousSkill = new Dictionary<Player, float>();
            public static float defaultVelocity = 55f;
            public static float specialVelocity = 100f;
            internal static float cooldown = 30f; // cooldown time for the spectral arrow ability
            public static void Call()
            {
                if (Player.m_localPlayer != null && Player.m_localPlayer.m_seman.HaveStatusEffect(SpecialEffect.StatusEffect.m_nameHash))
                {
                    return;
                }
            }
            public static bool CallPending()
            {
                if (Player.m_localPlayer == null)
                {
                    return false;
                }
                // If user picks the spectral arrow ability in radial, give them the buff
                RadialAbility radial_ability = GetRadialAbility();
                string ability_name = radial_ability.ToString();
                if (ability_name == RadialAbility.SpectralArrow.ToString())
                {
                    if (!Player.m_localPlayer.m_seman.HaveStatusEffect(SpecialCDEffect.StatusEffect.m_nameHash) && !Player.m_localPlayer.m_seman.HaveStatusEffect(SpecialEffect.StatusEffect.m_nameHash))
                    {
                        // If the player doesn't have the spectral arrow effect or the pending effect, add the pending effect
                        Jotunn.Logger.LogInfo("Spectral Arrow ability selected, adding PendingSpectralArrowEffect status effect");
                        {
                            Jotunn.Logger.LogInfo("Adding PendingSpectralArrowEffect status effect");
                            Player.m_localPlayer.m_seman.AddStatusEffect(SpecialEffect.StatusEffect, true);
                        }
                        return ZInput.GetButton(ModInput.SpecialRadialButton.Name);
                    }
                    return false;
                }
                return false;
            }
            public static void Cancel(Player __instance, ItemDrop.ItemData weapon = null)
            {
                /// <summary>
                /// Cancels the spectral arrow ability, removing the status effects and resetting the weapon projectile velocity.
                /// Does not allow the player to cancel from radial menu, only from the status effect.
                /// /// </summary>
                if (__instance != null)
                {
                    if (__instance.m_seman.HaveStatusEffect(SpecialEffect.StatusEffect.m_nameHash))
                    {
                        Jotunn.Logger.LogInfo("Removing SpectralArrowEffect status effect and adding SpectralArrowCDEffect status effect");
                        __instance.m_seman.RemoveStatusEffect(SpecialEffect.StatusEffect.m_nameHash, false);
                        __instance.m_seman.AddStatusEffect(SpecialCDEffect.StatusEffect, false);
                    }
                    if (weapon != null)
                    {
                        Jotunn.Logger.LogInfo($"Resetting weapon projectile velocity to default {defaultVelocity}");
                        weapon.m_shared.m_attack.m_projectileVel = defaultVelocity;
                    }
                    // remove the skill and reset the shots fired
                    Jotunn.Logger.LogInfo("Resetting SpectralArrow skill level and shots fired");
                    Player.m_localPlayer.m_skills.GetSkill(Skills.SkillType.Bows).m_level = PreviousSkill[Player.m_localPlayer];
                    ShotsFired.Remove(Player.m_localPlayer);
                    PreviousSkill.Remove(Player.m_localPlayer);
                    Jotunn.Logger.LogInfo("Spectral Arrow ability cancelled");
                }
            }
            public static void AddEffects()
            {
                StatusEffect effect = ScriptableObject.CreateInstance<StatusEffect>();
                StatusEffect pendeffect = ScriptableObject.CreateInstance<StatusEffect>();
                pendeffect.name = "SpectralArrowEffect";
                pendeffect.m_name = "$spectral_arrow_effect";
                pendeffect.m_tooltip = "$special_spectral_arrow_tooltip";
                pendeffect.m_icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                pendeffect.m_startMessageType = MessageHud.MessageType.TopLeft;
                pendeffect.m_startMessage = "$spectral_arrow_start";
                pendeffect.m_stopMessageType = MessageHud.MessageType.TopLeft;
                pendeffect.m_ttl = 0f; // No TTL for pending effect
                effect.name = "SpectralArrowCDEffect";
                effect.m_name = "$spectral_arrow_effect";
                effect.m_tooltip = "$spectral_arrow_cd_tooltip";
                effect.m_icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                effect.m_startMessageType = MessageHud.MessageType.TopLeft;
                effect.m_startMessage = "$spectral_arrow_cd_start";
                effect.m_stopMessageType = MessageHud.MessageType.TopLeft;
                effect.m_stopMessage = "$spectral_arrow_cd_stop";
                effect.m_ttl = 60f * cooldown; // 30 minutes cooldown
                effect.m_cooldownIcon = effect.m_icon;

                SpecialEffect = new CustomStatusEffect(pendeffect, fixReference: false);
                SpecialCDEffect = new CustomStatusEffect(effect, fixReference: false);
            }
            public static void UpdateStatusEffectTexture(Hud __instance, StatusEffect statusEffect, int index)
            {
                if (statusEffect.m_name == SpecialEffect.StatusEffect.m_name)
                {
                    // Find the correct icon for the current arrow count
                    int arrowsLeft = 3 - ShotsFired.GetValueOrDefault(Player.m_localPlayer, 0);
                    if (arrowsLeft > 0 && arrowsLeft <= 3)
                    {
                        // Update the icon in the HUD
                        RectTransform val2 = __instance.m_statusEffects[index];
                        Image component = ((Component)((Transform)val2).Find("Icon")).GetComponent<Image>();
                        component.sprite = textures[arrowsLeft - 1];
                    }
                }
            }
        }

        public class ValhallaDome
        {
            public static Texture2D texture;
            public static GameObject ActiveDome;
            public static string LastDomeUID;
            public static string dome_uid = "valhalladome_uid";
            public static CustomStatusEffect SpecialEffect;
            public static CustomStatusEffect CDSpecialEffect;
            public static bool abilityUsed = false; // Flag to indicate if the ability has been used
            internal static float ttl = 30f; // Time before the dome is destroyed
            internal static float cooldown = 120f * 60f; // Time before ability can be used again

            public static void Call()
            {
                if (Player.m_localPlayer == null) return;
                GameObject domePrefab = ZNetScene.instance.GetPrefab("piece_shieldgenerator");
                if (domePrefab != null)
                {
                    Vector3 pos = Player.m_localPlayer.transform.position;
                    Quaternion rot = Quaternion.identity;
                    ActiveDome = UnityEngine.Object.Instantiate(domePrefab, pos, rot);
                    var znetView = ActiveDome.GetComponent<ZNetView>();
                    if (znetView != null && znetView.IsValid())
                    {
                        string uniqueId = System.Guid.NewGuid().ToString();
                        znetView.GetZDO().Set(dome_uid, uniqueId);
                        // Save this somewhere (e.g., static field) for later lookup
                        ValhallaDome.LastDomeUID = uniqueId;
                        PlayerPrefs.SetString("Dome_LastDomeUID", uniqueId);
                        PlayerPrefs.Save();

                        Jotunn.Logger.LogInfo($"Dome created with UID: {uniqueId}");
                    }
                    // Start a coroutine to set up the shield after one frame
                    Player.m_localPlayer.StartCoroutine(SetupShieldNextFrame(ActiveDome));
                }
            }
            public static void AddEffects()
            {
                StatusEffect cddomeeffect = ScriptableObject.CreateInstance<StatusEffect>();
                StatusEffect domeeffect = ScriptableObject.CreateInstance<StatusEffect>();
                cddomeeffect.name = "CDDomeEffect";
                cddomeeffect.m_name = "$cd_dome_effect";
                cddomeeffect.m_tooltip = "$cd_dome_tooltip";
                cddomeeffect.m_icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                cddomeeffect.m_startMessageType = MessageHud.MessageType.Center;
                cddomeeffect.m_startMessage = "$cd_domeeffect_start";
                cddomeeffect.m_stopMessageType = MessageHud.MessageType.Center;
                cddomeeffect.m_ttl = cooldown; // No TTL for pending effect
                cddomeeffect.m_cooldownIcon = cddomeeffect.m_icon;
                domeeffect.name = "DomeEffect";
                domeeffect.m_name = "$dome_effect";
                domeeffect.m_tooltip = "$dome_tooltip";
                domeeffect.m_icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                domeeffect.m_startMessageType = MessageHud.MessageType.Center;
                domeeffect.m_startMessage = "$domeeffect_start";
                domeeffect.m_stopMessageType = MessageHud.MessageType.Center;
                domeeffect.m_stopMessage = "$domeeffect_stop";
                domeeffect.m_ttl = ttl;

                CDSpecialEffect = new CustomStatusEffect(cddomeeffect, fixReference: false);
                SpecialEffect = new CustomStatusEffect(domeeffect, fixReference: false);
            }
            public class MobOnlyShield : MonoBehaviour
            {
                private float repelForce = 30f; // Adjust this value to change the force applied to mobs
                private void OnTriggerEnter(Collider other)
                {
                    Jotunn.Logger.LogInfo($"MobOnlyShield OnTriggerEnter called for {other.name}");
                    Character character = other.GetComponent<Character>();
                    if (character != null && character.IsMonsterFaction(0f))
                    {
                        Jotunn.Logger.LogInfo($"Repelling mob: {character.name}");
                        Vector3 repelDir = (character.transform.position - transform.position).normalized;
                        character.m_body?.AddForce(repelDir * repelForce, ForceMode.VelocityChange);

                    }
                    else if (character != null)
                    {
                        Jotunn.Logger.LogInfo($"Ignoring non-monster character: {character.name}");
                    }
                }
                private void OnTriggerStay(Collider other)
                {
                    Character character = other.GetComponent<Character>();
                    if (character != null && character.IsMonsterFaction(0f))
                    {
                        Vector3 repelDir = (character.transform.position - transform.position).normalized;
                        character.m_body?.AddForce(repelDir * repelForce, ForceMode.VelocityChange); // Use Force for continuous push
                    }
                }
            }
            private static IEnumerator SetupShieldNextFrame(GameObject dome)
            {
                yield return null; // Wait one frame
                
                var shieldGen = dome.GetComponent<ShieldGenerator>();
                if (shieldGen != null)
                {
                    shieldGen.m_offWhenNoFuel = false;
                    shieldGen.m_minShieldRadius = 4f;
                    shieldGen.m_maxShieldRadius = 4f; // Set the desired shield radius
                    shieldGen.SetFuel(shieldGen.m_maxFuel);
                    shieldGen.UpdateShield();

                    // Hide all MeshRenderers except the dome
                    foreach (var renderer in dome.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (shieldGen.m_shieldDome == null || !renderer.transform.IsChildOf(shieldGen.m_shieldDome.transform))
                        {
                            renderer.enabled = false;
                        }
                    }
                    // Add a collider to the dome mesh if not present
                    var domeObj = shieldGen.m_shieldDome;
                    // Remove unwanted components for visuals only
                    UnityEngine.Object.Destroy(dome.GetComponent<Collider>());
                    foreach (var col in dome.GetComponentsInChildren<Collider>())
                    {
                        UnityEngine.Object.Destroy(col);
                    }
                    if (domeObj != null)
                    {
                        var collider = domeObj.GetComponent<SphereCollider>();
                        if (collider == null)
                        {
                            collider = domeObj.AddComponent<SphereCollider>();
                            collider.isTrigger = true; // Use trigger for custom logic
                            float visualRadius = shieldGen.m_maxShieldRadius;
                            Jotunn.Logger.LogInfo($"Dome scale: {domeObj.transform.lossyScale}, collider.radius: {collider.radius}, visualRadius: {visualRadius}");
                            float scale = domeObj.transform.lossyScale.x; // Use .x, .y, or .z if non-uniform
                            float fudge = 0.3f; // Adjust this value to change the size of the collider relative to the visual radius
                            collider.radius = (visualRadius / scale) * fudge; // Adjust radius based on the scale of the dome
                            domeObj.AddComponent<MobOnlyShield>();
                        }
                        // Set to a custom layer (make sure this layer exists and is set up in Unity)
                        domeObj.layer = LayerMask.NameToLayer("character");
                    }
                    else
                    {
                        Jotunn.Logger.LogWarning("ShieldGenerator component not found on the instantiated dome!");
                    }

                    var timedDestruction = ActiveDome.GetComponent<TimedDestruction>();
                    if (timedDestruction == null)
                    {
                        timedDestruction = ActiveDome.AddComponent<TimedDestruction>();
                        Jotunn.Logger.LogInfo("Added TimedDestruction component to ValhallaDome");
                    }
                        timedDestruction.m_forceTakeOwnershipAndDestroy = true;
                        timedDestruction.m_timeout = ttl; // time before destruction
                        timedDestruction.Trigger();
                        Jotunn.Logger.LogInfo("Set m_forceTakeOwnershipAndDestroy = true on TimedDestruction");
                }
            }
            public static void CallPending()
            {
                // If user picks the valhalla dome ability in radial, give them the buff
                RadialAbility radial_ability = GetRadialAbility();
                string ability_name = radial_ability.ToString();
                if (ability_name == RadialAbility.ValhallaDome.ToString())
                {
                    if (Player.m_localPlayer.m_seman.HaveStatusEffect(SpecialEffect.StatusEffect.m_nameHash) || Player.m_localPlayer.m_seman.HaveStatusEffect(CDSpecialEffect.StatusEffect.m_nameHash))
                    {
                        return;
                    }
                    Player.m_localPlayer.m_seman.AddStatusEffect(SpecialEffect.StatusEffect, true);
                    ValhallaDome.abilityUsed = true; // Reset the active dome
                    Call();
                }
            }
            
            public static void OnPlayerLogout()
            {
                Jotunn.Logger.LogInfo($"OnPlayerLogout called. Dome ref: {ActiveDome}");
                if (ActiveDome != null)
                {
                    var timedDestruction = ActiveDome.GetComponent<TimedDestruction>();
                    if (timedDestruction == null)
                    {
                        timedDestruction = ActiveDome.AddComponent<TimedDestruction>();
                        Jotunn.Logger.LogInfo("Added TimedDestruction component to ValhallaDome");
                    }
                    else
                    {
                        Jotunn.Logger.LogInfo("TimedDestruction component already exists on ValhallaDome");
                    }
                        timedDestruction.m_forceTakeOwnershipAndDestroy = true;
                        timedDestruction.m_timeout = 0f; // or your desired time
                        timedDestruction.Trigger();
                        timedDestruction.DestroyNow();
                        Jotunn.Logger.LogInfo("Set m_forceTakeOwnershipAndDestroy = true on TimedDestruction");
                    }
                else
                    {
                        Jotunn.Logger.LogInfo("No active Dome to destroy.");
                    }
                }
            }
        
        public static void UpdateStatusEffectTextures(Hud __instance, List<StatusEffect> statusEffects)
        {
            for (int j = 0; j < statusEffects.Count; j++)
            {
                StatusEffect statusEffect = statusEffects[j];
                SpectralArrow.UpdateStatusEffectTexture(__instance, statusEffect, j);

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
                SpectralArrow.AddEffects();
                ValhallaDome.AddEffects();
            }
            // todo: save the effects on player logout and load them on login using PlayerPrefs
        }
    }
}
