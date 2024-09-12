using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.SignalR;

namespace API.SignalR;

public class MessageHub(IMessageRepository messageRepo,
    IUserRepository userRepo, IMapper mapper) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var otherUser = httpContext?.Request.Query["user"];

        if (Context.User == null || string.IsNullOrEmpty(otherUser))
            throw new Exception("Cannot join group");

        var userName = Context.User.GetUserName();
        var groupName = GetGroupName(userName, otherUser);

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await AddToGroup(groupName);

        var messages = await messageRepo.GetMessageThread(userName, otherUser!);

        await Clients.Group(groupName).SendAsync("ReceivedMessageThread", messages);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await RemoveFromMessageGroup();
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendMessage(CreateMessageDto createMessageDto)
    {
        var username = Context.User?.GetUserName() ??
            throw new Exception("Could not get user");

        if (username == createMessageDto.RecipientUsername)
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

        messageRepo.AddMessage(message);

        if (await messageRepo.SaveAllAsync())
        {
            await Clients.Group(groupName).SendAsync("NewMessage",
                mapper.Map<MessageDto>(message));
        }

    }

    private async Task<bool> AddToGroup(string groupName)
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

        return await messageRepo.SaveAllAsync();
    }

    private async Task RemoveFromMessageGroup()
    {
        var connection = await messageRepo.GetConnection(Context.ConnectionId);

        if (connection != null)
        {
            messageRepo.RemoveConnection(connection);
            await messageRepo.SaveAllAsync();
        }
    }

    private string GetGroupName(string caller, string? other)
    {
        var stringCompare = string.CompareOrdinal(other, caller) < 0;
        return stringCompare ? $"{caller}-{other}" : $"{other}-{caller}";
    }
}
