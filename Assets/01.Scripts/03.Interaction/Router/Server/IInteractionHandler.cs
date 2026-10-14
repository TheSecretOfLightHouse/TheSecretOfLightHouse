using Lighthouse.Interaction.Requests.Rules;

namespace Lighthouse.Interaction.Router.Server
{
    // Implemented by the feature that owns an action (harvest, workbench, boat repair...).
    // Must finish within the call, without await/yield: there is no pending state, and the result is final.
    // On failure it must leave every state unchanged. Return Rejected rather than a code that reveals hidden state.
    public interface IInteractionHandler
    {
        InteractionResultCode Handle(in InteractionContext context);
    }
}
