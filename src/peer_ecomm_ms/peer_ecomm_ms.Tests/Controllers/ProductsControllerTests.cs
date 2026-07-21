using eComm_ms.Controllers;
using eComm_ms.Models;
using eComm_ms.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc;

namespace eComm_ms.Tests.Controllers
{
    public class ProductsControllerTests
    {
        [Fact]
        public void Get_ReturnsEmptyList_WhenNoProductsExist()
        {
            using var context = DbContextFactory.Create();
            var controller = new ProductsController(context);

            var result = controller.Get();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var products = Assert.IsAssignableFrom<IEnumerable<Products>>(ok.Value);
            Assert.Empty(products);
        }

        [Fact]
        public void Get_ReturnsAllProducts()
        {
            using var context = DbContextFactory.Create();
            context.Products.AddRange(
                new Products { Id = 1, Name = "A", Price = 1.5m, Icon = "a" },
                new Products { Id = 2, Name = "B", Price = 2.5m, Icon = "b" }
            );
            context.SaveChanges();
            var controller = new ProductsController(context);

            var result = controller.Get();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var products = Assert.IsAssignableFrom<IEnumerable<Products>>(ok.Value);
            Assert.Equal(2, products.Count());
        }

        [Fact]
        public void GetById_ReturnsNotFound_WhenProductDoesNotExist()
        {
            using var context = DbContextFactory.Create();
            var controller = new ProductsController(context);

            var result = controller.Get(999);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public void GetById_ReturnsProduct_WhenItExists()
        {
            using var context = DbContextFactory.Create();
            context.Products.Add(new Products { Id = 5, Name = "Widget", Price = 9.99m, Icon = "w" });
            context.SaveChanges();
            var controller = new ProductsController(context);

            var result = controller.Get(5);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var product = Assert.IsType<Products>(ok.Value);
            Assert.Equal("Widget", product.Name);
        }

        [Fact]
        public void Post_ReturnsBadRequest_WhenProductIsNull()
        {
            using var context = DbContextFactory.Create();
            var controller = new ProductsController(context);

            var result = controller.Post(null!);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public void Post_AssignsIdOne_WhenStoreIsEmpty()
        {
            using var context = DbContextFactory.Create();
            var controller = new ProductsController(context);

            var result = controller.Post(new Products { Name = "New", Price = 10m, Icon = "n" });

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var product = Assert.IsType<Products>(created.Value);
            Assert.Equal(1, product.Id);
        }

        [Fact]
        public void Post_AssignsIncrementedId_WhenProductsAlreadyExist()
        {
            using var context = DbContextFactory.Create();
            context.Products.Add(new Products { Id = 7, Name = "Existing", Price = 1m, Icon = "e" });
            context.SaveChanges();
            var controller = new ProductsController(context);

            var result = controller.Post(new Products { Name = "New", Price = 10m, Icon = "n" });

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var product = Assert.IsType<Products>(created.Value);
            Assert.Equal(8, product.Id);
            Assert.Equal(2, context.Products.Count());
        }
    }
}
