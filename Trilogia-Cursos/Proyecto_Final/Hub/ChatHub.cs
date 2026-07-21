using Microsoft.AspNetCore.SignalR;

namespace Proyecto_Final.Hubs
{
    public class ChatHub : Hub
    {
        public async Task JoinConversation(string conversationId)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"chat-{conversationId}"
            );
        }

        public async Task LeaveConversation(string conversationId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                $"chat-{conversationId}"
            );
        }
    }
}