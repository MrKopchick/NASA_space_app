using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AstraLink.Core
{
    [Serializable]
    public class SaveSlotMetadata
    {
        public string slotId;
        public string saveName;
        public string updatedAt;
        public float capitalThousands;
        public int completedMissionsCount;
        public string agencyJsonPayload;
    }

    public static class SaveManager
    {
        private const string ActiveSlotPrefsKey = "AstraLink_ActiveSlotId";
        private static string SavesDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        public static string ActiveSlotId
        {
            get => PlayerPrefs.GetString(ActiveSlotPrefsKey, string.Empty);
            private set
            {
                PlayerPrefs.SetString(ActiveSlotPrefsKey, value ?? string.Empty);
                PlayerPrefs.Save();
            }
        }

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(SavesDirectory))
                Directory.CreateDirectory(SavesDirectory);
        }

        private static string GetFilePath(string slotId) => Path.Combine(SavesDirectory, $"{slotId}.json");

        public static void MigrateLegacySaveIfNeeded()
        {
            EnsureDirectory();
            if (Directory.GetFiles(SavesDirectory, "*.json").Length > 0)
                return;

            if (PlayerPrefs.HasKey(GameManager.AgencyStatePrefsKey))
            {
                string legacyJson = PlayerPrefs.GetString(GameManager.AgencyStatePrefsKey);
                if (!string.IsNullOrWhiteSpace(legacyJson))
                {
                    CreateNewSave("CAMPAIGN 01", legacyJson);
                }
            }
        }

        public static List<SaveSlotMetadata> GetAllSaves()
        {
            MigrateLegacySaveIfNeeded();
            var list = new List<SaveSlotMetadata>();

            foreach (string file in Directory.GetFiles(SavesDirectory, "*.json"))
            {
                try
                {
                    string raw = File.ReadAllText(file);
                    var meta = JsonUtility.FromJson<SaveSlotMetadata>(raw);
                    if (meta != null && !string.IsNullOrEmpty(meta.slotId))
                        list.Add(meta);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SaveManager] Пошкоджений файл сейву {file}: {ex.Message}");
                }
            }

            return list.OrderByDescending(s => s.updatedAt).ToList();
        }

        /// <summary>
        /// Створює новий слот збереження (якщо jsonPayload порожній — створюється чиста кампанія).
        /// </summary>
        public static SaveSlotMetadata CreateNewSave(string customName, string jsonPayload = "")
        {
            EnsureDirectory();
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            string finalName = string.IsNullOrWhiteSpace(customName)
                ? $"MISSION {DateTime.Now:dd.MM HH:mm}"
                : customName.Trim().ToUpperInvariant();

            var meta = new SaveSlotMetadata
            {
                slotId = id,
                saveName = finalName,
                updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                agencyJsonPayload = jsonPayload ?? string.Empty
            };

            PopulatePreviewStats(meta);
            File.WriteAllText(GetFilePath(id), JsonUtility.ToJson(meta, true));
            SetActiveSlotInPrefs(meta);
            return meta;
        }

        /// <summary>
        /// Зберігає актуальний стан гри у вибраний слот.
        /// </summary>
        public static bool SaveToSlot(string slotId, string overrideName = null, string explicitJson = null)
        {
            if (string.IsNullOrEmpty(slotId)) return false;
            EnsureDirectory();

            string path = GetFilePath(slotId);
            SaveSlotMetadata meta = File.Exists(path)
                ? JsonUtility.FromJson<SaveSlotMetadata>(File.ReadAllText(path))
                : new SaveSlotMetadata { slotId = slotId, saveName = $"SAVE {slotId}" };

            if (!string.IsNullOrWhiteSpace(overrideName))
                meta.saveName = overrideName.Trim().ToUpperInvariant();

            string payload = explicitJson ?? PlayerPrefs.GetString(GameManager.AgencyStatePrefsKey, string.Empty);
            meta.agencyJsonPayload = payload;
            meta.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            PopulatePreviewStats(meta);

            File.WriteAllText(path, JsonUtility.ToJson(meta, true));
            ActiveSlotId = slotId;
            return true;
        }

        public static bool LoadSlot(string slotId)
        {
            string path = GetFilePath(slotId);
            if (!File.Exists(path)) return false;

            var meta = JsonUtility.FromJson<SaveSlotMetadata>(File.ReadAllText(path));
            if (meta == null) return false;

            SetActiveSlotInPrefs(meta);
            return true;
        }

        public static void DeleteSlot(string slotId)
        {
            string path = GetFilePath(slotId);
            if (File.Exists(path))
                File.Delete(path);

            if (ActiveSlotId == slotId)
            {
                ActiveSlotId = string.Empty;
                PlayerPrefs.DeleteKey(GameManager.AgencyStatePrefsKey);
                PlayerPrefs.Save();
            }
        }

        private static void PopulatePreviewStats(SaveSlotMetadata meta)
        {
            if (string.IsNullOrWhiteSpace(meta.agencyJsonPayload))
            {
                meta.capitalThousands = GameManager.StartingCapitalThousands;
                meta.completedMissionsCount = 0;
                return;
            }

            try
            {
                var state = JsonUtility.FromJson<AgencyState>(meta.agencyJsonPayload);
                if (state != null)
                {
                    meta.capitalThousands = state.CapitalThousands;
                    meta.completedMissionsCount = state.CompletedMissionIds != null ? state.CompletedMissionIds.Count : 0;
                }
            }
            catch
            {
                meta.capitalThousands = GameManager.StartingCapitalThousands;
                meta.completedMissionsCount = 0;
            }
        }

        private static void SetActiveSlotInPrefs(SaveSlotMetadata meta)
        {
            ActiveSlotId = meta.slotId;
            if (string.IsNullOrEmpty(meta.agencyJsonPayload))
                PlayerPrefs.DeleteKey(GameManager.AgencyStatePrefsKey);
            else
                PlayerPrefs.SetString(GameManager.AgencyStatePrefsKey, meta.agencyJsonPayload);

            PlayerPrefs.Save();
        }
    }
}