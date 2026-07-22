using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using eComm_ms.Models;
using eComm_ms.Services;
using eComm_ms.Tests.TestHelpers;

namespace eComm_ms.Tests.Integration
{
    /// <summary>
    /// Simulates a full customer journey purely over HTTP against the real pipeline, then
    /// triggers the background sweep directly (instead of waiting on its real 5-minute timer)
    /// to confirm every module — auth, catalog, order creation, status updates, and the
    /// background job — still cooperate correctly end to end.
    ///
    /// This is the single test most likely to catch a regression introduced anywhere in the
    /// stack, and the one worth running first after any change before deploying.
    /// </summary>
    public class EndToEndCheckoutFlowTests : IClassFixture<ApiWebApplicationFactory>
    {
        private readonly ApiWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public EndToEndCheckoutFlowTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();

            using var context = factory.CreateDbContext();
            if (!context.StatusStates.Any())
            {
                context.StatusStates.AddRange(
                    new StatusStates { Id = 2, Name = "New Order - COD", Description = "d", Icon = "i" },
                    new StatusStates { Id = 3, Name = "New Order - Prepaid", Description = "d", Icon = "i" },
                    new StatusStates { Id = 10, Name = "Placed", Description = "d", Icon = "i" }
                );
                context.SaveChanges();
            }
        }

        [Fact]
        public async Task Register_Login_Browse_Cart_Checkout_BackgroundSweep_TrackOrder_AllWorkTogether()
        {
            // 1. Register + login
            var username = $"shopper-{Guid.NewGuid():N}";
            (await _client.PostAsJsonAsync("/users/add", new { username, password = "S3cret!" })).EnsureSuccessStatusCode();
            var authResponse = await _client.PostAsJsonAsync("/users/authenticate", new { username, password = "S3cret!" });
            var authBody = await authResponse.Content.ReadFromJsonAsync<JsonElement>();
            var userId = authBody.GetProperty("userId").GetInt64();

            // 2. Browse catalog
            var productResponse = await _client.PostAsJsonAsync("/products/add", new Products { Name = "Headphones", Price = 49.99m, Icon = "🎧" });
            var product = await productResponse.Content.ReadFromJsonAsync<Products>();
            var catalog = await _client.GetFromJsonAsync<List<Products>>("/products");
            Assert.Contains(catalog!, p => p.Id == product!.Id);

            // 3. Add to cart (StatusId 1 — persisted immediately, same as the real frontend does)
            var cartResponse = await _client.PostAsJsonAsync("/orders/add", new
            {
                userId,
                productId = product!.Id,
                statusId = 1,
                lastUpdatedByUserId = userId
            });
            var cartOrder = await cartResponse.Content.ReadFromJsonAsync<JsonElement>();
            var orderId = cartOrder.GetProperty("order").GetProperty("id").GetInt64();

            // 4. Checkout — advance the cart line to Placed (StatusId 10), COD payment
            var checkoutResponse = await _client.PutAsJsonAsync($"/orders/{orderId}/status", new
            {
                statusId = 10,
                lastUpdatedByUserId = userId,
                paymentMode = 0,
                orderedFor = username,
                deliveryAddress = "1 Test Way"
            });
            Assert.Equal(HttpStatusCode.OK, checkoutResponse.StatusCode);

            // 5. Background job hasn't run yet — order is still "Placed"
            var beforeSweep = await (await _client.GetAsync($"/orders/{orderId}")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(10, beforeSweep.GetProperty("statusId").GetInt64());

            // 6. Trigger the sweep directly — same logic the real Timer calls every 5 minutes
            using (var context = _factory.CreateDbContext())
            {
                var swept = await OrderStatusUpdateService.SweepPlacedOrdersAsync(context);
                Assert.Equal(1, swept);
            }

            // 7. Order is now auto-advanced to "New Order - COD" (StatusId 2)
            var afterSweep = await (await _client.GetAsync($"/orders/{orderId}")).Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(2, afterSweep.GetProperty("statusId").GetInt64());

            // 8. Customer can still see it in their order history
            var history = await _client.GetAsync($"/orders/user/{userId}?pageNumber=1&pageSize=10");
            Assert.Equal(HttpStatusCode.OK, history.StatusCode);
            var historyBody = await history.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(historyBody.GetProperty("totalCount").GetInt32() >= 1);
        }
    }
}
