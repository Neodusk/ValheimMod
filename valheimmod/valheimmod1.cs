using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using Mono.Security.Cryptography;
using MonoMod.Utils;
using UnityEngine;
using valheimmod;
using static valheimmod.valheimmod;

namespace valheimmod
{
    


    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal partial class valheimmod : BaseUnityPlugin
    {
        public const string PluginGUID = "com.jotunn.valheimmod";
        public const string PluginName = "valheimmod";
        public const string PluginVersion = "0.0.1";
        public static bool SpecialJumpTriggered = false; // Flag to indicate if the special jump key is pressed down
        public static int SpecialJumpForce = 15; // Set the jump force for the special jump
        public static int DefaultJumpForce = 8; // Set the default jump force
        public static CustomStatusEffect JumpSpecialEffect; // Custom status effect for the special jump
        public static CustomStatusEffect JumpPendingSpecialEffect; // Custom status effect for the special jump
        public static Texture2D TestTex;
        public static bool blockAttackThisFrame = false;

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private void AddStatusEffects()
        {
            StatusEffect effect = ScriptableObject.CreateInstance<StatusEffect>();
            StatusEffect pendeffect = ScriptableObject.CreateInstance<StatusEffect>();
            effect.name = "SpecialJumpEffect";
            effect.m_name = "$special_jumpeffect";
            effect.m_tooltip = "$special_jumpeffect_tooltip";
            effect.m_icon = Sprite.Create(TestTex, new Rect(0, 0, TestTex.width, TestTex.height), new Vector2(0.5f, 0.5f));
            effect.m_startMessageType = MessageHud.MessageType.Center;
            //effect.m_startMessage = "$special_jumpeffect_start";
            effect.m_stopMessageType = MessageHud.MessageType.Center;
            effect.m_ttl = 10f;
            effect.m_cooldownIcon = effect.m_icon;
            JumpSpecialEffect = new CustomStatusEffect(effect, fixReference: false);

            pendeffect.name = "PendingSpecialJumpEffect";
            pendeffect.m_name = "$pending_special_jumpeffect";
            pendeffect.m_tooltip = "$special_jumpeffect_tooltip";
            pendeffect.m_icon = Sprite.Create(TestTex, new Rect(0, 0, TestTex.width, TestTex.height), new Vector2(0.5f, 0.5f));
            pendeffect.m_startMessageType = MessageHud.MessageType.Center;
            pendeffect.m_startMessage = "$pending_special_jumpeffect_start";
            pendeffect.m_stopMessageType = MessageHud.MessageType.Center;
            pendeffect.m_stopMessage = "$pending_special_jumpeffect_stop";
            pendeffect.m_ttl = 0f; // No TTL for pending effect
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
            } if (leafPuffHeathPrefab != null)
            {
                effectList.Add(new EffectList.EffectData
                {
                    m_prefab = leafPuffHeathPrefab,
                    m_enabled = true,
                    m_attach = true,
                    m_follow = true,
                });
            }if (ghostDeathPrefab != null)
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


            pendeffect.m_startEffects = new EffectList
            {
                m_effectPrefabs = effectList.ToArray()
            };

            // Unsubscribe so it only runs once
            PrefabManager.OnPrefabsRegistered -= AddStatusEffects;
            JumpPendingSpecialEffect = new CustomStatusEffect(pendeffect, fixReference: false);

        }
        public class ModInput
        {
            public static ConfigEntry<KeyCode> SpecialRadialKeyConfig;
            public static ConfigEntry<InputManager.GamepadButton> SpecialRadialGamepadConfig;
            public static ButtonConfig SpecialRadialButton { get; private set; }

