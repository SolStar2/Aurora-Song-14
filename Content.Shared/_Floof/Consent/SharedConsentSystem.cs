// SPDX-FileCopyrightText: 2024 Pierson Arnold
// SPDX-FileCopyrightText: 2025 Mnemotechnican
// SPDX-FileCopyrightText: 2025 sleepyyapril
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Examine;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;


namespace Content.Shared._Floof.Consent;

public abstract partial class SharedConsentSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mindSystem = default!;
    [Dependency] private ExamineSystemShared _examineSystem = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private ILogManager _log = default!;

    private ISawmill _sawmill = default!;

    /// <summary>
    /// Stores consent settigns for all connected players, including guests.
    /// </summary>
    protected readonly Dictionary<NetUserId, PlayerConsentSettings> UserConsents = new();

    public override void Initialize()
    {
        _sawmill = _log.GetSawmill("consent");
        SubscribeLocalEvent<MindContainerComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    public bool TryGetConsent(NetUserId userId, [NotNullWhen(true)] out PlayerConsentSettings? consentSettings)
    {
        var exists = UserConsents.TryGetValue(userId, out consentSettings);
        return exists;
    }

    public virtual void SetConsent(NetUserId userId, PlayerConsentSettings? consentSettings)
    {
        if (consentSettings == null)
        {
            UserConsents.Remove(userId);
            return;
        }

        UserConsents[userId] = consentSettings;
    }

    private void OnGetExamineVerbs(Entity<MindContainerComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (_mindSystem.GetMind(ent, ent) is not { } mind
            || !TryComp<MindComponent>(mind, out var mindComponent)
            || mindComponent.UserId is not { } userId)
        {
            return;
        }

        var user = args.User;

        args.Verbs.Add(new()
        {
            Text = Loc.GetString("consent-examine-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () =>
            {
                var message = GetConsentText(userId);
                _examineSystem.SendExamineTooltip(user, ent, message, getVerbs: false, centerAtCursor: false);
            },
            Category = VerbCategory.Examine,
            CloseMenu = true,
        });
    }

    protected virtual FormattedMessage GetConsentText(NetUserId userId)
    {
        return new();
    }

    /// <summary>
    /// Checks if the given entity has the given consent toggle enabled or not.
    /// </summary>
    /// <param name="ent">The entity who consent settings will be checked for the relevant flag.</param>
    /// <param name="consentId">The protoId for the consent flag to check.</param>
    /// <returns>State of consent flag for the player's attached to the entity consent settings.
    /// Default state of the consent flag if consent settings not found.</returns>
    public bool HasConsent(Entity<MindContainerComponent?> ent, ProtoId<ConsentTogglePrototype> consentId)
    {
        if (!_prototypeManager.TryIndex(consentId, out var consentToggle))
            return false;

        if (!_mindSystem.TryGetMind(ent.Owner, out _, out var mind)
            || !_playerManager.TryGetSessionById(mind.UserId, out var session) // Aurora's Song - Make use PlayerManager
            || !UserConsents.TryGetValue(session.UserId, out var consentSettings) // Aurora's Song - Make use PlayerManager
            || !consentSettings.Toggles.TryGetValue(consentId, out var toggle))
            return consentToggle.DefaultValue;

        return toggle == "on";
    }

    // Aurora
    /// <summary>
    /// Variant of <see cref="HasConsent"/> that can accept a pre-generated <paramref name="settings"/>
    /// </summary>
    /// <param name="settings">The consent settings that will be checked for the relevant flag.</param>
    /// <param name="consentId">The protoId for the consent flag to check.</param>
    /// <returns></returns>
    /// <remarks>Prefer usage of this method in performance critical areas.</remarks>
    public bool HasConsent(PlayerConsentSettings? settings, ProtoId<ConsentTogglePrototype> consentId)
    {
        if (!_prototypeManager.TryIndex(consentId, out var consentToggle))
            return false;

        if (settings == null || !settings.Toggles.TryGetValue(consentId, out var toggle))
            return consentToggle.DefaultValue;

        return toggle == "on";
    }
}
