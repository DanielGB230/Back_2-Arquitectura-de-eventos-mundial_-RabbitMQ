using Microsoft.AspNetCore.SignalR;

namespace Backend.Consumidor.Api.Hubs;

public class NotificationsHub : Hub
{
    // Los clientes pueden unirse a un "grupo" para recibir notificaciones de un partido específico.
    public async Task JoinMatchGroup(string matchId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, matchId);
        // Opcionalmente, se podría enviar un mensaje de confirmación al cliente.
        await Clients.Caller.SendAsync("JoinedGroup", matchId);
    }

    public async Task LeaveMatchGroup(string matchId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, matchId);
    }
}
