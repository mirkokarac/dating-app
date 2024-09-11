using System.Text.RegularExpressions;
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

        var messages = await messageRepo.GetMessageThread(userName, otherUser!);

        await Clients.Group(groupName).SendAsync("ReceivedMessageThread", messages);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        return base.OnDisconnectedAsync(exception);
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

        messageRepo.AddMessage(message);

        if (await messageRepo.SaveAllAsync())
        {
            var group = GetGroupName(sender.UserName, recipient.UserName);
            await Clients.Group(group).SendAsync("NewMessage",
                mapper.Map<MessageDto>(message));
        }

    }

    private string GetGroupName(string caller, string? other)
    {
        var stringCompare = string.CompareOrdinal(other, caller) < 0;
        return stringCompare ? $"{caller}-{other}" : $"{other}-{caller}";
    }
}
