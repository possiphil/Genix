using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Genix.Editor.Evaluation;
using Genix.Tests.Framework;
using NUnit.Framework;
using UnityEngine;

namespace Genix.Tests
{
    [Category(GenixTestCategories.Full)]
    [Category(GenixTestCategories.Integration)]
    [Category(GenixTestCategories.WorkflowArea)]
    public sealed class GenerationEvaluationReviewCaptureTests
    {
        [TestCase(false, -1f, -0.72f, 1f, 0f, 1f, 0f)]
        [TestCase(true, 0f, -1f, 0f, 0f, 0f, 1f)]
        [TestCase(true, -1f, 0f, 0f, 0f, 1f, 0f)]
        [TestCase(true, 0f, 0f, -1f, 0f, 1f, 0f)]
        public void CalculatedReviewCameraFacesBoundsCenter(
            bool orthographic,
            float lookX,
            float lookY,
            float lookZ,
            float upX,
            float upY,
            float upZ)
        {
            Bounds bounds = new(new Vector3(4f, 3f, -2f), new Vector3(12f, 5f, 8f));
            GenerationEvaluationReviewCaptureService.ReviewViewDefinition definition = new(
                "test",
                orthographic,
                new Vector3(lookX, lookY, lookZ),
                new Vector3(upX, upY, upZ));

            GenerationEvaluationReviewCaptureService.ReviewCameraPose pose =
                GenerationEvaluationReviewCaptureService.CalculateCameraPose(bounds, definition, 16f / 9f);

            Vector3 directionToCenter = (bounds.center - pose.Position).normalized;
            Vector3 cameraForward = pose.Rotation * Vector3.forward;
            Assert.That(Vector3.Dot(directionToCenter, cameraForward), Is.GreaterThan(0.999f));
            Assert.That(pose.FarClipPlane, Is.GreaterThan(Vector3.Distance(pose.Position, bounds.center)));
            Assert.That(pose.OrthographicSize, orthographic ? Is.GreaterThan(0f) : Is.EqualTo(0f));
        }

        [Test]
        public void PerspectiveOverviewContainsEveryBoundsCorner()
        {
            Bounds bounds = new(new Vector3(-0.5f, 5.5f, -0.5f), new Vector3(40f, 12f, 40f));
            GenerationEvaluationReviewCaptureService.ReviewViewDefinition definition = new(
                "overview",
                false,
                new Vector3(-1f, -0.72f, 1f),
                Vector3.up);

            const float aspect = 16f / 9f;
            GenerationEvaluationReviewCaptureService.ReviewCameraPose pose =
                GenerationEvaluationReviewCaptureService.CalculateCameraPose(bounds, definition, aspect);
            Quaternion inverseRotation = Quaternion.Inverse(pose.Rotation);
            float verticalTangent = Mathf.Tan(pose.FieldOfView * 0.5f * Mathf.Deg2Rad);
            float horizontalTangent = verticalTangent * aspect;
            float largestViewportExtent = 0f;

            foreach (Vector3 corner in GetBoundsCorners(bounds))
            {
                Vector3 cameraSpace = inverseRotation * (corner - pose.Position);
                Assert.That(cameraSpace.z, Is.GreaterThan(0f));
                largestViewportExtent = Mathf.Max(
                    largestViewportExtent,
                    Mathf.Abs(cameraSpace.x) / (cameraSpace.z * horizontalTangent),
                    Mathf.Abs(cameraSpace.y) / (cameraSpace.z * verticalTangent));
            }

            Assert.That(largestViewportExtent, Is.LessThanOrEqualTo(1f));
            Assert.That(definition.IsOverview, Is.True);
        }

