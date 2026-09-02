using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Genix.Areas;
using Genix.Core;
using Genix.Editor.Generation;
using Genix.Editor.Layouts;
using Genix.Editor.TargetAreas;
using Genix.Layouts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Genix.Editor.Evaluation
{
    /// <summary>Creates reproducible four-view image evidence for retained evaluation layouts.</summary>
    internal static class GenerationEvaluationReviewCaptureService
    {
        internal enum ReviewCaptureStatus
        {
            Missing,
            Valid,
            Invalid
        }

        internal const int ImageWidth = 1280;
        internal const int ImageHeight = 720;
        internal const int ContactSheetGap = 8;

        internal const int CurrentManifestSchemaVersion = 3;
        private const float FramingMargin = 1.18f;
        private const float OverviewContentPadding = 0.08f;
        private const int BackgroundColorThreshold = 3;
        private const float MinimumBoundsSize = 0.5f;
        private const string OutputRootName = "EvaluationReview";

        private static readonly ReviewViewDefinition[] ViewDefinitions =
        {
            new("overview", false, new Vector3(-1f, -0.72f, 1f), Vector3.up),
            new("top", true, Vector3.down, Vector3.forward),
            new("side-x", true, Vector3.left, Vector3.up),
            new("side-z", true, Vector3.back, Vector3.up)
        };

        /// <summary>Captures one retained layout and records the resulting manifest on its run.</summary>
        public static bool CaptureRun(
            GenerationEvaluationReport report,
            int runIndex,
            out string contactSheetPath,
            out string error)
        {
            contactSheetPath = string.Empty;
            error = string.Empty;

            if (!report)
            {
                error = "No evaluation report is selected.";
                return false;
            }

            if (runIndex < 0 || runIndex >= report.Runs.Count)
            {
                error = $"Run index {runIndex} is outside the report.";
                return false;
            }

            string reportAssetPath = AssetDatabase.GetAssetPath(report);
            string reportName = report.name;
            string campaignCreatedAtUtc = report.CreatedAtUtc;
            string suiteAssetPath = report.SuiteAssetPath;
            GenerationEvaluationRunRecord run = report.Runs[runIndex];
            string sourceScenePath = EvaluationSceneWorkspace.ResolveSourceScenePath(suiteAssetPath, run);
            SavedLayout layout = run.LoadLayout();
            if (!layout)
            {
                error = $"The referenced layout asset '{run.layoutAssetPath}' is missing.";
                return false;
            }

            if (!TryOpenSceneAndApplyLayout(
                    suiteAssetPath,
                    sourceScenePath,
                    run,
                    layout,
                    out Bounds reviewBounds,
                    out string framingSource,
                    out Transform generatedParent,
                    out error))
                return false;

            // Scene loading or asset imports can reload ScriptableObjects. Continue with the current
            // report instance so capture metadata is written to the retained asset.
            report = AssetDatabase.LoadAssetAtPath<GenerationEvaluationReport>(reportAssetPath);
            if (!report || runIndex >= report.Runs.Count)
            {
                error = $"The evaluation report '{reportAssetPath}' could not be reloaded after scene preparation.";
                return false;
            }
            run = report.Runs[runIndex];
            Bounds generatedDetailBounds = TryGetGeneratedBounds(generatedParent, out Bounds generatedBounds)
                ? EnsureUsableBounds(generatedBounds)
                : reviewBounds;

            string projectRoot = GetProjectRoot();
            string reportDirectory = GetReportDirectory(reportName);
            string runDirectory = Path.Combine(
                reportDirectory,
                $"{runIndex + 1:0000}_{Sanitize(run.scenario)}_seed_{run.seed.ToString(CultureInfo.InvariantCulture)}");
            string stagingDirectory = runDirectory + ".tmp-" + Guid.NewGuid().ToString("N");

            List<ReviewImageRecord> imageRecords = new(ViewDefinitions.Length);
            List<Texture2D> images = new(ViewDefinitions.Length);
            GameObject cameraObject = null;
            RenderTexture renderTexture = null;

            try
            {
                Directory.CreateDirectory(reportDirectory);
                Directory.CreateDirectory(stagingDirectory);

                cameraObject = new GameObject("Genix Evaluation Review Camera")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                Camera camera = cameraObject.AddComponent<Camera>();
                ConfigureCamera(camera);
                renderTexture = new RenderTexture(ImageWidth, ImageHeight, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Genix Evaluation Review Render",
                    antiAliasing = 4,
                    hideFlags = HideFlags.HideAndDontSave
                };
                renderTexture.Create();
                camera.targetTexture = renderTexture;

                using TargetSceneVisibilityScope visibilityScope =
                    new(reviewBounds, generatedParent);
                foreach (ReviewViewDefinition definition in ViewDefinitions)
                {
                    Bounds viewBounds = GetViewFramingBounds(
                        reviewBounds,
                        generatedDetailBounds,
                        definition);
                    ConfigureView(camera, viewBounds, definition, ImageWidth / (float)ImageHeight);
                    Texture2D image;
                    using (FixedSceneTransparencyScope transparencyScope =
                           new(generatedParent))
                    {
                        image = Render(camera, renderTexture);
                        Rect viewportCrop = new(0f, 0f, 1f, 1f);
                        if (definition.IsOverview)
                        {
                            Texture2D framedImage = CropOverviewToVisibleContent(
                                image,
                                camera.backgroundColor,
                                out viewportCrop);
                            if (framedImage != image)
                            {
                                Object.DestroyImmediate(image);
                                image = framedImage;
                            }
                        }

                        imageRecords.Add(new ReviewImageRecord
                        {
                            view = definition.Key,
                            file = definition.Key + ".png",
                            orthographic = definition.Orthographic,
                            cameraPosition = camera.transform.position,
                            cameraRotation = camera.transform.rotation,
                            orthographicSize = camera.orthographic ? camera.orthographicSize : 0f,
                            fieldOfView = camera.orthographic ? 0f : camera.fieldOfView,
                            viewportCrop = viewportCrop,
                            fixedSceneRendering = transparencyScope?.RenderingMode ?? "original"
                        });
                    }
                    images.Add(image);

                    string fileName = definition.Key + ".png";
                    string absolutePath = Path.Combine(stagingDirectory, fileName);
                    byte[] png = image.EncodeToPNG();
                    File.WriteAllBytes(absolutePath, png);
                    imageRecords[imageRecords.Count - 1].sha256 = ComputeSha256(png);
                }

                Texture2D contactSheet = CreateContactSheet(images, ImageWidth, ImageHeight, ContactSheetGap);
                try
                {
                    string stagedContactSheetPath = Path.Combine(stagingDirectory, "contact-sheet.png");
                    byte[] contactSheetPng = contactSheet.EncodeToPNG();
                    File.WriteAllBytes(stagedContactSheetPath, contactSheetPng);

                    ReviewCaptureManifest manifest = new()
                    {
                        schemaVersion = CurrentManifestSchemaVersion,
                        reportAssetPath = reportAssetPath,
                        reportName = reportName,
                        campaignCreatedAtUtc = campaignCreatedAtUtc,
                        capturedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        runIndex = runIndex,
                        scenario = run.scenario,
                        scenarioKind = run.scenarioKind,
                        scene = sourceScenePath,
                        targetId = run.targetId,
                        seed = run.seed,
                        requestedCount = run.requestedCount,
                        placedCount = run.placedCount,
                        layoutAssetPath = run.ResolvedLayoutAssetPath,
                        layoutGuid = run.layoutGuid,
                        framingSource = framingSource,
                        framingBoundsCenter = reviewBounds.center,
                        framingBoundsSize = reviewBounds.size,
                        imageWidth = ImageWidth,
                        imageHeight = ImageHeight,
                        sceneRenderingScope = "translucent fixed renderers intersecting the target framing bounds plus the applied layout",
                        contactSheetFile = Path.GetFileName(stagedContactSheetPath),
                        contactSheetSha256 = ComputeSha256(contactSheetPng),
                        contactSheetOrder = "top-left overview; top-right top; bottom-left side-x; bottom-right side-z",
                        images = imageRecords
                    };
                    string stagedManifestPath = Path.Combine(stagingDirectory, "manifest.json");
                    File.WriteAllText(stagedManifestPath, JsonUtility.ToJson(manifest, true));

                    CommitCapture(
                        report,
                        run,
                        stagingDirectory,
                        runDirectory,
                        projectRoot,
                        ComputeFileSha256(stagedManifestPath),
                        manifest.capturedAtUtc);
                    contactSheetPath = Path.Combine(runDirectory, manifest.contactSheetFile);
                }
                finally
                {
                    Object.DestroyImmediate(contactSheet);
                }
            }
            catch (Exception exception)
            {
                error = $"Could not capture review images for '{run.scenario}', seed {run.seed}: {exception.Message}";
                return false;
            }
            finally
            {
                foreach (Texture2D image in images)
                {
                    if (image)
                        Object.DestroyImmediate(image);
                }

                if (renderTexture)
                {
                    renderTexture.Release();
                    Object.DestroyImmediate(renderTexture);
                }

                if (cameraObject)
                    Object.DestroyImmediate(cameraObject);

                TryDeleteDirectory(stagingDirectory);
            }

            return true;
        }

        /// <summary>Gets the absolute path of a retained run's capture manifest when it still exists.</summary>
        public static string GetExistingManifestPath(GenerationEvaluationRunRecord run)
        {
            if (run == null || string.IsNullOrWhiteSpace(run.visualReviewCaptureManifestPath))
                return string.Empty;

            string path = Path.Combine(GetProjectRoot(), run.visualReviewCaptureManifestPath);
            return File.Exists(path) ? path : string.Empty;
        }

        /// <summary>Gets the contact sheet next to a retained capture manifest when it still exists.</summary>
        public static string GetExistingContactSheetPath(GenerationEvaluationRunRecord run)
        {
            string manifestPath = GetExistingManifestPath(run);
            if (string.IsNullOrWhiteSpace(manifestPath))
                return string.Empty;

            string path = Path.Combine(Path.GetDirectoryName(manifestPath) ?? string.Empty, "contact-sheet.png");
            return File.Exists(path) ? path : string.Empty;
        }

        /// <summary>Validates a retained capture against its report metadata, manifest, and file hashes.</summary>
        public static ReviewCaptureStatus GetCaptureStatus(
            GenerationEvaluationReport report,
            int runIndex,
            out string contactSheetPath,
            out string error)
        {
            contactSheetPath = string.Empty;
            error = string.Empty;

            if (!report || runIndex < 0 || runIndex >= report.Runs.Count)
            {
                error = "The review capture does not refer to a valid report run.";
                return ReviewCaptureStatus.Invalid;
            }

            GenerationEvaluationRunRecord run = report.Runs[runIndex];
            bool hasMetadata = !string.IsNullOrWhiteSpace(run.visualReviewCaptureManifestPath) ||
                               !string.IsNullOrWhiteSpace(run.visualReviewCaptureManifestSha256) ||
                               !string.IsNullOrWhiteSpace(run.visualReviewCapturedAtUtc);
            if (!hasMetadata)
                return ReviewCaptureStatus.Missing;

            string manifestPath = GetAbsoluteStoredPath(run.visualReviewCaptureManifestPath);
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            {
                error = "The stored review-capture manifest is missing.";
                return ReviewCaptureStatus.Invalid;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(run.visualReviewCaptureManifestSha256) ||
                    !HashesMatch(ComputeFileSha256(manifestPath), run.visualReviewCaptureManifestSha256))
                {
                    error = "The review-capture manifest hash does not match the report.";
                    return ReviewCaptureStatus.Invalid;
                }

                ReviewCaptureManifest manifest = JsonUtility.FromJson<ReviewCaptureManifest>(
                    File.ReadAllText(manifestPath));
                if (!ManifestMatchesRun(manifest, report, run, runIndex, out error))
                    return ReviewCaptureStatus.Invalid;

                string captureDirectory = Path.GetDirectoryName(manifestPath) ?? string.Empty;
                if (!TryValidateCapturedFile(
                        captureDirectory,
                        manifest.contactSheetFile,
                        manifest.contactSheetSha256,
                        out contactSheetPath,
                        out error))
                    return ReviewCaptureStatus.Invalid;

                if (manifest.images == null || manifest.images.Count != ViewDefinitions.Length)
                {
                    error = $"The review-capture manifest must contain {ViewDefinitions.Length} view images.";
                    return ReviewCaptureStatus.Invalid;
                }

                foreach (ReviewViewDefinition definition in ViewDefinitions)
                {
                    ReviewImageRecord record = manifest.images.SingleOrDefault(candidate =>
                        candidate != null && string.Equals(candidate.view, definition.Key, StringComparison.Ordinal));
                    if (record == null ||
                        !string.Equals(record.file, definition.Key + ".png", StringComparison.Ordinal) ||
                        record.orthographic != definition.Orthographic ||
                        !IsValidViewportCrop(record.viewportCrop) ||
                        record.fixedSceneRendering is not ("translucent" or "not-present") ||
                        !TryValidateCapturedFile(
                            captureDirectory,
                            record.file,
                            record.sha256,
                            out _,
                            out error))
                    {
                        if (string.IsNullOrWhiteSpace(error))
                            error = $"The '{definition.Key}' review view is missing or does not match the current capture format.";
                        return ReviewCaptureStatus.Invalid;
                    }
                }

                return ReviewCaptureStatus.Valid;
            }
            catch (Exception exception)
            {
                contactSheetPath = string.Empty;
                error = $"The review capture could not be validated: {exception.Message}";
                return ReviewCaptureStatus.Invalid;
            }
        }

        private static void CommitCapture(
            GenerationEvaluationReport report,
            GenerationEvaluationRunRecord run,
            string stagingDirectory,
            string runDirectory,
            string projectRoot,
            string manifestSha256,
            string capturedAtUtc)
        {
            string backupDirectory = runDirectory + ".backup-" + Guid.NewGuid().ToString("N");
            string previousManifestPath = run.visualReviewCaptureManifestPath;
            string previousManifestSha256 = run.visualReviewCaptureManifestSha256;
            string previousCapturedAtUtc = run.visualReviewCapturedAtUtc;
            bool movedPreviousCapture = false;
            bool committedStagingCapture = false;

            try
            {
                if (Directory.Exists(runDirectory))
                {
                    Directory.Move(runDirectory, backupDirectory);
                    movedPreviousCapture = true;
                }

                Directory.Move(stagingDirectory, runDirectory);
                committedStagingCapture = true;

                string manifestPath = Path.Combine(runDirectory, "manifest.json");
                run.visualReviewCaptureManifestPath = MakeProjectRelative(manifestPath, projectRoot);
                run.visualReviewCaptureManifestSha256 = manifestSha256;
                run.visualReviewCapturedAtUtc = capturedAtUtc;
                EditorUtility.SetDirty(report);
                AssetDatabase.SaveAssetIfDirty(report);
            }
            catch
            {
                run.visualReviewCaptureManifestPath = previousManifestPath;
                run.visualReviewCaptureManifestSha256 = previousManifestSha256;
                run.visualReviewCapturedAtUtc = previousCapturedAtUtc;
                EditorUtility.SetDirty(report);
                AssetDatabase.SaveAssetIfDirty(report);

                if (committedStagingCapture && Directory.Exists(runDirectory))
                    Directory.Delete(runDirectory, true);
                if (movedPreviousCapture && Directory.Exists(backupDirectory))
                    Directory.Move(backupDirectory, runDirectory);
                throw;
            }

            TryDeleteDirectory(backupDirectory);
        }

        private static bool ManifestMatchesRun(
            ReviewCaptureManifest manifest,
            GenerationEvaluationReport report,
            GenerationEvaluationRunRecord run,
            int runIndex,
            out string error)
        {
            error = string.Empty;
            if (manifest == null || manifest.schemaVersion != CurrentManifestSchemaVersion)
            {
                error = "The review capture uses an outdated or unreadable manifest format.";
                return false;
            }

            string reportAssetPath = AssetDatabase.GetAssetPath(report);
            string sourceScenePath = EvaluationSceneWorkspace.ResolveSourceScenePath(
                report.SuiteAssetPath,
                run);
            bool layoutMatches = !string.IsNullOrWhiteSpace(run.layoutGuid)
                ? string.Equals(manifest.layoutGuid, run.layoutGuid, StringComparison.Ordinal)
                : PathsMatch(manifest.layoutAssetPath, run.ResolvedLayoutAssetPath);
            bool matches = manifest.runIndex == runIndex &&
                           string.Equals(manifest.reportName, report.name, StringComparison.Ordinal) &&
                           PathsMatch(manifest.reportAssetPath, reportAssetPath) &&
                           string.Equals(manifest.campaignCreatedAtUtc, report.CreatedAtUtc, StringComparison.Ordinal) &&
                           string.Equals(manifest.capturedAtUtc, run.visualReviewCapturedAtUtc, StringComparison.Ordinal) &&
                           string.Equals(manifest.scenario, run.scenario, StringComparison.Ordinal) &&
                           string.Equals(manifest.scenarioKind, run.scenarioKind, StringComparison.Ordinal) &&
                           PathsMatch(manifest.scene, sourceScenePath) &&
                           string.Equals(manifest.targetId, run.targetId, StringComparison.Ordinal) &&
                           manifest.seed == run.seed &&
                           manifest.requestedCount == run.requestedCount &&
                           manifest.placedCount == run.placedCount &&
                           layoutMatches &&
                           manifest.imageWidth == ImageWidth &&
                           manifest.imageHeight == ImageHeight &&
                           string.Equals(manifest.contactSheetFile, "contact-sheet.png", StringComparison.Ordinal);
            if (matches)
                return true;

            error = "The review-capture manifest no longer matches this evaluation run.";
            return false;
        }

        private static bool TryValidateCapturedFile(
            string directory,
            string fileName,
            string expectedSha256,
            out string absolutePath,
            out string error)
        {
            absolutePath = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(fileName) ||
                !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
                fileName.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                error = "The review-capture manifest contains an invalid file name.";
                return false;
            }

            absolutePath = Path.Combine(directory, fileName);
            if (!File.Exists(absolutePath))
            {
                error = $"The captured file '{fileName}' is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(expectedSha256) ||
                !HashesMatch(ComputeFileSha256(absolutePath), expectedSha256))
            {
                error = $"The captured file '{fileName}' does not match its recorded hash.";
                return false;
            }

            return true;
        }

        private static string GetAbsoluteStoredPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(GetProjectRoot(), path));
        }

        private static bool PathsMatch(string left, string right) =>
            string.Equals(
                (left ?? string.Empty).Replace('\\', '/'),
                (right ?? string.Empty).Replace('\\', '/'),
                StringComparison.OrdinalIgnoreCase);

        private static bool HashesMatch(string left, string right) =>
            string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                // A stale staging directory is harmless and can be removed on the next cleanup.
            }
            catch (UnauthorizedAccessException)
            {
                // Keep the completed capture even if an obsolete backup cannot be removed.
            }
        }

        internal static ReviewCameraPose CalculateCameraPose(
            Bounds bounds,
            ReviewViewDefinition definition,
            float aspect)
        {
            bounds = EnsureUsableBounds(bounds);
            aspect = Mathf.Max(0.01f, aspect);
            Quaternion rotation = Quaternion.LookRotation(definition.LookDirection.normalized, definition.Up.normalized);
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 extents = bounds.extents;
            float horizontalExtent = ProjectedExtent(extents, right);
            float verticalExtent = ProjectedExtent(extents, up);
            float depthExtent = ProjectedExtent(extents, forward);

            if (definition.Orthographic)
            {
                float orthographicSize = Mathf.Max(verticalExtent, horizontalExtent / aspect) * FramingMargin;
                float distance = depthExtent + Mathf.Max(bounds.size.magnitude, 2f);
                return new ReviewCameraPose(
                    bounds.center - forward * distance,
                    rotation,
                    Mathf.Max(0.25f, orthographicSize),
                    distance + depthExtent + Mathf.Max(bounds.size.magnitude, 10f));
            }

            const float fieldOfView = 35f;
            float verticalHalfAngle = fieldOfView * 0.5f * Mathf.Deg2Rad;
            float horizontalHalfAngle = Mathf.Atan(Mathf.Tan(verticalHalfAngle) * aspect);
            float limitingHalfAngle = Mathf.Min(verticalHalfAngle, horizontalHalfAngle);
            float perspectiveDistance = bounds.extents.magnitude /
                                        Mathf.Max(0.001f, Mathf.Sin(limitingHalfAngle)) * FramingMargin;
            perspectiveDistance = Mathf.Max(perspectiveDistance, bounds.size.magnitude);
            return new ReviewCameraPose(
                bounds.center - forward * perspectiveDistance,
                rotation,
                0f,
                perspectiveDistance + depthExtent + Mathf.Max(bounds.size.magnitude, 10f),
                fieldOfView);
        }

        internal static Texture2D CreateContactSheet(
            IReadOnlyList<Texture2D> images,
            int imageWidth,
            int imageHeight,
            int gap)
        {
            if (images == null || images.Count != 4)
                throw new ArgumentException("A contact sheet requires exactly four images.", nameof(images));

            int width = imageWidth * 2 + gap;
            int height = imageHeight * 2 + gap;
            Texture2D sheet = new(width, height, TextureFormat.RGB24, false);
            Color32[] background = Enumerable.Repeat(new Color32(32, 37, 43, 255), width * height).ToArray();
            sheet.SetPixels32(background);
            sheet.SetPixels(0, imageHeight + gap, imageWidth, imageHeight, images[0].GetPixels());
            sheet.SetPixels(imageWidth + gap, imageHeight + gap, imageWidth, imageHeight, images[1].GetPixels());
            sheet.SetPixels(0, 0, imageWidth, imageHeight, images[2].GetPixels());
            sheet.SetPixels(imageWidth + gap, 0, imageWidth, imageHeight, images[3].GetPixels());
            sheet.Apply(false, false);
            return sheet;
        }

        internal static string Sanitize(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "Genix" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '_');
            return result.Replace(' ', '_');
        }

        internal static string GetReportDirectory(string reportName) =>
            Path.Combine(GetProjectRoot(), OutputRootName, Sanitize(reportName));

        private static bool TryOpenSceneAndApplyLayout(
            string suiteAssetPath,
            string sourceScenePath,
            GenerationEvaluationRunRecord run,
            SavedLayout layout,
            out Bounds reviewBounds,
            out string framingSource,
            out Transform generatedParent,
            out string error)
        {
            reviewBounds = EnsureUsableBounds(layout.Bounds);
            framingSource = "saved layout bounds";
            generatedParent = null;
            error = string.Empty;
            string layoutAssetPath = AssetDatabase.GetAssetPath(layout);

            if (!EvaluationSceneWorkspace.TryPrepare(sourceScenePath, out string writableScenePath, out error))
                return false;

            Scene scene = SceneManager.GetActiveScene();
            if (!EvaluationSceneWorkspace.MatchesSource(scene.path, sourceScenePath))
                scene = EditorSceneManager.OpenScene(writableScenePath, OpenSceneMode.Single);

            IBenchmarkAreaResolver resolver = BenchmarkAreaResolverRegistry.CreateResolvers()
                .FirstOrDefault(candidate => candidate.ProviderId == run.areaProviderId);
            IAreaSource areaSource = resolver?.Resolve(scene, run.targetId);
            if (areaSource == null)
            {
                error = $"Could not resolve target '{run.targetId}' in '{sourceScenePath}'.";
                return false;
            }
            // Scene loading or an intervening import can invalidate retained Unity object references.
            layout = AssetDatabase.LoadAssetAtPath<SavedLayout>(layoutAssetPath);
            if (!layout)
            {
                error = $"The referenced layout asset '{layoutAssetPath}' could not be reloaded after scene preparation.";
                return false;
            }

            if (!LayoutApplyService.Apply(layout, areaSource, out error))
                return false;
            if (!GeneratedHierarchy.TryGet(areaSource, out generatedParent))
            {
                error = "The applied layout did not create a generated hierarchy for review capture.";
                return false;
            }

            bool hasGeneratedBounds = TryGetGeneratedBounds(generatedParent, out Bounds generatedBounds);
            if (hasGeneratedBounds)
            {
                reviewBounds = generatedBounds;
                framingSource = "applied layout renderer bounds";
            }

            if (TryResolveAreaBounds(suiteAssetPath, run, areaSource, out Bounds areaBounds))
            {
                reviewBounds = areaBounds;
                reviewBounds.Encapsulate(hasGeneratedBounds ? generatedBounds : layout.Bounds);
                reviewBounds = EnsureUsableBounds(reviewBounds);
                framingSource = hasGeneratedBounds
                    ? "target planning-area and applied layout renderer bounds"
                    : "target planning-area and saved-layout bounds";
            }

            return true;
        }

        private static bool TryResolveAreaBounds(
            string suiteAssetPath,
            GenerationEvaluationRunRecord run,
            IAreaSource areaSource,
            out Bounds bounds)
        {
            bounds = default;
            GenerationEvaluationSuite suite = AssetDatabase.LoadAssetAtPath<GenerationEvaluationSuite>(
                suiteAssetPath);
            GenerationEvaluationScenario scenario = suite?.Scenarios.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(candidate.TargetId, run.targetId, StringComparison.Ordinal) &&
                string.Equals(candidate.AreaProviderId, run.areaProviderId, StringComparison.Ordinal));
            if (scenario == null || !scenario.GenerationPreset)
                return false;

            GenerationPresetSettings settings = scenario.GenerationPreset.Settings;

            LayerMask combinedLayers = settings.FloorSurfaceLayers |
                                       settings.WallSurfaceLayers |
                                       settings.CeilingSurfaceLayers;
            AreaBuildSettings areaSettings = new(
                settings.AreaDecompositionMode,
                combinedLayers,
                settings.FloorSurfaceLayers,
                settings.WallSurfaceLayers,
                settings.CeilingSurfaceLayers,
                floorNormalYThreshold: Mathf.Cos(settings.FloorSurfaceAngleDegrees * Mathf.Deg2Rad),
                ceilingNormalYThreshold: -Mathf.Cos(settings.CeilingSurfaceAngleDegrees * Mathf.Deg2Rad),
                surfaceDiscoveryMode: settings.SurfaceDiscoveryMode);

            if (!areaSource.TryBuildArea(areaSettings, out PlacementArea area, out _))
                return false;

            bounds = area.WorldBounds;
            return HasUsableBounds(bounds);
        }

        private static bool TryGetGeneratedBounds(Transform generatedParent, out Bounds bounds)
        {
            bounds = default;
            bool found = false;

            foreach (Renderer renderer in generatedParent.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer || !HasUsableBounds(renderer.bounds))
                    continue;

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (found)
                return true;

            foreach (Collider collider in generatedParent.GetComponentsInChildren<Collider>(true))
            {
                if (!collider || !HasUsableBounds(collider.bounds))
                    continue;

                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return found;
        }

        private static void ConfigureCamera(Camera camera)
        {
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.88f, 0.9f, 0.92f, 1f);
            camera.cullingMask = ~0;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.useOcclusionCulling = false;
            camera.nearClipPlane = 0.01f;
        }

        internal static Bounds GetViewFramingBounds(
            Bounds reviewBounds,
            Bounds generatedBounds,
            ReviewViewDefinition definition)
        {
            if (!definition.IsSideView)
                return reviewBounds;

            reviewBounds.center = new Vector3(
                reviewBounds.center.x,
                generatedBounds.center.y,
                reviewBounds.center.z);
            reviewBounds.size = new Vector3(
                reviewBounds.size.x,
                generatedBounds.size.y,
                reviewBounds.size.z);
            return EnsureUsableBounds(reviewBounds);
        }

        private static void ConfigureView(
            Camera camera,
            Bounds bounds,
            ReviewViewDefinition definition,
            float aspect)
        {
            ReviewCameraPose pose = CalculateCameraPose(bounds, definition, aspect);
            camera.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            camera.orthographic = definition.Orthographic;
            camera.orthographicSize = pose.OrthographicSize;
            camera.fieldOfView = pose.FieldOfView;
            camera.farClipPlane = Mathf.Max(100f, pose.FarClipPlane);
        }

        private static Texture2D Render(Camera camera, RenderTexture renderTexture)
        {
            RenderTexture previous = RenderTexture.active;
            camera.Render();
            RenderTexture.active = renderTexture;
            Texture2D image = new(ImageWidth, ImageHeight, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, ImageWidth, ImageHeight), 0, 0, false);
            image.Apply(false, false);
            RenderTexture.active = previous;
            return image;
        }

        private static Texture2D CropOverviewToVisibleContent(
            Texture2D source,
            Color backgroundColor,
            out Rect viewportCrop)
        {
            if (!TryCalculateOverviewCrop(source, backgroundColor, out viewportCrop) ||
                viewportCrop.width >= 0.995f && viewportCrop.height >= 0.995f)
            {
                viewportCrop = new Rect(0f, 0f, 1f, 1f);
                return source;
            }

            RenderTexture croppedRender = RenderTexture.GetTemporary(
                ImageWidth,
                ImageHeight,
                0,
                RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(
                    source,
                    croppedRender,
                    new Vector2(viewportCrop.width, viewportCrop.height),
                    new Vector2(viewportCrop.x, viewportCrop.y));
                RenderTexture.active = croppedRender;
                Texture2D cropped = new(ImageWidth, ImageHeight, TextureFormat.RGB24, false);
                cropped.ReadPixels(new Rect(0f, 0f, ImageWidth, ImageHeight), 0, 0, false);
                cropped.Apply(false, false);
                return cropped;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(croppedRender);
            }
        }

        internal static bool TryCalculateOverviewCrop(
            Texture2D image,
            Color backgroundColor,
            out Rect viewportCrop)
        {
            viewportCrop = new Rect(0f, 0f, 1f, 1f);
            if (!image || image.width <= 0 || image.height <= 0)
                return false;

            Color32 background = backgroundColor;
            Color32[] pixels = image.GetPixels32();
            int minX = image.width;
            int minY = image.height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < image.height; y++)
            {
                int row = y * image.width;
                for (int x = 0; x < image.width; x++)
                {
                    Color32 pixel = pixels[row + x];
                    int difference = Mathf.Max(
                        Mathf.Abs(pixel.r - background.r),
                        Mathf.Abs(pixel.g - background.g),
                        Mathf.Abs(pixel.b - background.b));
                    if (difference <= BackgroundColorThreshold)
                        continue;

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
                return false;

            float contentWidth = maxX - minX + 1f;
            float contentHeight = maxY - minY + 1f;
            float cropWidth = Mathf.Min(
                image.width,
                contentWidth * (1f + OverviewContentPadding * 2f));
            float cropHeight = Mathf.Min(
                image.height,
                contentHeight * (1f + OverviewContentPadding * 2f));
            float targetAspect = image.width / (float)image.height;
            if (cropWidth / cropHeight < targetAspect)
                cropWidth = Mathf.Min(image.width, cropHeight * targetAspect);
            else
                cropHeight = Mathf.Min(image.height, cropWidth / targetAspect);

            float centerX = (minX + maxX + 1f) * 0.5f;
            float centerY = (minY + maxY + 1f) * 0.5f;
            float cropX = Mathf.Clamp(centerX - cropWidth * 0.5f, 0f, image.width - cropWidth);
            float cropY = Mathf.Clamp(centerY - cropHeight * 0.5f, 0f, image.height - cropHeight);
            viewportCrop = new Rect(
                cropX / image.width,
                cropY / image.height,
                cropWidth / image.width,
                cropHeight / image.height);
            return true;
        }

        private static float ProjectedExtent(Vector3 extents, Vector3 axis) =>
            Mathf.Abs(axis.x) * extents.x +
            Mathf.Abs(axis.y) * extents.y +
            Mathf.Abs(axis.z) * extents.z;

        private static Bounds EnsureUsableBounds(Bounds bounds)
        {
            Vector3 size = bounds.size;
            size.x = Mathf.Max(MinimumBoundsSize, size.x);
            size.y = Mathf.Max(MinimumBoundsSize, size.y);
            size.z = Mathf.Max(MinimumBoundsSize, size.z);
            bounds.size = size;
            return bounds;
        }

        private static bool HasUsableBounds(Bounds bounds) =>
            IsFinite(bounds.center) &&
            IsFinite(bounds.size) &&
            bounds.size.x > 0f &&
            bounds.size.y > 0f &&
            bounds.size.z > 0f;

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool IsValidViewportCrop(Rect crop) =>
            !float.IsNaN(crop.x) && !float.IsInfinity(crop.x) &&
            !float.IsNaN(crop.y) && !float.IsInfinity(crop.y) &&
            !float.IsNaN(crop.width) && !float.IsInfinity(crop.width) &&
            !float.IsNaN(crop.height) && !float.IsInfinity(crop.height) &&
            crop.x >= 0f && crop.y >= 0f &&
            crop.width > 0f && crop.height > 0f &&
            crop.xMax <= 1.0001f && crop.yMax <= 1.0001f;

        private static string GetProjectRoot() =>
            Directory.GetParent(Application.dataPath)?.FullName ?? Environment.CurrentDirectory;

        private static string MakeProjectRelative(string path, string projectRoot)
        {
            string prefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(prefix.Length).Replace('\\', '/')
                : path.Replace('\\', '/');
        }

        private static string ComputeFileSha256(string path) => ComputeSha256(File.ReadAllBytes(path));

        private static string ComputeSha256(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return string.Concat(sha256.ComputeHash(bytes).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        internal readonly struct ReviewViewDefinition
        {
            public string Key { get; }
            public bool Orthographic { get; }
            public Vector3 LookDirection { get; }
            public Vector3 Up { get; }
            public bool IsSideView => Key.StartsWith("side-", StringComparison.Ordinal);
            public bool IsOverview => string.Equals(Key, "overview", StringComparison.Ordinal);

            public ReviewViewDefinition(string key, bool orthographic, Vector3 lookDirection, Vector3 up)
            {
                Key = key;
                Orthographic = orthographic;
                LookDirection = lookDirection;
                Up = up;
            }
        }

        internal readonly struct ReviewCameraPose
        {
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public float OrthographicSize { get; }
            public float FarClipPlane { get; }
            public float FieldOfView { get; }

            public ReviewCameraPose(
                Vector3 position,
                Quaternion rotation,
                float orthographicSize,
                float farClipPlane,
                float fieldOfView = 35f)
            {
                Position = position;
                Rotation = rotation;
                OrthographicSize = orthographicSize;
                FarClipPlane = farClipPlane;
                FieldOfView = fieldOfView;
            }
        }

        [Serializable]
        private sealed class ReviewCaptureManifest
        {
            public int schemaVersion;
            public string reportAssetPath = string.Empty;
            public string reportName = string.Empty;
            public string campaignCreatedAtUtc = string.Empty;
            public string capturedAtUtc = string.Empty;
            public int runIndex;
            public string scenario = string.Empty;
            public string scenarioKind = string.Empty;
            public string scene = string.Empty;
            public string targetId = string.Empty;
            public int seed;
            public int requestedCount;
            public int placedCount;
            public string layoutAssetPath = string.Empty;
            public string layoutGuid = string.Empty;
            public string framingSource = string.Empty;
            public Vector3 framingBoundsCenter;
            public Vector3 framingBoundsSize;
            public int imageWidth;
            public int imageHeight;
            public string sceneRenderingScope = string.Empty;
            public string contactSheetFile = string.Empty;
            public string contactSheetSha256 = string.Empty;
            public string contactSheetOrder = string.Empty;
            public List<ReviewImageRecord> images = new();
        }

        [Serializable]
        private sealed class ReviewImageRecord
        {
            public string view = string.Empty;
            public string file = string.Empty;
            public string sha256 = string.Empty;
            public bool orthographic;
            public Vector3 cameraPosition;
            public Quaternion cameraRotation;
            public float orthographicSize;
            public float fieldOfView;
            public Rect viewportCrop;
            public string fixedSceneRendering = string.Empty;
        }

        private sealed class FixedSceneTransparencyScope : IDisposable
        {
            private readonly List<(Renderer Renderer, Material[] Materials)> _renderers = new();
            private readonly Dictionary<Material, Material> _materialCopies = new();

            public string RenderingMode => _renderers.Count > 0 ? "translucent" : "not-present";

            public FixedSceneTransparencyScope(Transform generatedParent)
            {
                Scene scene = SceneManager.GetActiveScene();
                foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                {
                    if (!renderer || renderer.gameObject.scene != scene ||
                        generatedParent && renderer.transform.IsChildOf(generatedParent))
                    {
                        continue;
                    }

                    Material[] materials = renderer.sharedMaterials;
                    if (materials.Length == 0)
                        continue;

                    _renderers.Add((renderer, materials));
                    renderer.sharedMaterials = materials
                        .Select(GetOrCreateTranslucentMaterial)
                        .ToArray();
                }
            }

            public void Dispose()
            {
                foreach ((Renderer renderer, Material[] materials) in _renderers)
                {
                    if (renderer)
                        renderer.sharedMaterials = materials;
                }

                foreach (Material material in _materialCopies.Values)
                {
                    if (material)
                        Object.DestroyImmediate(material);
                }
            }

            private Material GetOrCreateTranslucentMaterial(Material source)
            {
                if (!source)
                    return CreateFallbackTranslucentMaterial();
                if (_materialCopies.TryGetValue(source, out Material existing))
                    return existing;

                Material material = CreateTranslucentMaterialCopy(source);
                _materialCopies.Add(source, material);
                return material;
            }

            private Material CreateFallbackTranslucentMaterial()
            {
                Material material = CreateTranslucentMaterialCopy(null);
                _materialCopies.Add(material, material);
                return material;
            }
        }

        internal static Material CreateTranslucentMaterialCopy(Material source)
        {
            Shader shader = source
                ? source.shader
                : Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader)
                throw new InvalidOperationException(
                    "No supported transparent shader is available for review capture.");

            Material material = source ? new Material(source) : new Material(shader);
            material.name = source
                ? $"Genix Review {source.name}"
                : "Genix Review Fixed Geometry";
            material.hideFlags = HideFlags.HideAndDontSave;
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            Color color = GetMaterialColor(material);
            color.a = Mathf.Clamp(color.a * 0.32f, 0.2f, 0.32f);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Mode"))
                material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return material;
        }

        private static Color GetMaterialColor(Material material)
        {
            if (material.HasProperty("_BaseColor"))
                return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color"))
                return material.GetColor("_Color");
            return new Color(0.45f, 0.55f, 0.65f, 1f);
        }

        private sealed class TargetSceneVisibilityScope : IDisposable
        {
            private readonly List<Renderer> _hiddenRenderers = new();

            public TargetSceneVisibilityScope(Bounds focusBounds, Transform generatedParent)
            {
                focusBounds.Expand(0.02f);

                Scene scene = SceneManager.GetActiveScene();
                foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                {
                    if (!renderer || !renderer.enabled || renderer.gameObject.scene != scene ||
                        renderer.bounds.Intersects(focusBounds) ||
                        generatedParent && renderer.transform.IsChildOf(generatedParent))
                    {
                        continue;
                    }

                    _hiddenRenderers.Add(renderer);
                    renderer.enabled = false;
                }
            }

            public void Dispose()
            {
                foreach (Renderer renderer in _hiddenRenderers)
                {
                    if (renderer)
                        renderer.enabled = true;
                }
            }
        }
    }
}
