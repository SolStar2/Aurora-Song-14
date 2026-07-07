using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Client._AS.Consent;

public interface IConsentCardManager
{
    public void RaiseConsentCard(NetUserId playerId, EntProtoId cardId);
}
