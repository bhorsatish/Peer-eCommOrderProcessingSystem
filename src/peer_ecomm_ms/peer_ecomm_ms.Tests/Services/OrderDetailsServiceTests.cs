using eComm_ms.Models;
using eComm_ms.Services;
using eComm_ms.Tests.TestHelpers;

namespace eComm_ms.Tests.Services
{
    public class OrderDetailsServiceTests
    {
        private static Products MakeProduct(long id = 1) => new()
        {
            Id = id,
            Name = "Test Product",
            Price = 19.99m,
            Icon = "📦"
        };

        private static StatusStates MakeStatus(long id = 2) => new()
        {
            Id = id,
            Name = "New Order",
            Description = "Order placed",
            Icon = "🆕"
        };

        private static Users MakeUser(long id = 10, string userId = "buyer") => new()
        {
            Id = id,
            UserId = userId,
            RoleId = 2,
            Password = "irrelevant"
        };

        private static Orders MakeOrder(long id = 1, long userId = 10, long productId = 1, long statusId = 2, long lastUpdatedByUserId = 10) => new()
        {
            Id = id,
            UserId = userId,
            ProductId = productId,
            StatusId = statusId,
            LastUpdatedByUserId = lastUpdatedByUserId,
            OrderedFor = "Test Buyer",
            DeliveryAddress = "123 Test St"
        };

        [Fact]
        public void BuildOrderDetails_ReturnsNull_WhenOrderIsNull()
        {
            using var context = DbContextFactory.Create();
            var service = new OrderDetailsService(context);

            var result = service.BuildOrderDetails(null!);

            Assert.Null(result);
        }

        [Fact]
        public void BuildOrderDetails_ReturnsNull_WhenProductMissing()
        {
            using var context = DbContextFactory.Create();
            context.StatusStates.Add(MakeStatus());
            context.Users.Add(MakeUser());
            context.SaveChanges();
            var service = new OrderDetailsService(context);

            var result = service.BuildOrderDetails(MakeOrder(productId: 999));

            Assert.Null(result);
        }

        [Fact]
        public void BuildOrderDetails_ReturnsNull_WhenStatusMissing()
        {
            using var context = DbContextFactory.Create();
            context.Products.Add(MakeProduct());
            context.Users.Add(MakeUser());
            context.SaveChanges();
            var service = new OrderDetailsService(context);

            var result = service.BuildOrderDetails(MakeOrder(statusId: 999));

            Assert.Null(result);
        }

        [Fact]
        public void BuildOrderDetails_ReturnsNull_WhenOrderingUserMissing()
        {
            using var context = DbContextFactory.Create();
            context.Products.Add(MakeProduct());
            context.StatusStates.Add(MakeStatus());
            context.SaveChanges();
            var service = new OrderDetailsService(context);

            var result = service.BuildOrderDetails(MakeOrder(userId: 999));

            Assert.Null(result);
        }

        [Fact]
        public void BuildOrderDetails_PopulatesAllNestedDetails_WhenEverythingExists()
        {
            using var context = DbContextFactory.Create();
            context.Products.Add(MakeProduct());
            context.StatusStates.Add(MakeStatus());
            context.Users.Add(MakeUser());
            context.SaveChanges();
            var service = new OrderDetailsService(context);

            var result = service.BuildOrderDetails(MakeOrder());

            Assert.NotNull(result);
            Assert.Equal(1, result!.OrderId);
            Assert.NotNull(result.Product);
            Assert.Equal("Test Product", result.Product!.Name);
            Assert.NotNull(result.Status);
            Assert.Equal("New Order", result.Status!.Name);
            Assert.NotNull(result.User);
            Assert.Equal("buyer", result.User!.UserId);
        }

        [Fact]
        public void BuildOrderDetails_UpdatedByUserIsNull_WhenLastUpdatedByUserIdDoesNotMatchAnyUser()
        {
            using var context = DbContextFactory.Create();
            context.Products.Add(MakeProduct());
            context.StatusStates.Add(MakeStatus());
            context.Users.Add(MakeUser());
            context.SaveChanges();
            var service = new OrderDetailsService(context);

            var result = service.BuildOrderDetails(MakeOrder(lastUpdatedByUserId: 999));

            Assert.NotNull(result);
            Assert.Null(result!.UpdatedByUser);
        }

        [Fact]
        public void BuildOrderDetailsList_ExcludesOrdersWithMissingRelatedData()
        {
            using var context = DbContextFactory.Create();
            context.Products.Add(MakeProduct());
            context.StatusStates.Add(MakeStatus());
            context.Users.Add(MakeUser());
            context.SaveChanges();
            var service = new OrderDetailsService(context);

            var orders = new[]
            {
                MakeOrder(id: 1),
                MakeOrder(id: 2, productId: 999) // invalid product -> excluded
            };

            var result = service.BuildOrderDetailsList(orders);

            Assert.Single(result);
            Assert.Equal(1, result[0].OrderId);
        }
    }
}
