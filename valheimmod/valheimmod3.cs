using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
using Mono.Security.Interface;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
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
        teleportCountdownCoroutine = StartCoroutine(ModAbilities.SpecialTeleport.Instance.TeleportCountdownCoroutine(seconds));
    }

        /// <summary>
        /// Contains methods for calling special abilities
        /// </summary>
        public class ModAbilities
        {
            public static List<SpecialAbilityBase> specialAbilities = GetAllAbilityInstances();
            
            /// <summary>
            /// Gets all instances of SpecialAbilityBase subclasses
            /// /// This method uses reflection to find all classes that inherit from SpecialAbilityBase
            /// and attempts to retrieve a static instance of each class.
            /// </summary>
            /// <returns></returns>
            public static List<SpecialAbilityBase> GetAllAbilityInstances()
            {
                var abilityTypes = typeof(SpecialAbilityBase).Assembly
                    .GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(SpecialAbilityBase)));

                List<SpecialAbilityBase> specialAbilities = new List<SpecialAbilityBase>();

                foreach (var type in abilityTypes)
                {
                    // Try to get a static field called "Instance"
                    var instanceField = type.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceField != null)
                    {
                        var instance = instanceField.GetValue(null) as SpecialAbilityBase;
                        if (instance != null)
                            specialAbilities.Add(instance);
                        continue;
                    }

                    // Or try to get a static property called "Instance"
                    var instanceProp = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null)
                    {
                        var instance = instanceProp.GetValue(null) as SpecialAbilityBase;
                        if (instance != null)
                            specialAbilities.Add(instance);
                    }
                }
                return specialAbilities;
            }
        
            public static void CallPendingAbilities(valheimmod Instance)
            {
                foreach (var ability in specialAbilities)
                {
                    ability?.CallPending(Instance);
                }
            }
            public static void CallSpecialAbilities()
            {
                foreach (var ability in specialAbilities)
                {
                    ability?.Call();
                }

            }
            public class Effects
            {
                public static List<StatusEffect> statusEffects = new List<StatusEffect>();
                /// <summary>
                /// Register status effects for special abilities.
                /// </summary>
                public static void Register()
                {
                    foreach (var ability in specialAbilities)
                    {
                        ability.AddEffects();
                        List<StatusEffect> se = ability.GetStatusEffects();
                        foreach (var effect in se)
                        {
                            statusEffects.Add(effect);
                        }
                    }
                }

                public static void Save()
                {
                    foreach (StatusEffect effect in statusEffects)
                    {
                        // Save the status effect name and whether it is active
                        Jotunn.Logger.LogInfo($"Saving status effect: {effect.m_name} with hash {effect.m_nameHash}");
                        string remainingTime = effect.GetRemaningTime().ToString();
                        if (effect.m_name == "TeleportEffect" && Player.m_localPlayer.m_seman.HaveStatusEffect(effect.m_nameHash))
                        {
                            // If the teleport effect is active, we save the remaining time
                            remainingTime = "1";
                        }
                        string day = EnvMan.instance.GetDay().ToString();
                        PlayerPrefs.SetString("effect_day", day);
                        Jotunn.Logger.LogInfo($"Saving status effect: {effect.m_name} with remaining time: {remainingTime}");
                        PlayerPrefs.SetString(effect.m_name, Player.m_localPlayer.m_seman.HaveStatusEffect(effect.m_nameHash) ? remainingTime : "0");
                    }
                }

                public static void Load()
                {
                    foreach (SpecialAbilityBase ability in specialAbilities)
                    {
                        // Load the status effect name and whether it is active
                        List<StatusEffect> abilityStatusEffects = ability.GetStatusEffects();
                        Dictionary<string, string> statusEffectDict = new Dictionary<string, string>();
                        foreach (StatusEffect effect in abilityStatusEffects)
                        {
                            string remainingTime = PlayerPrefs.GetString(effect.name, "0");
                            if (float.TryParse(remainingTime, out float time) && time > 0)
                            {
                                statusEffectDict[effect.m_name] = remainingTime;
                            }
                        }
                        ability.updateDuration(statusEffectDict);
                    }
                }

                public static void UpdateStatusEffect(Hud __instance, List<StatusEffect> statusEffectsList, Dictionary<string, string> durationDict = null)
                {
                    // Update the textures for each status effect in the list
                    {
                        foreach (var ability in specialAbilities)
                        {
                            for (int j = 0; j < statusEffectsList.Count; j++)
                            {
                                StatusEffect statusEffect = statusEffectsList[j];
                                ability?.updateTexture(__instance, statusEffect, j);
                            }
                        }
                    }
                }
            }

            public abstract class SpecialAbilityBase
            {
                public virtual void Call() { }
                public virtual void CallPending(valheimmod instance = null) { }
                public abstract void AddEffects();
                public abstract List<StatusEffect> abilitySE { get; set; }  // Property to hold the status effects for the ability
                public List<StatusEffect> GetStatusEffects()
                {
                    return abilitySE;
                }
                /// <summary>
                /// Updates the durations of all the special ability effects
                public abstract void updateDuration(Dictionary<string, string> statusEffectDict); // mandatory method to update the duration of the ability
                public virtual void updateDurationByName(string name) { }
                public virtual void updateTexture(Hud __instance, StatusEffect statusEffect, int index) { }  // todo: remove the index thing and handle that differently for the one function
                protected virtual List<EffectList.EffectData> SetupEffectList() => null;
            }

            public class SpecialJump : SpecialAbilityBase
            {
                public Texture2D texture;
                public bool Triggered = false; // Flag to indicate if the special jump key is pressed down
                public int specialForce = 15; // Set the jump force for the special jump
                public int defaultForce = 8; // Set the default jump force
                public CustomStatusEffect SpecialEffect; // Custom status effect for the special jump
                public CustomStatusEffect PendingSpecialEffect; // Custom status effect for the special jump
                public override List<StatusEffect> abilitySE { get; set; } = new List<StatusEffect>();
                public static SpecialJump Instance = new SpecialJump();

                public override void CallPending(valheimmod instance = null)
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
                    }
                }
                public override void Call()
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

                public void Cancel()
                {
                    // If the player presses the jump button when they have the jump pending buff, give super jump effect
                }

                /// <summary>
                /// Sets up the sfx and vfx for the special jump ability.
                /// </summary>
                protected override List<EffectList.EffectData> SetupEffectList()
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

                public override void AddEffects()
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
                    PrefabManager.OnPrefabsRegistered -= ModAbilities.Effects.Register;
                    PendingSpecialEffect = new CustomStatusEffect(pendeffect, fixReference: false);
                    abilitySE.Add(SpecialEffect.StatusEffect);
                    abilitySE.Add(PendingSpecialEffect.StatusEffect);
                }
                public override void updateDuration(Dictionary<string, string> statusEffectDict)
                {
                    foreach (var kvp in statusEffectDict)
                    {
                        string effectName = kvp.Key;
                        string remainingTime = kvp.Value;

                        if (effectName == SpecialEffect.StatusEffect.m_name)
                        {
                            float time;
                            if (float.TryParse(remainingTime, out time) && time > 0)
                            {
                                Jotunn.Logger.LogInfo($"Updating Special Jump effect duration: {effectName} with remaining time: {time}");
                                SpecialEffect.StatusEffect.m_time = time;
                                Player.m_localPlayer.m_seman.AddStatusEffect(SpecialEffect.StatusEffect, true);
                            }
                        }

                    }
                }

            }
            /// <summary>
            /// Handles the special teleport ability, including adding effects and managing the teleport countdown.
            /// </summary>
            public class SpecialTeleport : SpecialAbilityBase
            {
                public Texture2D texture;
                public CustomStatusEffect PendingSpecialEffect; // Custom status effect for the teleport home pending state
                public CustomStatusEffect SpecialEffect; // Custom status effect for the teleport home
                public bool teleportCancelled = false;
                public bool teleportPending = false;
                public string teleportEndingMsg = "Traveling...";
                public override List<StatusEffect> abilitySE { get; set; } = new List<StatusEffect>();
                public static SpecialTeleport Instance = new SpecialTeleport();

                public override void Call()
                {
                    // no need to implement as we do it all in CallPending()
                    return;
                }
                public override void CallPending(valheimmod Instance)
                {
                    // If user picks the teleport home ability in radial, teleport them home
                    RadialAbility radial_ability = GetRadialAbility();
                    string ability_name = radial_ability.ToString();
                    if (ability_name == RadialAbility.TeleportHome.ToString())
                    {
                        if (Player.m_localPlayer.m_seman.HaveStatusEffect(SpecialEffect.StatusEffect.m_nameHash))
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$teleporteffect_cd");
                            return;
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
                    }
                }

                public void Cancel()
                {
                    if (valheimmod.Instance.teleportCountdownCoroutine != null)
                    {
                        valheimmod.Instance.StopCoroutine(valheimmod.Instance.teleportCountdownCoroutine);
                        valheimmod.Instance.teleportCountdownCoroutine = null;
                        if (Player.m_localPlayer != null)
                        {
                            teleportCancelled = true;
                            teleportPending = false;
                            Player.m_localPlayer.m_seman.RemoveStatusEffect(PendingSpecialEffect.StatusEffect.m_nameHash, false);
                        }
                    }
                }
                public override void AddEffects()
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
                    abilitySE.Add(SpecialEffect.StatusEffect);
                    abilitySE.Add(PendingSpecialEffect.StatusEffect);
                }

                public override void updateDuration(Dictionary<string, string> statusEffectDict)
                {
                    foreach (var kvp in statusEffectDict)
                    {
                        string effectName = kvp.Key;
                        string remainingTime = kvp.Value;

                        if (effectName == SpecialEffect.StatusEffect.m_name)
                        {
                            float time;
                            if (float.TryParse(remainingTime, out time) && time > 0)
                            {
                                string day_str = PlayerPrefs.GetString("effect_day");
                                int day = day_str != null ? int.Parse(day_str) : 0;
                                // don't re-add the duration if there is a new day since last
                                if (currentDay <= day)
                                {
                                    Jotunn.Logger.LogInfo($"Updating ValhallaDome effect duration: {effectName} with remaining time: {time}");
                                    Player.m_localPlayer.m_seman.AddStatusEffect(SpecialEffect.StatusEffect, true);
                                }
                                    // don't set a time update as the ttl is handled by the game days
                            }
                        }

                    }
                }

                public System.Collections.IEnumerator TeleportCountdownCoroutine(int seconds)
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


            // todo: fix issues with player bow level not reverting on death
            // and on logout
            public class SpectralArrow : SpecialAbilityBase
            {
                public Texture2D texture;
                public Sprite[] textures = new Sprite[3];
                public CustomStatusEffect SpecialEffect;
                public CustomStatusEffect SpecialCDEffect;
                public override List<StatusEffect> abilitySE { get; set; } = new List<StatusEffect>();
                public Dictionary<Player, int> ShotsFired = new Dictionary<Player, int>();
                public Dictionary<Player, float> PreviousSkill = new Dictionary<Player, float>();
                public float specialVelocity = 100f + modIdentifierPostfix; // base velocity for the spectral arrow, modified by the modIdentifierPostfix
                public float specialRange = 200 + modIdentifierPostfix;
                public float specialDamageMultiplier = 200f + modIdentifierPostfix;
                public float specialAccuracy = .1f + modIdentifierPostfix; 
                public float specialDrawDurationMin = 0.1f + modIdentifierPostfix;
                public static float modIdentifierPostfix = 0.012345f; // used to identify values changed by the mod on weapons

                // public ItemDrop.ItemData weapon;
                public List<ItemDrop.ItemData> weaponList = new List<ItemDrop.ItemData>(); // List of weapons to apply the spectral arrow effect to
                public Dictionary<string, Dictionary<string, float>> weaponDefaults = new Dictionary<string, Dictionary<string, float>>(); // Store default weapon velocities
                internal float cooldown = 30f; // cooldown time for the spectral arrow ability
                public static SpectralArrow Instance = new SpectralArrow();
                public override void Call()
                {
                    return;
                }
                public override void CallPending(valheimmod instance = null)
                {
                    if (Player.m_localPlayer == null)
                    {
                        return;
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
                        }
                    }
                }
                public void Cancel(Player __instance)
                {
                    /// <summary>
                    /// Cancels the spectral arrow ability, removing the status effects and resetting the weapon projectile velocity.
                    /// Does not allow the player to cancel from radial menu, only from the status effect.
                    /// /// </summary>
                    if (__instance != null)
                    {
                        if (SpecialEffect == null)
                        {
                            Jotunn.Logger.LogError("SpectralArrow SpecialEffect or SpecialCDEffect is null, cannot cancel ability.");
                            return;
                        }
                        if (__instance.m_seman.HaveStatusEffect(SpecialEffect.StatusEffect.m_nameHash))
                        {
                            Jotunn.Logger.LogInfo("Removing SpectralArrowEffect status effect and adding SpectralArrowCDEffect status effect");
                            __instance.m_seman.RemoveStatusEffect(SpecialEffect.StatusEffect.m_nameHash, false);
                            __instance.m_seman.AddStatusEffect(SpecialCDEffect.StatusEffect, false);
                        }
                        // if (weapon != null)
                        // {
                        //     Jotunn.Logger.LogInfo($"Resetting weapon projectile velocity to default {defaultVelocity}");
                        //     weapon.m_shared.m_attack.m_projectileVel = defaultVelocity;
                        // }
                        // remove the skill and reset the shots fired
                        Jotunn.Logger.LogInfo("Resetting SpectralArrow skill level and shots fired");
                        // Player.m_localPlayer.m_skills.GetSkill(Skills.SkillType.Bows).m_level = PreviousSkill[Player.m_localPlayer];
                        ShotsFired.Remove(Player.m_localPlayer);
                        foreach (ItemDrop.ItemData weapon in weaponList)
                        {
                            if (ModAbilities.SpectralArrow.Instance.weaponDefaults.TryGetValue(weapon.m_shared.m_name, out var defaults))
                            {
                                Jotunn.Logger.LogInfo($"Resetting weapon {weapon.m_shared.m_name} to default values");
                                // Helper function to check if a value ends with modIdentifierPostfix
                                bool IsModdedValue(float value, float moddedValue)
                                {
                                    return Mathf.Abs(value - moddedValue) < 0.00001f;
                                }
                                // Only revert if the current value "ends with" modIdentifierPostfix
                                if (IsModdedValue(weapon.m_shared.m_attack.m_projectileVel, ModAbilities.SpectralArrow.Instance.specialVelocity))
                                    weapon.m_shared.m_attack.m_projectileVel = defaults["velocity"];
                                if (IsModdedValue(weapon.m_shared.m_attack.m_attackRange, ModAbilities.SpectralArrow.Instance.specialRange))
                                    weapon.m_shared.m_attack.m_attackRange = defaults["range"];
                                if (IsModdedValue(weapon.m_shared.m_attack.m_damageMultiplier, ModAbilities.SpectralArrow.Instance.specialDamageMultiplier))
                                    weapon.m_shared.m_attack.m_damageMultiplier = defaults["dmgMultiplier"];
                                if (IsModdedValue(weapon.m_shared.m_attack.m_projectileAccuracy, ModAbilities.SpectralArrow.Instance.specialAccuracy))
                                    weapon.m_shared.m_attack.m_projectileAccuracy = defaults["accuracy"];
                                if (IsModdedValue(weapon.m_shared.m_attack.m_drawDurationMin, ModAbilities.SpectralArrow.Instance.specialDrawDurationMin))
                                    weapon.m_shared.m_attack.m_drawDurationMin = defaults["drawMin"];
                            }
                        }
                        // PreviousSkill.Remove(Player.m_localPlayer);
                        Jotunn.Logger.LogInfo("Spectral Arrow ability cancelled");
                        weaponList.Clear(); // Clear the weapon list after cancelling the ability
                        weaponDefaults.Clear(); // Clear the weapon defaults after cancelling the ability
                    }
                }
                public override void AddEffects()
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
                    abilitySE.Add(SpecialEffect.StatusEffect);
                    abilitySE.Add(SpecialCDEffect.StatusEffect);
                }
                public override void updateDuration(Dictionary<string, string> statusEffectDict)
                {
                    foreach (var kvp in statusEffectDict)
                    {
                        string effectName = kvp.Key;
                        string remainingTime = kvp.Value;

                        if (effectName == SpecialCDEffect.StatusEffect.m_name)
                        {
                            float time;
                            if (float.TryParse(remainingTime, out time) && time > 0)
                            {
                                Jotunn.Logger.LogInfo($"Updating ValhallaDome effect duration: {effectName} with remaining time: {time}");
                                SpecialCDEffect.StatusEffect.m_cooldown = time;
                                Player.m_localPlayer.m_seman.AddStatusEffect(SpecialCDEffect.StatusEffect, true);
                            }
                        }
                    }

                }
                public override void updateTexture(Hud __instance, StatusEffect statusEffect, int index)
                {
                    if (statusEffect.m_name == SpecialEffect.StatusEffect.m_name)
                    {
                        // Find the correct icon for the current arrow count
                        int arrowsLeft = 3 - (ShotsFired.ContainsKey(Player.m_localPlayer) ? ShotsFired[Player.m_localPlayer] : 0);
                        if (arrowsLeft > 0 && arrowsLeft <= 3)
                        {
                            // Update the icon in the HUD
                            RectTransform val2 = __instance.m_statusEffects[index];
                            Image component = ((UnityEngine.Component)((Transform)val2).Find("Icon")).GetComponent<Image>();
                            component.sprite = textures[arrowsLeft - 1];
                        }
                    }

                }
            }

            public class ValhallaDome : SpecialAbilityBase
            {
                public Texture2D texture;
                public GameObject ActiveDome;
                public string LastDomeUID;
                public string dome_uid = "valhalladome_uid";
                public CustomStatusEffect SpecialEffect;
                public CustomStatusEffect SpecialCDEffect;
                public override List<StatusEffect> abilitySE { get; set; }
                public bool abilityUsed = false; // Flag to indicate if the ability has been used
                internal float ttl = 30f; // Time before the dome is destroyed
                internal float cooldown = 120f * 60f; // Time before ability can be used again
                public static ValhallaDome Instance = new ValhallaDome();
                public override void Call()
                {
                    return;
                }
                public void CallManual()
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
                            // Save this somewhere (e.g.,  field) for later lookup
                            Instance.LastDomeUID = uniqueId;
                            PlayerPrefs.SetString("Dome_LastDomeUID", uniqueId);
                            PlayerPrefs.Save();

                            Jotunn.Logger.LogInfo($"Dome created with UID: {uniqueId}");
                        }
                        // Start a coroutine to set up the shield after one frame
                        Player.m_localPlayer.StartCoroutine(SetupShieldNextFrame(ActiveDome));
                    }
                }
                public override void AddEffects()
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

                    SpecialCDEffect = new CustomStatusEffect(cddomeeffect, fixReference: false);
                    SpecialEffect = new CustomStatusEffect(domeeffect, fixReference: false);
                    abilitySE.Add(SpecialEffect.StatusEffect);
                    abilitySE.Add(SpecialCDEffect.StatusEffect);
                }

                public override void updateDuration(Dictionary<string, string> statusEffectDict)
                {
                    foreach (var kvp in statusEffectDict)
                    {
                        string effectName = kvp.Key;
                        string remainingTime = kvp.Value;

                        if (effectName == SpecialCDEffect.StatusEffect.m_name)
                        {
                            float time;
                            if (float.TryParse(remainingTime, out time) && time > 0)
                            {
                                Jotunn.Logger.LogInfo($"Updating ValhallaDome effect duration: {effectName} with remaining time: {time}");
                                SpecialCDEffect.StatusEffect.m_cooldown = time;
                                Player.m_localPlayer.m_seman.AddStatusEffect(SpecialCDEffect.StatusEffect, true);
                            }
                        }
                    }

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
                private IEnumerator SetupShieldNextFrame(GameObject dome)
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
                public override void CallPending(valheimmod instance = null)
                {
                    // If user picks the valhalla dome ability in radial, give them the buff
                    RadialAbility radial_ability = GetRadialAbility();
                    string ability_name = radial_ability.ToString();
                    if (ability_name == RadialAbility.ValhallaDome.ToString())
                    {
                        if (Player.m_localPlayer.m_seman.HaveStatusEffect(SpecialEffect.StatusEffect.m_nameHash) || Player.m_localPlayer.m_seman.HaveStatusEffect(SpecialCDEffect.StatusEffect.m_nameHash))
                        {
                            return;
                        }
                        Player.m_localPlayer.m_seman.AddStatusEffect(SpecialEffect.StatusEffect, true);
                        ValhallaDome.Instance.abilityUsed = true; // Reset the active dome
                        CallManual();
                    }
                }

                public void OnPlayerLogout()
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
        }
    }
}