        [Test]
        public void TranslucentFixedMaterialPreservesSourceColor()
        {
            Shader shader = Shader.Find("Standard");
            if (!shader)
                Assert.Ignore("The Standard shader is not available in this test project.");

            Material source = new(shader);
            Material copy = null;
            try
            {
                Color sourceColor = new(0.18f, 0.46f, 0.72f, 1f);
                source.SetColor("_Color", sourceColor);

                copy = GenerationEvaluationReviewCaptureService.CreateTranslucentMaterialCopy(source);
                Color copyColor = copy.GetColor("_Color");

                Assert.That(copyColor.r, Is.EqualTo(sourceColor.r).Within(0.001f));
                Assert.That(copyColor.g, Is.EqualTo(sourceColor.g).Within(0.001f));
                Assert.That(copyColor.b, Is.EqualTo(sourceColor.b).Within(0.001f));
                Assert.That(copyColor.a, Is.InRange(0.2f, 0.32f));
                Assert.That(copy.renderQueue, Is.EqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void OverviewCropCentersVisibleContentAndPreservesAspectRatio()
        {
            Color background = new(0.88f, 0.9f, 0.92f, 1f);
            Texture2D texture = new(160, 90, TextureFormat.RGB24, false);
            try
            {
                Color32[] pixels = new Color32[texture.width * texture.height];
                Color32 backgroundPixel = background;
                for (int index = 0; index < pixels.Length; index++)
                    pixels[index] = backgroundPixel;
                for (int y = 30; y < 60; y++)
                {
                    for (int x = 50; x < 110; x++)
                        pixels[y * texture.width + x] = new Color32(20, 100, 180, 255);
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, false);

                bool found = GenerationEvaluationReviewCaptureService.TryCalculateOverviewCrop(
                    texture,
                    background,
                    out Rect crop);

                Assert.That(found, Is.True);
                Assert.That(crop.width, Is.EqualTo(crop.height).Within(0.001f));
                Assert.That(crop.width, Is.InRange(0.35f, 0.55f));
                Assert.That(crop.center.x, Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(crop.center.y, Is.EqualTo(0.5f).Within(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void ContactSheetUsesDocumentedQuadrantOrder()
        {
            Texture2D overview = CreateSolidTexture(Color.red);
            Texture2D top = CreateSolidTexture(Color.green);
            Texture2D sideX = CreateSolidTexture(Color.blue);
            Texture2D sideZ = CreateSolidTexture(Color.yellow);
            Texture2D sheet = null;

            try
            {
                sheet = GenerationEvaluationReviewCaptureService.CreateContactSheet(
                    new List<Texture2D> { overview, top, sideX, sideZ },
                    2,
                    2,
                    1);

                Assert.That(sheet.width, Is.EqualTo(5));
                Assert.That(sheet.height, Is.EqualTo(5));
                AssertColor(sheet.GetPixel(0, 3), Color.red);
                AssertColor(sheet.GetPixel(3, 3), Color.green);
                AssertColor(sheet.GetPixel(0, 0), Color.blue);
                AssertColor(sheet.GetPixel(3, 0), Color.yellow);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sheet);
                UnityEngine.Object.DestroyImmediate(overview);
                UnityEngine.Object.DestroyImmediate(top);
                UnityEngine.Object.DestroyImmediate(sideX);
                UnityEngine.Object.DestroyImmediate(sideZ);
            }
        }

        [Test]
        public void ReviewPdfContainsOneLabeledPagePerContactSheet()
        {
            string directory = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Environment.CurrentDirectory,
                "Library",
                "GenixTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string imagePath = Path.Combine(directory, "contact-sheet.png");
            string pdfPath = Path.Combine(directory, "review.pdf");
            Texture2D texture = CreateSolidTexture(Color.cyan);

            try
            {
                File.WriteAllBytes(imagePath, texture.EncodeToPNG());
                List<GenerationEvaluationReviewPdfService.ReviewPdfPage> pages = new()
                {
                    new(imagePath, 0, "Spatial - Control", 42, 20, 20, "SpatialTargetTests"),
                    new(imagePath, 1, "Spatial - Obstacles", 84, 20, 18, "SpatialTargetTests")
                };

                GenerationEvaluationReviewPdfService.WritePdf(pdfPath, "Review Test", pages);

                byte[] bytes = File.ReadAllBytes(pdfPath);
                string pdf = System.Text.Encoding.ASCII.GetString(bytes);
                Assert.That(pdf, Does.StartWith("%PDF-1.4"));
                Assert.That(pdf, Does.Contain("/Type /Pages /Count 2"));
                Assert.That(
                    CountOccurrences(pdf, "/Subtype /Image"),
                    Is.EqualTo(2));
                Assert.That(pdf, Does.Contain("Spatial - Control"));
                Assert.That(pdf, Does.Contain("Spatial - Obstacles"));
                Assert.That(pdf, Does.EndWith("%%EOF\n"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SideViewKeepsTargetWidthButCentersGeneratedHeight()
        {
            Bounds targetBounds = new(new Vector3(1f, 4f, 2f), new Vector3(12f, 8f, 10f));
            Bounds generatedBounds = new(new Vector3(0f, 0.5f, 0f), new Vector3(8f, 1f, 6f));
            GenerationEvaluationReviewCaptureService.ReviewViewDefinition side = new(
                "side-x",
                true,
                Vector3.left,
                Vector3.up);

            Bounds framed = GenerationEvaluationReviewCaptureService.GetViewFramingBounds(
                targetBounds,
                generatedBounds,
                side);

            Assert.That(framed.center.y, Is.EqualTo(generatedBounds.center.y));
            Assert.That(framed.size.y, Is.EqualTo(generatedBounds.size.y));
            Assert.That(framed.center.x, Is.EqualTo(targetBounds.center.x));
            Assert.That(framed.center.z, Is.EqualTo(targetBounds.center.z));
            Assert.That(framed.size.x, Is.EqualTo(targetBounds.size.x));
            Assert.That(framed.size.z, Is.EqualTo(targetBounds.size.z));
        }

        [Test]
        public void CaptureDirectoryNamesAreFileSystemSafe()
        {
            string sanitized = GenerationEvaluationReviewCaptureService.Sanitize("Office / target: seed");

            Assert.That(sanitized, Does.Not.Contain("/"));
            foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
                Assert.That(sanitized, Does.Not.Contain(invalid.ToString()));
        }

        [Test]
        public void CaptureStatusValidatesManifestAndEveryImageHash()
        {
            string directory = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? Environment.CurrentDirectory,
                "Library",
                "GenixTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            GenerationEvaluationReport report = ScriptableObject.CreateInstance<GenerationEvaluationReport>();
            report.name = "Review Capture Test";

            try
            {
                GenerationEvaluationRunRecord run = new()
                {
                    scenario = "Test Scenario",
                    scenarioKind = "Spatial",
                    scene = "Assets/Test.unity",
                    targetId = "Target",
                    seed = 42,
                    requestedCount = 10,
                    placedCount = 9,
                    layoutGuid = "layout-guid",
                    visualReviewCapturedAtUtc = "2026-09-01T12:00:00.0000000Z"
                };
                report.Initialize(new GenerationEvaluationCampaignResult
                {
                    createdAtUtc = "2026-09-01T10:00:00.0000000Z",
                    runs = new List<GenerationEvaluationRunRecord> { run }
                });

                byte[] contactBytes = { 1, 2, 3 };
                File.WriteAllBytes(Path.Combine(directory, "contact-sheet.png"), contactBytes);
                string[] views = { "overview", "top", "side-x", "side-z" };
                List<CaptureImageStub> images = new();
                for (int index = 0; index < views.Length; index++)
                {
                    byte[] bytes = { (byte)(10 + index), (byte)(20 + index) };
                    string file = views[index] + ".png";
                    File.WriteAllBytes(Path.Combine(directory, file), bytes);
                    images.Add(new CaptureImageStub
                    {
                        view = views[index],
                        file = file,
                        sha256 = ComputeSha256(bytes),
                        orthographic = index > 0,
                        viewportCrop = new Rect(0f, 0f, 1f, 1f),
                        fixedSceneRendering = "translucent"
                    });
                }

                CaptureManifestStub manifest = new()
                {
                    schemaVersion = GenerationEvaluationReviewCaptureService.CurrentManifestSchemaVersion,
                    runIndex = 0,
                    reportName = report.name,
                    campaignCreatedAtUtc = report.CreatedAtUtc,
                    capturedAtUtc = run.visualReviewCapturedAtUtc,
                    scenario = run.scenario,
                    scenarioKind = run.scenarioKind,
                    scene = run.scene,
                    targetId = run.targetId,
                    seed = run.seed,
                    requestedCount = run.requestedCount,
                    placedCount = run.placedCount,
                    layoutGuid = run.layoutGuid,
                    imageWidth = GenerationEvaluationReviewCaptureService.ImageWidth,
                    imageHeight = GenerationEvaluationReviewCaptureService.ImageHeight,
                    contactSheetFile = "contact-sheet.png",
                    contactSheetSha256 = ComputeSha256(contactBytes),
                    images = images
                };
                string manifestPath = Path.Combine(directory, "manifest.json");
                File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
                run.visualReviewCaptureManifestPath = manifestPath;
                run.visualReviewCaptureManifestSha256 = ComputeFileSha256(manifestPath);

                Assert.That(
                    GenerationEvaluationReviewCaptureService.GetCaptureStatus(
                        report,
                        0,
                        out string contactSheet,
                        out string validError),
                    Is.EqualTo(GenerationEvaluationReviewCaptureService.ReviewCaptureStatus.Valid),
                    validError);
                Assert.That(contactSheet, Is.EqualTo(Path.Combine(directory, "contact-sheet.png")));

                File.WriteAllBytes(Path.Combine(directory, "side-z.png"), new byte[] { 99 });

                Assert.That(
                    GenerationEvaluationReviewCaptureService.GetCaptureStatus(
                        report,
                        0,
                        out _,
                        out string invalidError),
                    Is.EqualTo(GenerationEvaluationReviewCaptureService.ReviewCaptureStatus.Invalid));
                Assert.That(invalidError, Does.Contain("side-z.png").And.Contain("hash"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(report);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        private static Texture2D CreateSolidTexture(Color color)
        {
            Texture2D texture = new(2, 2, TextureFormat.RGB24, false);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            return texture;
        }

        private static IEnumerable<Vector3> GetBoundsCorners(Bounds bounds)
        {
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        yield return bounds.center + Vector3.Scale(
                            bounds.extents,
                            new Vector3(x, y, z));
                    }
                }
            }
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f));
        }

        private static int CountOccurrences(string value, string search)
        {
            int count = 0;
            int index = 0;
            while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += search.Length;
            }

            return count;
        }

        private static string ComputeFileSha256(string path) => ComputeSha256(File.ReadAllBytes(path));

        private static string ComputeSha256(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(bytes))
                .Replace("-", string.Empty)
                .ToLower(CultureInfo.InvariantCulture);
        }

        [Serializable]
        private sealed class CaptureManifestStub
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
            public int imageWidth;
            public int imageHeight;
            public string contactSheetFile = string.Empty;
            public string contactSheetSha256 = string.Empty;
            public List<CaptureImageStub> images = new();
        }

        [Serializable]
        private sealed class CaptureImageStub
        {
            public string view = string.Empty;
            public string file = string.Empty;
            public string sha256 = string.Empty;
            public bool orthographic;
            public Rect viewportCrop;
            public string fixedSceneRendering = string.Empty;
        }
    }
}
