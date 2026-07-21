using eComm_ms.Controllers;
using eComm_ms.Models;
using eComm_ms.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc;

namespace eComm_ms.Tests.Controllers
{
    public class UsersControllerTests
    {
        // ---------- Post (register) ----------

        [Fact]
        public void Post_ReturnsBadRequest_WhenRequestIsNull()
        {
            using var context = DbContextFactory.Create();
            var controller = new UsersController(context);

            var result = controller.Post(null!);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Theory]
        [InlineData(null, "password")]
        [InlineData("", "password")]
        [InlineData("username", null)]
        [InlineData("username", "")]
        public void Post_ReturnsBadRequest_WhenUsernameOrPasswordMissing(string? username, string? password)
        {
            using var context = DbContextFactory.Create();
            var controller = new UsersController(context);

            var result = controller.Post(new AuthenticationRequest { Username = username, Password = password });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public void Post_ReturnsBadRequest_WhenUsernameAlreadyRegistered()
        {
            using var context = DbContextFactory.Create();
            context.Users.Add(new Users { Id = 1, UserId = "satish", RoleId = 2, Password = "hash" });
            context.SaveChanges();
            var controller = new UsersController(context);

            var result = controller.Post(new AuthenticationRequest { Username = "satish", Password = "newpass" });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public void Post_CreatesUser_WithClientRoleAndHashedPassword()
        {
            using var context = DbContextFactory.Create();
            var controller = new UsersController(context);

            var result = controller.Post(new AuthenticationRequest { Username = "newuser", Password = "PlainTextPassword" });

            Assert.IsType<CreatedAtActionResult>(result.Result);
            var saved = context.Users.Single(u => u.UserId == "newuser");
            Assert.Equal(2, saved.RoleId);
            Assert.NotEqual("PlainTextPassword", saved.Password);
            Assert.Contains(':', saved.Password);
        }

        [Fact]
        public void Post_AssignsIncrementedId_WhenUsersAlreadyExist()
        {
            using var context = DbContextFactory.Create();
            context.Users.Add(new Users { Id = 5, UserId = "existing", RoleId = 2, Password = "hash" });
            context.SaveChanges();
            var controller = new UsersController(context);

            controller.Post(new AuthenticationRequest { Username = "newuser", Password = "pw" });

            var saved = context.Users.Single(u => u.UserId == "newuser");
            Assert.Equal(6, saved.Id);
        }

        // ---------- Authenticate (login) ----------

        [Fact]
        public void Authenticate_ReturnsBadRequest_WhenRequestIsNull()
        {
            using var context = DbContextFactory.Create();
            var controller = new UsersController(context);

            var result = controller.Authenticate(null!);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public void Authenticate_ReturnsUnauthorized_WhenUserDoesNotExist()
        {
            using var context = DbContextFactory.Create();
            var controller = new UsersController(context);

            var result = controller.Authenticate(new AuthenticationRequest { Username = "ghost", Password = "pw" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public void Authenticate_ReturnsUnauthorized_WhenPasswordIsWrong()
        {
            using var context = DbContextFactory.Create();
            var controller = new UsersController(context);
            controller.Post(new AuthenticationRequest { Username = "satish", Password = "correct-password" });

            var result = controller.Authenticate(new AuthenticationRequest { Username = "satish", Password = "wrong-password" });

            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public void Authenticate_ReturnsOkWithUserInfo_WhenCredentialsAreCorrect()
        {
            using var context = DbContextFactory.Create();
            var controller = new UsersController(context);
            controller.Post(new AuthenticationRequest { Username = "satish", Password = "correct-password" });

            var result = controller.Authenticate(new AuthenticationRequest { Username = "satish", Password = "correct-password" });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var username = ok.Value!.GetType().GetProperty("username")!.GetValue(ok.Value) as string;
            var roleId = ok.Value!.GetType().GetProperty("roleId")!.GetValue(ok.Value);
            Assert.Equal("satish", username);
            Assert.Equal(2L, roleId);
        }
    }
}
