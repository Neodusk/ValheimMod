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
using UnityEngine.UI;

namespace valheimmod
{
    internal partial class valheimmod : BaseUnityPlugin
    {
        public static GameObject radialMenuInstance;
        public static bool RadialMenuIsOpen = false;
        public static GameObject radialButtonPrefab; // Assign this in the inspector with a Unity UI Button prefab
        public static int radialItemClicked;
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
        private void CloseRadialMenu()
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

        /// <summary>
        /// Updates highlighting of radial menu buttons based on mouse position.
        /// </summary>
        private static void UpdateRadialHighlight()
        {
            if (radialMenuInstance == null || radialButtons.Count == 0) return;

            Vector2 mousePos = Input.mousePosition;
            int highlighted = -1;

            for (int i = 0; i < radialButtons.Count; i++)
            {
                var rect = radialButtons[i].GetComponent<RectTransform>();
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, mousePos))
                {
                    highlighted = i;
                }
            }

            for (int i = 0; i < radialButtons.Count; i++)
            {
                var img = radialButtons[i].GetComponent<Image>();
                img.color = (i == highlighted) ? Color.yellow : Color.white;
            }
            currentHighlightedIndex = highlighted;
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

                int segmentCount = 3;
                float radius = 100f;
                for (int i = 0; i < segmentCount; i++)
                {
                    // Create Button
                    GameObject buttonObj = new GameObject($"Button_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                    buttonObj.transform.SetParent(radialMenuInstance.transform, false);

                    // Set position in a circle
                    float angle = i * Mathf.PI * 2f / segmentCount;
                    Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    buttonObj.GetComponent<RectTransform>().anchoredPosition = pos;
                    buttonObj.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 60);

                    // Add Text
                    GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
                    textObj.transform.SetParent(buttonObj.transform, false);
                    var text = textObj.GetComponent<Text>();
                    text.text = GetRadialAbilityName(RadialAbilityMap[i + 1]);
                    text.alignment = TextAnchor.MiddleCenter;
                    text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    text.color = Color.black;
                    text.rectTransform.sizeDelta = new Vector2(60, 60);

                    int index = i;

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
