using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using MonoMod.Utils;
using UnityEngine;
using valheimmod;

namespace valheimmod
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    //[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class valheimmod : BaseUnityPlugin
    {
        public const string PluginGUID = "com.jotunn.valheimmod";
        public const string PluginName = "valheimmod";
        public const string PluginVersion = "0.0.1";

        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private void Awake()
        {
            // Jotunn comes with its own Logger class to provide a consistent Log style for all mods using it
            Jotunn.Logger.LogInfo("valheimmod has landed");
            Harmony harmony = new Harmony(PluginGUID);
            harmony.PatchAll();

            // To learn more about Jotunn's features, go to
            // https://valheim-modding.github.io/Jotunn/tutorials/overview.html
        }
    }
    public static class InputHelper
    {
        public static bool IsSpaceHeld()
        {
            // Only check input if the game is focused and not in a background thread
            return UnityEngine.Input.GetKey(KeyCode.Space);
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
                bool specialJump = InputHelper.IsSpaceHeld();
                JumpState.SpecialJumpActive[__instance] = specialJump;
                if (specialJump)
                {
                    Jotunn.Logger.LogInfo("Jumped with space held");
                    __instance.m_jumpForce = 15;
                }
            }

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
                return;
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
        if (character != null && character.IsPlayer() &&
            JumpState.SpecialJumpActive.TryGetValue(character, out bool specialJump) && specialJump)
        {
            damage = 0f;
            Jotunn.Logger.LogInfo("Fall damage prevented by patch!");
        }
    }
}