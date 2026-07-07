using Content.Shared._AS.Consent;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Client._AS.Consent;

public sealed partial class ConsentCardManager : IConsentCardManager
{
    [Dependency] private IEntityNetworkManager _netManager = default!;

    public void RaiseConsentCard(NetUserId playerId, EntProtoId cardId)
    {
        var msg = new ConsentCardRaisedEvent(playerId, cardId);
        _netManager.SendSystemNetworkMessage(msg);
    }
}
