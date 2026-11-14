using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstraLink.UI.Common
{
    [Serializable]
    public struct ItemSpriteEntry
    {
        public string Id;
        public Sprite Sprite;
    }

    /// <summary>
    /// Central asset registry for all _Project/Art UI, icon, portrait, planet, and spacecraft sprites.
    /// Stored in Resources/UIThemeAssets so both Editor and standalone builds load it reliably.
    /// </summary>
    [CreateAssetMenu(fileName = "UIThemeAssets", menuName = "AstraLink/UI Theme Assets")]
    public class UIThemeAssets : ScriptableObject
    {
        [Header("UI Frames & Buttons")]
        public Sprite buttonNormal;
        public Sprite buttonHover;
        public Sprite buttonPressed;
        public Sprite buttonDisabled;
        public Sprite panelFrame;
        public Sprite dialogueBox;
        public Sprite itemSlot;
        public Sprite itemSlotSelected;
        public Sprite nameTab;
        public Sprite radarScreen;
        public Sprite missionBadge;
        public Sprite spaceCreditsCoin;

        [Header("Portraits")]
        public Sprite astraNormal;
        public Sprite astraWarn;
        public Sprite astraError;
        public Sprite dispatcher;

        [Header("Backgrounds")]
        public Sprite bgStarfield;
        public Sprite bgDistantWorlds;
        public Sprite bgPlanetApproach;
        public Sprite bgQuietNebula;
        public Sprite bgRingedWorld;

        [Header("Planets")]
        public Sprite planetEarth;
        public Sprite planetMoon;
        public Sprite planetMars;
        public Sprite planetVenus;
        public Sprite planetSun;

        [Header("Indicators & Icons")]
        public Sprite iconDataChip;
        public Sprite iconRepairWrench;
        public Sprite iconStarChart;
        public Sprite iconCountdownClock;
        public Sprite iconControlPanel;

        [Header("Components, Rockets & Orbits")]
        public List<ItemSpriteEntry> itemSprites = new List<ItemSpriteEntry>();

        private Dictionary<string, Sprite> _lookup;

        public Sprite FindItemSprite(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (_lookup == null || _lookup.Count != itemSprites.Count)
            {
                _lookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < itemSprites.Count; i++)
                {
                    if (!string.IsNullOrEmpty(itemSprites[i].Id) && itemSprites[i].Sprite != null)
                    {
                        _lookup[itemSprites[i].Id] = itemSprites[i].Sprite;
                    }
                }
            }

            return _lookup.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }
    }
}
