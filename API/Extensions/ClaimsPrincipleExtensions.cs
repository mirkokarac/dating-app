using System.Security.Claims;

namespace API.Extensions;

public static class ClaimsPrincipleExtensions
{
    public static string GetUserName(this ClaimsPrincipal user)
    {
        var userName = user.FindFirstValue(ClaimTypes.Name)
            ?? throw new Exception("Cannot get username from token");
        return userName;
    }
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var userId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new Exception("Cannot get user id from token"));
        return userId;
    }
}
