using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace valheimmod
{
    internal partial class valheimmod : BaseUnityPlugin
    {
        public static GameObject radialMenuInstance;
        public static bool RadialMenuIsOpen = false;
        public static GameObject radialButtonPrefab; // Assign this in the inspector with a Unity UI Button prefab
        public static int radialItemClicked;
        public static int gamepadSelectedIndex = -1;
        private static List<GameObject> radialButtonHighlights = new List<GameObject>();
        private static void SetRadialAbility(int index)
        {
            Debug.Log($"Radial menu option {index} clicked!");
            radialItemClicked = index;
        }

        public enum RadialAbility
        {
            None = 0,
            SuperJump,
            TreeCut,
            MineExplode,
            TeleportHome,
        }

        private static readonly RadialAbility[] RadialAbilityMap = new[]
        {
            RadialAbility.None,      // 0 (not used)
            RadialAbility.SuperJump, // 1
            RadialAbility.TreeCut,  // 2
            RadialAbility.MineExplode,       // 3
            RadialAbility.TeleportHome       // 3
        };

        private static string GetRadialAbilityName(RadialAbility ability)
        {
            return ability switch
            {
                RadialAbility.SuperJump => "Super Jump",
                RadialAbility.TreeCut => "Spectral Axe",
                RadialAbility.MineExplode => "Mine Explosion",
                RadialAbility.TeleportHome => "Hearth",
                _ => "None"
            };
        }

        private static RadialAbility GetRadialAbility()
        {
            if (!RadialMenuIsOpen)
            {
                return RadialAbility.None;
            }
            if (radialItemClicked > 0 && radialItemClicked < RadialAbilityMap.Length)
            {
                Jotunn.Logger.LogInfo($"Radial item clicked: {radialItemClicked}");
                return RadialAbilityMap[radialItemClicked];
            }
            return RadialAbility.None;
        }

        /// <summary>
        /// Closes the radial menu if it is open and blocks attack button status on close
        /// </summary>
        private static void CloseRadialMenu()
        {
            if (radialMenuInstance != null)
            {
                radialMenuInstance.SetActive(false);
                Jotunn.Logger.LogInfo("Radial menu closed.");
            }
            else
            {
                Jotunn.Logger.LogWarning("Radial menu instance is null, cannot close.");
            }
            RadialMenuIsOpen = false;
            // reset radial item clicked
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetRadialAbility(0);
            ZInput.ResetButtonStatus("Attack"); 
        }

        private static List<GameObject> radialButtons = new List<GameObject>();
        private static int currentHighlightedIndex = -1;

        ///<summary>
        /// Updates the radial menu highlight based on gamepad selection
        ///</summary>
        private static void UpdateGamepadHighlight()
        {
            for (int i = 0; i < radialButtons.Count; i++)
            {
                var text = radialButtons[i].GetComponentInChildren<UnityEngine.UI.Text>();
                if (text != null)
                    text.color = (i == gamepadSelectedIndex) ? Color.yellow : Color.white;
            }
        }


        /// <summary>
        /// Shows the radial menu for special abilities
        /// </summary>
        private static void ShowRadialMenu()
        {
            if (radialMenuInstance == null)
            {
                // Create Canvas
                GameObject canvasObj = new GameObject("RadialMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = canvasObj.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                // Create an empty GameObject for the menu
                radialMenuInstance = new GameObject("RadialMenu");
                radialMenuInstance.transform.SetParent(canvasObj.transform, false);


                int segmentCount = 4;
                float buttonRadius = 70f; // Set this to match your asset's segment center distance
                float[] angles = { 90f, 0f, 270f, 180f }; // Adjust if your asset is rotated

                for (int i = 0; i < segmentCount; i++)
                {
                    float angleRad = angles[i] * Mathf.Deg2Rad;
                    Vector2 pos = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * buttonRadius;
                    float textRadius = 160f;
                    Vector2 textPos = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * textRadius;

                    GameObject buttonObj = new GameObject($"Button_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    buttonObj.transform.SetParent(radialMenuInstance.transform, false);
                    var rect = buttonObj.GetComponent<RectTransform>();
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(600, 600); // Use your full radial size

                    var img = buttonObj.GetComponent<Image>();
                    img.sprite = valheimmod.RadialSegmentSprites[i];
                    img.color = new Color(1f, 1f, 1f, 0f);
                    img.color = Color.white;
                    img.alphaHitTestMinimumThreshold = 0.1f;

                    // Add text label
                    GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
                    textObj.transform.SetParent(buttonObj.transform, false);
                    var text = textObj.GetComponent<Text>();
                    text.text = GetRadialAbilityName(RadialAbilityMap.Length > i + 1 ? RadialAbilityMap[i + 1] : RadialAbility.None);
                    text.alignment = TextAnchor.MiddleCenter;
                    text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    text.fontStyle = FontStyle.Bold;
                    text.color = Color.red;
                    text.rectTransform.sizeDelta = new Vector2(180, 90);
                    text.rectTransform.anchoredPosition = textPos; // <-- Offset text outward

                    GameObject highlightObj = new GameObject("Highlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    highlightObj.transform.SetParent(buttonObj.transform, false);
                    highlightObj.transform.SetAsFirstSibling(); // Ensure it's behind
                    var highlightRect = highlightObj.GetComponent<RectTransform>();
                    highlightRect.anchorMin = Vector2.zero;
                    highlightRect.anchorMax = Vector2.one;
                    highlightRect.offsetMin = Vector2.zero;
                    highlightRect.offsetMax = Vector2.zero;
                    var highlightImg = highlightObj.GetComponent<Image>();
                    highlightImg.sprite = valheimmod.RadialSegmentHighlightSprites[i];
                    highlightImg.color = new Color(1f, 1f, 1f, 0.8f); // Adjust alpha as needed
                    highlightObj.SetActive(false); // Hide by default


                    int index = i + 1;
                    var button = buttonObj.GetComponent<Button>();
                    button.GetComponent<Button>().onClick.AddListener(() =>
                    {
                        SetRadialAbility(index);
                        ModInput.CallPendingAbilities(); // If you want to trigger the ability immediately
                        CloseRadialMenu();
                    }

                    );

                    var eventTrigger = buttonObj.AddComponent<EventTrigger>();

                    // PointerEnter
                    var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    entryEnter.callback.AddListener((eventData) =>
                    {
                        var text = buttonObj.GetComponentInChildren<Text>();
                        if (text != null) text.color = Color.yellow;
                        currentHighlightedIndex = i;
                    });
                    eventTrigger.triggers.Add(entryEnter);

                    // PointerExit
                    var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                    entryExit.callback.AddListener((eventData) =>
                    {
                        var text = buttonObj.GetComponentInChildren<Text>();
                        if (text != null) text.color = Color.red;
                        currentHighlightedIndex = -1;
                    });
                    eventTrigger.triggers.Add(entryExit);
                    gamepadSelectedIndex = -1;
                    UpdateGamepadHighlight();

                    radialButtons.Add(buttonObj);
                    radialButtonHighlights.Add(highlightObj);
                }
            }
            else
            {
                radialMenuInstance.SetActive(true);
            }
            RadialMenuIsOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            gamepadSelectedIndex = -1;
            UpdateGamepadHighlight();
        }

        public static void HandleRadialMenu()
        {
        //if (RadialMenuIsOpen)
        //    {
                foreach (var name in ZInput.instance.m_buttons.Keys)
                {
                    if (ZInput.GetButtonDown(name))
                        Jotunn.Logger.LogInfo($"ZInput button pressed: {name}");
                }

                //start
                Vector2 menuCenter = (Vector2)radialMenuInstance.transform.position;
                Vector2 mousePos = Input.mousePosition;
                Vector2 dir = mousePos - menuCenter;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;

                // For 4 segments: 0=East, 1=North, 2=West, 3=South (adjust as needed)
                int hoveredIndex = -1;
                if (dir.magnitude > 30f) // Only highlight if mouse is away from center
                {
                    if (angle >= 45f && angle < 135f) hoveredIndex = 0;      // North
                    else if (angle >= 135f && angle < 225f) hoveredIndex = 3; // West
                    else if (angle >= 225f && angle < 315f) hoveredIndex = 2; // South
                    else hoveredIndex = 1;                                   // East
                }

                // Highlight logic
                for (int i = 0; i < radialButtons.Count; i++)
                {
                    var text = radialButtons[i].GetComponentInChildren<UnityEngine.UI.Text>();
                    bool highlighted = (i == hoveredIndex) || (i == gamepadSelectedIndex);
                    text.color = highlighted ? Color.yellow : Color.white;
                    if (radialButtonHighlights.Count > i && radialButtonHighlights[i] != null)
                        radialButtonHighlights[i].SetActive(highlighted);
                }

                // Click to select
                if (hoveredIndex != -1 && Input.GetMouseButtonDown(0))
                {
                    radialButtons[hoveredIndex].GetComponent<Button>().onClick.Invoke();
                }






                //end


                int prevIndex = gamepadSelectedIndex;

                if (ZInput.GetButtonDown("JoyRStickUp")) gamepadSelectedIndex = 0;    // North
                else if (ZInput.GetButtonDown("JoyRStickRight")) gamepadSelectedIndex = 1; // East
                else if (ZInput.GetButtonDown("JoyRStickDown")) gamepadSelectedIndex = 2;  // South
                else if (ZInput.GetButtonDown("JoyRStickLeft")) gamepadSelectedIndex = 3;  // West

                if (gamepadSelectedIndex != prevIndex)
                    UpdateGamepadHighlight();

                // Confirm selection with JoyUse
                if (ZInput.GetButtonDown("JoyUse"))
                {
                    if (gamepadSelectedIndex >= 0 && gamepadSelectedIndex < radialButtons.Count)
                    {
                        radialButtons[gamepadSelectedIndex].GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    }
                }

                // handle closing the radial menu with options outside the radial menu button
                if ((Input.GetMouseButtonDown(1) || ZInput.GetButtonDown("JoyJump")))
                {
                    Jotunn.Logger.LogInfo("Right click or special radial button pressed, closing radial menu.");
                    CloseRadialMenu();
                }
            //}
            //if (!RadialMenuIsOpen)
            //{
            //    ModInput.CallSpecialAbilities();
            //}
        }

        public class RadialMenu : MonoBehaviour
        {
            public int segmentCount = 6;
            public float radius = 100f;
            public GameObject buttonPrefab; // Assign a Unity UI Button prefab

            void Start()
            {
                CreateRadialMenu();
            }

            void CreateRadialMenu()
            {
                for (int i = 0; i < segmentCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / segmentCount;
                    Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    GameObject buttonObj = Instantiate(buttonPrefab, transform);
                    buttonObj.GetComponent<RectTransform>().anchoredPosition = pos;
                    buttonObj.GetComponentInChildren<Text>().text = $"Option {i + 1}";
                    int index = i;
                    buttonObj.GetComponent<Button>().onClick.AddListener(() => OnSegmentClicked(index));
                }
            }

            void OnSegmentClicked(int index)
            {
                Debug.Log($"Radial menu option {index + 1} clicked!");
                // todo maybe we can set pending spells here instead
                // Add your action here
            }
        }
    }
}
