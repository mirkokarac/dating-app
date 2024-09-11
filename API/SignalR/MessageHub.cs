using System.Text.RegularExpressions;
using API.Extensions;
using API.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR;

public class MessageHub(IMessageRepository messageRepo) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var otherUser = httpContext?.Request.Query["user"];

        if (Context.User == null || string.IsNullOrEmpty(otherUser))
            throw new Exception("Cannot join group");

        var userName = Context.User.GetUserName();
        var groupName = GetRoupName(userName, otherUser);

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var messages = await messageRepo.GetMessageThread(userName, otherUser!);

        await Clients.Group(groupName).SendAsync("ReceivedMessageThread", messages);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        return base.OnDisconnectedAsync(exception);
    }

    private string GetRoupName(string caller, string? other)
    {
        var stringCompare = string.CompareOrdinal(other, caller) < 0;
        return stringCompare ? $"{caller}-{other}" : $"{other}-{caller}";
    }
}
