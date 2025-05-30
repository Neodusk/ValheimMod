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
        public static int gamepadSelectedIndex = 0;
        private static void SetRadialAbility(int index)
        {
            Debug.Log($"Radial menu option {index} clicked!");
            radialItemClicked = index;
        }

        public enum RadialAbility
        {
            None = 0,
            SuperJump,
            None2,
            None3
        }

        private static readonly RadialAbility[] RadialAbilityMap = new[]
        {
            RadialAbility.None,      // 0 (not used)
            RadialAbility.SuperJump, // 1
            RadialAbility.None2,  // 2
            RadialAbility.None3       // 3
        };

        private static string GetRadialAbilityName(RadialAbility ability)
        {
            return ability switch
            {
                RadialAbility.SuperJump => "Super Jump",
                RadialAbility.None2 => "None 2",
                RadialAbility.None3 => "None 3",
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
                    float textRadius = 70f;
                    Vector2 textPos = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)) * textRadius;

                    GameObject buttonObj = new GameObject($"Button_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    buttonObj.transform.SetParent(radialMenuInstance.transform, false);
                    var rect = buttonObj.GetComponent<RectTransform>();
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(300, 300); // Use your full radial size

                    // Optional: make the button background transparent
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
                    text.color = Color.white;
                    text.rectTransform.sizeDelta = new Vector2(60, 60);
                    text.rectTransform.anchoredPosition = textPos; // <-- Offset text outward

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
                        if (text != null) text.color = Color.white;
                        currentHighlightedIndex = -1;
                    });
                    eventTrigger.triggers.Add(entryExit); gamepadSelectedIndex = 0;
                    UpdateGamepadHighlight();

                    radialButtons.Add(buttonObj);
                }
            }
            else
            {
                radialMenuInstance.SetActive(true);
            }
            RadialMenuIsOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            gamepadSelectedIndex = 0;
            UpdateGamepadHighlight();
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
