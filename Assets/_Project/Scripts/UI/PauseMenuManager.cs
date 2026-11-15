using System;
using AstraLink.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AstraLink.UI
{
    public class PauseMenuManager : MonoBehaviour
    {
        private GameObject _pauseRoot;
        private SaveManagerPanelUI _saveManagerPanel;
        private Font _uiFont;

        public bool IsPaused => _pauseRoot != null && _pauseRoot.activeSelf;

        public void Initialize(Transform canvasTransform, Font referenceFont)
        {
            if (_pauseRoot != null) return;
            _uiFont = referenceFont != null ? referenceFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _saveManagerPanel = GetComponent<SaveManagerPanelUI>();
            if (_saveManagerPanel == null)
            {
                _saveManagerPanel = gameObject.AddComponent<SaveManagerPanelUI>();
            }
            _saveManagerPanel.Initialize(canvasTransform, _uiFont);

            BuildPauseMenuHierarchy(canvasTransform);
            _pauseRoot.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Якщо відкрите вікно сейвів — закриваємо його і повертаємося до меню паузи
                if (_saveManagerPanel != null && _saveManagerPanel.IsOpen)
                {
                    _saveManagerPanel.Close();
                    _pauseRoot.SetActive(true);
                    return;
                }

                // Інакше перемикаємо стан паузи (відкрити/закрити)
                if (IsPaused)
                {
                    ResumeGame();
                }
                else
                {
                    PauseGame();
                }
            }
        }

        public void PauseGame()
        {
            if (_pauseRoot == null) return;
            _pauseRoot.SetActive(true);
            _pauseRoot.transform.SetAsLastSibling();
            Time.timeScale = 0f;
        }

        public void ResumeGame()
        {
            if (_pauseRoot == null) return;
            _pauseRoot.SetActive(false);
            Time.timeScale = 1f;
        }

        private void OpenSaves()
        {
            if (_saveManagerPanel != null)
            {
                _pauseRoot.SetActive(false); // Ховаємо паузу, відкриваємо сейви
                _saveManagerPanel.Open();
            }
        }

        private void OpenSettings()
        {
            Debug.Log("[PauseMenu] Settings clicked.");
        }

        private void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Menu");
        }

        private void BuildPauseMenuHierarchy(Transform parent)
        {
            _pauseRoot = new GameObject("PauseMenuModal", typeof(RectTransform), typeof(Image));
            _pauseRoot.transform.SetParent(parent, false);
            var rootRt = _pauseRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            _pauseRoot.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.85f);

            GameObject window = new GameObject("Window", typeof(RectTransform), typeof(Image), typeof(Outline));
            window.transform.SetParent(_pauseRoot.transform, false);
            var winRt = window.GetComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.sizeDelta = new Vector2(460f, 400f);
            window.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.14f, 0.98f);
            window.GetComponent<Outline>().effectColor = new Color(0.35f, 0.85f, 1f, 0.45f);

            CreateText(window.transform, "Header", "PAUSE MENU", 24, TextAnchor.MiddleCenter,
                new Vector2(0, 140), new Vector2(400, 40), new Color(0.45f, 0.92f, 1f));

            CreateMenuButton(window.transform, "Btn_Continue", "CONTINUE", 70f, ResumeGame);
            CreateMenuButton(window.transform, "Btn_Settings", "SETTINGS", 5f, OpenSettings);
            CreateMenuButton(window.transform, "Btn_Saves", "SAVES", -60f, OpenSaves);
            CreateMenuButton(window.transform, "Btn_Exit", "MAIN MENU", -125f, ReturnToMainMenu);
        }

        private Button CreateMenuButton(Transform parent, string name, string label, float yPos, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            var rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, yPos);
            rt.sizeDelta = new Vector2(360f, 48f);

            var img = btnObj.GetComponent<Image>();
            img.color = new Color(0.16f, 0.27f, 0.44f, 1f);

            var btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            CreateText(btnObj.transform, "Label", label, 18, TextAnchor.MiddleCenter, Vector2.zero, rt.sizeDelta, Color.white);
            return btn;
        }

        private Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor align, Vector2 pos, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var txt = obj.GetComponent<Text>();
            txt.font = _uiFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = color;
            return txt;
        }
    }
}