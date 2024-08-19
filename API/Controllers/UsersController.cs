using API.Data;
using API.Entities;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [Route("api/[controller]")] // /api/users
    [ApiController]
    public class UsersController(DataContext context) : ControllerBase
    {
        [HttpGet]
        public ActionResult<IEnumerable<AppUser>> GetUsers()
        {
            var users = context.Users.ToList();
            return users;
        }

        [HttpGet("{id}")] // /api/users/1
        public ActionResult<AppUser> GetUser(int id)
        {
            var user = context.Users.Find(id);

            if (user == null) return NotFound();

            return user;
        }
    }
}
