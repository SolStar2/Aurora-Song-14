using System.Diagnostics.CodeAnalysis;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Chemistry.EntitySystems;

public abstract partial class SharedSolutionContainerSystem
{
    /// <summary>
    /// Tries to get first available solution from an EntityPrototype, this is intended to be used for the guidebook, and various UI elements.
    /// </summary>
    public bool TryGetAnySolution(
        EntityPrototype entProto,
        [NotNullWhen(true)] out SolutionComponent? solution)
    {
        solution = null;

        if (entProto.TryGetComponent(out solution, EntityManager.ComponentFactory))
            return true;

        if (!TryGetSolutionFill(entProto, out var solutionFill))
            return false;

        if (solutionFill.FirstOrNull() is not { } firstSolution)
            return false;

        var solutionProto = PrototypeManager.Index(firstSolution);

        return solutionProto.TryGetComponent(out solution, EntityManager.ComponentFactory);

    }
}
