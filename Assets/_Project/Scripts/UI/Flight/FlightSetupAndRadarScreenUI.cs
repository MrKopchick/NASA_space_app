using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.Core
{
    /// <summary>
    /// Combined root controller for the Task S1-08 prefab (<c>RocketOrbitAndRadarScreen.prefab</c>).
    /// Left panel: Rocket (3 cards) and Orbit (LEO, Moon, Mars) selection.
    /// Right panel: 2D Orbit Radar with Sunlight sector, Shadow/Eclipse cone, Communication Window sector, and flight test controls.
    /// </summary>
    public class FlightSetupAndRadarScreenUI : MonoBehaviour
    {
        [SerializeField] private RocketOrbitSelectorUI selectorUI;
        [SerializeField] private OrbitRadarUI radarUI;

        /// <summary>
        /// Gets the left-side <see cref="RocketOrbitSelectorUI"/> controller.
        /// </summary>
        public RocketOrbitSelectorUI SelectorUI => selectorUI;

        /// <summary>
        /// Gets the right-side <see cref="OrbitRadarUI"/> controller.
        /// </summary>
        public OrbitRadarUI RadarUI => radarUI;

        /// <summary>
        /// Fired when a launch vehicle card is selected.
        /// </summary>
        public event Action<RocketOption> OnRocketSelected;

        /// <summary>
        /// Fired when a target orbit card is selected.
        /// </summary>
        public event Action<OrbitDefinition> OnOrbitSelected;

        /// <summary>
        /// Fired when the 20-tick orbital flight test starts.
        /// </summary>
        public event Action OnTestFlightStarted;

        /// <summary>
        /// Fired on each simulation tick update during the orbital flight test.
        /// </summary>
        public event Action<SimulationTickSnapshot, int> OnTickUpdated;

        /// <summary>
        /// Fired when the 20-tick orbital flight test completes.
        /// </summary>
        public event Action<MissionReport> OnTestFlightCompleted;

        private bool _initialized;

        private void Awake()
        {
            EnsureScreenHierarchy();
        }

        private void Start()
        {
            if (!_initialized)
            {
                IMissionSimulator sim = (GameManager.Instance != null && GameManager.Instance.Simulator != null)
                    ? GameManager.Instance.Simulator
                    : new MockMissionSimulator();
                Initialize(sim);
            }
        }

        /// <summary>
        /// Single entry point for initializing the combined S1-08 screen prefab in MainScene.
        /// </summary>
        public void Initialize(
            IMissionSimulator simulator,
            List<RocketOption> rockets = null,
            List<OrbitDefinition> orbits = null,
            List<SatelliteComponent> components = null,
            MissionGoal goal = null,
            List<AutopilotRule> rules = null)
        {
            EnsureScreenHierarchy();

            if (simulator == null)
            {
                simulator = new MockMissionSimulator();
            }

            _initialized = true;

            if (selectorUI != null)
            {
                selectorUI.Initialize(simulator, rockets, orbits, radarUI, components, goal);
            }

            if (radarUI != null)
            {
                OrbitDefinition initialOrbit = selectorUI != null ? selectorUI.SelectedOrbit : null;
                RocketOption initialRocket = selectorUI != null ? selectorUI.SelectedRocket : null;
                radarUI.Initialize(simulator, initialOrbit, initialRocket, goal, components, rules);
            }
        }

        /// <summary>
        /// Ensures the child panels (<see cref="RocketOrbitSelectorUI"/> and <see cref="OrbitRadarUI"/>) exist and are wired together.
        /// </summary>
        public void EnsureScreenHierarchy()
        {
            RectTransform rootRt = GetComponent<RectTransform>();
            if (rootRt == null)
            {
                rootRt = gameObject.AddComponent<RectTransform>();
            }

            Image bg = GetComponent<Image>();
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
                bg.color = new Color(0.03f, 0.06f, 0.11f, 1.0f);
            }

            if (selectorUI == null)
            {
                Transform existingSelector = rootRt.Find("RocketOrbitSelector");
                if (existingSelector != null)
                {
                    selectorUI = existingSelector.GetComponent<RocketOrbitSelectorUI>();
                }
                else
                {
                    var selObj = new GameObject("RocketOrbitSelector", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RocketOrbitSelectorUI));
                    selObj.transform.SetParent(rootRt, false);
                    var selRt = selObj.GetComponent<RectTransform>();
                    selRt.anchorMin = new Vector2(0.02f, 0.03f);
                    selRt.anchorMax = new Vector2(0.48f, 0.97f);
                    selRt.offsetMin = Vector2.zero;
                    selRt.offsetMax = Vector2.zero;
                    selectorUI = selObj.GetComponent<RocketOrbitSelectorUI>();
                }
            }

            if (radarUI == null)
            {
                Transform existingRadar = rootRt.Find("OrbitRadar");
                if (existingRadar != null)
                {
                    radarUI = existingRadar.GetComponent<OrbitRadarUI>();
                }
                else
                {
                    var radObj = new GameObject("OrbitRadar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(OrbitRadarUI));
                    radObj.transform.SetParent(rootRt, false);
                    var radRt = radObj.GetComponent<RectTransform>();
                    radRt.anchorMin = new Vector2(0.50f, 0.03f);
                    radRt.anchorMax = new Vector2(0.98f, 0.97f);
                    radRt.offsetMin = Vector2.zero;
                    radRt.offsetMax = Vector2.zero;
                    radarUI = radObj.GetComponent<OrbitRadarUI>();
                }
            }

            if (selectorUI != null && radarUI != null)
            {
                selectorUI.LinkedOrbitRadar = radarUI;
                selectorUI.OnRocketSelected -= HandleRocketSelected;
                selectorUI.OnRocketSelected += HandleRocketSelected;
                selectorUI.OnOrbitSelected -= HandleOrbitSelected;
                selectorUI.OnOrbitSelected += HandleOrbitSelected;
            }

            if (radarUI != null)
            {
                radarUI.OnTestFlightStarted -= HandleTestFlightStarted;
                radarUI.OnTestFlightStarted += HandleTestFlightStarted;
                radarUI.OnTickUpdated -= HandleTickUpdated;
                radarUI.OnTickUpdated += HandleTickUpdated;
                radarUI.OnTestFlightCompleted -= HandleTestFlightCompleted;
                radarUI.OnTestFlightCompleted += HandleTestFlightCompleted;
            }
        }

        private void HandleRocketSelected(RocketOption rocket) => OnRocketSelected?.Invoke(rocket);
        private void HandleOrbitSelected(OrbitDefinition orbit) => OnOrbitSelected?.Invoke(orbit);
        private void HandleTestFlightStarted() => OnTestFlightStarted?.Invoke();
        private void HandleTickUpdated(SimulationTickSnapshot snap, int index) => OnTickUpdated?.Invoke(snap, index);
        private void HandleTestFlightCompleted(MissionReport report) => OnTestFlightCompleted?.Invoke(report);
    }
}
