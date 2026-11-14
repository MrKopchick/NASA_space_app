using System;
using System.Collections.Generic;
using System.Globalization;
using AstraLink.Core;
using AstraLink.UI.Hub;
using AstraLink.UI.Workshop;
using UnityEngine;
using UnityEngine.UI;

namespace AstraLink.UI.Debrief
{
    /// <summary>
    /// Screen 4 Controller: Post-Flight Mission Debriefing and Root-Cause Analysis (Task S2-05).
    /// Receives a MissionReport and renders two distinct states:
    /// 1. Victory (MissionStatus.Success): FinalScore, engineering grade, DeliveredScience, BudgetSpentThousands,
    ///    MinBatteryPercent, FinalHealthPercent, +50 reliability margin bonus, and contract reward + passive income payout,
    ///    with a button to return to the Contract Hub.
    /// 2. Failure (MissionStatus.Failed): "Why did it fail?" root-cause analysis powered by FailureDebriefBuilder (Task S2-02),
    ///    displaying FailureTitle, FailureCause (exact orbit/tick and numeric deficit/losses), and FailureRecommendation,
    ///    with a free return button to the Hangar (Workshop) for immediate build or autopilot fixes.
    /// </summary>
    public class MissionReportUI : MonoBehaviour
    {
        /// <summary>
        /// Reliability bonus points awarded when battery, health, and system reliability margins are met.
        /// </summary>
        public const float ReliabilityBonusPoints = 50.0f;

        /// <summary>
        /// Minimum battery charge percentage required throughout the flight to qualify for the reliability bonus.
        /// </summary>
        public const float MinBatteryBonusThreshold = 15.0f;

        /// <summary>
        /// Minimum final electronics health percentage required to qualify for the reliability bonus.
        /// </summary>
        public const float MinHealthBonusThreshold = 80.0f;

        /// <summary>
        /// Minimum spacecraft overall reliability (0..1) required to qualify for the reliability bonus.
        /// </summary>
        public const float MinReliabilityBonusThreshold = 0.85f;

        /// <summary>
        /// Singleton instance for convenient access during scene integration.
        /// </summary>
        public static MissionReportUI Instance { get; private set; }

        [Header("Header & Outcome Banner")]
        [SerializeField] private Text missionContextSubtitleText;
        [SerializeField] private Image statusBadgeBackground;
        [SerializeField] private Text statusBadgeText;
        [SerializeField] private Image outcomeBannerBackground;
        [SerializeField] private Text outcomeHeadlineText;
        [SerializeField] private Text outcomeSublineText;

        [Header("Left Column: Flight Telemetry Metrics")]
        [SerializeField] private Text scienceMetricValueText;
        [SerializeField] private Text scienceMetricDetailText;
        [SerializeField] private Image scienceMetricBarFill;

        [SerializeField] private Text budgetMetricValueText;
        [SerializeField] private Text budgetMetricDetailText;
        [SerializeField] private Image budgetMetricBarFill;

        [SerializeField] private Text minBatteryMetricValueText;
        [SerializeField] private Text minBatteryMetricDetailText;
        [SerializeField] private Image minBatteryMetricBarFill;

        [SerializeField] private Text finalHealthMetricValueText;
        [SerializeField] private Text finalHealthMetricDetailText;
        [SerializeField] private Image finalHealthMetricBarFill;

        [SerializeField] private Text reliabilityMetricValueText;
        [SerializeField] private Text reliabilityMetricDetailText;
        [SerializeField] private Image reliabilityMetricBarFill;

        [Header("Right Column: Success State (MissionStatus.Success)")]
        [SerializeField] private GameObject successStateRoot;
        [SerializeField] private Text finalScoreValueText;
        [SerializeField] private Image ratingGradeBadgeBg;
        [SerializeField] private Text ratingGradeBadgeText;
        [SerializeField] private Text scoreFormulaBreakdownText;
        [SerializeField] private Image reliabilityBonusCardBg;
        [SerializeField] private Text reliabilityBonusTitleText;
        [SerializeField] private Text reliabilityBonusDetailText;
        [SerializeField] private Text contractRewardValueText;
        [SerializeField] private Text passiveIncomeValueText;
        [SerializeField] private Text agencyCapitalSummaryText;
        [SerializeField] private Button returnToHubButton;

        [Header("Right Column: Failure State (MissionStatus.Failed - Why Did It Fail?)")]
        [SerializeField] private GameObject failureStateRoot;
        [SerializeField] private Image failureTitleBannerBg;
        [SerializeField] private Text failureTitleText;
        [SerializeField] private Text failureCauseText;
        [SerializeField] private Text failureRecommendationText;
        [SerializeField] private Text failureTelemetryNoteText;
        [SerializeField] private Button returnToWorkshopButton;

        [Header("Sandbox & Verification Controls")]
        [SerializeField] private bool showVerificationBar = true;
        [SerializeField] private GameObject verificationBarRoot;

        private readonly FailureDebriefBuilder _debriefBuilder = new FailureDebriefBuilder();

        private IGameDataRepository _repository;
        private AgencyState _agencyState;
        private MissionGoal _activeGoal;
        private ResourceSummary _activeResources;
        private OrbitDefinition _activeOrbit;
        private MissionReport _currentReport;
        private MissionReport _lastRewardedReport;

        /// <summary>
        /// Currently displayed mission report.
        /// </summary>
        public MissionReport CurrentReport => _currentReport;

        /// <summary>
        /// Fired when the player clicks the button to return to the Contract Hub after a successful mission.
        /// </summary>
        public event Action OnReturnToHubRequested;

        /// <summary>
        /// Fired when the player clicks the button to return to the Hangar (Workshop) for free after a failed mission.
        /// </summary>
        public event Action OnReturnToWorkshopRequested;

        /// <summary>
        /// Fired whenever a new MissionReport is displayed on the debrief screen.
        /// </summary>
        public event Action<MissionReport> OnReportDisplayed;

        private void Awake()
        {
            Instance = this;
            EnsureDebriefUIHierarchy();
            BindButtons();
        }

        private void Start()
        {
            if (_currentReport == null)
            {
                ResolveContextFromScene();
                MissionReport initialReport = BuildSampleSuccessReport(true);
                ShowReport(initialReport, _activeGoal, _activeResources, _activeOrbit, false);
            }
        }

        private void OnEnable()
        {
            if (_currentReport != null)
            {
                RefreshUI();
            }
        }

        /// <summary>
        /// Initializes the Debrief screen with a MissionReport and optional mission context models.
        /// </summary>
        public void Initialize(
            MissionReport report,
            MissionGoal goal = null,
            AgencyState agencyState = null,
            ResourceSummary resources = null,
            OrbitDefinition orbit = null,
            IGameDataRepository repository = null)
        {
            EnsureDebriefUIHierarchy();
            BindButtons();

            _repository = repository ?? _repository;
            if (agencyState != null)
            {
                _agencyState = agencyState;
            }
            else if (_agencyState == null)
            {
                _agencyState = GameManager.Instance != null
                    ? GameManager.Instance.CurrentAgencyState
                    : new AgencyState();
            }

            ShowReport(report, goal, resources, orbit, true);
        }

        /// <summary>
        /// Displays the provided MissionReport on the Debrief screen and updates either the Success or Failure view.
        /// Automatically invokes FailureDebriefBuilder (S2-02) if a failed report does not yet have its debrief fields populated.
        /// </summary>
        public void ShowReport(
            MissionReport report,
            MissionGoal goal = null,
            ResourceSummary resources = null,
            OrbitDefinition orbit = null,
            bool applyContractPayoutOnSuccess = true)
        {
            EnsureDebriefUIHierarchy();
            ResolveContextFromScene(goal, resources, orbit);

            _currentReport = report ?? BuildSampleSuccessReport(true);

            if (_currentReport.Status == MissionStatus.Failed)
            {
                EnsureFailureDebriefPopulated(_currentReport);
            }
            else if (_currentReport.Status == MissionStatus.Success && applyContractPayoutOnSuccess)
            {
                ApplySuccessPayoutIfNeeded(_currentReport);
            }

            RefreshUI();
            OnReportDisplayed?.Invoke(_currentReport);
        }

        /// <summary>
        /// Convenience overload for displaying a MissionReport using the active mission context.
        /// </summary>
        public void DisplayReport(MissionReport report)
        {
            ShowReport(report, _activeGoal, _activeResources, _activeOrbit, true);
        }

        /// <summary>
        /// Handles the "Return to Contract Hub" button on the Victory screen.
        /// </summary>
        public void ReturnToHub()
        {
            OnReturnToHubRequested?.Invoke();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ShowHub();
            }
        }

