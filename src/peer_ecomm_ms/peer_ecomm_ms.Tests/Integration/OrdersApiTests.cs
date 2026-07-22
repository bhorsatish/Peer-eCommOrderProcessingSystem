using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using eComm_ms.Models;
using eComm_ms.Tests.TestHelpers;

namespace eComm_ms.Tests.Integration
{
    /// <summary>
    /// Drives /orders purely over real HTTP through the actual ASP.NET Core pipeline,
    /// including the cross-controller enrichment (Product/Status/User join) that only
    /// happens correctly when routing, DI, and JSON serialization all agree.
    /// </summary>
    public class OrdersApiTests : IClassFixture<ApiWebApplicationFactory>
    {
        private readonly ApiWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public OrdersApiTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            SeedStatusStatesOnce();
        }

        private void SeedStatusStatesOnce()
        {
            using var context = _factory.CreateDbContext();
            if (context.StatusStates.Any())
            {
                return;
            }

            context.StatusStates.AddRange(
                new StatusStates { Id = 2, Name = "New Order - COD", Description = "d", Icon = "i" },
                new StatusStates { Id = 3, Name = "New Order - Prepaid", Description = "d", Icon = "i" },
                new StatusStates { Id = 4, Name = "Packaged", Description = "d", Icon = "i" },
                new StatusStates { Id = 5, Name = "Delivered", Description = "d", Icon = "i" },
                new StatusStates { Id = 6, Name = "Cancellation Requested", Description = "d", Icon = "i" },
                new StatusStates { Id = 8, Name = "Cancelled", Description = "d", Icon = "i" },
                new StatusStates { Id = 9, Name = "In Transit", Description = "d", Icon = "i" },
                new StatusStates { Id = 10, Name = "Placed", Description = "d", Icon = "i" }
            );
            context.SaveChanges();
        }

        private async Task<long> RegisterUserAsync()
        {
            var username = $"user-{Guid.NewGuid():N}";
            (await _client.PostAsJsonAsync("/users/add", new { username, password = "pw" })).EnsureSuccessStatusCode();

            var auth = await _client.PostAsJsonAsync("/users/authenticate", new { username, password = "pw" });
            var body = await auth.Content.ReadFromJsonAsync<JsonElement>();
            return body.GetProperty("userId").GetInt64();
        }

        private async Task<long> CreateProductAsync()
        {
            var response = await _client.PostAsJsonAsync("/products/add", new Products { Name = "Widget", Price = 9.99m, Icon = "🔧" });
            var product = await response.Content.ReadFromJsonAsync<Products>();
            return product!.Id;
        }

        [Fact]
        public async Task FullOrderLifecycle_CreateGetUpdateDelete_WorksOverRealHttp()
        {
            var userId = await RegisterUserAsync();
            var productId = await CreateProductAsync();

            var createResponse = await _client.PostAsJsonAsync("/orders/add", new
            {
                userId,
                productId,
                statusId = 10,
                lastUpdatedByUserId = userId,
                paymentMode = 0,
                orderedFor = "Test Buyer",
                deliveryAddress = "123 Test St"
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
            var orderId = created.GetProperty("order").GetProperty("id").GetInt64();

            var getResponse = await _client.GetAsync($"/orders/{orderId}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var dto = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Widget", dto.GetProperty("product").GetProperty("name").GetString());

            var byUserResponse = await _client.GetAsync($"/orders/user/{userId}?pageNumber=1&pageSize=10");
            Assert.Equal(HttpStatusCode.OK, byUserResponse.StatusCode);

            var advanceResponse = await _client.PutAsJsonAsync($"/orders/{orderId}/status", new { statusId = 4, lastUpdatedByUserId = userId });
            Assert.Equal(HttpStatusCode.OK, advanceResponse.StatusCode);

            // Cancellation must now be rejected — order is past the pending phase (Packaged).
            var cancelResponse = await _client.PutAsJsonAsync($"/orders/{orderId}/status", new { statusId = 6, lastUpdatedByUserId = userId });
            Assert.Equal(HttpStatusCode.BadRequest, cancelResponse.StatusCode);

            var deleteResponse = await _client.DeleteAsync($"/orders/{orderId}");
            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

            var afterDelete = await _client.GetAsync($"/orders/{orderId}");
            Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
        }

        [Fact]
        public async Task CancelOrder_Succeeds_WhileStillPending_OverRealHttp()
        {
            var userId = await RegisterUserAsync();
            var productId = await CreateProductAsync();

            var createResponse = await _client.PostAsJsonAsync("/orders/add", new
            {
                userId,
                productId,
                statusId = 2, // New Order - COD, still pending
                lastUpdatedByUserId = userId
            });
            var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
            var orderId = created.GetProperty("order").GetProperty("id").GetInt64();

            var cancelResponse = await _client.PutAsJsonAsync($"/orders/{orderId}/status", new { statusId = 8, lastUpdatedByUserId = userId });

            Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
            var afterCancel = await (await _client.GetAsync($"/orders/{orderId}")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(8, afterCancel.GetProperty("statusId").GetInt64());
        }

        [Fact]
        public async Task GetOrdersByStatus_ReturnsPaginatedResults_OverRealHttp()
        {
            var userId = await RegisterUserAsync();
            var productId = await CreateProductAsync();

            for (var i = 0; i < 3; i++)
            {
                await _client.PostAsJsonAsync("/orders/add", new
                {
                    userId,
                    productId,
                    statusId = 3, // New Order - Prepaid — kept distinct from other tests in this class
                    lastUpdatedByUserId = userId
                });
            }

            var response = await _client.GetAsync("/orders/status/3?pageNumber=1&pageSize=2");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(body.GetProperty("totalCount").GetInt32() >= 3);
            Assert.Equal(2, body.GetProperty("data").GetArrayLength());
        }

        [Fact]
        public async Task CreateOrder_ReturnsBadRequest_ForInvalidUserId_OverRealHttp()
        {
            var response = await _client.PostAsJsonAsync("/orders/add", new
            {
                userId = 0,
                productId = 1,
                statusId = 10,
                lastUpdatedByUserId = 1
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetOrderById_ReturnsNotFound_ForUnknownId_OverRealHttp()
        {
            var response = await _client.GetAsync("/orders/999999999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
