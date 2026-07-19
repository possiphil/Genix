using System;
using UnityEngine;

namespace Genix.Core
{
    public static class GenerationContextFactory
    {
        public static GenerationContext Create(
            GenerationRequest request,
            Transform generatedParent)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (!generatedParent)
                throw new ArgumentException("Generation could not start because the generated object parent could not be created.", nameof(generatedParent));

            if (request.AreaSource == null)
                throw new ArgumentException("Generation could not start because no target area/location is selected.", nameof(request));

            if (!request.AreaSource.TryBuildArea(request.AreaBuildSettings, out Genix.Areas.PlacementArea area, out string error))
                throw new ArgumentException(error, nameof(request));

            return new GenerationContext(request, generatedParent, area);
        }
    }
}
