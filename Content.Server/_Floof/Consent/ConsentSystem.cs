// SPDX-FileCopyrightText: 2024 Pierson Arnold
// SPDX-FileCopyrightText: 2025 Mnemotechnican
// SPDX-FileCopyrightText: 2025 sleepyyapril
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared._Floof.Consent;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid; // Aurora's Song
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Utility;


namespace Content.Server._Floof.Consent;

public sealed partial class ConsentSystem : SharedConsentSystem
{
    // [Dependency] private ConsentSystem _consent = default!; // Aurora's Song
    [Dependency] private IPlayerManager _playerManager = default!;
    // [Dependency] private StationSpawningSystem _stationSpawning = default!; // Aurora's Song

    protected override FormattedMessage GetConsentText(NetUserId userId)
    {
        TryGetConsent(userId, out var consent);
        var text = consent?.Freetext ?? string.Empty;

        if (text == string.Empty)
            text = Loc.GetString("consent-examine-not-set");

        text += GetCharacterConsent(userId); // DEN: per-character consent.

        var message = new FormattedMessage();
        message.AddText(text);

        return message;
    }

    private string GetCharacterConsent(NetUserId userId)
    {
        var result = string.Empty;
        var hasSession = _playerManager.TryGetSessionById(userId, out var session);

        if (hasSession && session != null
            && TryComp<HumanoidProfileComponent>(session.AttachedEntity, out var profile) // Aurora's Song - Nubody
            && TryComp<MetaDataComponent>(session.AttachedEntity, out var metadata) // Aurora's Song - Remove last profile
            && !string.IsNullOrWhiteSpace(profile.CharacterConsent))
        {
            result += $"\n\n- [{metadata.EntityName}] -"; // Aurora's Song - Remove last profile
            result += $"\n{profile.CharacterConsent}";
        }

        return result;
    }

    public override void SetConsent(NetUserId userId, PlayerConsentSettings? consentSettings)
    {
        base.SetConsent(userId, consentSettings);

        if (consentSettings == null)
        {
            UserConsents.Remove(userId);
            return;
        }

        UserConsents[userId] = consentSettings;
    }
}