            public static void AddInputs(ConfigFile config)
            {
                SpecialRadialKeyConfig = config.Bind(
                   "Controls",
                   "SpecialJumpKey",
                   KeyCode.H,
                   new ConfigDescription("Key to activate special jump")
                );
                SpecialRadialGamepadConfig = config.Bind(
                   "Controls",
                   "SpecialJumpKey Gamepad",
                   InputManager.GamepadButton.DPadUp,
                   new ConfigDescription("Gamepad button to activate special jump")
                );

                // Register the key with Jotunn's InputManager
                SpecialRadialButton = new ButtonConfig
                {
                    Name = "SpecialRadialButton",
                    Key = SpecialRadialKeyConfig.Value,
                    Config = SpecialRadialKeyConfig,
                    GamepadConfig = SpecialRadialGamepadConfig,
                    HintToken = "$special_jump",
                    BlockOtherInputs = true,
                };
                InputManager.Instance.AddButton("com.jotunn.valheimmod", SpecialRadialButton);
            }
            public static bool IsSpecialRadialButtonHeld()
            {
                // Only check input if the game is focused and not in a background thread
                //return UnityEngine.Input.GetKey(KeyCode.Space);
                //bool hadPending = false;
                //RadialAbility radial_ability = GetRadialAbility();
                //string ability_name = radial_ability.ToString();
                //if (ability_name != "None")
                //{
                //    Jotunn.Logger.LogInfo($"Radial ability: {ability_name}"); // Log the radial ability for debugging
                //}
                //if ((ZInput.GetButton("Jump") && Player.m_localPlayer.m_seman.HaveStatusEffect(JumpPendingSpecialEffect.StatusEffect.m_nameHash)))
                //{
                //    hadPending = true;
                //    Jotunn.Logger.LogInfo("Special jump button is pressed down");
                //    Jotunn.Logger.LogInfo($"JumpPendingSpecialEffect StatusEffect Duration: {valheimmod.JumpPendingSpecialEffect.StatusEffect.GetDuration()}");
                //    Jotunn.Logger.LogInfo($"JumpPendingSpecialEffect StatusEffect IsDone: {valheimmod.JumpPendingSpecialEffect.StatusEffect.IsDone()}");
                //    if (Player.m_localPlayer.m_seman.HaveStatusEffect(JumpPendingSpecialEffect.StatusEffect.m_nameHash) && !Player.m_localPlayer.m_seman.HaveStatusEffect(JumpSpecialEffect.StatusEffect.m_nameHash))
                //    {
                //        Jotunn.Logger.LogInfo("Removing JumpPendingSpecialEffect status effect and adding JumpSpecialEffect status effect");
                //        Player.m_localPlayer.m_seman.RemoveStatusEffect(JumpPendingSpecialEffect.StatusEffect.m_nameHash, false);
                //        Player.m_localPlayer.m_seman.AddStatusEffect(valheimmod.JumpSpecialEffect.StatusEffect, false);
                //        SpecialJumpTriggered = true;
                //        Jotunn.Logger.LogInfo($"SpecialJumpTriggered1 = {SpecialJumpTriggered}");
                //    }

                //}
                //if (ability_name == "SuperJump")
                //{
                    
                //    if (!Player.m_localPlayer.m_seman.HaveStatusEffect(JumpSpecialEffect.StatusEffect.m_nameHash))
                //    {
                //        Jotunn.Logger.LogInfo("Adding JumpPendingSpecialEffect status effect");
                //        Player.m_localPlayer.m_seman.AddStatusEffect(valheimmod.JumpPendingSpecialEffect.StatusEffect, true);
                //    }
                //    if ((ZInput.GetButton("Jump") && hadPending))
                //    {
                //        Jotunn.Logger.LogInfo("Normal jump key is held down, triggering jump action");
                //        SpecialJumpTriggered = true;
                //        Jotunn.Logger.LogInfo($"SpecialJumpTriggered2 = {SpecialJumpTriggered}");
                //        return ZInput.GetButton("Jump");
                //    }
                //    //SetRadialAbility(0);
                //    //SetRadialAbility(currentHighlightedIndex + 1);
                //    //CloseRadialMenu();
                //    return ZInput.GetButton(ModInput.SpecialRadialButton.Name);
                //}
                if (ZInput.GetButton(ModInput.SpecialRadialButton.Name))
                {
                    ShowRadialMenu();
                }
                return ZInput.GetButton(ModInput.SpecialRadialButton.Name);
            }


            public static bool CallPendingSpecialJump()
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


            public static void CallSpecialJump()
            {
                // if the player presses the jump button when they have the jump pending buff, give super jump effect
                if ((ZInput.GetButton("Jump") && Player.m_localPlayer.m_seman.HaveStatusEffect(JumpPendingSpecialEffect.StatusEffect.m_nameHash)))
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
            public static void CallPendingAbilities()
            {
                CallPendingSpecialJump();
            }

            public static void CallSpecialAbilities()
            {
                CallSpecialJump();
            }
        }

        private void AddInputs()
        {
            // This method is called to add custom inputs to the game
            // You can add more inputs here if needed
            Config.SaveOnConfigSet = true;
            ModInput.AddInputs(Config);
        }

        private void LoadAssets()
        {
            // https://valheim-modding.github.io/Jotunn/tutorials/asset-loading.html
            string modPath = Path.GetDirectoryName(Info.Location);

            // Load texture from filesystem
            TestTex = AssetUtils.LoadTexture(Path.Combine(modPath, "Assets/Untitled.jpg"));
            Sprite TestSprite = Sprite.Create(TestTex, new Rect(0f, 0f, TestTex.width, TestTex.height), Vector2.zero);

            // Load asset bundle from filesystem
            //TestAssets = AssetUtils.LoadAssetBundle(Path.Combine(modPath, "Assets/jotunnlibtest"));
            //Jotunn.Logger.LogInfo(TestAssets);

            // Print Embedded Resources
            Jotunn.Logger.LogInfo($"Embedded resources: {string.Join(", ", typeof(valheimmod).Assembly.GetManifestResourceNames())}");

            // Load asset bundles from embedded resources
        }

