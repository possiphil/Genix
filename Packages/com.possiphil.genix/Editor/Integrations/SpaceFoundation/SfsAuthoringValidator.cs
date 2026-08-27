using System.Collections.Generic;
using SpaceFoundationSystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Genix.SpaceFoundation.Editor
{
    /// <summary>Reports scene conditions that would make SFS computation invalid or ambiguous.</summary>
    internal static class SfsAuthoringValidator
    {
        public static List<SfsAuthoringValidationMessage> ValidateScene(
            SpaceFoundationSystem.SpaceFoundation selectedFoundation)
        {
            List<SfsAuthoringValidationMessage> messages = new();
            SpaceFoundationSystem.SpaceFoundation[] foundations =
                Object.FindObjectsByType<SpaceFoundationSystem.SpaceFoundation>(FindObjectsInactive.Include);
            Anchor[] anchors = Object.FindObjectsByType<Anchor>(FindObjectsInactive.Include);
            Delimiter[] delimiters = Object.FindObjectsByType<Delimiter>(FindObjectsInactive.Include);

            if (foundations.Length == 0)
                messages.Add(Error("No Space Foundation exists in the loaded scene."));
            else if (foundations.Length > 1)
                messages.Add(Error($"The loaded scene contains {foundations.Length} Space Foundations. Select one explicitly and compute them in isolated scenes."));

            if (!selectedFoundation && foundations.Length == 1)
                selectedFoundation = foundations[0];

            int delimiterLayer = LayerMask.NameToLayer(SfsAuthoringSceneBuilder.DelimiterLayerName);
            if (delimiterLayer < 0)
            {
                messages.Add(Error($"The '{SfsAuthoringSceneBuilder.DelimiterLayerName}' layer is missing."));
            }
            else if (selectedFoundation &&
                     (selectedFoundation.delimitingLayerMask.value & (1 << delimiterLayer)) == 0)
            {
                messages.Add(Error("The selected Space Foundation does not include the SFS Delimiter layer in its delimiting mask."));
            }

            if (selectedFoundation && (!IsFinitePositive(selectedFoundation.voxelSize)))
                messages.Add(Error("The selected Space Foundation has an invalid voxel size."));

            if (anchors.Length == 0)
                messages.Add(Warning("No SFS Anchor exists, so Compute cannot create a location."));
            if (delimiters.Length == 0)
                messages.Add(Warning("No SFS Delimiter exists, so an anchor can expand until its range limit."));

            foreach (Delimiter delimiter in delimiters)
            {
                if (!delimiter.TryGetComponent(out Collider collider))
                {
                    messages.Add(Error($"Delimiter '{delimiter.name}' has no Collider."));
                    continue;
                }

                if (!collider.enabled || !delimiter.gameObject.activeInHierarchy)
                    messages.Add(Warning($"Delimiter '{delimiter.name}' is disabled and will not bound a computed location."));
                if (delimiterLayer >= 0 && delimiter.gameObject.layer != delimiterLayer)
                    messages.Add(Error($"Delimiter '{delimiter.name}' is not on the '{SfsAuthoringSceneBuilder.DelimiterLayerName}' layer."));
                if (collider.isTrigger && !Physics.queriesHitTriggers)
                    messages.Add(Error($"Delimiter '{delimiter.name}' is a trigger while Physics.queriesHitTriggers is disabled."));
            }

            foreach (Anchor anchor in anchors)
            {
                if (!anchor.gameObject.activeInHierarchy)
                    messages.Add(Warning($"Anchor '{anchor.name}' is disabled and will not seed a location."));
                if (!anchor.correspondingSpaceFoundation)
                    messages.Add(Error($"Anchor '{anchor.name}' has no corresponding Space Foundation."));
                else if (selectedFoundation && anchor.correspondingSpaceFoundation != selectedFoundation)
                    messages.Add(Warning($"Anchor '{anchor.name}' references a different Space Foundation than the selected one."));
                else if (anchor.transform.IsChildOf(anchor.correspondingSpaceFoundation.transform))
                    messages.Add(Error($"Anchor '{anchor.name}' is parented below its Space Foundation and would be deleted by SFS Reset."));
                if (!IsFinitePositive(anchor.GetMaxDistance()))
                    messages.Add(Error($"Anchor '{anchor.name}' has an invalid maximum range."));

                foreach (Delimiter delimiter in delimiters)
                {
                    Collider collider = delimiter.GetComponent<Collider>();
                    if (collider && collider.ClosestPoint(anchor.transform.position) == anchor.transform.position)
                    {
                        messages.Add(Error($"Anchor '{anchor.name}' is inside delimiter '{delimiter.name}'."));
                        break;
                    }
                }
            }


            if (selectedFoundation)
            {
                foreach (Delimiter delimiter in delimiters)
                {
                    if (delimiter.transform.IsChildOf(selectedFoundation.transform))
                        messages.Add(Error($"Delimiter '{delimiter.name}' is parented below its Space Foundation and would be deleted by SFS Reset."));
                }
            }

            if (messages.Count == 0)
                messages.Add(new SfsAuthoringValidationMessage("Scene setup is ready for SFS Compute.", MessageType.Info));

            return messages;
        }

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static SfsAuthoringValidationMessage Error(string message) =>
            new(message, MessageType.Error);

        private static SfsAuthoringValidationMessage Warning(string message) =>
            new(message, MessageType.Warning);
    }
}
