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
        public static CustomStatusEffect PendingTeleportHomeEffect; // Custom status effect for the special jump
        public static CustomStatusEffect TeleportHomeEffect; // Custom status effect for the special jump
        public static Texture2D SpecialJumpTexture;
        public static Texture2D TeleportTexture;
        public static Sprite[] RadialSegmentSprites;
        public static Sprite[] RadialSegmentHighlightSprites;
        public static bool allowForsakenPower = false;
        private float dpadDownPressTime = -1f;
        private bool dpadDownHeld = false;
        private bool forsakenPowerTriggered = false;
        private const float holdThreshold = 0.35f; // seconds
        public Coroutine teleportCountdownCoroutine;
        public bool teleportCancelled = false;
        public bool teleportPending = false;
        public string teleportEndingMsg = "Traveling...";

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private void AddStatusEffects()
        {
            StatusEffect effect = ScriptableObject.CreateInstance<StatusEffect>();
            StatusEffect pendeffect = ScriptableObject.CreateInstance<StatusEffect>();
            StatusEffect pendteleporteffect = ScriptableObject.CreateInstance<StatusEffect>();
            StatusEffect teleporteffect = ScriptableObject.CreateInstance<StatusEffect>();
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
            teleporteffect.m_ttl = 60f; // No TTL for pending effect
            effect.m_cooldownIcon = effect.m_icon;
            PendingTeleportHomeEffect = new CustomStatusEffect(pendteleporteffect, fixReference: false);
            TeleportHomeEffect = new CustomStatusEffect(teleporteffect, fixReference: false);


        }
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

           

            //public static bool CallPendingTreeCut()
            //{
            //    RadialAbility radial_ability = GetRadialAbility();
            //    string ability_name = radial_ability.ToString();
            //    if (ability_name == RadialAbility.SuperJump.ToString())
            //    {
            //        if (!Player.m_localPlayer.m_seman.HaveStatusEffect(TreeCutPendingSpecialEffect.StatusEffect.m_nameHash))
            //        {
            //            Jotunn.Logger.LogInfo("Adding TreeCutPendingSpecialEffect status effect");
            //            Player.m_localPlayer.m_seman.AddStatusEffect(valheimmod.TreeCutPendingSpecialEffect.StatusEffect, true);
            //        }
            //        return ZInput.GetButton(ModInput.SpecialRadialButton.Name);
            //    }
            //    return false;
            //}

            public static void CallPendingAbilities()
            {
                CallPendingSpecialJump();
                CallPendingTeleportHome();
            }

            public static void CallSpecialAbilities()
            {
                CallSpecialJump();
                
            }
        }

        public static bool CallPendingTeleportHome()
        {
            // If user picks the teleport home ability in radial, teleport them home
            RadialAbility radial_ability = GetRadialAbility();
            string ability_name = radial_ability.ToString();
            if (radial_ability != RadialAbility.None)
            {
                Jotunn.Logger.LogInfo($"Radial ability selected: {ability_name}");
                Jotunn.Logger.LogInfo($"RadialAbility to string {RadialAbility.TeleportHome.ToString()}");
            }
            if (ability_name == RadialAbility.TeleportHome.ToString())
            {
                if (!Player.m_localPlayer.m_seman.HaveStatusEffect(PendingTeleportHomeEffect.StatusEffect.m_nameHash))
                {
                    valheimmod.Instance.teleportCancelled = false;
                    valheimmod.Instance.teleportPending = true;
                    Jotunn.Logger.LogInfo("Adding TeleportHomeSpecialEffect status effect");
                    Player.m_localPlayer.m_seman.AddStatusEffect(valheimmod.PendingTeleportHomeEffect.StatusEffect, true);
                    valheimmod.Instance.StartTeleportCountdown(10);

                } else
                {
                    CancelTeleportCountdown();
                }
                    return ZInput.GetButton(ModInput.SpecialRadialButton.Name);
            }
            return false;
        }
        public static void CallTeleportHome()
        {
            //if ((ZInput))
        }

        public static void CancelTeleportCountdown()
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
            if (SpecialJumpTexture == null)
            {
                Jotunn.Logger.LogError("Failed to load SpecialJumpTexture! Check if the PNG is valid and not corrupted.");
            }
            RadialSegmentSprites = new Sprite[4];
            RadialSegmentHighlightSprites = new Sprite[4];
            string[] segmentFiles = { "radial_n.png", "radial_e.png", "radial_s.png", "radial_w.png"};
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
            PrefabManager.OnPrefabsRegistered += AddStatusEffects;


            // To learn more about Jotunn's features, go to
            // https://valheim-modding.github.io/Jotunn/tutorials/overview.html
        }
        public  void StartTeleportCountdown(int seconds)
        {
            if (teleportCountdownCoroutine != null)
            {
                StopCoroutine(teleportCountdownCoroutine);
            }
            teleportCountdownCoroutine = StartCoroutine(TeleportCountdownCoroutine(seconds));
        }

        private System.Collections.IEnumerator TeleportCountdownCoroutine(int seconds)
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
                    teleportCountdownCoroutine = null;
                    yield break;
                }
            }
            teleportCountdownCoroutine = null;

            // Only run this if teleport wasn't cancelled
            if (!teleportCancelled && teleportPending)
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
                teleportPending = false;
            }
        }

        private void Update()
        {
            if (ZInput.instance != null)
            {
                //foreach (var name in ZInput.instance.m_buttons.Keys)
                //{
                //    if (ZInput.GetButtonDown(name))
                //        Jotunn.Logger.LogInfo($"ZInput button pressed: {name}");
                //}

                // todo check that JoyGP and SpecialRadialButton are the same button before doign this
                string guardianPowerButton = "JoyGP";
                string radialButton = ModInput.SpecialRadialButton.Name;
                string radialButtonvalue = ModInput.SpecialRadialButton.GamepadButton.ToString();
                bool sameButtonAsGP = false;
                // Check if both are mapped to the same physical button
                if (ZInput.GetButtonDown("JoyGP"))
                {
                    if (ZInput.GetButtonDown(radialButton)) {
                        sameButtonAsGP = true;
                    }
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
                valheimmod.SpecialJumpTriggered = false; // Reset the flag here instead of in jump to prevent pre-emptive fall damage 

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
            if (valheimmod.RadialMenuIsOpen)
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
            if (valheimmod.RadialMenuIsOpen)
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
            if (!allowForsakenPower)
            {
                Jotunn.Logger.LogInfo("Forsaken power use blocked by radial menu");
                // Prevent the guardian power from being used
                return false;
            }
            else
            {
                Jotunn.Logger.LogInfo("Forsaken power use allowed by radial menu");
                allowForsakenPower = false; // Reset the flag after use
                return true;
            }
        }

        // Or use Postfix if you want to run code after activation
        // static void Postfix(Player __instance) { ... }
    }
}