        private void AddLocs()
        {
            // Use the instance of the CustomLocalization object instead of trying to call it statically  
            Localization.AddTranslation("English", new Dictionary<string, string>
           {
               {"special_jumpeffect", "Super jump" },
               {"pending_special_jumpeffect", "Super jump" },
               {"pending_special_jumpeffect_start", "You feel lighter" },
               {"pending_special_jumpeffect_stop", "You feel heavier" },
               {"special_jumpeffect_tooltip", "A wind gust aids you." },
           });
        }

        private void Awake()
        {
            // Jotunn comes with its own Logger class to provide a consistent Log style for all mods using it
            Jotunn.Logger.LogInfo("valheimmod has landed");
            Harmony harmony = new Harmony(PluginGUID);
            harmony.PatchAll();
            LoadAssets();
            AddLocs();
            AddInputs();
            PrefabManager.OnPrefabsRegistered += AddStatusEffects;


            // To learn more about Jotunn's features, go to
            // https://valheim-modding.github.io/Jotunn/tutorials/overview.html
        }
        private void Update()
        {
            if (ZInput.instance != null)
            {
                if (ModInput.IsSpecialRadialButtonHeld() && Player.m_localPlayer.IsOnGround())
                {   
                    //ShowRadialMenu();
                    Jotunn.Logger.LogInfo("Special jump key is held down, triggering jump action");
                }
                if (isRadialMenuOpen)
                {
                    //Cursor.lockState = CursorLockMode.None;
                    //Cursor.visible = true;
                    UpdateRadialHighlight();
                    if (Input.GetMouseButtonDown(0) && currentHighlightedIndex >= 0)
                    {
                        SetRadialAbility(currentHighlightedIndex + 1);
                        Jotunn.Logger.LogInfo($"Radial ability set to: {RadialAbilityMap[currentHighlightedIndex + 1]}");
                        ModInput.CallPendingAbilities();
                        CloseRadialMenu();
                    }
                    // Optional: close on right click
                    if (Input.GetMouseButtonDown(1))
                    {
                        // Optional: close on right click
                        if (Input.GetMouseButtonDown(1))
                        {
                            CloseRadialMenu();
                        }
                    }
                }
                if (!isRadialMenuOpen)
                {
                    ModInput.CallSpecialAbilities();
                }
            }
        }

    public static class JumpState
    {
        public static Dictionary<Character, bool> SpecialJumpActive = new Dictionary<Character, bool>();
    }

    [HarmonyPatch(typeof(Character), nameof(Character.Jump))]
    class Jump_Patch
    {
        static void Prefix(Character __instance)
        {
            if (__instance.IsPlayer())
            {
                bool specialJump = false;
                if (Player.m_localPlayer != null && Player.m_localPlayer == __instance)
                {
                    specialJump = valheimmod.SpecialJumpTriggered;
                }
                JumpState.SpecialJumpActive[__instance] = specialJump;
                Jotunn.Logger.LogInfo($"Jump force {__instance.m_jumpForce}");
                if (specialJump)
                {
                    Jotunn.Logger.LogInfo("Jumped with special jump key");
                    __instance.m_jumpForce = SpecialJumpForce;
                }
                else
                {
                    Jotunn.Logger.LogInfo("Jumped with default jump key");
                    __instance.m_jumpForce = DefaultJumpForce; // Default jump force
                }
                valheimmod.SpecialJumpTriggered = false; // Reset the flag
            }
            bool s2;
            s2 = JumpState.SpecialJumpActive.TryGetValue(__instance, out bool sj) ? sj : false;
            Jotunn.Logger.LogInfo($"prefix jump checking fall damage. Special jump active: {s2}");
        }
    }

    [HarmonyPatch(typeof(Character), "UpdateGroundContact")]
    class Character_Landing_Patch
    {
        // Track previous grounded state per character
        static Dictionary<Character, bool> wasOnGround = new Dictionary<Character, bool>();

