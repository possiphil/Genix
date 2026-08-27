using System.Collections.Generic;
using Genix.Diagnostics;
using Genix.Extensions;
using Genix.Sampling;
using Genix.Styles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Genix.Editor.Diagnostics
{
    [InitializeOnLoad]
    public static partial class DiagnosticsGizmos
    {
        private const float CandidateMarkerSize = 0.06f;

        private const float SeedWorldRadius = 0.045f;
        private const float SeedYOffset = 0.03f;
        private const int SeedCircleSegments = 8;

        private const float ClusterYOffset = 0.02f;
        private const float ClusterWireYOffset = ClusterYOffset + 0.003f;
        private const float ClusterWireWidth = 3f;
        private const int ClusterCircleSegments = 64;

        private const float GridYOffset = 0.02f;
        private const float GridWireYOffset = GridYOffset + 0.003f;
        private const float GridCellPadding = 0.02f;
        private const float GridWireWidth = 2f;

        private static Mesh _clusterMesh;
        private static string _clusterMeshKey;
        private static Material _clusterMaterial;

        private static Mesh _candidateSeedMesh;
        private static string _candidateSeedMeshKey;
        private static Material _candidateSeedMaterial;

        private static Mesh _gridMesh;
        private static string _gridMeshKey;
        private static Material _gridMaterial;

        private static readonly Color GridWireColor = new(0f, 0f, 0f, 1f);
        private static readonly Color GridCellColor = new(0.25f, 0.45f, 1f, 0.3f);
        private static readonly Color BoundsColor = new(0.2f, 0.45f, 1f, 1f);
        private static readonly Color SeedColor = new(0.3f, 0.3f, 0.3f, 1f);
        private static readonly Color AcceptedColor = new(0.25f, 1f, 0.25f, 0.9f);
        private static readonly Color RejectedColor = new(1f, 0.25f, 0.25f, 0.9f);
        private static readonly Color ClusterFillColor = new(1f, 0.75f, 0.05f, 0.3f);
        private static readonly Color ClusterWireColor = new(0f, 0f, 0f, 1f);

        private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int Cull = Shader.PropertyToID("_Cull");
        private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
        private static readonly int ZTest = Shader.PropertyToID("_ZTest");

        static DiagnosticsGizmos()
        {
            SceneView.duringSceneGui += DrawDiagnostics;
        }

        private static void DrawDiagnostics(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            if (!TryGetSceneViewData(out SceneViewData data, out SceneViewOptions options))
                return;

            Handles.zTest = CompareFunction.LessEqual;

            if (options.ShowBounds)
                DrawTargetBounds(data);

            if (options.ShowGrid)
                DrawGrid(data);

            if (options.ShowCandidateSeeds)
                DrawCandidateSeeds(data);

            if (options.ShowAccepted)
                DrawCandidates(data, true);

            if (options.ShowRejected)
                DrawCandidates(data, false);

            if (options.ShowClusters)
                DrawClusters(data);
        }

        private static bool TryGetSceneViewData(out SceneViewData data, out SceneViewOptions options)
        {
            DiagnosticsReport report = DiagnosticsPreview.CurrentReport;

            if (report)
            {
                data = CreateReportData(report);
                options = CreateReportOptions();
                return true;
            }

            GenerationDiagnostics diagnostics = DiagnosticsStore.LastDiagnostics;

            if (diagnostics != null)
            {
                data = CreateLiveData(diagnostics);
                options = CreateLiveOptions();
                return true;
            }

            data = default;
            options = default;
            return false;
        }

        private static SceneViewData CreateLiveData(GenerationDiagnostics diagnostics)
        {
            List<CandidateView> candidates = new(diagnostics.Candidates.Count);

            foreach (CandidateDiagnostic candidate in diagnostics.Candidates)
                candidates.Add(new CandidateView(candidate.Position, candidate.Rotation, candidate.Bounds, candidate.Accepted));

            return new SceneViewData($"Live_{diagnostics.RunId}", diagnostics.TargetBounds, diagnostics.SamplingAlgorithm, diagnostics.StyleSettings,
                diagnostics.Sampler.CandidateSeeds, diagnostics.Sampler.RawSamplePositions, diagnostics.Sampler.ClusterCenters, candidates);
        }

        private static SceneViewData CreateReportData(DiagnosticsReport report)
        {
            List<CandidateView> candidates = new(report.CandidateDetails.Count);

            foreach (DiagnosticsReport.CandidateEntry candidate in report.CandidateDetails)
                candidates.Add(new CandidateView(candidate.Position, candidate.Rotation, candidate.Bounds, candidate.Accepted));

            return new SceneViewData($"Report_{report.GetLocalObjectId()}_{report.RunId}_{report.Mode}", report.TargetBounds, report.SamplingAlgorithm,
                report.StyleSettings, report.CandidateSeeds, report.RawSamplePositions, report.ClusterCenters, candidates);
        }

        private static SceneViewOptions CreateLiveOptions()
        {
            return new SceneViewOptions(DiagnosticsStore.ShowTargetBounds, DiagnosticsStore.ShowGrid, DiagnosticsStore.ShowClusters,
                DiagnosticsStore.ShowCandidateSeeds, DiagnosticsStore.ShowAcceptedCandidates, DiagnosticsStore.ShowRejectedCandidates);
        }

        private static SceneViewOptions CreateReportOptions()
        {
            return new SceneViewOptions(DiagnosticsPreview.ShowBounds, DiagnosticsPreview.ShowGrid, DiagnosticsPreview.ShowClusters,
                DiagnosticsPreview.ShowCandidateSeeds, DiagnosticsPreview.ShowAccepted, DiagnosticsPreview.ShowRejected);
        }

        private static void DrawTargetBounds(SceneViewData data)
        {
            Handles.color = BoundsColor;
            Handles.DrawWireCube(data.TargetBounds.center, data.TargetBounds.size);
        }

        private static void DrawCandidates(SceneViewData data, bool accepted)
        {
            Handles.color = accepted ? AcceptedColor : RejectedColor;

            foreach (CandidateView candidate in data.Candidates)
            {
                if (candidate.Accepted != accepted)
                    continue;

                DrawCandidate(candidate, CandidateMarkerSize);
            }
        }

        private static void DrawCandidate(CandidateView candidate, float markerSize)
        {
            Bounds bounds = candidate.Bounds;

            Matrix4x4 previousMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(bounds.center, candidate.Rotation, Vector3.one);
            Handles.DrawWireCube(Vector3.zero, bounds.size);
            Handles.matrix = previousMatrix;

            DrawPoint(candidate.Position, markerSize);
        }

        private static void DrawPoint(Vector3 position, float sizeMultiplier)
        {
            float size = HandleUtility.GetHandleSize(position) * sizeMultiplier;
            Handles.SphereHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);
        }

        private static Material CreatePreviewMaterial()
        {
            Material material = new(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };

            material.SetInt(SrcBlend, (int)BlendMode.SrcAlpha);
            material.SetInt(DstBlend, (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt(Cull, (int)CullMode.Off);
            material.SetInt(ZWrite, 0);
            material.SetInt(ZTest, (int)CompareFunction.LessEqual);

            return material;
        }

        private readonly struct SceneViewData
        {
            public string Key { get; }
            public Bounds TargetBounds { get; }
            public SamplingAlgorithm SamplingAlgorithm { get; }
            public StyleSettings StyleSettings { get; }
            public IReadOnlyList<Vector3> CandidateSeeds { get; }
            public IReadOnlyList<Vector3> RawSamplePositions { get; }
            public IReadOnlyList<Vector3> ClusterCenters { get; }
            public IReadOnlyList<CandidateView> Candidates { get; }

            public SceneViewData(string key, Bounds targetBounds, SamplingAlgorithm samplingAlgorithm, StyleSettings styleSettings, IReadOnlyList<Vector3> candidateSeeds,
                IReadOnlyList<Vector3> rawSamplePositions, IReadOnlyList<Vector3> clusterCenters, IReadOnlyList<CandidateView> candidates)
            {
                Key = key;
                TargetBounds = targetBounds;
                SamplingAlgorithm = samplingAlgorithm;
                StyleSettings = styleSettings;
                CandidateSeeds = candidateSeeds;
                RawSamplePositions = rawSamplePositions;
                ClusterCenters = clusterCenters;
                Candidates = candidates;
            }
        }

        private readonly struct CandidateView
        {
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Bounds Bounds { get; }
            public bool Accepted { get; }

            public CandidateView(Vector3 position, Quaternion rotation, Bounds bounds, bool accepted)
            {
                Position = position;
                Rotation = rotation;
                Bounds = bounds;
                Accepted = accepted;
            }
        }

        private readonly struct SceneViewOptions
        {
            public bool ShowBounds { get; }
            public bool ShowGrid { get; }
            public bool ShowClusters { get; }
            public bool ShowCandidateSeeds { get; }
            public bool ShowAccepted { get; }
            public bool ShowRejected { get; }

            public SceneViewOptions(bool showBounds, bool showGrid, bool showClusters, bool showCandidateSeeds, bool showAccepted, bool showRejected)
            {
                ShowBounds = showBounds;
                ShowGrid = showGrid;
                ShowClusters = showClusters;
                ShowCandidateSeeds = showCandidateSeeds;
                ShowAccepted = showAccepted;
                ShowRejected = showRejected;
            }
        }
    }
}
