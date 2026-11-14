using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AstraLink.Core
{
    public enum JsonDataLoaderLogType
    {
        None,
        MissingComponentsAsset,
        MissingRocketsAsset,
        MissingOrbitsAsset,
        MissingMissionsAsset,
        MissingEventsAsset,
        RepositoryCreationFailed
    }

    /// <summary>
    /// Unity bridge helper that loads JSON TextAssets from Resources/Data and constructs an IGameDataRepository instance.
    /// </summary>
    public class JsonDataLoader : MonoBehaviour
    {
        public const string ComponentsResourcePath = "Data/components";
        public const string RocketsResourcePath = "Data/rockets";
        public const string OrbitsResourcePath = "Data/orbits";
        public const string MissionsResourcePath = "Data/missions";
        public const string EventsResourcePath = "Data/events";

        private static readonly Dictionary<JsonDataLoaderLogType, string> LogMessages = new Dictionary<JsonDataLoaderLogType, string>
        {
            { JsonDataLoaderLogType.None, string.Empty },
            { JsonDataLoaderLogType.MissingComponentsAsset, "[JsonDataLoader] Missing TextAsset at Resources/Data/components." },
            { JsonDataLoaderLogType.MissingRocketsAsset, "[JsonDataLoader] Missing TextAsset at Resources/Data/rockets." },
            { JsonDataLoaderLogType.MissingOrbitsAsset, "[JsonDataLoader] Missing TextAsset at Resources/Data/orbits." },
            { JsonDataLoaderLogType.MissingMissionsAsset, "[JsonDataLoader] Missing TextAsset at Resources/Data/missions." },
            { JsonDataLoaderLogType.MissingEventsAsset, "[JsonDataLoader] Missing TextAsset at Resources/Data/events." },
            { JsonDataLoaderLogType.RepositoryCreationFailed, "[JsonDataLoader] Failed to initialize JsonDataRepository: {0}" }
        };

        public IGameDataRepository Repository { get; private set; }

        public static string GetLogMessage(JsonDataLoaderLogType logType)
        {
            string message;
            return LogMessages.TryGetValue(logType, out message) ? message : string.Empty;
        }

        private void Awake()
        {
            if (Repository == null)
            {
                Repository = LoadRepository();
            }
        }

        public IGameDataRepository GetOrLoadRepository()
        {
            if (Repository == null)
            {
                Repository = LoadRepository();
            }
            return Repository;
        }

        public static IGameDataRepository LoadFromResources()
        {
            return LoadRepository();
        }

        public static IGameDataRepository LoadRepository()
        {
            TextAsset comp = Resources.Load<TextAsset>(ComponentsResourcePath);
            TextAsset rock = Resources.Load<TextAsset>(RocketsResourcePath);
            TextAsset orb = Resources.Load<TextAsset>(OrbitsResourcePath);
            TextAsset miss = Resources.Load<TextAsset>(MissionsResourcePath);
            TextAsset evt = Resources.Load<TextAsset>(EventsResourcePath);

            return LoadFromTextAssets(comp, rock, orb, miss, evt);
        }

        public static IGameDataRepository LoadFromTextAssets(
            TextAsset comp,
            TextAsset rock,
            TextAsset orb,
            TextAsset miss,
            TextAsset evt)
        {
            bool hasMissingAsset = false;

            if (comp == null)
            {
                Debug.LogError(GetLogMessage(JsonDataLoaderLogType.MissingComponentsAsset));
                hasMissingAsset = true;
            }

            if (rock == null)
            {
                Debug.LogError(GetLogMessage(JsonDataLoaderLogType.MissingRocketsAsset));
                hasMissingAsset = true;
            }

            if (orb == null)
            {
                Debug.LogError(GetLogMessage(JsonDataLoaderLogType.MissingOrbitsAsset));
                hasMissingAsset = true;
            }

            if (miss == null)
            {
                Debug.LogError(GetLogMessage(JsonDataLoaderLogType.MissingMissionsAsset));
                hasMissingAsset = true;
            }

            if (evt == null)
            {
                Debug.LogError(GetLogMessage(JsonDataLoaderLogType.MissingEventsAsset));
                hasMissingAsset = true;
            }

            if (hasMissingAsset)
                return null;

            try
            {
                return new JsonDataRepository(comp.text, rock.text, orb.text, miss.text, evt.text);
            }
            catch (Exception ex)
            {
                Debug.LogError(string.Format(
                    CultureInfo.InvariantCulture,
                    GetLogMessage(JsonDataLoaderLogType.RepositoryCreationFailed),
                    ex.Message));
                return null;
            }
        }
    }
}
