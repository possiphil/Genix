using System.Linq;
using Genix.Layouts;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.Editor.Layouts
{
    /// <summary>Manages the non-persistent Scene view preview for one saved layout.</summary>
    internal static class LayoutPreviewService
    {
        private const string RootName = "Genix Layout Preview";
        private static readonly string[] LegacyRootNames = { "Genix Layout Preview A", "Genix Layout Preview B" };
        private static readonly Color PreviewColor = new(0f, 0.75f, 1f, 0.28f);
        private static SavedLayout _currentLayout;

        public static bool Show(SavedLayout layout, out string error)
        {
            if (!layout)
            {
                error = "No layout is selected.";
                return false;
            }

            if (!layout.Prefab)
            {
                error = $"Layout '{layout.DisplayName}' has no saved prefab.";
                return false;
            }

            Object[] previousSelection = Selection.objects;
            Clear();
            GameObject root = new(RootName) { hideFlags = HideFlags.DontSave };
            GameObject preview = LayoutApplyService.Instantiate(layout.Prefab);
            preview.name = layout.DisplayName;
            preview.transform.SetParent(root.transform, false);
            preview.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            preview.transform.localScale = Vector3.one;
            Prepare(preview, PreviewColor);
            SetHideFlags(root, HideFlags.DontSave);
            _currentLayout = layout;
            Selection.objects = previousSelection.Where(item => item).ToArray();
            SceneView.RepaintAll();
            error = string.Empty;
            return true;
        }

        public static bool IsShowing(SavedLayout layout) =>
            layout && _currentLayout == layout && GameObject.Find(RootName);

        public static void Clear()
        {
            GameObject root = GameObject.Find(RootName);

            if (root)
                Object.DestroyImmediate(root);

            foreach (string legacyRootName in LegacyRootNames)
            {
                GameObject legacyRoot = GameObject.Find(legacyRootName);
                if (legacyRoot)
                    Object.DestroyImmediate(legacyRoot);
            }

            _currentLayout = null;
            SceneView.RepaintAll();
        }

        private static void Prepare(GameObject preview, Color color)
        {
            foreach (Collider collider in preview.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            foreach (Rigidbody rigidbody in preview.GetComponentsInChildren<Rigidbody>(true))
                rigidbody.isKinematic = true;

            Material previewMaterial = CreateMaterial(color);

            if (!previewMaterial)
                return;

            foreach (Renderer renderer in preview.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;

                for (int i = 0; i < materials.Length; i++)
                    materials[i] = previewMaterial;

                renderer.sharedMaterials = materials;
            }
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Standard") ??
                            Shader.Find("Unlit/Color") ??
                            Shader.Find("Hidden/Internal-Colored");

            if (!shader)
                return null;

            Material material = new(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                color = color
            };

            if (shader.name == "Standard")
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            return material;
        }

        private static void SetHideFlags(GameObject root, HideFlags hideFlags)
        {
            root.hideFlags = hideFlags;

            foreach (Transform child in root.transform)
                SetHideFlags(child.gameObject, hideFlags);
        }
    }
}
