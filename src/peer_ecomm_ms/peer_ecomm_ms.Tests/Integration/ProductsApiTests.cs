using System.Net;
using System.Net.Http.Json;
using eComm_ms.Models;
using eComm_ms.Tests.TestHelpers;

namespace eComm_ms.Tests.Integration
{
    /// <summary>
    /// Drives /products purely over real HTTP through the actual ASP.NET Core pipeline.
    /// </summary>
    public class ProductsApiTests : IClassFixture<ApiWebApplicationFactory>
    {
        private readonly HttpClient _client;

        public ProductsApiTests(ApiWebApplicationFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CreateProduct_ThenListAndGetById_RoundTripsOverRealHttp()
        {
            var createResponse = await _client.PostAsJsonAsync("/products/add", new Products { Name = "Widget", Price = 12.50m, Icon = "🔧" });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<Products>();
            Assert.NotNull(created);
            Assert.True(created!.Id > 0);

            var listResponse = await _client.GetAsync("/products");
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var products = await listResponse.Content.ReadFromJsonAsync<List<Products>>();
            Assert.Contains(products!, p => p.Id == created.Id && p.Name == "Widget");

            var getResponse = await _client.GetAsync($"/products/{created.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var fetched = await getResponse.Content.ReadFromJsonAsync<Products>();
            Assert.Equal(12.50m, fetched!.Price);
        }

        [Fact]
        public async Task GetProduct_ReturnsNotFound_ForUnknownId_OverRealHttp()
        {
            var response = await _client.GetAsync("/products/999999999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task CreateProduct_ReturnsBadRequest_ForNullBody_OverRealHttp()
        {
            var response = await _client.PostAsJsonAsync<Products?>("/products/add", null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