        static void Postfix(Character __instance)
        {
            if (!__instance.IsPlayer() || !JumpState.SpecialJumpActive.TryGetValue(__instance, out bool specialJump) || !specialJump) { 
                return;
            }
            Jotunn.Logger.LogInfo($"Character {__instance.m_name} is checking ground contact.");
            bool isOnGround = __instance.IsOnGround();

            // Get previous state (default to true to avoid false positives on first frame)
            bool prevOnGround = wasOnGround.TryGetValue(__instance, out var prev) ? prev : true;
            var currentVel = __instance.m_body.velocity;
            // Detect landing: was not on ground, now is on ground
            if (!prevOnGround && isOnGround)
            {
                if (__instance.IsPlayer())
                {
                    Jotunn.Logger.LogInfo("Player has landed!");
                    Vector3 targetVel = new Vector3(currentVel.x, 0f, currentVel.z);
                    __instance.m_body.velocity = targetVel;
                    JumpState.SpecialJumpActive[__instance] = false;
                }
            }
            if (!prevOnGround && !isOnGround && __instance.m_body.velocity.y < -5f)
            {

                float gentleFallSpeed = -6f; // Set to a gentle fall speed
                Vector3 targetVel = new Vector3(currentVel.x, gentleFallSpeed, currentVel.z);
                __instance.m_body.velocity = Vector3.Lerp(currentVel, targetVel, 0.1f);
                Jotunn.Logger.LogInfo($"Gentle falling velocity: {__instance.m_body.velocity}");
            }

            // Update state
            wasOnGround[__instance] = isOnGround;
            bool s2;
            s2 = JumpState.SpecialJumpActive.TryGetValue(__instance, out bool sj) ? sj : false;
            Jotunn.Logger.LogInfo($"postfix updateground checking fall damage. Special jump active: {s2}");

        }
    }
}
    [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyFallDamage))]
    class NoFallDamage_SEMan_Patch
    {
        static void Prefix(SEMan __instance, float baseDamage, ref float damage)
        {
            // Get the Character this SEMan belongs to
            Character character = __instance.m_character;
            bool s2;
            s2 = JumpState.SpecialJumpActive.TryGetValue(character, out bool sj) ? sj : false;
            Jotunn.Logger.LogInfo($"nofalldmg checking fall damage. Special jump active: {s2}");
            if (character != null && character.IsPlayer() &&
                JumpState.SpecialJumpActive.TryGetValue(character, out bool specialJump) && specialJump)
            {
                damage = 0f;
                Jotunn.Logger.LogInfo("Fall damage prevented by patch!");
            }
        }
    }


    [HarmonyPatch(typeof(StatusEffect), "UpdateStatusEffect")]
    class StatusEffect_Update_Patch
    {
        private static Dictionary<Character, float> lastVfxTime = new Dictionary<Character, float>();
        private static void SpecialJumpSEPatch(StatusEffect __instance, Character ___m_character)
        {
            if (__instance.name == "PendingSpecialJumpEffect" && ___m_character != null && ___m_character.IsPlayer())
            {
                // Only spawn if enough time has passed (e.g., 1 second)
                float now = Time.time;
                if (!lastVfxTime.TryGetValue(___m_character, out float lastTime) || now - lastTime > 1f)
                {
                    lastVfxTime[___m_character] = now;

                    Jotunn.Logger.LogInfo("Pending special jump effect is active, spawning VFX");
                    var leafPuffPrefab = ZNetScene.instance.GetPrefab("vfx_bush_leaf_puff");
                    if (leafPuffPrefab != null)
                    {
                        var vfx = UnityEngine.Object.Instantiate(leafPuffPrefab, ___m_character.transform.position, Quaternion.identity);
                        vfx.transform.SetParent(___m_character.transform);
                    }
                }
            }
        }
        static void Postfix(StatusEffect __instance, Character ___m_character)
        {
            SpecialJumpSEPatch(__instance, ___m_character);
        }
    }

    [HarmonyPatch(typeof(Player), "Update")]
    class Player_CameraBlock_RadialMenu_Patch
    {
        static bool wasRadialMenuOpen = false;

        static void Prefix(Player __instance)
        {
            if (valheimmod.isRadialMenuOpen)
            {
                if (ZInput.instance != null && ZInput.instance.m_mouseDelta != null)
                {
                    ZInput.instance.m_mouseDelta.Disable();
                }
                wasRadialMenuOpen = true;
            }
            else if (wasRadialMenuOpen)
            {
                // Re-enable mouse look when menu closes
                if (ZInput.instance != null && ZInput.instance.m_mouseDelta != null)
                {
                    ZInput.instance.m_mouseDelta.Enable();
                }
                wasRadialMenuOpen = false;
            }
        }
    }

    [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
    class Player_AttackInput_RadialBlock_Patch
    {
        static bool Prefix(Player __instance, float dt)
        {
            if (valheimmod.radialMenuInstance != null && valheimmod.radialMenuInstance.activeSelf)
            {
                Jotunn.Logger.LogInfo("Attack input blocked: radial menu is open.");
                return false;
            }
            if (valheimmod.blockAttackThisFrame)
            {
                Jotunn.Logger.LogInfo("Attack input blocked: just closed radial menu.");
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(Hud), nameof(Hud.InRadial))]
    class Hud_InRadial_RadialMenu_Patch
    {
        static void Postfix(ref bool __result)
        {
            if (valheimmod.isRadialMenuOpen)
            {
                __result = true;
            }
        }
    }
}