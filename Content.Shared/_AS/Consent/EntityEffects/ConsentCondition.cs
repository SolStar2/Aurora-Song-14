using Content.Shared._Floof.Consent;
using Content.Shared.EntityConditions;
using Content.Shared.Mind;
using Robust.Shared.Prototypes;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;

namespace Content.Shared._AS.Consent.EntityEffects;

public sealed partial class ConsentEntityConditionSystem
    : EntityConditionSystem<MindContainerComponent, Consent>
{
    [Dependency] private SharedConsentSystem _consent = default!;
    [Dependency] private SharedMindSystem _mind = default!;

    protected override void Condition(Entity<MindContainerComponent> ent, ref EntityConditionEvent<Consent> args)
    {
        args.Result = false;

        if (!_mind.TryGetMind(ent.Owner, out _, out var mind))
            return;

        if (mind.UserId is not { } userId) // Aurora's Song - Use mind userId
            return;

        if (!_consent.TryGetConsent(userId, out var settings)) // Aurora's Song - Use mind userId
            return;

        foreach (var effect in args.Condition.EffectTypes)
        {
            if (_consent.HasConsent(settings, effect))
            {
                args.Result = true;
                return;
            }
        }
    }
}

public sealed partial class Consent : EntityConditionBase<Consent>
{
    [DataField(required: true)]
    public List<ProtoId<ConsentTogglePrototype>> EffectTypes;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        return string.Empty;
    }
}