        /// <summary>
        /// Handles the "Free Return to Hangar (Workshop)" button on the Failure screen.
        /// Does not deduct any capital so the player can freely adjust components or autopilot rules.
        /// </summary>
        public void ReturnToWorkshopFree()
        {
            OnReturnToWorkshopRequested?.Invoke();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ShowWorkshop();
            }
        }

        /// <summary>
        /// Editor context menu utility to generate the complete Debrief UI hierarchy inside a prefab or scene.
        /// </summary>
        [ContextMenu("Generate Debrief UI In Scene")]
        public void BuildUIInEditor()
        {
            Transform existing = transform.Find("DebriefContentRoot");
            if (existing != null)
            {
                DestroyImmediate(existing.gameObject);
            }

            EnsureDebriefUIHierarchy();
            BindButtons();
            ResolveContextFromScene();
            ShowReport(BuildSampleSuccessReport(true), _activeGoal, _activeResources, _activeOrbit, false);
        }

        private void ResolveContextFromScene(
            MissionGoal explicitGoal = null,
            ResourceSummary explicitResources = null,
            OrbitDefinition explicitOrbit = null)
        {
            if (_agencyState == null)
            {
                _agencyState = GameManager.Instance != null && GameManager.Instance.CurrentAgencyState != null
                    ? GameManager.Instance.CurrentAgencyState
                    : new AgencyState();
            }

            if (explicitGoal != null)
            {
                _activeGoal = explicitGoal;
            }
            else if (_activeGoal == null)
            {
                if (ContractHubUI.Instance != null && ContractHubUI.Instance.SelectedMission != null)
                {
                    _activeGoal = ContractHubUI.Instance.SelectedMission;
                }
                else
                {
                    _activeGoal = BuildDefaultMissionGoal();
                }
            }

            if (explicitOrbit != null)
            {
                _activeOrbit = explicitOrbit;
            }
            else if (_activeOrbit == null)
            {
                _activeOrbit = BuildDefaultOrbitForGoal(_activeGoal);
            }

            if (explicitResources != null)
            {
                _activeResources = explicitResources;
            }
            else if (_activeResources == null)
            {
                _activeResources = new ResourceSummary
                {
                    TotalMassKg = 15.0f,
                    TotalCostThousands = 90.0f,
                    MaxSolarWatts = 45.0f,
                    RtgWatts = 0.0f,
                    MaxBatteryWh = 45.0f,
                    MaxSsdGb = 16.0f,
                    DownlinkGbPerTick = 2.0f,
                    OverallReliability = 0.92f,
                    TotalShielding = 15.0f,
                    IsBudgetValid = true,
                    IsMassValid = true
                };
            }
        }

        private void EnsureFailureDebriefPopulated(MissionReport report)
        {
            if (report == null || report.Status != MissionStatus.Failed)
            {
                return;
            }

            bool needsDebrief =
                string.IsNullOrWhiteSpace(report.FailureTitle) ||
                string.IsNullOrWhiteSpace(report.FailureCause) ||
                string.IsNullOrWhiteSpace(report.FailureRecommendation);

            if (needsDebrief)
            {
                _debriefBuilder.BuildDebrief(report, _activeGoal, _activeResources, _activeOrbit);
            }
        }

        private void ApplySuccessPayoutIfNeeded(MissionReport report)
        {
            if (report == null || report.Status != MissionStatus.Success || _lastRewardedReport == report)
            {
                return;
            }

            _lastRewardedReport = report;

            if (_agencyState == null)
            {
                return;
            }

            int contractReward = _activeGoal != null ? _activeGoal.RewardThousands : 130;
            int newPassive = _activeGoal != null ? _activeGoal.PassiveIncomeThousands : 20;
            int existingPassive = _agencyState.TotalPassiveIncomeThousands;

            _agencyState.CapitalThousands += contractReward + existingPassive + newPassive;
            _agencyState.TotalPassiveIncomeThousands += newPassive;

            if (_activeGoal != null &&
                !string.IsNullOrEmpty(_activeGoal.Id) &&
                _agencyState.CompletedMissionIds != null &&
                !_agencyState.CompletedMissionIds.Contains(_activeGoal.Id))
            {
                _agencyState.CompletedMissionIds.Add(_activeGoal.Id);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SaveAgencyState();
            }
        }

        private void RefreshUI()
        {
            if (_currentReport == null)
            {
                return;
            }

            ResolveContextFromScene();

            string missionTitle = _activeGoal != null && !string.IsNullOrEmpty(_activeGoal.Title)
                ? _activeGoal.Title
                : "1. LEO Weather CubeSat";
            string orbitName = _activeOrbit != null && !string.IsNullOrEmpty(_activeOrbit.Name)
                ? _activeOrbit.Name
                : "LEO 400 km (Earth)";

            int completedOrbits = GetCompletedOrbits(_currentReport);
            int requiredOrbits = _activeGoal != null && _activeGoal.RequiredOrbits > 0
                ? _activeGoal.RequiredOrbits
                : Math.Max(1, completedOrbits);

            if (missionContextSubtitleText != null)
            {
                missionContextSubtitleText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Contract: {0}   •   Target Orbit: {1}   •   Orbits Logged: {2} / {3}",
                    missionTitle,
                    orbitName,
                    completedOrbits,
                    requiredOrbits);
            }

            RefreshTelemetryMetricsColumn(_currentReport);

            bool isSuccess = _currentReport.Status == MissionStatus.Success;
            if (successStateRoot != null) successStateRoot.SetActive(isSuccess);
            if (failureStateRoot != null) failureStateRoot.SetActive(!isSuccess);
            if (verificationBarRoot != null) verificationBarRoot.SetActive(showVerificationBar);

            if (isSuccess)
            {
                RefreshSuccessState(_currentReport);
            }
            else
            {
                RefreshFailureState(_currentReport);
            }
        }

        private void RefreshTelemetryMetricsColumn(MissionReport report)
        {
            float targetScience = _activeGoal != null && _activeGoal.TargetSciencePoints > 0f
                ? _activeGoal.TargetSciencePoints
                : 100.0f;
            float scienceRatio = Mathf.Clamp01(report.DeliveredScience / Mathf.Max(1f, targetScience));
            bool scienceMet = report.DeliveredScience + 0.01f >= targetScience;

            if (scienceMetricValueText != null)
            {
                scienceMetricValueText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.0} / {1:0.0} pts",
                    report.DeliveredScience,
                    targetScience);
                scienceMetricValueText.color = scienceMet
                    ? new Color(0.35f, 0.96f, 0.58f, 1f)
                    : new Color(1.0f, 0.45f, 0.42f, 1f);
            }
            if (scienceMetricDetailText != null)
            {
                scienceMetricDetailText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Downlinked Quota: {0:0}%   •   Lost SSD Overflow Data: {1:0.0} Gb",
                    scienceRatio * 100.0f,
                    report.LostDataGb);
            }
            if (scienceMetricBarFill != null)
            {
                scienceMetricBarFill.fillAmount = scienceRatio;
                scienceMetricBarFill.color = scienceMet
                    ? new Color(0.22f, 0.84f, 0.48f, 1f)
                    : new Color(0.92f, 0.32f, 0.30f, 1f);
            }

            float budgetLimit = _activeGoal != null && _activeGoal.BudgetLimitThousands > 0f
                ? _activeGoal.BudgetLimitThousands
                : 120.0f;
            float budgetSpent = report.BudgetSpentThousands > 0f
                ? report.BudgetSpentThousands
                : (_activeResources != null ? _activeResources.TotalCostThousands : 90.0f);
            float budgetSaved = Mathf.Max(0f, budgetLimit - budgetSpent);
            float budgetRatio = Mathf.Clamp01(budgetSpent / Mathf.Max(1f, budgetLimit));
            bool budgetValid = budgetSpent <= budgetLimit + 0.01f;

            if (budgetMetricValueText != null)
            {
                budgetMetricValueText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "${0:0.0}k / ${1:0.0}k",
                    budgetSpent,
                    budgetLimit);
                budgetMetricValueText.color = budgetValid
                    ? new Color(0.45f, 0.88f, 1.0f, 1f)
                    : new Color(1.0f, 0.45f, 0.42f, 1f);
            }
            if (budgetMetricDetailText != null)
            {
                budgetMetricDetailText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Unspent Margin: ${0:0.0}k   •   Efficiency Score Bonus: +{1:0.0} pts",
                    budgetSaved,
                    budgetSaved * 0.5f);
            }
            if (budgetMetricBarFill != null)
            {
                budgetMetricBarFill.fillAmount = budgetRatio;
                budgetMetricBarFill.color = budgetValid
                    ? new Color(0.24f, 0.72f, 0.96f, 1f)
                    : new Color(0.92f, 0.32f, 0.30f, 1f);
            }

            float minBat = Mathf.Clamp(report.MinBatteryPercent, 0f, 100f);
            bool batMarginMet = minBat >= MinBatteryBonusThreshold;
            if (minBatteryMetricValueText != null)
            {
                minBatteryMetricValueText.text = string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", minBat);
                minBatteryMetricValueText.color = minBat <= 0.01f
                    ? new Color(1.0f, 0.36f, 0.36f, 1f)
                    : (batMarginMet ? new Color(0.35f, 0.96f, 0.58f, 1f) : new Color(1.0f, 0.78f, 0.28f, 1f));
            }
            if (minBatteryMetricDetailText != null)
            {
                minBatteryMetricDetailText.text = minBat <= 0.01f
                    ? "CRITICAL: Battery depleted to 0% during orbital eclipse"
                    : (batMarginMet
                        ? "PASS: Above 15.0% NASA reserve threshold (Reliability Bonus OK)"
                        : "WARNING: Dropped below 15.0% reserve threshold (No +50 Bonus)");
            }
            if (minBatteryMetricBarFill != null)
            {
                minBatteryMetricBarFill.fillAmount = minBat / 100.0f;
                minBatteryMetricBarFill.color = minBat <= 0.01f
                    ? new Color(0.92f, 0.26f, 0.26f, 1f)
                    : (batMarginMet ? new Color(0.22f, 0.84f, 0.48f, 1f) : new Color(0.95f, 0.68f, 0.20f, 1f));
            }

            float finalHealth = Mathf.Clamp(report.FinalHealthPercent, 0f, 100f);
            bool healthMarginMet = finalHealth >= MinHealthBonusThreshold;
            if (finalHealthMetricValueText != null)
            {
                finalHealthMetricValueText.text = string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", finalHealth);
                finalHealthMetricValueText.color = finalHealth <= 0.01f
                    ? new Color(1.0f, 0.36f, 0.36f, 1f)
                    : (healthMarginMet ? new Color(0.35f, 0.96f, 0.58f, 1f) : new Color(1.0f, 0.78f, 0.28f, 1f));
            }
            if (finalHealthMetricDetailText != null)
            {
                finalHealthMetricDetailText.text = finalHealth <= 0.01f
                    ? "CRITICAL: Avionics destroyed by radiation (0% Health)"
                    : (healthMarginMet
                        ? "PASS: Above 80.0% electronics integrity threshold (Reliability Bonus OK)"
                        : "WARNING: Degraded below 80.0% integrity threshold (No +50 Bonus)");
            }
            if (finalHealthMetricBarFill != null)
            {
                finalHealthMetricBarFill.fillAmount = finalHealth / 100.0f;
                finalHealthMetricBarFill.color = finalHealth <= 0.01f
                    ? new Color(0.92f, 0.26f, 0.26f, 1f)
                    : (healthMarginMet ? new Color(0.22f, 0.84f, 0.48f, 1f) : new Color(0.95f, 0.68f, 0.20f, 1f));
            }

            float reliability = _activeResources != null && _activeResources.OverallReliability > 0f
                ? _activeResources.OverallReliability
                : 0.92f;
            float reliabilityPercent = Mathf.Clamp(reliability * 100.0f, 0f, 100f);
            bool relMarginMet = reliability >= MinReliabilityBonusThreshold;

            if (reliabilityMetricValueText != null)
            {
                reliabilityMetricValueText.text = string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", reliabilityPercent);
                reliabilityMetricValueText.color = relMarginMet
                    ? new Color(0.35f, 0.96f, 0.58f, 1f)
                    : new Color(1.0f, 0.78f, 0.28f, 1f);
            }
            if (reliabilityMetricDetailText != null)
            {
                reliabilityMetricDetailText.text = relMarginMet
                    ? "PASS: Spacecraft assembly reliability >= 85.0% NASA standard"
                    : "WARNING: Spacecraft assembly reliability < 85.0% threshold";
            }
            if (reliabilityMetricBarFill != null)
            {
                reliabilityMetricBarFill.fillAmount = reliabilityPercent / 100.0f;
                reliabilityMetricBarFill.color = relMarginMet
                    ? new Color(0.22f, 0.84f, 0.48f, 1f)
                    : new Color(0.95f, 0.68f, 0.20f, 1f);
            }
        }

        private void RefreshSuccessState(MissionReport report)
        {
            float budgetLimit = _activeGoal != null && _activeGoal.BudgetLimitThousands > 0f
                ? _activeGoal.BudgetLimitThousands
                : 120.0f;
            float budgetSpent = report.BudgetSpentThousands > 0f
                ? report.BudgetSpentThousands
                : (_activeResources != null ? _activeResources.TotalCostThousands : 90.0f);
            float budgetBonus = Mathf.Max(0f, (budgetLimit - budgetSpent) * 0.5f);

            float overallReliability = _activeResources != null && _activeResources.OverallReliability > 0f
                ? _activeResources.OverallReliability
                : 0.92f;

            bool reliabilityBonusEarned =
                report.MinBatteryPercent >= MinBatteryBonusThreshold &&
                report.FinalHealthPercent >= MinHealthBonusThreshold &&
                overallReliability >= MinReliabilityBonusThreshold;

            float bonusPoints = reliabilityBonusEarned ? ReliabilityBonusPoints : 0.0f;
            float calculatedScore = report.DeliveredScience + budgetBonus + bonusPoints;
            float displayScore = report.FinalScore > 0.01f ? report.FinalScore : calculatedScore;

            string gradeLetter = GetMissionGrade(displayScore, reliabilityBonusEarned);

            if (statusBadgeBackground != null)
            {
                statusBadgeBackground.color = new Color(0.12f, 0.48f, 0.28f, 1f);
            }
            if (statusBadgeText != null)
            {
                statusBadgeText.text = "STATUS: MISSION SUCCESS";
                statusBadgeText.color = new Color(0.85f, 1.0f, 0.90f, 1f);
            }

            if (outcomeBannerBackground != null)
            {
                outcomeBannerBackground.color = new Color(0.08f, 0.26f, 0.18f, 0.96f);
            }
            if (outcomeHeadlineText != null)
            {
                outcomeHeadlineText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "MISSION ACCOMPLISHED  •  FINAL SCORE: {0:0.0} PTS  ({1})",
                    displayScore,
                    gradeLetter);
                outcomeHeadlineText.color = new Color(0.42f, 0.98f, 0.64f, 1f);
            }
            if (outcomeSublineText != null)
            {
                outcomeSublineText.text =
                    "All orbital objectives verified by NASA Ground Control. Science data downlinked and contract reward credited.";
                outcomeSublineText.color = new Color(0.82f, 0.95f, 0.88f, 1f);
            }

            if (finalScoreValueText != null)
            {
                finalScoreValueText.text = string.Format(CultureInfo.InvariantCulture, "{0:0.0} PTS", displayScore);
            }

            if (ratingGradeBadgeBg != null)
            {
                ratingGradeBadgeBg.color = reliabilityBonusEarned
                    ? new Color(0.14f, 0.54f, 0.32f, 1f)
                    : new Color(0.20f, 0.44f, 0.68f, 1f);
            }
            if (ratingGradeBadgeText != null)
            {
                ratingGradeBadgeText.text = gradeLetter;
            }

            if (scoreFormulaBreakdownText != null)
            {
                scoreFormulaBreakdownText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "• Delivered Science Points: <b>+{0:0.0} pts</b>\n" +
                    "• Budget Efficiency Bonus (50% of ${1:0.0}k saved): <b>+{2:0.0} pts</b>\n" +
                    "• Engineering Reliability Margin Bonus: <b>+{3:0.0} pts</b>",
                    report.DeliveredScience,
                    Mathf.Max(0f, budgetLimit - budgetSpent),
                    budgetBonus,
                    bonusPoints);
            }

            if (reliabilityBonusCardBg != null)
            {
                reliabilityBonusCardBg.color = reliabilityBonusEarned
                    ? new Color(0.09f, 0.30f, 0.20f, 0.95f)
                    : new Color(0.28f, 0.22f, 0.10f, 0.95f);
            }
            if (reliabilityBonusTitleText != null)
            {
                reliabilityBonusTitleText.text = reliabilityBonusEarned
                    ? "RELIABILITY MARGIN BONUS: +50.0 PTS AWARDED"
                    : "RELIABILITY MARGIN BONUS: +0.0 PTS (THRESHOLD MISSED)";
                reliabilityBonusTitleText.color = reliabilityBonusEarned
                    ? new Color(0.42f, 0.98f, 0.64f, 1f)
                    : new Color(1.0f, 0.82f, 0.38f, 1f);
            }
            if (reliabilityBonusDetailText != null)
            {
                reliabilityBonusDetailText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Conditions: Min Battery {0:0.0}% (req >= 15%)  •  Final Health {1:0.0}% (req >= 80%)  •  Reliability {2:0.0}% (req >= 85%)",
                    report.MinBatteryPercent,
                    report.FinalHealthPercent,
                    overallReliability * 100.0f);
            }

            int rewardThousands = _activeGoal != null ? _activeGoal.RewardThousands : 130;
            int passiveThousands = _activeGoal != null ? _activeGoal.PassiveIncomeThousands : 20;
            float currentCapital = _agencyState != null ? _agencyState.CapitalThousands : 200.0f;
            int totalPassive = _agencyState != null ? _agencyState.TotalPassiveIncomeThousands : passiveThousands;

            if (contractRewardValueText != null)
            {
                contractRewardValueText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "+${0}k Contract Payout",
                    rewardThousands);
            }
            if (passiveIncomeValueText != null)
            {
                passiveIncomeValueText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "+${0}k / Flight Passive Income",
                    passiveThousands);
            }
            if (agencyCapitalSummaryText != null)
            {
                agencyCapitalSummaryText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Total Payout Credited: <b>+${0}k</b> (Contract +${1}k + Passive Income +${2}k)\n" +
                    "Agency Balance: <b>${3:0}k</b>   •   Active Passive Income Stream: <b>+${4}k / mission</b>",
                    rewardThousands + passiveThousands,
                    rewardThousands,
                    passiveThousands,
                    currentCapital,
                    totalPassive);
            }
        }

        private void RefreshFailureState(MissionReport report)
        {
            EnsureFailureDebriefPopulated(report);

            string failTitle = !string.IsNullOrWhiteSpace(report.FailureTitle)
                ? report.FailureTitle
                : FailureDebriefBuilder.GetTitle(FailureScenarioType.InsufficientScienceNoLoss);

            string failCause = !string.IsNullOrWhiteSpace(report.FailureCause)
                ? report.FailureCause
                : "Telemetry stream ended before mission objectives were satisfied.";

            string failRecommendation = !string.IsNullOrWhiteSpace(report.FailureRecommendation)
                ? report.FailureRecommendation
                : FailureDebriefBuilder.GetRecommendation(FailureScenarioType.InsufficientScienceNoLoss);

            if (statusBadgeBackground != null)
            {
                statusBadgeBackground.color = new Color(0.58f, 0.14f, 0.16f, 1f);
            }
            if (statusBadgeText != null)
            {
                statusBadgeText.text = "STATUS: MISSION FAILED";
                statusBadgeText.color = new Color(1.0f, 0.88f, 0.88f, 1f);
            }

            if (outcomeBannerBackground != null)
            {
                outcomeBannerBackground.color = new Color(0.32f, 0.09f, 0.12f, 0.96f);
            }
            if (outcomeHeadlineText != null)
            {
                outcomeHeadlineText.text = "WHY DID IT FAIL?  •  A.S.T.R.A. POST-FLIGHT ANOMALY INVESTIGATION";
                outcomeHeadlineText.color = new Color(1.0f, 0.48f, 0.46f, 1f);
            }
            if (outcomeSublineText != null)
            {
                outcomeSublineText.text =
                    "Review the exact orbital telemetry numbers below and return to the Hangar for free to update your satellite or autopilot rules.";
                outcomeSublineText.color = new Color(0.96f, 0.84f, 0.84f, 1f);
            }

            if (failureTitleBannerBg != null)
            {
                failureTitleBannerBg.color = new Color(0.42f, 0.11f, 0.14f, 0.98f);
            }
            if (failureTitleText != null)
            {
                failureTitleText.text = failTitle;
            }
            if (failureCauseText != null)
            {
                failureCauseText.text = failCause;
            }
            if (failureRecommendationText != null)
            {
                failureRecommendationText.text = failRecommendation;
            }

            if (failureTelemetryNoteText != null)
            {
                int lastOrbit = GetCompletedOrbits(report);
                int lastTick = GetLastTickIndex(report);
                failureTelemetryNoteText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "Last Recorded Telemetry: Orbit <b>{0}</b> (Tick <b>{1}</b>)   •   Delivered Science: <b>{2:0.0} pts</b>   •   " +
                    "Lost SSD Data: <b>{3:0.0} Gb</b>\n" +
                    "<color=#7CE89A><b>FREE RETRY POLICY:</b> $0k deducted from Agency Capital. Modify your components or autopilot rules in the Hangar at no penalty.</color>",
                    lastOrbit,
                    lastTick,
                    report.DeliveredScience,
                    report.LostDataGb);
            }
        }

        private static string GetMissionGrade(float score, bool reliabilityBonusEarned)
        {
            if (reliabilityBonusEarned && score >= 150.0f)
            {
                return "GRADE S  •  EXEMPLARY";
            }
            if (score >= 120.0f)
            {
                return "GRADE A  •  OPTIMAL";
            }
            return "GRADE B  •  QUALIFIED";
        }

        private static int GetCompletedOrbits(MissionReport report)
        {
            if (report == null || report.Ticks == null || report.Ticks.Count == 0)
            {
                return 1;
            }
            SimulationTickSnapshot last = report.Ticks[report.Ticks.Count - 1];
            return last != null && last.OrbitNumber > 0 ? last.OrbitNumber : 1;
        }

        private static int GetLastTickIndex(MissionReport report)
        {
            if (report == null || report.Ticks == null || report.Ticks.Count == 0)
            {
                return 0;
            }
            SimulationTickSnapshot last = report.Ticks[report.Ticks.Count - 1];
            return last != null ? last.TickIndex : (report.Ticks.Count - 1);
        }

        // --- Sample Scenarios Powered by FailureDebriefBuilder (Task S2-02) ---

        /// <summary>
        /// Triggers a sample successful mission report (with or without the +50 reliability bonus) for testing.
        /// </summary>
        public void TriggerSampleSuccessScenario(bool withReliabilityBonus)
        {
            ResolveContextFromScene();
            MissionReport report = BuildSampleSuccessReport(withReliabilityBonus);
            ShowReport(report, _activeGoal, _activeResources, _activeOrbit, false);
        }

        /// <summary>
        /// Triggers one of the deterministic failure scenarios and populates it using FailureDebriefBuilder (S2-02).
        /// </summary>
        public void TriggerSampleFailureScenario(FailureScenarioType scenarioType)
        {
            ResolveContextFromScene();
            MissionReport report = BuildSampleFailureReport(scenarioType);
            ShowReport(report, _activeGoal, _activeResources, _activeOrbit, false);
        }

        private MissionReport BuildSampleSuccessReport(bool withReliabilityBonus)
        {
            float targetScience = _activeGoal != null && _activeGoal.TargetSciencePoints > 0f
                ? _activeGoal.TargetSciencePoints
                : 100.0f;
            float budgetLimit = _activeGoal != null && _activeGoal.BudgetLimitThousands > 0f
                ? _activeGoal.BudgetLimitThousands
                : 120.0f;
            float budgetSpent = 90.0f;
            float deliveredScience = targetScience + 20.0f;
            float minBattery = withReliabilityBonus ? 60.0f : 9.5f;
            float finalHealth = withReliabilityBonus ? 100.0f : 76.0f;
            float bonus = withReliabilityBonus ? ReliabilityBonusPoints : 0.0f;
            float finalScore = deliveredScience + Mathf.Max(0f, (budgetLimit - budgetSpent) * 0.5f) + bonus;

            var report = new MissionReport
            {
                Status = MissionStatus.Success,
                FinalScore = finalScore,
                DeliveredScience = deliveredScience,
                LostDataGb = 0.0f,
                BudgetSpentThousands = budgetSpent,
                MinBatteryPercent = minBattery,
                FinalHealthPercent = finalHealth,
                FailureTitle = string.Empty,
                FailureCause = string.Empty,
                FailureRecommendation = string.Empty,
                Ticks = new List<SimulationTickSnapshot>
                {
                    new SimulationTickSnapshot
                    {
                        TickIndex = 79,
                        OrbitNumber = _activeGoal != null && _activeGoal.RequiredOrbits > 0 ? _activeGoal.RequiredOrbits : 4,
                        OrbitProgress = 0.95f,
                        BatteryPercent = 88.0f,
                        BatteryWh = 39.6f,
                        HealthPercent = finalHealth,
                        DeliveredScience = deliveredScience
                    }
                }
            };

            return report;
        }

        private MissionReport BuildSampleFailureReport(FailureScenarioType scenarioType)
        {
            float targetScience = _activeGoal != null && _activeGoal.TargetSciencePoints > 0f
                ? _activeGoal.TargetSciencePoints
                : 100.0f;
            int requiredOrbits = _activeGoal != null && _activeGoal.RequiredOrbits > 0
                ? _activeGoal.RequiredOrbits
                : 4;

            var report = new MissionReport
            {
                Status = MissionStatus.Failed,
                FinalScore = 0.0f,
                BudgetSpentThousands = 90.0f,
                Ticks = new List<SimulationTickSnapshot>()
            };

            switch (scenarioType)
            {
                case FailureScenarioType.BatteryDepleted:
                    report.DeliveredScience = 32.5f;
                    report.LostDataGb = 0.0f;
                    report.MinBatteryPercent = 0.0f;
                    report.FinalHealthPercent = 100.0f;
                    report.Ticks.Add(new SimulationTickSnapshot
                    {
                        TickIndex = 11,
                        OrbitNumber = 1,
                        OrbitProgress = 0.75f,
                        IsInEclipse = true,
                        SolarGenWatts = 0.0f,
                        PowerDrawWatts = 27.0f,
                        BatteryWh = 0.0f,
                        BatteryPercent = 0.0f,
                        HealthPercent = 100.0f,
                        DeliveredScience = 32.5f
                    });
                    break;

                case FailureScenarioType.RadiationDamage:
                    report.DeliveredScience = 64.0f;
                    report.LostDataGb = 0.0f;
                    report.MinBatteryPercent = 48.0f;
                    report.FinalHealthPercent = 0.0f;
                    report.Ticks.Add(new SimulationTickSnapshot
                    {
                        TickIndex = 47,
                        OrbitNumber = 3,
                        OrbitProgress = 0.35f,
                        IsInEclipse = false,
                        SafeModeActive = false,
                        SolarGenWatts = 45.0f,
                        PowerDrawWatts = 24.0f,
                        BatteryWh = 22.0f,
                        BatteryPercent = 48.0f,
                        RadiationLevel = 105.0f,
                        HealthPercent = 0.0f,
                        DeliveredScience = 64.0f,
                        ActiveEventTitle = "Solar Storm (CME)"
                    });
                    break;

                case FailureScenarioType.InsufficientScienceWithSsdLoss:
                    report.DeliveredScience = targetScience * 0.62f;
                    report.LostDataGb = 6.4f;
                    report.MinBatteryPercent = 42.0f;
                    report.FinalHealthPercent = 96.0f;
                    report.Ticks.Add(new SimulationTickSnapshot
                    {
                        TickIndex = requiredOrbits * 20 - 1,
                        OrbitNumber = requiredOrbits,
                        OrbitProgress = 0.95f,
                        BatteryWh = 30.0f,
                        BatteryPercent = 66.0f,
                        SsdUsedGb = 16.0f,
                        SsdUsedPercent = 100.0f,
                        HealthPercent = 96.0f,
                        DeliveredScience = report.DeliveredScience
                    });
                    break;

                case FailureScenarioType.InsufficientScienceNoLoss:
                default:
                    report.DeliveredScience = targetScience * 0.70f;
                    report.LostDataGb = 0.0f;
                    report.MinBatteryPercent = 55.0f;
                    report.FinalHealthPercent = 100.0f;
                    report.Ticks.Add(new SimulationTickSnapshot
                    {
                        TickIndex = requiredOrbits * 20 - 1,
                        OrbitNumber = requiredOrbits,
                        OrbitProgress = 0.95f,
                        BatteryWh = 36.0f,
                        BatteryPercent = 80.0f,
                        SsdUsedGb = 2.0f,
                        SsdUsedPercent = 12.5f,
                        HealthPercent = 100.0f,
                        DeliveredScience = report.DeliveredScience
                    });
                    break;
            }

            _debriefBuilder.BuildDebrief(report, _activeGoal, _activeResources, _activeOrbit);
            return report;
        }

        private static MissionGoal BuildDefaultMissionGoal()
        {
            return new MissionGoal
            {
                Id = "m1_leo_cubesat",
                Title = "1. LEO Weather CubeSat",
                Brief = "Launch a weather satellite to 400 km LEO, deliver 100 science points, and survive 4 orbits.",
                OrbitId = "orbit_leo",
                BudgetLimitThousands = 120.0f,
                TargetSciencePoints = 100.0f,
                RequiredOrbits = 4,
                RewardThousands = 130,
                PassiveIncomeThousands = 20,
                MinChipSlots = 2,
                RequiredSensorTag = "Camera",
                EventId = string.Empty
            };
        }

        private static OrbitDefinition BuildDefaultOrbitForGoal(MissionGoal goal)
        {
            string orbitId = goal != null ? goal.OrbitId : "orbit_leo";
            if (string.Equals(orbitId, "orbit_lunar", StringComparison.OrdinalIgnoreCase))
            {
                return new OrbitDefinition
                {
                    Id = "orbit_lunar",
                    Name = "Lunar Orbit (100 km)",
                    Type = OrbitType.Lunar,
                    OrbitPeriodHours = 2.0f,
                    EclipseFraction = 0.45f,
                    CommWindowFraction = 0.15f,
                    SolarFluxFactor = 1.0f,
                    SignalDelaySeconds = 3.0f,
                    BaseRadiation = 15.0f
                };
            }
            if (string.Equals(orbitId, "orbit_mars", StringComparison.OrdinalIgnoreCase))
            {
                return new OrbitDefinition
                {
                    Id = "orbit_mars",
                    Name = "Mars Orbit (350 km)",
                    Type = OrbitType.Mars,
                    OrbitPeriodHours = 2.0f,
                    EclipseFraction = 0.35f,
                    CommWindowFraction = 0.15f,
                    SolarFluxFactor = 0.43f,
                    SignalDelaySeconds = 10.0f,
                    BaseRadiation = 20.0f
                };
            }
            return new OrbitDefinition
            {
                Id = "orbit_leo",
                Name = "LEO 400 km (Earth)",
                Type = OrbitType.LowEarth,
                OrbitPeriodHours = 1.5f,
                EclipseFraction = 0.35f,
                CommWindowFraction = 0.25f,
                SolarFluxFactor = 1.0f,
                SignalDelaySeconds = 1.0f,
                BaseRadiation = 5.0f
            };
        }

        // --- UI Hierarchy Builder & Binding ---

        private void BindButtons()
        {
            if (returnToHubButton != null)
            {
                returnToHubButton.onClick.RemoveListener(ReturnToHub);
                returnToHubButton.onClick.AddListener(ReturnToHub);
            }

            if (returnToWorkshopButton != null)
            {
                returnToWorkshopButton.onClick.RemoveListener(ReturnToWorkshopFree);
                returnToWorkshopButton.onClick.AddListener(ReturnToWorkshopFree);
            }
        }

        private void EnsureDebriefUIHierarchy()
        {
            Transform root = transform.Find("DebriefContentRoot");
            if (root != null && finalScoreValueText != null && failureTitleText != null)
            {
                return;
            }

            if (root != null)
            {
                DestroyImmediate(root.gameObject);
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            RectTransform selfRt = GetComponent<RectTransform>();
            if (selfRt == null)
            {
                selfRt = gameObject.AddComponent<RectTransform>();
            }
            StretchFull(selfRt);

            Image baseBg = GetComponent<Image>();
            if (baseBg == null)
            {
                baseBg = gameObject.AddComponent<Image>();
            }
            baseBg.color = new Color(0.05f, 0.08f, 0.14f, 1f);

            GameObject contentRoot = CreateUIObject("DebriefContentRoot", transform);
            StretchFull(contentRoot.GetComponent<RectTransform>());

            VerticalLayoutGroup rootLayout = contentRoot.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(28, 28, 20, 20);
            rootLayout.spacing = 14f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            // 1. Top Header Bar
            BuildTopHeaderBar(contentRoot.transform, font);

            // 2. Outcome Headline Banner
            BuildOutcomeBanner(contentRoot.transform, font);

            // 3. Main Two-Column Content Row
            GameObject mainRow = CreateUIObject("MainContentRow", contentRoot.transform);
            LayoutElement mainRowLe = mainRow.AddComponent<LayoutElement>();
            mainRowLe.flexibleHeight = 1f;
            mainRowLe.preferredHeight = 640f;

            HorizontalLayoutGroup mainRowLayout = mainRow.AddComponent<HorizontalLayoutGroup>();
            mainRowLayout.spacing = 20f;
            mainRowLayout.childControlWidth = true;
            mainRowLayout.childControlHeight = true;
            mainRowLayout.childForceExpandWidth = true;
            mainRowLayout.childForceExpandHeight = true;

            BuildTelemetryMetricsColumn(mainRow.transform, font);
            BuildRightStateColumn(mainRow.transform, font);

            // 4. Bottom Verification Switcher Bar
            BuildVerificationSwitcherBar(contentRoot.transform, font);
        }

        private void BuildTopHeaderBar(Transform parent, Font font)
        {
            GameObject headerBar = CreatePanel("HeaderBar", parent, new Color(0.08f, 0.13f, 0.22f, 0.98f), 74f);
            HorizontalLayoutGroup headerLayout = headerBar.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(22, 22, 10, 10);
            headerLayout.spacing = 16f;
            headerLayout.childAlignment = TextAnchor.MiddleLeft;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;

            GameObject titleStack = CreateUIObject("TitleStack", headerBar.transform);
            LayoutElement titleStackLe = titleStack.AddComponent<LayoutElement>();
            titleStackLe.flexibleWidth = 1f;

            VerticalLayoutGroup titleStackLayout = titleStack.AddComponent<VerticalLayoutGroup>();
            titleStackLayout.spacing = 2f;
            titleStackLayout.childControlWidth = true;
            titleStackLayout.childControlHeight = true;
            titleStackLayout.childForceExpandWidth = true;
            titleStackLayout.childForceExpandHeight = false;

            CreateText(
                "ScreenTitle",
                titleStack.transform,
                font,
                "ASTRA-LINK // POST-FLIGHT MISSION DEBRIEFING & ANALYSIS",
                22,
                FontStyle.Bold,
                new Color(0.92f, 0.97f, 1.0f, 1f),
                TextAnchor.MiddleLeft);

            missionContextSubtitleText = CreateText(
                "MissionSubtitle",
                titleStack.transform,
                font,
                "Contract: 1. LEO Weather CubeSat   •   Target Orbit: LEO 400 km (Earth)",
                14,
                FontStyle.Normal,
                new Color(0.65f, 0.80f, 0.95f, 1f),
                TextAnchor.MiddleLeft);

            GameObject badgeObj = CreatePanel("StatusBadge", headerBar.transform, new Color(0.12f, 0.48f, 0.28f, 1f), 48f);
            LayoutElement badgeLe = badgeObj.GetComponent<LayoutElement>();
            badgeLe.preferredWidth = 290f;
            statusBadgeBackground = badgeObj.GetComponent<Image>();

            statusBadgeText = CreateText(
                "StatusBadgeText",
                badgeObj.transform,
                font,
                "STATUS: MISSION SUCCESS",
                16,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter);
            StretchFull(statusBadgeText.GetComponent<RectTransform>());
        }

        private void BuildOutcomeBanner(Transform parent, Font font)
        {
            GameObject bannerObj = CreatePanel("OutcomeBanner", parent, new Color(0.08f, 0.26f, 0.18f, 0.96f), 82f);
            outcomeBannerBackground = bannerObj.GetComponent<Image>();

            VerticalLayoutGroup bannerLayout = bannerObj.AddComponent<VerticalLayoutGroup>();
            bannerLayout.padding = new RectOffset(22, 22, 10, 10);
            bannerLayout.spacing = 4f;
            bannerLayout.childControlWidth = true;
            bannerLayout.childControlHeight = true;
            bannerLayout.childForceExpandWidth = true;
            bannerLayout.childForceExpandHeight = false;

            outcomeHeadlineText = CreateText(
                "OutcomeHeadline",
                bannerObj.transform,
                font,
                "MISSION ACCOMPLISHED  •  FINAL SCORE: 162.5 PTS",
                22,
                FontStyle.Bold,
                new Color(0.42f, 0.98f, 0.64f, 1f),
                TextAnchor.MiddleLeft);

            outcomeSublineText = CreateText(
                "OutcomeSubline",
                bannerObj.transform,
                font,
                "All orbital objectives verified by NASA Ground Control.",
                14,
                FontStyle.Normal,
                new Color(0.84f, 0.95f, 0.88f, 1f),
                TextAnchor.MiddleLeft);
        }

        private void BuildTelemetryMetricsColumn(Transform parent, Font font)
        {
            GameObject leftCol = CreatePanel("TelemetryMetricsColumn", parent, new Color(0.07f, 0.11f, 0.19f, 0.98f), 0f);
            LayoutElement leftLe = leftCol.AddComponent<LayoutElement>();
            leftLe.flexibleWidth = 0.95f;
            leftLe.preferredWidth = 820f;

            VerticalLayoutGroup leftLayout = leftCol.AddComponent<VerticalLayoutGroup>();
            leftLayout.padding = new RectOffset(20, 20, 18, 18);
            leftLayout.spacing = 12f;
            leftLayout.childControlWidth = true;
            leftLayout.childControlHeight = true;
            leftLayout.childForceExpandWidth = true;
            leftLayout.childForceExpandHeight = false;

            CreateText(
                "MetricsHeader",
                leftCol.transform,
                font,
                "FLIGHT TELEMETRY & NASA RESOURCE SUMMARY",
                18,
                FontStyle.Bold,
                new Color(0.55f, 0.84f, 1.0f, 1f),
                TextAnchor.MiddleLeft);

            BuildMetricCard(
                leftCol.transform,
                font,
                "Metric_Science",
                "1. DELIVERED SCIENCE (DOWNLINKED QUOTA)",
                "120.0 / 100.0 pts",
                "Downlinked Quota: 100%   •   Lost SSD Overflow Data: 0.0 Gb",
                new Color(0.22f, 0.84f, 0.48f, 1f),
                out scienceMetricValueText,
                out scienceMetricDetailText,
                out scienceMetricBarFill);

            BuildMetricCard(
                leftCol.transform,
                font,
                "Metric_Budget",
                "2. MISSION BUDGET SPENT (HARDWARE + ROCKET)",
                "$90.0k / $120.0k",
                "Unspent Margin: $30.0k   •   Efficiency Score Bonus: +15.0 pts",
                new Color(0.24f, 0.72f, 0.96f, 1f),
                out budgetMetricValueText,
                out budgetMetricDetailText,
                out budgetMetricBarFill);

            BuildMetricCard(
                leftCol.transform,
                font,
                "Metric_MinBattery",
                "3. MINIMUM BATTERY CHARGE (ECLIPSE RESERVE)",
                "60.0%",
                "PASS: Above 15.0% NASA reserve threshold (Reliability Bonus OK)",
                new Color(0.22f, 0.84f, 0.48f, 1f),
                out minBatteryMetricValueText,
                out minBatteryMetricDetailText,
                out minBatteryMetricBarFill);

            BuildMetricCard(
                leftCol.transform,
                font,
                "Metric_FinalHealth",
                "4. FINAL ELECTRONICS HEALTH (RADIATION INTEGRITY)",
                "100.0%",
                "PASS: Above 80.0% electronics integrity threshold (Reliability Bonus OK)",
                new Color(0.22f, 0.84f, 0.48f, 1f),
                out finalHealthMetricValueText,
                out finalHealthMetricDetailText,
                out finalHealthMetricBarFill);

            BuildMetricCard(
                leftCol.transform,
                font,
                "Metric_Reliability",
                "5. SPACECRAFT OVERALL RELIABILITY",
                "92.0%",
                "PASS: Spacecraft assembly reliability >= 85.0% NASA standard",
                new Color(0.22f, 0.84f, 0.48f, 1f),
                out reliabilityMetricValueText,
                out reliabilityMetricDetailText,
                out reliabilityMetricBarFill);
        }

        private static void BuildMetricCard(
            Transform parent,
            Font font,
            string name,
            string label,
            string initialValue,
            string initialDetail,
            Color barColor,
            out Text valueText,
            out Text detailText,
            out Image barFill)
        {
            GameObject card = CreatePanel(name, parent, new Color(0.04f, 0.07f, 0.13f, 0.95f), 96f);
            VerticalLayoutGroup cardLayout = card.AddComponent<VerticalLayoutGroup>();
            cardLayout.padding = new RectOffset(16, 16, 10, 10);
            cardLayout.spacing = 6f;
            cardLayout.childControlWidth = true;
            cardLayout.childControlHeight = true;
            cardLayout.childForceExpandWidth = true;
            cardLayout.childForceExpandHeight = false;

            GameObject topRow = CreateUIObject("TopRow", card.transform);
            HorizontalLayoutGroup topLayout = topRow.AddComponent<HorizontalLayoutGroup>();
            topLayout.childControlWidth = true;
            topLayout.childControlHeight = true;
            topLayout.childForceExpandWidth = false;
            topLayout.childForceExpandHeight = false;

            Text titleTxt = CreateText(
                "Label",
                topRow.transform,
                font,
                label,
                14,
                FontStyle.Bold,
                new Color(0.76f, 0.86f, 0.96f, 1f),
                TextAnchor.MiddleLeft);
            LayoutElement titleLe = titleTxt.gameObject.AddComponent<LayoutElement>();
            titleLe.flexibleWidth = 1f;

            valueText = CreateText(
                "Value",
                topRow.transform,
                font,
                initialValue,
                18,
                FontStyle.Bold,
                new Color(0.35f, 0.96f, 0.58f, 1f),
                TextAnchor.MiddleRight);

            GameObject barTrack = CreatePanel("BarTrack", card.transform, new Color(0.12f, 0.18f, 0.28f, 1f), 12f);
            GameObject fillObj = CreateUIObject("BarFill", barTrack.transform);
            StretchFull(fillObj.GetComponent<RectTransform>());
            barFill = fillObj.AddComponent<Image>();
            barFill.sprite = CreateSolidWhiteSprite();
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFill.fillAmount = 0.85f;
            barFill.color = barColor;

            detailText = CreateText(
                "Detail",
                card.transform,
                font,
                initialDetail,
                13,
                FontStyle.Normal,
                new Color(0.68f, 0.78f, 0.90f, 1f),
                TextAnchor.MiddleLeft);
        }

        private void BuildRightStateColumn(Transform parent, Font font)
        {
            GameObject rightContainer = CreateUIObject("RightStateColumn", parent);
            LayoutElement rightLe = rightContainer.AddComponent<LayoutElement>();
            rightLe.flexibleWidth = 1.05f;
            rightLe.preferredWidth = 920f;

            BuildSuccessStatePanel(rightContainer.transform, font);
            BuildFailureStatePanel(rightContainer.transform, font);
        }

        private void BuildSuccessStatePanel(Transform parent, Font font)
        {
            successStateRoot = CreatePanel("SuccessStatePanel", parent, new Color(0.07f, 0.13f, 0.19f, 0.98f), 0f);
            StretchFull(successStateRoot.GetComponent<RectTransform>());

            VerticalLayoutGroup layout = successStateRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 18);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateText(
                "SuccessHeader",
                successStateRoot.transform,
                font,
                "MISSION RATING, RELIABILITY BONUS & CONTRACT PAYOUT",
                18,
                FontStyle.Bold,
                new Color(0.45f, 0.96f, 0.66f, 1f),
                TextAnchor.MiddleLeft);

            // 1. Score & Rating Card
            GameObject scoreCard = CreatePanel("ScoreCard", successStateRoot.transform, new Color(0.04f, 0.08f, 0.14f, 0.96f), 156f);
            VerticalLayoutGroup scoreLayout = scoreCard.AddComponent<VerticalLayoutGroup>();
            scoreLayout.padding = new RectOffset(18, 18, 12, 12);
            scoreLayout.spacing = 8f;
            scoreLayout.childControlWidth = true;
            scoreLayout.childControlHeight = true;
            scoreLayout.childForceExpandWidth = true;
            scoreLayout.childForceExpandHeight = false;

            GameObject scoreTopRow = CreateUIObject("ScoreTopRow", scoreCard.transform);
            HorizontalLayoutGroup scoreTopLayout = scoreTopRow.AddComponent<HorizontalLayoutGroup>();
            scoreTopLayout.spacing = 14f;
            scoreTopLayout.childAlignment = TextAnchor.MiddleLeft;
            scoreTopLayout.childControlWidth = true;
            scoreTopLayout.childControlHeight = true;
            scoreTopLayout.childForceExpandWidth = false;
            scoreTopLayout.childForceExpandHeight = false;

            Text scoreLabel = CreateText(
                "FinalScoreLabel",
                scoreTopRow.transform,
                font,
                "FINAL MISSION SCORE:",
                16,
                FontStyle.Bold,
                new Color(0.78f, 0.88f, 0.98f, 1f),
                TextAnchor.MiddleLeft);
            LayoutElement scoreLabelLe = scoreLabel.gameObject.AddComponent<LayoutElement>();
            scoreLabelLe.flexibleWidth = 1f;

            finalScoreValueText = CreateText(
                "FinalScoreValue",
                scoreTopRow.transform,
                font,
                "162.5 PTS",
                28,
                FontStyle.Bold,
                new Color(0.38f, 0.98f, 0.62f, 1f),
                TextAnchor.MiddleRight);

            GameObject gradeBadge = CreatePanel("GradeBadge", scoreTopRow.transform, new Color(0.14f, 0.54f, 0.32f, 1f), 38f);
            LayoutElement gradeLe = gradeBadge.GetComponent<LayoutElement>();
            gradeLe.preferredWidth = 220f;
            ratingGradeBadgeBg = gradeBadge.GetComponent<Image>();

            ratingGradeBadgeText = CreateText(
                "GradeBadgeText",
                gradeBadge.transform,
                font,
                "GRADE S  •  EXEMPLARY",
                14,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter);
            StretchFull(ratingGradeBadgeText.GetComponent<RectTransform>());

            scoreFormulaBreakdownText = CreateText(
                "ScoreFormulaBreakdown",
                scoreCard.transform,
                font,
                "• Delivered Science Points: <b>+120.0 pts</b>\n" +
                "• Budget Efficiency Bonus: <b>+15.0 pts</b>\n" +
                "• Engineering Reliability Margin Bonus: <b>+50.0 pts</b>",
                14,
                FontStyle.Normal,
                new Color(0.84f, 0.92f, 0.98f, 1f),
                TextAnchor.MiddleLeft);
            scoreFormulaBreakdownText.supportRichText = true;
            scoreFormulaBreakdownText.lineSpacing = 1.2f;

            // 2. +50 Reliability Bonus Card
            GameObject bonusCard = CreatePanel("ReliabilityBonusCard", successStateRoot.transform, new Color(0.09f, 0.30f, 0.20f, 0.95f), 88f);
            reliabilityBonusCardBg = bonusCard.GetComponent<Image>();

            VerticalLayoutGroup bonusLayout = bonusCard.AddComponent<VerticalLayoutGroup>();
            bonusLayout.padding = new RectOffset(18, 18, 10, 10);
            bonusLayout.spacing = 4f;
            bonusLayout.childControlWidth = true;
            bonusLayout.childControlHeight = true;
            bonusLayout.childForceExpandWidth = true;
            bonusLayout.childForceExpandHeight = false;

            reliabilityBonusTitleText = CreateText(
                "ReliabilityBonusTitle",
                bonusCard.transform,
                font,
                "RELIABILITY MARGIN BONUS: +50.0 PTS AWARDED",
                16,
                FontStyle.Bold,
                new Color(0.42f, 0.98f, 0.64f, 1f),
                TextAnchor.MiddleLeft);

            reliabilityBonusDetailText = CreateText(
                "ReliabilityBonusDetail",
                bonusCard.transform,
                font,
                "Conditions: Min Battery >= 15%  •  Final Health >= 80%  •  Reliability >= 85%",
                13,
                FontStyle.Normal,
                new Color(0.85f, 0.95f, 0.90f, 1f),
                TextAnchor.MiddleLeft);

            // 3. Contract Payout & Passive Income Card
            GameObject payoutCard = CreatePanel("ContractPayoutCard", successStateRoot.transform, new Color(0.04f, 0.08f, 0.14f, 0.96f), 160f);
            VerticalLayoutGroup payoutLayout = payoutCard.AddComponent<VerticalLayoutGroup>();
            payoutLayout.padding = new RectOffset(18, 18, 12, 12);
            payoutLayout.spacing = 8f;
            payoutLayout.childControlWidth = true;
            payoutLayout.childControlHeight = true;
            payoutLayout.childForceExpandWidth = true;
            payoutLayout.childForceExpandHeight = false;

            CreateText(
                "PayoutHeader",
                payoutCard.transform,
                font,
                "CONTRACT REWARD & PASSIVE INCOME PAYOUT",
                15,
                FontStyle.Bold,
                new Color(1.0f, 0.84f, 0.38f, 1f),
                TextAnchor.MiddleLeft);

            GameObject payoutBoxesRow = CreateUIObject("PayoutBoxesRow", payoutCard.transform);
            HorizontalLayoutGroup payoutBoxesLayout = payoutBoxesRow.AddComponent<HorizontalLayoutGroup>();
            payoutBoxesLayout.spacing = 14f;
            payoutBoxesLayout.childControlWidth = true;
            payoutBoxesLayout.childControlHeight = true;
            payoutBoxesLayout.childForceExpandWidth = true;
            payoutBoxesLayout.childForceExpandHeight = false;

            GameObject rewardBox = CreatePanel("RewardBox", payoutBoxesRow.transform, new Color(0.08f, 0.16f, 0.26f, 1f), 46f);
            contractRewardValueText = CreateText(
                "ContractRewardValue",
                rewardBox.transform,
                font,
                "+$130k Contract Payout",
                17,
                FontStyle.Bold,
                new Color(0.42f, 0.98f, 0.64f, 1f),
                TextAnchor.MiddleCenter);
            StretchFull(contractRewardValueText.GetComponent<RectTransform>());

            GameObject passiveBox = CreatePanel("PassiveBox", payoutBoxesRow.transform, new Color(0.08f, 0.16f, 0.26f, 1f), 46f);
            passiveIncomeValueText = CreateText(
                "PassiveIncomeValue",
                passiveBox.transform,
                font,
                "+$20k / Flight Passive Income",
                17,
                FontStyle.Bold,
                new Color(0.48f, 0.88f, 1.0f, 1f),
                TextAnchor.MiddleCenter);
            StretchFull(passiveIncomeValueText.GetComponent<RectTransform>());

            agencyCapitalSummaryText = CreateText(
                "AgencyCapitalSummary",
                payoutCard.transform,
                font,
                "Total Payout Credited: <b>+$150k</b>\nAgency Balance: <b>$200k</b>   •   Active Passive Income Stream: <b>+$20k / mission</b>",
                14,
                FontStyle.Normal,
                new Color(0.84f, 0.92f, 0.98f, 1f),
                TextAnchor.MiddleLeft);
            agencyCapitalSummaryText.supportRichText = true;
            agencyCapitalSummaryText.lineSpacing = 1.2f;

            GameObject spacer = CreateUIObject("Spacer", successStateRoot.transform);
            LayoutElement spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleHeight = 1f;

            returnToHubButton = CreateActionButton(
                successStateRoot.transform,
                "Btn_ReturnToHub",
                "RETURN TO CONTRACT HUB (COLLECT REWARD & NEXT MISSION)",
                new Color(0.14f, 0.56f, 0.34f, 1f),
                0f,
                58f,
                font,
                ReturnToHub);
        }

        private void BuildFailureStatePanel(Transform parent, Font font)
        {
            failureStateRoot = CreatePanel("FailureStatePanel", parent, new Color(0.14f, 0.08f, 0.12f, 0.98f), 0f);
            StretchFull(failureStateRoot.GetComponent<RectTransform>());

            VerticalLayoutGroup layout = failureStateRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 18);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateText(
                "FailureHeader",
                failureStateRoot.transform,
                font,
                "WHY DID IT FAIL? — A.S.T.R.A. FAILURE DEBRIEF BUILDER",
                18,
                FontStyle.Bold,
                new Color(1.0f, 0.50f, 0.48f, 1f),
                TextAnchor.MiddleLeft);

            // 1. FailureTitle Box
            GameObject titleBox = CreatePanel("FailureTitleBox", failureStateRoot.transform, new Color(0.42f, 0.11f, 0.14f, 0.98f), 84f);
            failureTitleBannerBg = titleBox.GetComponent<Image>();

            VerticalLayoutGroup titleBoxLayout = titleBox.AddComponent<VerticalLayoutGroup>();
            titleBoxLayout.padding = new RectOffset(18, 18, 10, 10);
            titleBoxLayout.spacing = 4f;
            titleBoxLayout.childControlWidth = true;
            titleBoxLayout.childControlHeight = true;
            titleBoxLayout.childForceExpandWidth = true;
            titleBoxLayout.childForceExpandHeight = false;

            CreateText(
                "FailureTitleTag",
                titleBox.transform,
                font,
                "1. PRIMARY ANOMALY CLASSIFICATION (FailureTitle):",
                13,
                FontStyle.Bold,
                new Color(1.0f, 0.78f, 0.78f, 1f),
                TextAnchor.MiddleLeft);

            failureTitleText = CreateText(
                "FailureTitleValue",
                titleBox.transform,
                font,
                "Critical Battery Depletion (0%)",
                21,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleLeft);

            // 2. FailureCause Box
            GameObject causeBox = CreatePanel("FailureCauseBox", failureStateRoot.transform, new Color(0.06f, 0.08f, 0.14f, 0.96f), 140f);
            VerticalLayoutGroup causeBoxLayout = causeBox.AddComponent<VerticalLayoutGroup>();
            causeBoxLayout.padding = new RectOffset(18, 18, 12, 12);
            causeBoxLayout.spacing = 6f;
            causeBoxLayout.childControlWidth = true;
            causeBoxLayout.childControlHeight = true;
            causeBoxLayout.childForceExpandWidth = true;
            causeBoxLayout.childForceExpandHeight = false;

            CreateText(
                "FailureCauseTag",
                causeBox.transform,
                font,
                "2. ROOT CAUSE & ORBITAL TELEMETRY ANALYSIS (FailureCause):",
                14,
                FontStyle.Bold,
                new Color(1.0f, 0.74f, 0.36f, 1f),
                TextAnchor.MiddleLeft);

            failureCauseText = CreateText(
                "FailureCauseValue",
                causeBox.transform,
                font,
                "Satellite lost power on orbit 1 (tick 11). Power draw reached 27.0 W against 0.0 W generation, draining battery capacity (45.0 Wh) in eclipse.",
                16,
                FontStyle.Normal,
                new Color(0.94f, 0.96f, 1.0f, 1f),
                TextAnchor.UpperLeft);
            failureCauseText.lineSpacing = 1.25f;

            // 3. FailureRecommendation Box
            GameObject recBox = CreatePanel("FailureRecommendationBox", failureStateRoot.transform, new Color(0.06f, 0.17f, 0.24f, 0.96f), 130f);
            VerticalLayoutGroup recBoxLayout = recBox.AddComponent<VerticalLayoutGroup>();
            recBoxLayout.padding = new RectOffset(18, 18, 12, 12);
            recBoxLayout.spacing = 6f;
            recBoxLayout.childControlWidth = true;
            recBoxLayout.childControlHeight = true;
            recBoxLayout.childForceExpandWidth = true;
            recBoxLayout.childForceExpandHeight = false;

            CreateText(
                "FailureRecommendationTag",
                recBox.transform,
                font,
                "3. A.S.T.R.A. CORRECTIVE ENGINEERING ACTION (FailureRecommendation):",
                14,
                FontStyle.Bold,
                new Color(0.42f, 0.92f, 1.0f, 1f),
                TextAnchor.MiddleLeft);

            failureRecommendationText = CreateText(
                "FailureRecommendationValue",
                recBox.transform,
                font,
                "Add autopilot rule: IF SolarWatts < 5 THEN ScienceOff ELSE ScienceOn (or use BatteryPercent for RTG), or install a larger battery.",
                16,
                FontStyle.Bold,
                new Color(0.86f, 0.98f, 1.0f, 1f),
                TextAnchor.UpperLeft);
            failureRecommendationText.lineSpacing = 1.25f;

            // 4. Free Retry Summary Box
            GameObject noteBox = CreatePanel("FailureTelemetryNoteBox", failureStateRoot.transform, new Color(0.05f, 0.09f, 0.15f, 0.94f), 68f);
            VerticalLayoutGroup noteLayout = noteBox.AddComponent<VerticalLayoutGroup>();
            noteLayout.padding = new RectOffset(16, 16, 8, 8);
            noteLayout.childControlWidth = true;
            noteLayout.childControlHeight = true;
            noteLayout.childForceExpandWidth = true;
            noteLayout.childForceExpandHeight = true;

            failureTelemetryNoteText = CreateText(
                "FailureTelemetryNoteText",
                noteBox.transform,
                font,
                "FREE RETRY POLICY: $0k deducted from Agency Capital. Modify your components or autopilot rules in the Hangar at no penalty.",
                13,
                FontStyle.Normal,
                new Color(0.82f, 0.90f, 0.96f, 1f),
                TextAnchor.MiddleLeft);
            failureTelemetryNoteText.supportRichText = true;

            GameObject spacer = CreateUIObject("Spacer", failureStateRoot.transform);
            LayoutElement spacerLe = spacer.AddComponent<LayoutElement>();
            spacerLe.flexibleHeight = 1f;

            returnToWorkshopButton = CreateActionButton(
                failureStateRoot.transform,
                "Btn_ReturnToWorkshop",
                "FREE RETURN TO HANGAR (FIX SATELLITE BUILD / AUTOPILOT RULES)",
                new Color(0.78f, 0.42f, 0.12f, 1f),
                0f,
                58f,
                font,
                ReturnToWorkshopFree);
        }

        private void BuildVerificationSwitcherBar(Transform parent, Font font)
        {
            verificationBarRoot = CreatePanel("VerificationSwitcherBar", parent, new Color(0.06f, 0.10f, 0.18f, 0.96f), 54f);
            HorizontalLayoutGroup barLayout = verificationBarRoot.AddComponent<HorizontalLayoutGroup>();
            barLayout.padding = new RectOffset(14, 14, 7, 7);
            barLayout.spacing = 10f;
            barLayout.childAlignment = TextAnchor.MiddleCenter;
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = true;
            barLayout.childForceExpandHeight = true;

            Text barLabel = CreateText(
                "VerifyLabel",
                verificationBarRoot.transform,
                font,
                "DEBRIEF STATE PRESETS (S2-02 / S2-05):",
                13,
                FontStyle.Bold,
                new Color(0.68f, 0.82f, 0.96f, 1f),
                TextAnchor.MiddleLeft);
            LayoutElement labelLe = barLabel.gameObject.AddComponent<LayoutElement>();
            labelLe.preferredWidth = 270f;
            labelLe.flexibleWidth = 0f;

            CreateActionButton(
                verificationBarRoot.transform,
                "Btn_Preset_SuccessBonus",
                "VICTORY (+50 BONUS)",
                new Color(0.14f, 0.50f, 0.30f, 1f),
                0f,
                38f,
                font,
                () => TriggerSampleSuccessScenario(true));

            CreateActionButton(
                verificationBarRoot.transform,
                "Btn_Preset_SuccessNoBonus",
                "VICTORY (NO BONUS)",
                new Color(0.18f, 0.38f, 0.56f, 1f),
                0f,
                38f,
                font,
                () => TriggerSampleSuccessScenario(false));

            CreateActionButton(
                verificationBarRoot.transform,
                "Btn_Preset_FailBattery",
                "FAIL: BATTERY (0%)",
                new Color(0.56f, 0.18f, 0.20f, 1f),
                0f,
                38f,
                font,
                () => TriggerSampleFailureScenario(FailureScenarioType.BatteryDepleted));

            CreateActionButton(
                verificationBarRoot.transform,
                "Btn_Preset_FailRadiation",
                "FAIL: RADIATION (CME)",
                new Color(0.56f, 0.24f, 0.16f, 1f),
                0f,
                38f,
                font,
                () => TriggerSampleFailureScenario(FailureScenarioType.RadiationDamage));

            CreateActionButton(
                verificationBarRoot.transform,
                "Btn_Preset_FailSsdOverflow",
                "FAIL: SSD OVERFLOW",
                new Color(0.52f, 0.28f, 0.14f, 1f),
                0f,
                38f,
                font,
                () => TriggerSampleFailureScenario(FailureScenarioType.InsufficientScienceWithSsdLoss));

            CreateActionButton(
                verificationBarRoot.transform,
                "Btn_Preset_FailScienceQuota",
                "FAIL: SCIENCE QUOTA",
                new Color(0.46f, 0.22f, 0.38f, 1f),
                0f,
                38f,
                font,
                () => TriggerSampleFailureScenario(FailureScenarioType.InsufficientScienceNoLoss));
        }

        // --- UI Factory Helpers ---

        private static Sprite _solidWhiteSprite;

        private static Sprite CreateSolidWhiteSprite()
        {
            if (_solidWhiteSprite != null)
            {
                return _solidWhiteSprite;
            }

            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
            tex.SetPixels(pixels);
            tex.Apply();

            _solidWhiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            return _solidWhiteSprite;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject CreatePanel(string name, Transform parent, Color bgColor, float preferredHeight)
        {
            GameObject panel = CreateUIObject(name, parent);
            Image img = panel.AddComponent<Image>();
            img.color = bgColor;
            if (preferredHeight > 0f)
            {
                LayoutElement le = panel.AddComponent<LayoutElement>();
                le.preferredHeight = preferredHeight;
            }
            return panel;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Font font,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment)
        {
            GameObject txtObj = CreateUIObject(name, parent);
            Text txt = txtObj.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static Button CreateActionButton(
            Transform parent,
            string name,
            string label,
            Color bgColor,
            float width,
            float height,
            Font font,
            UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = CreateUIObject(name, parent);
            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            if (width > 0f) le.preferredWidth = width;
            if (height > 0f) le.preferredHeight = height;

            Text txt = CreateText("Text", btnObj.transform, font, label, 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            StretchFull(txt.GetComponent<RectTransform>());
            return btn;
        }
    }
}
