using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Core;
using Genix.Editor.Infrastructure;
using Genix.Editor.TargetAreas;
using Genix.Styles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Genix.Editor.Evaluation
{
    /// <summary>Creates the canonical, code-reviewed thesis evaluation configuration from authored scenes.</summary>
    internal static class ThesisEvaluationSuiteFactory
    {
        private sealed class SceneConfiguration
        {
            public string Path { get; set; }
            public EvaluationScenarioKind Kind { get; set; }
            public bool Ready { get; set; }
            public IReadOnlyList<BenchmarkAreaTarget> Targets { get; set; }
        }

        public const string SuitePath = "Assets/Genix/Evaluations/Suites/ThesisQualityEvaluation.asset";
        private const string EvaluationPresetPath = "Assets/Genix/Generation Presets/EvaluationPreset.asset";
        private const string OfficePresetPath = "Assets/Genix/Generation Presets/OfficeEval.asset";
        private const string IndustryPresetPath = "Assets/Genix/Generation Presets/IndustryEval.asset";
        private const string OutdoorPresetPath = "Assets/Genix/Generation Presets/OutdoorEval.asset";
        private const string ConstraintPresetPath = "Assets/Genix/Generation Presets/Evaluation Constraint Reference.asset";
        private const string SemanticPresetPath = "Assets/Genix/Generation Presets/Evaluation Semantic Ablation.asset";
        private const string RelativePresetPath = "Assets/Genix/Generation Presets/Evaluation Relative Placement.asset";
        private const string SpacingPresetPath = "Assets/Genix/Generation Presets/Evaluation Spacing.asset";
        private const string ImpossiblePresetPath = "Assets/Genix/Generation Presets/Evaluation Impossible Request.asset";
        private const string SpacingStylePath = "Assets/Genix/Presets/Evaluation Spacing.asset";
        private const string EvaluationScenesToken = "/Evaluation/Scenes/";
        internal const int FinalRunsPerScenario = 20;
        internal const int FinalSettleFrames = 2;
        internal const int FinalScenarioCount = 25;
        internal const int FinalExpectedRunCount = FinalRunsPerScenario * FinalScenarioCount;
        internal const int FinalExpectedLayoutCount = FinalRunsPerScenario * 17;

        private static readonly int[] SummativeSeeds =
        {
            -1851488837, 594494322, -1423066958, -1689967793, -625522695,
            2068292989, 1927287721, 1647605635, 1950899649, -2114452199,
            -1439182028, 1518213276, -1260957334, -2118255369, -96239834,
            559500117, 239994572, 476828007, -1364768060, -1207775653
        };

        private static readonly HashSet<string> ExpectedScenarioNames = new(StringComparer.Ordinal)
        {
            "Constraint And Relationship Lab - Control",
            "Constraint And Relationship Lab - FixedObstacles",
            "Constraint And Relationship Lab - ImpossibleRequest",
            "Constraint And Relationship Lab - RelativePlacement",
            "Constraint And Relationship Lab - Spacing",
            "Empty Baseline",
            "Semantic Context Ablation - IndustryRoom",
            "Semantic Context Ablation - NeutralRoom",
            "Semantic Context Ablation - OfficeRoom",
            "Spatial Representation And Target Lab - ConcaveLRoom",
            "Spatial Representation And Target Lab - ConvexRoom",
            "Spatial Representation And Target Lab - StackedRooms Bottom",
            "Spatial Representation And Target Lab - StackedRooms Top",
            "Spatial Representation And Target Lab - TerrainPatch",
            "Voxel Resolution_25",
            "Voxel Resolution_50",
            "Voxel Resolution_100",
            "Voxel Resolution_200",
            "Complex Terrain Surface Fit",
            "Dense Obstacles",
            "High Density",
            "Multi Level Mixed Targets",
            "Industry",
            "Office",
            "Outdoor Environment"
        };

        internal static IReadOnlyList<int> FinalSeeds => SummativeSeeds;

        public static GenerationEvaluationSuite CreateOrRefresh(out string summary)
        {
            EnsureFolders();
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            string originalPath = SceneManager.GetActiveScene().path;
            List<SceneConfiguration> configurations = new();
            int readyCount = 0;
            int pendingCount = 0;

            try
            {
                foreach (string scenePath in FindEvaluationScenePaths())
                {
                    configurations.Add(new SceneConfiguration
                    {
                        Path = scenePath,
                        Kind = Classify(scenePath),
                        Ready = IsReady(scenePath),
                        Targets = FindTargets(scenePath)
                    });
                }
            }
            finally
            {
                RestoreScenes(originalSetup, originalPath);
            }

            GenerationPreset evaluationPreset = AssetDatabase.LoadAssetAtPath<GenerationPreset>(EvaluationPresetPath);
            GenerationPreset officePreset = AssetDatabase.LoadAssetAtPath<GenerationPreset>(OfficePresetPath);
            GenerationPreset industryPreset = AssetDatabase.LoadAssetAtPath<GenerationPreset>(IndustryPresetPath);
            GenerationPreset outdoorPreset = AssetDatabase.LoadAssetAtPath<GenerationPreset>(OutdoorPresetPath);
            if (!evaluationPreset || !officePreset || !industryPreset || !outdoorPreset)
            {
                throw new InvalidOperationException(
                    "EvaluationPreset and the Office, Industry, and Outdoor evaluation presets must exist " +
                    "before creating the thesis suite.");
            }

            IReadOnlyDictionary<string, GenerationPreset> profiles = CreateEvaluationProfiles(evaluationPreset);

            GenerationEvaluationSuite suite = AssetDatabase.LoadAssetAtPath<GenerationEvaluationSuite>(SuitePath);
            if (!suite)
            {
                suite = ScriptableObject.CreateInstance<GenerationEvaluationSuite>();
                suite.name = "Thesis Quality Evaluation";
                AssetDatabase.CreateAsset(suite, SuitePath);
            }

            Undo.RecordObject(suite, "Refreshed Thesis Evaluation Suite");
            suite.ClearScenarios();
            suite.ConfigureCampaign(FinalRunsPerScenario, FinalSettleFrames, SummativeSeeds);

            foreach (SceneConfiguration configuration in configurations)
            {
                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(configuration.Path);
                GenerationPreset fallbackPreset = SelectScenePreset(
                    configuration.Path,
                    evaluationPreset,
                    officePreset,
                    industryPreset,
                    outdoorPreset);

                if (!configuration.Ready || configuration.Targets.Count == 0)
                {
                    suite.AddScenario(
                        $"{ObjectNames.NicifyVariableName(sceneAsset.name)} (Not Ready)",
                        configuration.Kind,
                        sceneAsset,
                        fallbackPreset,
                        ready: false,
                        saveLayouts: false);
                    pendingCount++;
                    continue;
                }

                foreach (BenchmarkAreaTarget target in configuration.Targets)
                {
                    string name = configuration.Targets.Count == 1
                        ? ObjectNames.NicifyVariableName(sceneAsset.name)
                        : $"{ObjectNames.NicifyVariableName(sceneAsset.name)} - {target.DisplayName}";
                    GenerationPreset preset = SelectPreset(target.DisplayName, fallbackPreset, profiles);
                    bool impossible = IsTarget(target.DisplayName, "ImpossibleRequest");
                    bool relative = IsTarget(target.DisplayName, "RelativePlacement");
                    bool spacing = IsTarget(target.DisplayName, "Spacing");
                    float minimumCompletion = impossible
                        ? 0.05f
                        : configuration.Kind == EvaluationScenarioKind.RealWorld ? 0.9f : 1f;
                    float maximumCompletion = impossible ? 0.25f : 1f;
                    EvaluationCheckSet checks = EvaluationCheckSet.AllStructural;
                    if (relative)
                        checks |= EvaluationCheckSet.RelativePlacement;
                    if (spacing)
                        checks |= EvaluationCheckSet.SamplingSpacing;
                    suite.AddScenario(
                        name,
                        configuration.Kind,
                        sceneAsset,
                        preset,
                        target.Id,
                        ready: true,
                        checks: checks,
                        minimumCompletionRatio: minimumCompletion,
                        maximumCompletionRatio: maximumCompletion,
                        saveLayouts: configuration.Kind != EvaluationScenarioKind.Performance);
                    readyCount++;
                }
            }

            IReadOnlyList<string> validationErrors = ValidateCanonicalConfiguration(suite);
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "The final thesis evaluation suite is incomplete:\n- " +
                    string.Join("\n- ", validationErrors));
            }

            EditorUtility.SetDirty(suite);
            AssetDatabase.SaveAssets();

            summary = $"Configured the final {readyCount}-scenario thesis suite with no pending placeholders: " +
                      $"{FinalExpectedRunCount} deterministic runs and {FinalExpectedLayoutCount} saved layouts.";
            return suite;
        }

        internal static IReadOnlyList<string> ValidateCanonicalConfiguration(GenerationEvaluationSuite suite)
        {
            List<string> errors = new();
            if (!suite)
            {
                errors.Add("The canonical thesis suite asset is missing.");
                return errors;
            }

            if (suite.RunsPerScenario != FinalRunsPerScenario)
                errors.Add($"Runs Per Scenario must be {FinalRunsPerScenario}.");
            if (suite.SettleFrames != FinalSettleFrames)
                errors.Add($"Scene Settle Frames must be {FinalSettleFrames}.");
            if (suite.Seeds.Count != SummativeSeeds.Length || !suite.Seeds.SequenceEqual(SummativeSeeds))
                errors.Add("The deterministic seed list does not match the frozen summative seed block 21-40.");
            if (suite.Scenarios.Count != FinalScenarioCount)
                errors.Add($"Expected {FinalScenarioCount} scenarios, but found {suite.Scenarios.Count}.");

            HashSet<string> actualNames = suite.Scenarios
                .Where(scenario => scenario != null)
                .Select(scenario => scenario.DisplayName)
                .ToHashSet(StringComparer.Ordinal);
            if (!actualNames.SetEquals(ExpectedScenarioNames))
            {
                string missing = string.Join(", ", ExpectedScenarioNames.Except(actualNames).OrderBy(name => name));
                string unexpected = string.Join(", ", actualNames.Except(ExpectedScenarioNames).OrderBy(name => name));
                errors.Add($"Scenario set differs from the frozen protocol. Missing [{missing}]; unexpected [{unexpected}].");
            }

            ValidateKindCount(suite, EvaluationScenarioKind.Isolated, 14, errors);
            ValidateKindCount(suite, EvaluationScenarioKind.Performance, 8, errors);
            ValidateKindCount(suite, EvaluationScenarioKind.RealWorld, 3, errors);

            foreach (GenerationEvaluationScenario scenario in suite.Scenarios.Where(item => item != null))
            {
                string label = scenario.DisplayName;
                if (!scenario.Enabled)
                    errors.Add($"{label}: scenario must be enabled for the final Run All campaign.");
                if (!scenario.Ready)
                    errors.Add($"{label}: scenario is not marked ready.");
                if (!scenario.Scene)
                    errors.Add($"{label}: scene reference is missing.");
                else if (Classify(AssetDatabase.GetAssetPath(scenario.Scene)) != scenario.Kind)
                    errors.Add($"{label}: scenario kind does not match its scene folder.");
                if (string.IsNullOrWhiteSpace(scenario.TargetId))
                    errors.Add($"{label}: stable target ID is missing.");

                string expectedPresetPath = ExpectedPresetPath(scenario);
                string actualPresetPath = scenario.GenerationPreset
                    ? AssetDatabase.GetAssetPath(scenario.GenerationPreset)
                    : string.Empty;
                if (!string.Equals(actualPresetPath, expectedPresetPath, StringComparison.Ordinal))
                    errors.Add($"{label}: expected preset '{expectedPresetPath}', found '{actualPresetPath}'.");

                EvaluationCheckSet expectedChecks = label.EndsWith(" - RelativePlacement", StringComparison.Ordinal)
                    ? EvaluationCheckSet.AllStructural | EvaluationCheckSet.RelativePlacement
                    : label.EndsWith(" - Spacing", StringComparison.Ordinal)
                        ? EvaluationCheckSet.AllStructural | EvaluationCheckSet.SamplingSpacing
                        : EvaluationCheckSet.AllStructural;
                if (scenario.Checks != expectedChecks)
                    errors.Add($"{label}: automatic checks differ from the frozen protocol.");

                float expectedMinimum = label.EndsWith(" - ImpossibleRequest", StringComparison.Ordinal)
                    ? 0.05f
                    : scenario.Kind == EvaluationScenarioKind.RealWorld ? 0.9f : 1f;
                float expectedMaximum = label.EndsWith(" - ImpossibleRequest", StringComparison.Ordinal) ? 0.25f : 1f;
                if (!Mathf.Approximately(scenario.MinimumCompletionRatio, expectedMinimum) ||
                    !Mathf.Approximately(scenario.MaximumCompletionRatio, expectedMaximum))
                {
                    errors.Add($"{label}: completion interval must be {expectedMinimum:0.##}-{expectedMaximum:0.##}.");
                }

                bool shouldSaveLayouts = scenario.Kind != EvaluationScenarioKind.Performance;
                if (scenario.SaveLayouts != shouldSaveLayouts)
                    errors.Add($"{label}: Save Layouts must be {shouldSaveLayouts}.");
            }

            int expectedRuns = suite.Scenarios.Count(scenario => scenario is { Enabled: true, Ready: true }) *
                               suite.RunsPerScenario;
            if (expectedRuns != FinalExpectedRunCount)
                errors.Add($"Run All must contain exactly {FinalExpectedRunCount} runs, but currently contains {expectedRuns}.");

            int expectedLayouts = suite.Scenarios.Count(scenario =>
                                      scenario is { Enabled: true, Ready: true, SaveLayouts: true }) *
                                  suite.RunsPerScenario;
            if (expectedLayouts != FinalExpectedLayoutCount)
                errors.Add($"The final campaign must retain {FinalExpectedLayoutCount} layouts, but currently retains {expectedLayouts}.");

            return errors;
        }

        private static void ValidateKindCount(
            GenerationEvaluationSuite suite,
            EvaluationScenarioKind kind,
            int expected,
            ICollection<string> errors)
        {
            int actual = suite.Scenarios.Count(scenario => scenario?.Kind == kind);
            if (actual != expected)
                errors.Add($"Expected {expected} {kind} scenarios, but found {actual}.");
        }

        private static string ExpectedPresetPath(GenerationEvaluationScenario scenario)
        {
            string name = scenario.DisplayName;
            if (name == "Office")
                return OfficePresetPath;
            if (name == "Industry")
                return IndustryPresetPath;
            if (name == "Outdoor Environment")
                return OutdoorPresetPath;
            if (name.EndsWith(" - ImpossibleRequest", StringComparison.Ordinal))
                return ImpossiblePresetPath;
            if (name.EndsWith(" - RelativePlacement", StringComparison.Ordinal))
                return RelativePresetPath;
            if (name.EndsWith(" - Spacing", StringComparison.Ordinal))
                return SpacingPresetPath;
            if (name.EndsWith(" - Control", StringComparison.Ordinal) ||
                name.EndsWith(" - FixedObstacles", StringComparison.Ordinal))
            {
                return ConstraintPresetPath;
            }

            if (name.StartsWith("Semantic Context Ablation - ", StringComparison.Ordinal))
                return SemanticPresetPath;
            return EvaluationPresetPath;
        }

        private static IReadOnlyDictionary<string, GenerationPreset> CreateEvaluationProfiles(
            GenerationPreset referencePreset)
        {
            GenerationPresetSettings reference = referencePreset.Settings;
            StyleSettings spacingSettings = reference.StylePreset.Settings;
            spacingSettings.description = "Evaluation profile isolating a 2.0-unit Poisson minimum distance.";
            spacingSettings.poisson.minDistance = 2f;
            StylePreset spacingStyle = GetOrCreateStyle(SpacingStylePath, "Evaluation Spacing", spacingSettings);

            return new Dictionary<string, GenerationPreset>(StringComparer.Ordinal)
            {
                ["constraint"] = GetOrCreatePreset(
                    ConstraintPresetPath,
                    "Evaluation Constraint Reference",
                    Copy(reference, reference.StylePreset, 20, PlacementTarget.Floor)),
                ["semantic"] = GetOrCreatePreset(
                    SemanticPresetPath,
                    "Evaluation Semantic Ablation",
                    Copy(reference, reference.StylePreset, 20, PlacementTarget.Floor)),
                ["relative"] = GetOrCreatePreset(
                    RelativePresetPath,
                    "Evaluation Relative Placement",
                    Copy(
                        reference,
                        reference.StylePreset,
                        12,
                        PlacementTarget.Floor,
                        RelativePlacementSource.SceneObjects,
                        3f,
                        1 << 9)),
                ["spacing"] = GetOrCreatePreset(
                    SpacingPresetPath,
                    "Evaluation Spacing",
                    Copy(reference, spacingStyle, 12, PlacementTarget.Floor)),
                ["impossible"] = GetOrCreatePreset(
                    ImpossiblePresetPath,
                    "Evaluation Impossible Request",
                    Copy(reference, reference.StylePreset, 100, PlacementTarget.Floor))
            };
        }

        private static GenerationPreset SelectPreset(
            string targetName,
            GenerationPreset fallback,
            IReadOnlyDictionary<string, GenerationPreset> profiles)
        {
            if (IsTarget(targetName, "ImpossibleRequest"))
                return profiles["impossible"];
            if (IsTarget(targetName, "RelativePlacement"))
                return profiles["relative"];
            if (IsTarget(targetName, "Spacing"))
                return profiles["spacing"];
            if (IsTarget(targetName, "Control") || IsTarget(targetName, "FixedObstacles"))
                return profiles["constraint"];
            if (IsTarget(targetName, "IndustryRoom") || IsTarget(targetName, "NeutralRoom") ||
                IsTarget(targetName, "OfficeRoom"))
            {
                return profiles["semantic"];
            }

            return fallback;
        }

        private static GenerationPreset SelectScenePreset(
            string path,
            GenerationPreset evaluationPreset,
            GenerationPreset officePreset,
            GenerationPreset industryPreset,
            GenerationPreset outdoorPreset)
        {
            if (path.EndsWith("/Office.unity", StringComparison.OrdinalIgnoreCase))
                return officePreset;
            if (path.EndsWith("/Industry.unity", StringComparison.OrdinalIgnoreCase))
                return industryPreset;
            if (path.EndsWith("/OutdoorEnvironment.unity", StringComparison.OrdinalIgnoreCase))
                return outdoorPreset;
            return evaluationPreset;
        }

        private static bool IsTarget(string displayName, string expected) =>
            string.Equals(displayName?.Replace(" ", string.Empty), expected, StringComparison.OrdinalIgnoreCase);

        private static GenerationPresetSettings Copy(
            GenerationPresetSettings source,
            StylePreset style,
            int objectCount,
            PlacementTarget targets,
            RelativePlacementSource relativeSource = RelativePlacementSource.None,
            float relativeRadius = 2f,
            LayerMask relativeLayers = default) => new(
            source.AssetPool,
            style,
            objectCount,
            targets,
            TargetDistributionMode.Random,
            source.TargetDistributionWeights,
            source.AreaDecompositionMode,
            source.SurfaceDiscoveryMode,
            source.FloorSurfaceLayers,
            source.WallSurfaceLayers,
            source.CeilingSurfaceLayers,
            source.FloorSurfaceAngleDegrees,
            source.CeilingSurfaceAngleDegrees,
            relativeSource,
            relativeRadius,
            relativeLayers,
            true,
            12345,
            true,
            source.SupportDistribution);

        private static GenerationPreset GetOrCreatePreset(
            string path,
            string name,
            GenerationPresetSettings settings)
        {
            GenerationPreset preset = AssetDatabase.LoadAssetAtPath<GenerationPreset>(path);
            if (!preset)
            {
                preset = ScriptableObject.CreateInstance<GenerationPreset>();
                preset.name = name;
                AssetDatabase.CreateAsset(preset, path);
            }

            preset.Apply(settings);
            EditorUtility.SetDirty(preset);
            return preset;
        }

        private static StylePreset GetOrCreateStyle(string path, string name, StyleSettings settings)
        {
            StylePreset style = AssetDatabase.LoadAssetAtPath<StylePreset>(path);
            if (!style)
            {
                style = ScriptableObject.CreateInstance<StylePreset>();
                style.name = name;
                style.Initialize(settings);
                AssetDatabase.CreateAsset(style, path);
            }
            else
            {
                style.Apply(settings);
                style.SetCurrentSettingsAsDefaults();
            }

            EditorUtility.SetDirty(style);
            return style;
        }

        private static IReadOnlyList<string> FindEvaluationScenePaths() => AssetDatabase
            .FindAssets("t:SceneAsset")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.IndexOf(EvaluationScenesToken, StringComparison.Ordinal) >= 0)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        private static IReadOnlyList<BenchmarkAreaTarget> FindTargets(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            IBenchmarkAreaResolver resolver = BenchmarkAreaResolverRegistry.CreateResolvers()
                .FirstOrDefault(item => item.ProviderId == "space-foundation");
            return resolver?.FindTargets(scene) ?? Array.Empty<BenchmarkAreaTarget>();
        }

        private static EvaluationScenarioKind Classify(string path)
        {
            if (path.IndexOf("/Isolated/", StringComparison.Ordinal) >= 0)
                return EvaluationScenarioKind.Isolated;
            if (path.IndexOf("/Performance/", StringComparison.Ordinal) >= 0)
                return EvaluationScenarioKind.Performance;
            return EvaluationScenarioKind.RealWorld;
        }

        private static bool IsReady(string path) =>
            path.IndexOf("/Isolated/", StringComparison.Ordinal) >= 0 ||
            path.IndexOf("/Performance/", StringComparison.Ordinal) >= 0 ||
            path.EndsWith("/RealWorld/Office.unity", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/RealWorld/Industry.unity", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/RealWorld/OutdoorEnvironment.unity", StringComparison.OrdinalIgnoreCase);

        private static void EnsureFolders()
        {
            AssetFileService.EnsureFolder(ProjectContentPaths.EvaluationSuites);
            AssetFileService.EnsureFolder(ProjectContentPaths.EvaluationReports);
        }

        private static void RestoreScenes(SceneSetup[] setup, string fallbackPath)
        {
            if (setup.Length > 0 && setup.Any(item => !string.IsNullOrWhiteSpace(item.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else if (!string.IsNullOrWhiteSpace(fallbackPath))
                EditorSceneManager.OpenScene(fallbackPath, OpenSceneMode.Single);
        }
    }
}
