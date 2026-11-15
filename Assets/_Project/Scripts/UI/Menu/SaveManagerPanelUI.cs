using System;
using System.Collections.Generic;
using AstraLink.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI
{
    public class SaveManagerPanelUI : MonoBehaviour
    {
        public event Action<SaveSlotMetadata> OnLoadRequested;
        public event Action OnSavesChanged;

        private GameObject _modalRoot;
        private Transform _listContent;
        private InputField _nameInput;
        private Text _statusText;

        private Button _saveCurrentBtn;
        private Button _loadBtn;
        private Button _deleteBtn;

        private string _selectedSlotId;
        private Font _uiFont;

        public void Initialize(Transform canvasTransform, Font referenceFont)
        {
            if (_modalRoot != null) return;
            _uiFont = referenceFont != null
                ? referenceFont
                : AstraLink.UI.Flight.SpaceMap.SolarSystemArtBank.GetPixelFont();

            BuildModalHierarchy(canvasTransform);
            _modalRoot.SetActive(false);
        }

        public void Open()
        {
            if (_modalRoot == null) return;
            _modalRoot.SetActive(true);
            _modalRoot.transform.SetAsLastSibling();
            _selectedSlotId = SaveManager.ActiveSlotId;
            RefreshSlots();
        }

        public void Close()
        {
            if (_modalRoot != null)
                _modalRoot.SetActive(false);
        }

        private void RefreshSlots()
        {
            foreach (Transform child in _listContent)
                Destroy(child.gameObject);

            List<SaveSlotMetadata> saves = SaveManager.GetAllSaves();

            if (saves.Count == 0)
            {
                CreateInfoLabel(_listContent, "NO TELEMETRY ARCHIVES FOUND. CREATE A NEW SAVE BELOW.");
            }
            else
            {
                foreach (var meta in saves)
                {
                    CreateSlotRow(meta, meta.slotId == _selectedSlotId);
                }
            }

            UpdateActionButtons();
        }

        private void UpdateActionButtons()
        {
            bool hasSelection = !string.IsNullOrEmpty(_selectedSlotId);
            _loadBtn.interactable = hasSelection;
            _deleteBtn.interactable = hasSelection;
            _saveCurrentBtn.interactable = hasSelection;
        }

        private void HandleCreateNew()
        {
            string customName = _nameInput != null ? _nameInput.text : string.Empty;
            string currentPayload = GameManager.Instance != null
                ? PlayerPrefs.GetString(GameManager.AgencyStatePrefsKey, string.Empty)
                : string.Empty;

            var created = SaveManager.CreateNewSave(customName, currentPayload);

            if (_nameInput != null) _nameInput.text = string.Empty;
            _selectedSlotId = created.slotId;
            SetStatus($"CREATED NEW SAVE: {created.saveName}");
            RefreshSlots();
            OnSavesChanged?.Invoke();
        }

        private void HandleOverwriteSelected()
        {
            if (string.IsNullOrEmpty(_selectedSlotId)) return;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SaveAgencyState();
            }

            string customName = _nameInput != null && !string.IsNullOrWhiteSpace(_nameInput.text)
                ? _nameInput.text
                : null;

            if (SaveManager.SaveToSlot(_selectedSlotId, customName))
            {
                if (_nameInput != null) _nameInput.text = string.Empty;
                SetStatus("SAVED CURRENT PROGRESS TO SELECTED SLOT.");
                RefreshSlots();
                OnSavesChanged?.Invoke();
            }
        }

        private void HandleLoadSelected()
        {
            if (string.IsNullOrEmpty(_selectedSlotId)) return;
            if (SaveManager.LoadSlot(_selectedSlotId))
            {
                var saves = SaveManager.GetAllSaves();
                var target = saves.Find(s => s.slotId == _selectedSlotId);
                Close();
                OnLoadRequested?.Invoke(target);
            }
        }

        private void HandleDeleteSelected()
        {
            if (string.IsNullOrEmpty(_selectedSlotId)) return;
            SaveManager.DeleteSlot(_selectedSlotId);
            _selectedSlotId = null;
            SetStatus("SAVE ARCHIVE DELETED.");
            RefreshSlots();
            OnSavesChanged?.Invoke();
        }

        private void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
        }

        private void BuildModalHierarchy(Transform parent)
        {
            _modalRoot = new GameObject("SaveManagerModal", typeof(RectTransform), typeof(Image));
            _modalRoot.transform.SetParent(parent, false);
            var rootRt = _modalRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            _modalRoot.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.88f);

            GameObject window = new GameObject("Window", typeof(RectTransform), typeof(Image), typeof(Outline));
            window.transform.SetParent(_modalRoot.transform, false);
            var winRt = window.GetComponent<RectTransform>();
            winRt.anchorMin = new Vector2(0.5f, 0.5f);
            winRt.anchorMax = new Vector2(0.5f, 0.5f);
            winRt.sizeDelta = new Vector2(820f, 540f);
            window.GetComponent<Image>().color = new Color(0.06f, 0.09f, 0.14f, 0.98f);
            window.GetComponent<Outline>().effectColor = new Color(0.35f, 0.85f, 1f, 0.45f);

            CreateText(window.transform, "Header", "MISSION ARCHIVES // SAVE MANAGER", 22, TextAnchor.MiddleLeft,
                new Vector2(24, -20), new Vector2(600, 36), new Color(0.45f, 0.92f, 1f));

            CreateActionButton(window.transform, "CLOSE [X]", new Vector2(680, -20), new Vector2(115, 34),
                new Color(0.35f, 0.15f, 0.2f), Close);

            GameObject scrollArea = new GameObject("SlotsScroll", typeof(RectTransform), typeof(Image), typeof(Mask));
            scrollArea.transform.SetParent(window.transform, false);
            var scrollRt = scrollArea.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0, 1);
            scrollRt.anchorMax = new Vector2(0, 1);
            scrollRt.pivot = new Vector2(0, 1);
            scrollRt.anchoredPosition = new Vector2(24, -70);
            scrollRt.sizeDelta = new Vector2(772, 290);
            scrollArea.GetComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.9f);
            scrollArea.GetComponent<Mask>().showMaskGraphic = true;

            GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObj.transform.SetParent(scrollArea.transform, false);
            var contentRt = contentObj.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0, 0);

            var vlg = contentObj.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;

            var fitter = contentObj.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollArea.AddComponent<ScrollRect>();
            scrollRect.content = contentRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            _listContent = contentObj.transform;

            _nameInput = CreateInputField(window.transform, new Vector2(24, -376), new Vector2(520, 40), "ENTER SAVE NAME (E.G. ORBIT TEST 01)...");

            CreateActionButton(window.transform, "+ NEW SAVE", new Vector2(556, -376), new Vector2(240, 40),
                new Color(0.14f, 0.38f, 0.45f), HandleCreateNew);

            _saveCurrentBtn = CreateActionButton(window.transform, "SAVE TO SLOT", new Vector2(24, -432), new Vector2(246, 44),
                new Color(0.18f, 0.32f, 0.48f), HandleOverwriteSelected);

            _loadBtn = CreateActionButton(window.transform, "LOAD SAVE", new Vector2(287, -432), new Vector2(246, 44),
                new Color(0.15f, 0.45f, 0.32f), HandleLoadSelected);

            _deleteBtn = CreateActionButton(window.transform, "DELETE SAVE", new Vector2(550, -432), new Vector2(246, 44),
                new Color(0.48f, 0.16f, 0.22f), HandleDeleteSelected);

            _statusText = CreateText(window.transform, "StatusText", "SELECT A SLOT OR CREATE A NEW ARCHIVE.", 14,
                TextAnchor.MiddleLeft, new Vector2(24, -490), new Vector2(772, 26), new Color(0.65f, 0.78f, 0.85f));
        }

        private void CreateSlotRow(SaveSlotMetadata meta, bool isSelected)
        {
            GameObject row = new GameObject($"Slot_{meta.slotId}", typeof(RectTransform), typeof(Image), typeof(Button));
            row.transform.SetParent(_listContent, false);
            var rt = row.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 50);

            var bg = row.GetComponent<Image>();
            bg.color = isSelected
                ? new Color(0.18f, 0.42f, 0.58f, 0.85f)
                : new Color(0.08f, 0.12f, 0.18f, 0.8f);

            string activeTag = meta.slotId == SaveManager.ActiveSlotId ? "  [ACTIVE]" : "";
            CreateText(row.transform, "Title", $"{meta.saveName}{activeTag}", 15, TextAnchor.MiddleLeft,
                new Vector2(14, -7), new Vector2(360, 36), Color.white);

            CreateText(row.transform, "Stats", $"${meta.capitalThousands:F0}K  |  MISSIONS: {meta.completedMissionsCount}", 13, TextAnchor.MiddleCenter,
                new Vector2(375, -7), new Vector2(190, 36), new Color(0.55f, 0.92f, 0.75f));

            CreateText(row.transform, "Date", meta.updatedAt, 13, TextAnchor.MiddleRight,
                new Vector2(570, -7), new Vector2(170, 36), new Color(0.7f, 0.82f, 0.9f));

            var btn = row.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                _selectedSlotId = meta.slotId;
                SetStatus($"SELECTED: {meta.saveName}");
                RefreshSlots();
            });
        }

        private void CreateInfoLabel(Transform parent, string message)
        {
            GameObject obj = new GameObject("EmptyMsg", typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 60);
            var txt = obj.GetComponent<Text>();
            txt.font = _uiFont;
            txt.text = message;
            txt.fontSize = 14;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.6f, 0.7f, 0.8f);
        }

        private Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor align, Vector2 pos, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
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

        private Button CreateActionButton(Transform parent, string label, Vector2 pos, Vector2 size, Color bgColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject obj = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            obj.GetComponent<Image>().color = bgColor;
            var btn = obj.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            var txtObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            txtObj.transform.SetParent(obj.transform, false);
            var txtRt = txtObj.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            var txt = txtObj.GetComponent<Text>();
            txt.font = _uiFont;
            txt.text = label;
            txt.fontSize = 14;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            return btn;
        }

        private InputField CreateInputField(Transform parent, Vector2 pos, Vector2 size, string placeholder)
        {
            GameObject obj = new GameObject("SaveNameInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            obj.GetComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 1f);

            var input = obj.GetComponent<InputField>();

            Text phText = CreateText(obj.transform, "Placeholder", placeholder, 14, TextAnchor.MiddleLeft, Vector2.zero, size, new Color(0.45f, 0.55f, 0.65f));
            phText.rectTransform.anchorMin = Vector2.zero;
            phText.rectTransform.anchorMax = Vector2.one;
            phText.rectTransform.offsetMin = new Vector2(12, 0);
            phText.rectTransform.offsetMax = new Vector2(-12, 0);

            Text valText = CreateText(obj.transform, "Text", "", 14, TextAnchor.MiddleLeft, Vector2.zero, size, Color.white);
            valText.rectTransform.anchorMin = Vector2.zero;
            valText.rectTransform.anchorMax = Vector2.one;
            valText.rectTransform.offsetMin = new Vector2(12, 0);
            valText.rectTransform.offsetMax = new Vector2(-12, 0);
            valText.supportRichText = false;

            input.placeholder = phText;
            input.textComponent = valText;
            return input;
        }
    }
}