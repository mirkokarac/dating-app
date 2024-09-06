using API.DTOs;
using API.Entities;
using API.Extensions;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

public class MessagesController(IMessageRepository messageRepo,
    IUserRepository userRepo, IMapper mapper) : BaseApiController
{
    [HttpPost]
    public async Task<ActionResult<MessageDto>> CreateMessage(CreateMessageDto createMessageDto)
    {
        var username = User.GetUserName();

        if (username == createMessageDto.RecipientUsername)
        {
            return BadRequest("You cannot message yourself");
        }

        var sender = await userRepo.GetUserByUsernameAsync(username);
        var recipient = await userRepo.GetUserByUsernameAsync(createMessageDto.RecipientUsername);

        if (recipient == null || sender == null) return BadRequest("Cannot send message at this time");

        var message = new Message
        {
            Sender = sender,
            Recipient = recipient,
            SendUsername = sender.UserName,
            RecipientUsername = recipient.UserName,
            Content = createMessageDto.Content
        };

        messageRepo.AddMessage(message);

        if (await messageRepo.SaveAllAsync()) return Ok(mapper.Map<MessageDto>(message));

        return BadRequest("Failed to save message");
    }
}
