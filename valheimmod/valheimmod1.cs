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
using PlayFab.ClientModels;
using UnityEngine;
using UnityEngine.UI;
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
        public static valheimmod Instance;
        public static bool SpecialJumpTriggered = false; // Flag to indicate if the special jump key is pressed down
        public static int SpecialJumpForce = 15; // Set the jump force for the special jump
        public static int DefaultJumpForce = 8; // Set the default jump force
        public static CustomStatusEffect JumpSpecialEffect; // Custom status effect for the special jump
        public static CustomStatusEffect JumpPendingSpecialEffect; // Custom status effect for the special jump
        public static CustomStatusEffect PendingTeleportHomeEffect; // Custom status effect for the teleport home pending state
        public static CustomStatusEffect TeleportHomeEffect; // Custom status effect for the teleport home
        public static CustomStatusEffect SpectralArrowEffect;
        public static CustomStatusEffect SpectralArrowCDEffect;
        public static Texture2D SpecialJumpTexture;
        public static Texture2D TeleportTexture;
        public static Texture2D SpectralArrowTexture;
        public static Sprite[] RadialSegmentSprites;
        public static Sprite[] RadialSegmentHighlightSprites;
        public static bool allowForsakenPower = true;
        private float dpadDownPressTime = -1f;
        private bool dpadDownHeld = false;
        private bool forsakenPowerTriggered = false;
        private const float holdThreshold = 0.35f; // seconds
        public Coroutine teleportCountdownCoroutine;
        public bool teleportCancelled = false;
        public bool teleportPending = false;
        public string teleportEndingMsg = "Traveling...";
        public static int currentDay = 0;

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        public class ModInput
        {
            public static ConfigEntry<KeyCode> SpecialRadialKeyConfig;
            public static ConfigEntry<InputManager.GamepadButton> SpecialRadialGamepadCloseConfig;
            public static ConfigEntry<InputManager.GamepadButton> SpecialRadialGamepadConfig;
            public static ButtonConfig SpecialRadialButton { get; private set; }

            public static void AddInputs(ConfigFile config)
            {
                SpecialRadialKeyConfig = config.Bind(
                   "Controls",
                   "SpecialRadialKey",
                   KeyCode.H,
                   new ConfigDescription("Key to activate special jump")
                );
                SpecialRadialGamepadConfig = config.Bind(
                   "Controls",
                   "SpecialRadialKey Gamepad",
                   InputManager.GamepadButton.DPadDown,
                   new ConfigDescription("Gamepad button to activate special jump")
                );

                SpecialRadialGamepadCloseConfig = config.Bind(
                   "Controls",
                   "SpecialRadialKeyClose",
                   InputManager.GamepadButton.ButtonEast,
                   new ConfigDescription("Gamepad button to close the radial menu")
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
                if (ZInput.GetButton(ModInput.SpecialRadialButton.Name) && !RadialMenuIsOpen)
                {
                    ShowRadialMenu();
                }
                return ZInput.GetButton(ModInput.SpecialRadialButton.Name);
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
            SpecialJumpTexture = AssetUtils.LoadTexture(Path.Combine(modPath, "Assets/specialjump.png"));
            TeleportTexture = AssetUtils.LoadTexture(Path.Combine(modPath, "Assets/teleport.png"));
            SpectralArrowTexture = AssetUtils.LoadTexture(Path.Combine(modPath, "Assets/teleport.png"));
            if (SpecialJumpTexture == null)
            {
                Jotunn.Logger.LogError("Failed to load SpecialJumpTexture! Check if the PNG is valid and not corrupted.");
            }
            RadialSegmentSprites = new Sprite[4];
            RadialSegmentHighlightSprites = new Sprite[4];
            string[] segmentFiles = { "radial_n.png", "radial_e.png", "radial_s.png", "radial_w.png" };
            string[] segmentHighlightFiles = { "rh_n.png", "rh_e.png", "rh_s.png", "rh_w.png" };
            for (int i = 0; i < segmentFiles.Length; i++)
            {
                var tex = AssetUtils.LoadTexture(Path.Combine(modPath, $"Assets", segmentFiles[i]));
                RadialSegmentSprites[i] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                var texHighlight = AssetUtils.LoadTexture(Path.Combine(modPath, $"Assets", segmentHighlightFiles[i]));
                RadialSegmentHighlightSprites[i] = Sprite.Create(texHighlight, new Rect(0, 0, texHighlight.width, texHighlight.height), new Vector2(0.5f, 0.5f));
            }
            //TestTex = AssetUtils.LoadTexture(Path.Combine(modPath, "Assets/Untitled.jpg"));
            Sprite TestSprite = Sprite.Create(SpecialJumpTexture, new Rect(0f, 0f, SpecialJumpTexture.width, SpecialJumpTexture.height), Vector2.zero);

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
                {   "special_jumpeffect", "Super jump" },
                {   "pending_special_jumpeffect", "Super jump" },
                {   "pending_special_jumpeffect_start", "You feel lighter" },
                {   "pending_special_jumpeffect_stop", "You feel heavier" },
                {   "special_jumpeffect_tooltip", "A wind gust aids you." },
                {   "pending_teleport_effect", "Hearth"},
                {   "special_teleport_tooltip", "Teleporting Home"},
                {   "pending_teleporteffect_stop_cancelled", "Teleport Cancelled"},
                {   "pending_teleporteffect_stop_complete", "Traveling..."},
                {   "$teleport_effect", "Hearth"},
                {   "$special_teleport_cd_tooltip", "Cannot instant travel home right now."},
                {   "teleporteffect_start", "Traveling.."},
                {   "$teleporteffect_stop", "You can now hearth home."},
                {   "$teleporteffect_cd", "You can not hearth home until tomorrow."},
                {   "spectral_arrow_effect", "Spectral Arrow"},
                {   "spectral_arrow_start", "Choose your next 3 shots wisely.."},
                {   "spectral_arrow_cd_tooltip", "You are worn out from the last 3 spectral arrows."},
                {   "spectral_arrow_effect_tooltip", "Your next 3 shots will be spectral arrows."},
                {   "spectral_arrow_cd_start", "You can not fire anymore spectral arrows right now."},
                {   "spectral_arrow_cd_stop", "You can now fire spectral arrows."},
            });
        }

        private void Awake()
        {
            // Jotunn comes with its own Logger class to provide a consistent Log style for all mods using it
            Jotunn.Logger.LogInfo("valheimmod has landed");
            Harmony harmony = new Harmony(PluginGUID);
            Instance = this;
            harmony.PatchAll();
            LoadAssets();
            AddLocs();
            AddInputs();
            PrefabManager.OnPrefabsRegistered += ModAbilitiesEffects.AddStatusEffects;


            // To learn more about Jotunn's features, go to
            // https://valheim-modding.github.io/Jotunn/tutorials/overview.html
        }

        private void Update()
        {
            if (ZInput.instance != null)
            {
                // todo check that JoyGP and SpecialRadialButton are the same button before doing this to help with user remap
                string guardianPowerButton = "JoyGP";
                // Check if both are mapped to the same physical button

                if (ZInput.IsGamepadActive())
                {
                    allowForsakenPower = false; // default to false, only set to true if we detect the button press

                    if (ZInput.GetButtonDown(guardianPowerButton))
                    {
                        dpadDownPressTime = Time.time;
                        dpadDownHeld = true;
                        forsakenPowerTriggered = false;
                    }
                    if (dpadDownHeld)
                    {
                        // If held long enough and not yet triggered, activate Forsaken Power
                        if (!forsakenPowerTriggered && (Time.time - dpadDownPressTime) > holdThreshold)
                        {
                            if (Player.m_localPlayer != null)
                            {
                                allowForsakenPower = true;
                                Player.m_localPlayer.StartGuardianPower(); // Use the guardian power
                                forsakenPowerTriggered = true;
                            }
                        }

                        // If released before threshold, show radial menu
                        if (ZInput.GetButtonUp(ModInput.SpecialRadialButton.Name))
                        {
                            dpadDownHeld = false;
                            if (!forsakenPowerTriggered && (Time.time - dpadDownPressTime) <= holdThreshold)
                            {
                                if (!RadialMenuIsOpen)
                                {
                                    ShowRadialMenu();
                                }
                                else
                                {
                                    CloseRadialMenu();
                                }
                            }
                        }
                    }
                }


                else if (ZInput.GetButtonDown(ModInput.SpecialRadialButton.Name))
                {
                    if (!RadialMenuIsOpen)
                    {
                        ShowRadialMenu();
                    }
                    else
                    {
                        // other keys to close are handled in the HandleRadialMenu method
                        CloseRadialMenu();
                    }
                }
                if (RadialMenuIsOpen)
                {
                    HandleRadialMenu();
                }
                if (!RadialMenuIsOpen)
                {
                    ModAbilities.CallSpecialAbilities();
                }
                if (TeleportHomeEffect?.StatusEffect != null)
                {
                    int day = EnvMan.instance.GetDay();
                    if (currentDay != day)
                    {
                        if (Player.m_localPlayer != null && Player.m_localPlayer.IsPlayer())
                        {
                            if (Player.m_localPlayer.m_seman.HaveStatusEffect(TeleportHomeEffect.StatusEffect.m_nameHash))
                            {
                                Player.m_localPlayer.m_seman.RemoveStatusEffect(TeleportHomeEffect.StatusEffect.m_nameHash, false);

                            }
                            currentDay = day;
                        }
                    }
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
                    //valheimmod.SpecialJumpTriggered = false; // this flag is reset in the patch for fall damage to prevent fall damage from coming back early
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
                if (!__instance.IsPlayer() || !JumpState.SpecialJumpActive.TryGetValue(__instance, out bool specialJump) || !specialJump)
                {
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

        [HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyFallDamage))]
        class NoFallDamage_SEMan_Patch
        {
            static void Prefix(SEMan __instance, float baseDamage, ref float damage)
            {
                // Get the Character this SEMan belongs toSpecialJumpTriggered
                Character character = __instance.m_character;
                bool s2;
                s2 = JumpState.SpecialJumpActive.TryGetValue(character, out bool sj) ? sj : false;
                Jotunn.Logger.LogInfo($"nofalldmg checking fall damage. Special jump active: {s2}");
                if (character != null && character.IsPlayer() &&
                    JumpState.SpecialJumpActive.TryGetValue(character, out bool specialJump) && specialJump)
                {
                    damage = 0f;
                    Jotunn.Logger.LogInfo("Fall damage prevented by patch!");
                    SpecialJumpTriggered = false; // Reset the flag here instead of in jump to prevent pre-emptive fall damage 

                }
                // TODO: Player will still take fall damage if they spam the jump button even in the air 
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
                if (RadialMenuIsOpen)
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
                if (radialMenuInstance != null && radialMenuInstance.activeSelf)
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Attack), "ProjectileAttackTriggered")]
        class Player_ProjectileAttackTriggered_SpectralArrow_Patch
        {
            static void Postfix(Attack __instance)
            {
                var character = __instance.m_character as Player;
                if (character == null)
                    return;

                if (!(character.m_seman?.HaveStatusEffect(valheimmod.SpectralArrowEffect.StatusEffect.m_nameHash) ?? false))
                    return;

                var weapon = character.GetCurrentWeapon();
                if (weapon != null && weapon.m_shared != null && weapon.m_shared.m_skillType == Skills.SkillType.Bows)
                {
                    // Track shots fired
                    SpectralArrow.ShotsFired[character]++;
                    Jotunn.Logger.LogInfo($"Spectral Arrow: {character.m_name} has fired {SpectralArrow.ShotsFired[character]} shots.");
                }
            }
        }
        [HarmonyPatch(typeof(Player), "PlayerAttackInput")]
        class Player_AttackInput_Bow_Patch
        {
            static void Prefix(Player __instance, float dt)
            {
                // Only proceed if player has the pending spectral arrow effect
                if (!(__instance.m_seman?.HaveStatusEffect(SpectralArrowEffect.StatusEffect.m_nameHash) ?? false))
                    return;

                var weapon = __instance.GetCurrentWeapon();
                if (weapon != null && weapon.m_shared != null && weapon.m_shared.m_skillType == Skills.SkillType.Bows)
                {
                    if (!__instance.m_seman.HaveStatusEffect(SpectralArrowEffect.StatusEffect.m_nameHash))
                    {
                        SpectralArrow.defaultVelocity = weapon.m_shared.m_attack.m_projectileVel;
                        Jotunn.Logger.LogInfo($"Default arrow velocity set to: {SpectralArrow.defaultVelocity}");
                    }
                    // Store previous skill if not already stored
                    if (!SpectralArrow.PreviousSkill.ContainsKey(__instance))
                    {
                        Skills.Skill defaultSkill = __instance.m_skills.GetSkill(Skills.SkillType.Bows);
                        SpectralArrow.PreviousSkill[__instance] = defaultSkill.m_level;
                        // Boost skill
                        __instance.m_skills.GetSkill(Skills.SkillType.Bows).m_level = 100f; // Set to desired fast value

                    }

                    // Boost arrow velocity (set on the weapon for this shot)
                    weapon.m_shared.m_attack.m_projectileVel = SpectralArrow.specialVelocity; // Set to desired fast value

                    // Track shots fired
                    if (!SpectralArrow.ShotsFired.ContainsKey(__instance))
                        SpectralArrow.ShotsFired[__instance] = 0;

                    // After 3 shots, revert skill and velocity, remove effect
                    if ((SpectralArrow.ShotsFired[__instance] >= 3) && __instance.m_seman.HaveStatusEffect(SpectralArrowEffect.StatusEffect.m_nameHash))
                    {
                        Jotunn.Logger.LogInfo($"Spectral Arrow: {__instance.m_name} has fired {SpectralArrow.ShotsFired[__instance]} shots, reverting skill and velocity.");
                        SpectralArrow.Cancel(__instance, weapon);

                        Jotunn.Logger.LogInfo("Spectral Arrow: Effect ended, reverted skill and velocity.");
                    }
                }
            }

            [HarmonyPatch(typeof(Hud), nameof(Hud.InRadial))]
            class Hud_InRadial_RadialMenu_Patch
            {
                static void Postfix(ref bool __result)
                {
                    if (RadialMenuIsOpen)
                    {
                        __result = true;
                    }
                }
            }

            [HarmonyPatch(typeof(Player), nameof(Player.StartGuardianPower))]
            class Player_UseGuardianPower_Patch
            {
                static bool Prefix(Player __instance)
                {
                    if (ZInput.IsGamepadActive())
                    {
                        if (!allowForsakenPower)
                        {
                            Jotunn.Logger.LogInfo("Forsaken power use blocked by radial menu");
                            // Prevent the guardian power from being used
                            return false;
                        }
                        else
                        {
                            Jotunn.Logger.LogInfo("Forsaken power use allowed by radial menu");
                            // allowForsakenPower = false; // Reset the flag after use
                            return true;
                        }
                    }
                    return true;
                }

                // Or use Postfix if you want to run code after activation
                // static void Postfix(Player __instance) { ... }
            }
            [HarmonyPatch(typeof(Player), "Awake")]
            class Player_Awake_DayTracker_Patch
            {
                static void Postfix(Player __instance)
                {
                    if (__instance.IsPlayer())
                    {
                        int day = EnvMan.instance != null ? EnvMan.instance.GetDay() : 0;
                        currentDay = day;
                        Jotunn.Logger.LogInfo($"Player loaded in on day {currentDay}");
                    }
                }
            }
        }
    }
}