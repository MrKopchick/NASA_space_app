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
    /// Central asset registry for all _Project/Art UI, icon, portrait, planet, celestial, spacecraft,
    /// and segmented LED telemetry bar sprites.
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

        [Header("Portraits & Frame")]
        public Sprite portraitFrame;
        public Sprite astraNormal;
        public Sprite astraWarn;
        public Sprite astraError;
        public Sprite dispatcher;
        public Sprite dispatcherBlue;
        public Sprite dispatcherArmored;
        public Sprite dispatcherCyborg;
        public Sprite spaceDog;

        [Header("Backgrounds")]
        public Sprite bgStarfield;
        public Sprite bgDistantWorlds;
        public Sprite bgPlanetApproach;
        public Sprite bgQuietNebula;
        public Sprite bgRingedWorld;

        [Header("Planets & Celestial Hazards")]
        public Sprite planetEarth;
        public Sprite planetMoon;
        public Sprite planetMars;
        public Sprite planetVenus;
        public Sprite planetSun;
        public Sprite celestialEclipse;
        public Sprite celestialSupernova;
        public Sprite celestialMeteor;
        public Sprite celestialAsteroidField;
        public Sprite celestialNebula;

        [Header("Launchpad & Rockets")]
        public Sprite launchPad;
        public Sprite launchTower;
        public Sprite rocketFlame;
        public Sprite rocketSmall;
        public Sprite rocketMedium;
        public Sprite rocketHeavy;

        [Header("Telemetry LED Sheets (4x10 grid of 64x16 bars)")]
        public Sprite telemetryBlueSheet;
        public Sprite telemetryCyanSheet;
        public Sprite telemetryPinkSheet;
        public Sprite telemetryPurpleSheet;
        public Sprite telemetryBrownSheet;

        [Header("Indicators & Icons")]
        public Sprite iconDataChip;
        public Sprite iconRepairWrench;
        public Sprite iconStarChart;
        public Sprite iconCountdownClock;
        public Sprite iconControlPanel;
        public Sprite iconOxygenGauge;
        public Sprite iconStarMapTablet;
        public Sprite iconHoloProjector;
        public Sprite iconMissionPatchBlank;
        public Sprite iconNextArrow;
        public Sprite iconShield;
        public Sprite iconSolarPanel;
        public Sprite iconEnergyCell;

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
