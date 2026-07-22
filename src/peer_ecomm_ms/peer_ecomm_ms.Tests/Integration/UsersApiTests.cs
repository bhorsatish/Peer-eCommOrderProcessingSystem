using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using eComm_ms.Tests.TestHelpers;

namespace eComm_ms.Tests.Integration
{
    /// <summary>
    /// Drives /users purely over real HTTP through the actual ASP.NET Core pipeline
    /// (routing, model binding, JSON, DI) — catches wiring regressions that calling
    /// UsersController methods directly (see Controllers/UsersControllerTests.cs) cannot.
    /// </summary>
    public class UsersApiTests : IClassFixture<ApiWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public UsersApiTests(ApiWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Register_ThenAuthenticate_RoundTripsOverRealHttp()
        {
            var username = $"user-{Guid.NewGuid():N}";

            var registerResponse = await _client.PostAsJsonAsync("/users/add", new { username, password = "S3cret!" });
            Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

            var authResponse = await _client.PostAsJsonAsync("/users/authenticate", new { username, password = "S3cret!" });
            Assert.Equal(HttpStatusCode.OK, authResponse.StatusCode);

            var body = await authResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(username, body.GetProperty("username").GetString());
            Assert.Equal(2, body.GetProperty("roleId").GetInt64());
        }

        [Fact]
        public async Task Authenticate_ReturnsUnauthorized_ForWrongPassword_OverRealHttp()
        {
            var username = $"user-{Guid.NewGuid():N}";
            await _client.PostAsJsonAsync("/users/add", new { username, password = "correct" });

            var response = await _client.PostAsJsonAsync("/users/authenticate", new { username, password = "wrong" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Authenticate_ReturnsUnauthorized_ForUnknownUser_OverRealHttp()
        {
            var response = await _client.PostAsJsonAsync("/users/authenticate", new { username = $"ghost-{Guid.NewGuid():N}", password = "whatever" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Register_ReturnsBadRequest_ForDuplicateUsername_OverRealHttp()
        {
            var username = $"user-{Guid.NewGuid():N}";
            await _client.PostAsJsonAsync("/users/add", new { username, password = "pw" });

            var response = await _client.PostAsJsonAsync("/users/add", new { username, password = "pw2" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_ReturnsBadRequest_ForMissingPassword_OverRealHttp()
        {
            var response = await _client.PostAsJsonAsync("/users/add", new { username = $"user-{Guid.NewGuid():N}", password = "" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
