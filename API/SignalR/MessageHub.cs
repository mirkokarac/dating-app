using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR;

public class MessageHub(IMessageRepository messageRepo, IUserRepository userRepo,
    IMapper mapper, IHubContext<PresenceHub> presenceHub) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var otherUser = httpContext?.Request.Query["user"];

        if (Context.User == null || string.IsNullOrEmpty(otherUser))
            throw new Exception("Cannot join group");

        var groupName = GetGroupName(Context.User.GetUserName(), otherUser);

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        var group = await AddToGroup(groupName);

        await Clients.Group(groupName).SendAsync("UpdatedGroup", group);

        var messages = await messageRepo.GetMessageThread(Context.User.GetUserName(), otherUser!);

        await Clients.Caller.SendAsync("ReceiveMessageThread", messages);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var group = await RemoveFromMessageGroup();
        await Clients.Group(group.Name).SendAsync("UpdatedGroup", group);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(CreateMessageDto createMessageDto)
    {
        var username = Context.User?.GetUserName() ??
            throw new Exception("Could not get user");

        if (username == createMessageDto.RecipientUsername.ToLower())
        {
            throw new HubException("You cannot message yourself");
        }

        var sender = await userRepo.GetUserByUsernameAsync(username);
        var recipient = await userRepo.GetUserByUsernameAsync(createMessageDto.RecipientUsername);

        if (recipient == null || sender == null || sender.UserName == null ||
            recipient.UserName == null)
            throw new HubException("Cannot send message at this time");

        var message = new Message
        {
            Sender = sender,
            Recipient = recipient,
            SenderUsername = sender.UserName,
            RecipientUsername = recipient.UserName,
            Content = createMessageDto.Content
        };

        var groupName = GetGroupName(sender.UserName, recipient.UserName);
        var group = await messageRepo.GetMessageGroup(groupName);

        if (group != null && group.Connections.Any(x => x.Username == recipient.UserName))
        {
            message.DateRead = DateTime.UtcNow;
        }
        else
        {
            var connections = await PresenceTracker.GetConnectionsForUser(recipient.UserName);
            if (connections != null && connections?.Count != null)
            {
                await presenceHub.Clients.Clients(connections)
                    .SendAsync("NewMessageReceived", new
                    {
                        username = sender.UserName,
                        knownAs = sender.KnownAs
                    });
            }
        }

        messageRepo.AddMessage(message);

        if (await messageRepo.SaveAllAsync())
        {
            await Clients.Group(groupName).SendAsync("NewMessage",
                mapper.Map<MessageDto>(message));
        }

    }

    private async Task<Group> AddToGroup(string groupName)
    {
        var username = Context.User?.GetUserName() ??
                    throw new Exception("Could not get username");
        var group = await messageRepo.GetMessageGroup(username);
        var connection = new Connection
        {
            ConnectionId = Context.ConnectionId,
            Username = username
        };

        if (group == null)
        {
            group = new Group { Name = groupName };
            messageRepo.AddGroup(group);
        }

        group.Connections.Add(connection);

        if (await messageRepo.SaveAllAsync()) return group;

        throw new HubException("Failed to join group");
    }

    private async Task<Group> RemoveFromMessageGroup()
    {
        var group = await messageRepo.GetGroupForConnection(Context.ConnectionId);
        var connection = group?.Connections
            .FirstOrDefault(x => x.ConnectionId == Context.ConnectionId);

        if (connection != null && group != null)
        {
            messageRepo.RemoveConnection(connection);
            if (await messageRepo.SaveAllAsync()) return group;
        }

        throw new HubException("Failed to remove from group");
    }

    private string GetGroupName(string caller, string? other)
    {
        var stringCompare = string.CompareOrdinal(other, caller) < 0;
        return stringCompare ? $"{caller}-{other}" : $"{other}-{caller}";
    }
}
