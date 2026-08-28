using System;
using System.Collections.Generic;
using System.Linq;
using Genix.Areas;
using Genix.Assets;
using Genix.Core;
using Genix.Editor.Assets;
using Genix.Placement;
using Genix.Sampling;
using Genix.Sampling.ClusterSampling;
using Genix.Sampling.GridSampling;
using Genix.Sampling.PoissonSampling;
using Genix.Semantics;
using Genix.Styles;
using UnityEditor;
using UnityEngine;

namespace Genix.Editor.Infrastructure
{
    internal static partial class StarterContentBuilder
    {
        private static void BuildDesk(GameObject root, StarterMaterials materials, SemanticTag desktop)
        {
            GameObject top = CreatePrimitive(
                root.transform, PrimitiveType.Cube, "Desktop", new Vector3(0f, 0.76f, 0f),
                new Vector3(1.6f, 0.08f, 0.8f), materials.Wood);
            top.AddComponent<PlacementSurfaceDescriptor>().SetSurfaceTags(new[] { desktop });

            foreach (float x in new[] { -0.68f, 0.68f })
            foreach (float z in new[] { -0.28f, 0.28f })
            {
                CreatePrimitive(
                    root.transform, PrimitiveType.Cube, "Leg", new Vector3(x, 0.37f, z),
                    new Vector3(0.08f, 0.74f, 0.08f), materials.Dark);
            }
        }

        private static void BuildMonitor(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Base", new Vector3(0f, 0.025f, 0f), new Vector3(0.3f, 0.05f, 0.18f), materials.Dark);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Stand", new Vector3(0f, 0.18f, 0f), new Vector3(0.06f, 0.3f, 0.06f), materials.Dark);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Screen", new Vector3(0f, 0.38f, 0f), new Vector3(0.62f, 0.38f, 0.07f), materials.Dark);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Display", new Vector3(0f, 0.38f, 0.038f), new Vector3(0.55f, 0.31f, 0.008f), materials.Blue, false);
        }

        private static void BuildKeyboard(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Keyboard", new Vector3(0f, 0.025f, 0f), new Vector3(0.48f, 0.05f, 0.18f), materials.Light);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Keybed", new Vector3(0f, 0.054f, 0f), new Vector3(0.42f, 0.012f, 0.13f), materials.Dark, false);
        }

        private static void BuildMouse(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Sphere, "Mouse", new Vector3(0f, 0.035f, 0f), new Vector3(0.09f, 0.07f, 0.14f), materials.Dark);
        }

        private static void BuildCoffeeMug(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cylinder, "Cup", new Vector3(0f, 0.065f, 0f), new Vector3(0.09f, 0.065f, 0.09f), materials.Yellow);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Handle", new Vector3(0.065f, 0.07f, 0f), new Vector3(0.05f, 0.055f, 0.025f), materials.Yellow, false);
        }

        private static void BuildChair(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Seat", new Vector3(0f, 0.46f, 0f), new Vector3(0.52f, 0.08f, 0.52f), materials.Blue);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Back", new Vector3(0f, 0.76f, -0.23f), new Vector3(0.52f, 0.52f, 0.08f), materials.Blue);
            foreach (float x in new[] { -0.2f, 0.2f })
            foreach (float z in new[] { -0.2f, 0.2f })
            {
                CreatePrimitive(root.transform, PrimitiveType.Cube, "Leg", new Vector3(x, 0.22f, z), new Vector3(0.06f, 0.44f, 0.06f), materials.Dark);
            }
        }

        private static void BuildCargoBox(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Crate", new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), materials.Orange);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Band", new Vector3(0f, 0.3f, 0.306f), new Vector3(0.08f, 0.5f, 0.012f), materials.Dark, false);
        }

        private static void BuildWarningSign(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Plate", new Vector3(0f, 0f, 0.025f), new Vector3(0.62f, 0.42f, 0.05f), materials.Red);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Inset", new Vector3(0f, 0f, 0.054f), new Vector3(0.48f, 0.28f, 0.008f), materials.Yellow, false);
        }

        private static void BuildCeilingLight(GameObject root, StarterMaterials materials)
        {
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Frame", new Vector3(0f, -0.04f, 0f), new Vector3(0.82f, 0.08f, 0.38f), materials.Dark);
            CreatePrimitive(root.transform, PrimitiveType.Cube, "Panel", new Vector3(0f, -0.085f, 0f), new Vector3(0.7f, 0.025f, 0.28f), materials.Light, false);
        }

        private static GameObject CreatePrimitive(
            Transform parent,
            PrimitiveType type,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool includeCollider = true)
        {
            GameObject child = GameObject.CreatePrimitive(type);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = localScale;

            if (child.TryGetComponent(out Renderer renderer))
                renderer.sharedMaterial = material;

            if (!includeCollider && child.TryGetComponent(out Collider collider))
                UnityEngine.Object.DestroyImmediate(collider);

            return child;
        }

        private sealed class StarterTaxonomy
        {
            public SemanticTag Indoor;
            public SemanticTag Outdoor;
            public SemanticTag Floor;
            public SemanticTag Wall;
            public SemanticTag Ceiling;
            public SemanticTag Desktop;
            public SemanticTag Shelf;
            public SemanticTag Terrain;
            public SemanticTag Path;
            public SemanticTag Water;
            public SemanticTag Prop;
            public SemanticTag Furniture;
            public SemanticTag Decoration;
            public SemanticTag Lighting;
            public SemanticTag Signage;
            public SemanticTag Structure;
            public SemanticTag Vegetation;
            public SemanticTag Display;
            public SemanticTag Utility;
            public SemanticTag FunctionPath;
            public SemanticTag RestArea;
            public SemanticTag Natural;
            public SemanticTag Industrial;
            public SemanticTag Minimal;
            public SemanticTag Urban;
            public SemanticTag SciFi;
            public SemanticTag Fantasy;
            public SemanticTag Tiny;
            public SemanticTag Small;
            public SemanticTag Medium;
            public SemanticTag Large;
            public SemanticTag Huge;
        }

        private sealed class StarterMaterials
        {
            public Material Wall;
            public Material Floor;
            public Material Wood;
            public Material Dark;
            public Material Light;
            public Material Blue;
            public Material Yellow;
            public Material Orange;
            public Material Red;
        }

        private sealed class StarterPrefabs
        {
            public GameObject Desk;
            public GameObject Monitor;
            public GameObject Keyboard;
            public GameObject Mouse;
            public GameObject CoffeeMug;
            public GameObject Chair;
            public GameObject CargoBox;
            public GameObject WarningSign;
            public GameObject CeilingLight;
        }

        private sealed class StarterDefinitions
        {
            public AssetDefinition Desk;
            public AssetDefinition Monitor;
            public AssetDefinition Keyboard;
            public AssetDefinition Mouse;
            public AssetDefinition CoffeeMug;
            public AssetDefinition Chair;
            public AssetDefinition CargoBox;
            public AssetDefinition WarningSign;
            public AssetDefinition CeilingLight;

            public IEnumerable<AssetDefinition> All
            {
                get
                {
                    yield return Desk;
                    yield return Monitor;
                    yield return Keyboard;
                    yield return Mouse;
                    yield return CoffeeMug;
                    yield return Chair;
                    yield return CargoBox;
                    yield return WarningSign;
                    yield return CeilingLight;
                }
            }
        }
    }
}

