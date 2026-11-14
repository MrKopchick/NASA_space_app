using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AstraLink.Core
{
    /// <summary>
    /// S1-05 (Саша Г): Реалізація інтерфейсу IGameDataRepository.
    /// Завантажує та парсить усі 5 JSON-файлів (components.json, rockets.json, orbits.json,
    /// missions.json, events.json) через Newtonsoft.Json без використання UnityEngine.
    /// </summary>
    public class JsonDataRepository : IGameDataRepository
    {
        [Serializable]
        public class ComponentCatalogWrapper
        {
            public List<SatelliteComponent> components = new List<SatelliteComponent>();
        }

        [Serializable]
        public class RocketCatalogWrapper
        {
            public List<RocketOption> rockets = new List<RocketOption>();
        }

        [Serializable]
        public class OrbitCatalogWrapper
        {
            public List<OrbitDefinition> orbits = new List<OrbitDefinition>();
        }

        [Serializable]
        public class MissionCatalogWrapper
        {
            public List<MissionGoal> missions = new List<MissionGoal>();
        }

        [Serializable]
        public class EventCatalogWrapper
        {
            public List<SpaceEventDefinition> events = new List<SpaceEventDefinition>();
        }

        private readonly List<SatelliteComponent> _components = new List<SatelliteComponent>();
        private readonly List<RocketOption> _rockets = new List<RocketOption>();
        private readonly List<OrbitDefinition> _orbits = new List<OrbitDefinition>();
        private readonly List<MissionGoal> _missions = new List<MissionGoal>();
        private readonly List<SpaceEventDefinition> _events = new List<SpaceEventDefinition>();

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            Converters = new List<JsonConverter> { new StringEnumConverter() }
        };

        public JsonDataRepository()
        {
        }

        public JsonDataRepository(
            string componentsJson,
            string rocketsJson,
            string orbitsJson,
            string missionsJson,
            string eventsJson)
        {
            LoadFromJsonStrings(componentsJson, rocketsJson, orbitsJson, missionsJson, eventsJson);
        }

        /// <summary>
        /// Десеріалізує всі 5 JSON-рядків у колекції даних гри.
        /// Підтримує як формат з кореневим об'єктом ({ "components": [...] }), так і прямий масив ([...]).
        /// </summary>
        public void LoadFromJsonStrings(
            string componentsJson,
            string rocketsJson,
            string orbitsJson,
            string missionsJson,
            string eventsJson)
        {
            if (string.IsNullOrWhiteSpace(componentsJson))
                throw new ArgumentException("Порожній JSON-рядок для components.json", nameof(componentsJson));
            if (string.IsNullOrWhiteSpace(rocketsJson))
                throw new ArgumentException("Порожній JSON-рядок для rockets.json", nameof(rocketsJson));
            if (string.IsNullOrWhiteSpace(orbitsJson))
                throw new ArgumentException("Порожній JSON-рядок для orbits.json", nameof(orbitsJson));
            if (string.IsNullOrWhiteSpace(missionsJson))
                throw new ArgumentException("Порожній JSON-рядок для missions.json", nameof(missionsJson));
            if (string.IsNullOrWhiteSpace(eventsJson))
                throw new ArgumentException("Порожній JSON-рядок для events.json", nameof(eventsJson));

            _components.Clear();
            if (componentsJson.TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                List<SatelliteComponent> list = JsonConvert.DeserializeObject<List<SatelliteComponent>>(componentsJson, SerializerSettings);
                if (list != null) _components.AddRange(list);
            }
            else
            {
                ComponentCatalogWrapper wrapper = JsonConvert.DeserializeObject<ComponentCatalogWrapper>(componentsJson, SerializerSettings);
                if (wrapper != null && wrapper.components != null) _components.AddRange(wrapper.components);
            }

            _rockets.Clear();
            if (rocketsJson.TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                List<RocketOption> list = JsonConvert.DeserializeObject<List<RocketOption>>(rocketsJson, SerializerSettings);
                if (list != null) _rockets.AddRange(list);
            }
            else
            {
                RocketCatalogWrapper wrapper = JsonConvert.DeserializeObject<RocketCatalogWrapper>(rocketsJson, SerializerSettings);
                if (wrapper != null && wrapper.rockets != null) _rockets.AddRange(wrapper.rockets);
            }

            _orbits.Clear();
            if (orbitsJson.TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                List<OrbitDefinition> list = JsonConvert.DeserializeObject<List<OrbitDefinition>>(orbitsJson, SerializerSettings);
                if (list != null) _orbits.AddRange(list);
            }
            else
            {
                OrbitCatalogWrapper wrapper = JsonConvert.DeserializeObject<OrbitCatalogWrapper>(orbitsJson, SerializerSettings);
                if (wrapper != null && wrapper.orbits != null) _orbits.AddRange(wrapper.orbits);
            }

            _missions.Clear();
            if (missionsJson.TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                List<MissionGoal> list = JsonConvert.DeserializeObject<List<MissionGoal>>(missionsJson, SerializerSettings);
                if (list != null) _missions.AddRange(list);
            }
            else
            {
                MissionCatalogWrapper wrapper = JsonConvert.DeserializeObject<MissionCatalogWrapper>(missionsJson, SerializerSettings);
                if (wrapper != null && wrapper.missions != null) _missions.AddRange(wrapper.missions);
            }

            _events.Clear();
            if (eventsJson.TrimStart().StartsWith("[", StringComparison.Ordinal))
            {
                List<SpaceEventDefinition> list = JsonConvert.DeserializeObject<List<SpaceEventDefinition>>(eventsJson, SerializerSettings);
                if (list != null) _events.AddRange(list);
            }
            else
            {
                EventCatalogWrapper wrapper = JsonConvert.DeserializeObject<EventCatalogWrapper>(eventsJson, SerializerSettings);
                if (wrapper != null && wrapper.events != null) _events.AddRange(wrapper.events);
            }
        }

        /// <summary>
        /// Допоміжний метод для завантаження всіх 5 JSON-файлів із локальної папки під час тестування.
        /// </summary>
        public static JsonDataRepository LoadFromDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                throw new DirectoryNotFoundException("Папку з JSON-файлами не знайдено: " + directoryPath);

            string componentsJson = File.ReadAllText(Path.Combine(directoryPath, "components.json"));
            string rocketsJson = File.ReadAllText(Path.Combine(directoryPath, "rockets.json"));
            string orbitsJson = File.ReadAllText(Path.Combine(directoryPath, "orbits.json"));
            string missionsJson = File.ReadAllText(Path.Combine(directoryPath, "missions.json"));
            string eventsJson = File.ReadAllText(Path.Combine(directoryPath, "events.json"));

            return new JsonDataRepository(componentsJson, rocketsJson, orbitsJson, missionsJson, eventsJson);
        }

        public List<SatelliteComponent> GetComponents()
        {
            return new List<SatelliteComponent>(_components);
        }

        public List<RocketOption> GetRockets()
        {
            return new List<RocketOption>(_rockets);
        }

        public List<OrbitDefinition> GetOrbits()
        {
            return new List<OrbitDefinition>(_orbits);
        }

        public OrbitDefinition GetOrbit(string orbitId)
        {
            if (string.IsNullOrWhiteSpace(orbitId))
                return null;

            for (int i = 0; i < _orbits.Count; i++)
            {
                if (_orbits[i] != null && string.Equals(_orbits[i].Id, orbitId, StringComparison.OrdinalIgnoreCase))
                    return _orbits[i];
            }
            return null;
        }

        public List<MissionGoal> GetMissions()
        {
            return new List<MissionGoal>(_missions);
        }

        public List<SpaceEventDefinition> GetEvents()
        {
            return new List<SpaceEventDefinition>(_events);
        }

        public SpaceEventDefinition GetEvent(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                return null;

            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i] != null && string.Equals(_events[i].Id, eventId, StringComparison.OrdinalIgnoreCase))
                    return _events[i];
            }
            return null;
        }

        public SatelliteComponent GetComponent(string componentId)
        {
            if (string.IsNullOrWhiteSpace(componentId))
                return null;

            for (int i = 0; i < _components.Count; i++)
            {
                if (_components[i] != null && string.Equals(_components[i].Id, componentId, StringComparison.OrdinalIgnoreCase))
                    return _components[i];
            }
            return null;
        }

        public RocketOption GetRocket(string rocketId)
        {
            if (string.IsNullOrWhiteSpace(rocketId))
                return null;

            for (int i = 0; i < _rockets.Count; i++)
            {
                if (_rockets[i] != null && string.Equals(_rockets[i].Id, rocketId, StringComparison.OrdinalIgnoreCase))
                    return _rockets[i];
            }
            return null;
        }

        public MissionGoal GetMission(string missionId)
        {
            if (string.IsNullOrWhiteSpace(missionId))
                return null;

            for (int i = 0; i < _missions.Count; i++)
            {
                if (_missions[i] != null && string.Equals(_missions[i].Id, missionId, StringComparison.OrdinalIgnoreCase))
                    return _missions[i];
            }
            return null;
        }
    }
}
