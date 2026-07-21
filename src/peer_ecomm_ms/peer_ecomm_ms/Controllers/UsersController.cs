using eComm_ms.DBA;
using eComm_ms.Models;
using eComm_ms.Services;
using Microsoft.AspNetCore.Mvc;

namespace eComm_ms.Controllers
{
    [ApiController]
    [Route("/users")]
    public class UsersController : ControllerBase
    {
        private readonly ECommDbContext _context;

        public UsersController(ECommDbContext context)
        {
            _context = context;
        }

        [HttpPost("add", Name = "adduser")]
        public ActionResult<Users> Post([FromBody] AuthenticationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Username and password are required");
            }

            if (_context.Users.Any(u => u.UserId.Equals(request.Username)))
            {
                return BadRequest("User already registered");
            }

            var uId = 0L;
            var maxId = _context.Users.Any() ? _context.Users.Max(u => u.Id) : 0;
            uId = maxId + 1;

            request.Password = AuthenticationService.HashPassword(request.Password);

            _context.Users.Add(new Users
            {
                Id = uId,
                RoleId = 2,
                UserId = request.Username,
                Password = request.Password
            });

            _context.SaveChanges();

            return CreatedAtAction("Post", new { id = uId });
        }

        [HttpPost("authenticate", Name = "authenticateuser")]
        public ActionResult<object> Authenticate([FromBody] AuthenticationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Username and password are required");
            }

            // Find user by username
            var user = _context.Users.FirstOrDefault(u => u.UserId == request.Username);
            if (user == null)
            {
                return Unauthorized(new { message = "Invalid username or password" });
            }

            // Verify the password
            bool isPasswordValid = AuthenticationService.VerifyPassword(request.Password, user.Password);
            if (!isPasswordValid)
            {
                return Unauthorized(new { message = "Invalid username or password" });
            }

            // Authentication successful
            return Ok(new
            {
                message = "Authentication successful",
                userId = user.Id,
                username = user.UserId,
                roleId = user.RoleId
            });
        }
    }
}
