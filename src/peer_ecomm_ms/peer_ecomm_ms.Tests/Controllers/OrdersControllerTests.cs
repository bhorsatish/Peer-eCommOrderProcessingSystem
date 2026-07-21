using eComm_ms.Controllers;
using eComm_ms.Models;
using eComm_ms.Services;
using eComm_ms.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc;

namespace eComm_ms.Tests.Controllers
{
    public class OrdersControllerTests
    {
        private static (OrdersController controller, DBA.ECommDbContext context) CreateController()
        {
            var context = DbContextFactory.Create();

            context.Products.Add(new Products { Id = 1, Name = "Widget", Price = 9.99m, Icon = "🔧" });
            context.StatusStates.AddRange(
                new StatusStates { Id = 2, Name = "New Order - COD", Description = "d", Icon = "i" },
                new StatusStates { Id = 4, Name = "Packaged", Description = "d", Icon = "i" },
                new StatusStates { Id = 5, Name = "Delivered", Description = "d", Icon = "i" },
                new StatusStates { Id = 6, Name = "Cancellation Requested", Description = "d", Icon = "i" },
                new StatusStates { Id = 8, Name = "Cancelled", Description = "d", Icon = "i" },
                new StatusStates { Id = 9, Name = "In Transit", Description = "d", Icon = "i" },
                new StatusStates { Id = 10, Name = "Placed", Description = "d", Icon = "i" }
            );
            context.Users.Add(new Users { Id = 1, UserId = "buyer", RoleId = 2, Password = "x" });
            context.SaveChanges();

            var service = new OrderDetailsService(context);
            var controller = new OrdersController(context, service);
            return (controller, context);
        }

        private static Orders SeedOrder(DBA.ECommDbContext context, long id = 1, long userId = 1, long statusId = 2)
        {
            var order = new Orders
            {
                Id = id,
                UserId = userId,
                ProductId = 1,
                StatusId = statusId,
                LastUpdatedByUserId = userId,
                OrderedFor = "Buyer",
                DeliveryAddress = "Addr",
                AddedOn = DateTime.Now.ToString("O"),
                LastUpdatedOn = DateTime.Now.ToString("O")
            };
            context.Orders.Add(order);
            context.SaveChanges();
            return order;
        }

        // ---------- GetOrderDetailsById ----------

        [Fact]
        public void GetOrderDetailsById_ReturnsBadRequest_WhenIdIsZeroOrNegative()
        {
            var (controller, _) = CreateController();

            var result = controller.GetOrderDetailsById(0);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public void GetOrderDetailsById_ReturnsNotFound_WhenOrderDoesNotExist()
        {
            var (controller, _) = CreateController();

            var result = controller.GetOrderDetailsById(999);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public void GetOrderDetailsById_ReturnsOk_WithEnrichedDetails_WhenOrderExists()
        {
            var (controller, context) = CreateController();
            SeedOrder(context);

            var result = controller.GetOrderDetailsById(1);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<Models.DTOs.OrderDetailsDto>(ok.Value);
            Assert.Equal(1, dto.OrderId);
        }

        // ---------- GetOrderDetailsByUserId ----------

        [Fact]
        public void GetOrderDetailsByUserId_ReturnsBadRequest_WhenUserIdInvalid()
        {
            var (controller, _) = CreateController();

            var result = controller.GetOrderDetailsByUserId(0);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public void GetOrderDetailsByUserId_ReturnsNotFound_WhenUserHasNoOrders()
        {
            var (controller, _) = CreateController();

            var result = controller.GetOrderDetailsByUserId(1);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public void GetOrderDetailsByUserId_ReturnsPaginatedResults()
        {
            var (controller, context) = CreateController();
            for (long i = 1; i <= 15; i++)
            {
                SeedOrder(context, id: i);
            }

            var result = controller.GetOrderDetailsByUserId(1, pageNumber: 2, pageSize: 10);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(2, GetProperty<int>(ok.Value!, "pageNumber"));
            Assert.Equal(15, GetProperty<int>(ok.Value!, "totalCount"));
            Assert.Equal(2, GetProperty<int>(ok.Value!, "totalPages"));
        }

        /// <summary>
        /// Reads a property off an anonymous-type response object via reflection.
        /// Anonymous types are compiler-generated as `internal`, so `dynamic` binding
        /// from this test assembly would throw a RuntimeBinderException; reflection
        /// on the (public) property works regardless of the declaring type's visibility.
        /// </summary>
        private static T GetProperty<T>(object obj, string propertyName)
        {
            var prop = obj.GetType().GetProperty(propertyName)
                ?? throw new InvalidOperationException($"Property '{propertyName}' not found on {obj.GetType()}");
            return (T)prop.GetValue(obj)!;
        }

        // ---------- GetOrderDetailsByStatusId ----------

        [Fact]
        public void GetOrderDetailsByStatusId_ReturnsBadRequest_WhenStatusIdInvalid()
        {
            var (controller, _) = CreateController();

            var result = controller.GetOrderDetailsByStatusId(0);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public void GetOrderDetailsByStatusId_ReturnsNotFound_WhenNoOrdersForStatus()
        {
            var (controller, _) = CreateController();

            var result = controller.GetOrderDetailsByStatusId(2);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public void GetOrderDetailsByStatusId_ReturnsOk_WhenOrdersExistForStatus()
        {
            var (controller, context) = CreateController();
            SeedOrder(context, id: 1, statusId: 2);
            SeedOrder(context, id: 2, statusId: 4);

            var result = controller.GetOrderDetailsByStatusId(2);

            Assert.IsType<OkObjectResult>(result.Result);
        }

        // ---------- CreateOrder ----------

        [Fact]
        public void CreateOrder_ReturnsBadRequest_WhenOrderIsNull()
        {
            var (controller, _) = CreateController();

            var result = controller.CreateOrder(null!);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Theory]
        [InlineData(0, 1, 2, 1)]   // invalid UserId
        [InlineData(1, 0, 2, 1)]   // invalid ProductId
        [InlineData(1, 1, 0, 1)]   // invalid StatusId
        [InlineData(1, 1, 2, 0)]   // invalid LastUpdatedByUserId
        public void CreateOrder_ReturnsBadRequest_WhenRequiredFieldsInvalid(long userId, long productId, long statusId, long lastUpdatedByUserId)
        {
            var (controller, _) = CreateController();
            var order = new Orders { UserId = userId, ProductId = productId, StatusId = statusId, LastUpdatedByUserId = lastUpdatedByUserId };

            var result = controller.CreateOrder(order);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public void CreateOrder_PersistsOrder_AndReturnsCreatedAtRoute()
        {
            var (controller, context) = CreateController();
            var order = new Orders { Id = 1, UserId = 1, ProductId = 1, StatusId = 2, LastUpdatedByUserId = 1 };

            var result = controller.CreateOrder(order);

            Assert.IsType<CreatedAtRouteResult>(result.Result);
            Assert.Single(context.Orders);
        }

        // ---------- UpdateOrderStatus ----------

        [Fact]
        public void UpdateOrderStatus_ReturnsBadRequest_WhenIdInvalid()
        {
            var (controller, _) = CreateController();

            var result = controller.UpdateOrderStatus(0, new Orders { StatusId = 4, LastUpdatedByUserId = 1 });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public void UpdateOrderStatus_ReturnsNotFound_WhenOrderMissing()
        {
            var (controller, _) = CreateController();

            var result = controller.UpdateOrderStatus(999, new Orders { StatusId = 4, LastUpdatedByUserId = 1 });

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public void UpdateOrderStatus_UpdatesStatusAndTimestamps_ForValidTransition()
        {
            var (controller, context) = CreateController();
            SeedOrder(context, id: 1, statusId: 2);

            var result = controller.UpdateOrderStatus(1, new Orders { StatusId = 4, LastUpdatedByUserId = 1 });

            Assert.IsType<OkObjectResult>(result.Result);
            var updated = context.Orders.First(o => o.Id == 1);
            Assert.Equal(4, updated.StatusId);
            Assert.False(string.IsNullOrEmpty(updated.PackagedOn));
        }

        [Fact]
        public void UpdateOrderStatus_SetsDeliveredOn_WhenStatusBecomesDelivered()
        {
            var (controller, context) = CreateController();
            SeedOrder(context, id: 1, statusId: 9);

            var result = controller.UpdateOrderStatus(1, new Orders { StatusId = 5, LastUpdatedByUserId = 1 });

            Assert.IsType<OkObjectResult>(result.Result);
            var updated = context.Orders.First(o => o.Id == 1);
            Assert.False(string.IsNullOrEmpty(updated.DeliveredOn));
        }

        [Fact]
        public void UpdateOrderStatus_RejectsCancellation_WhenOrderAlreadyInTransit()
        {
            var (controller, context) = CreateController();
            SeedOrder(context, id: 1, statusId: 9); // In Transit

            var result = controller.UpdateOrderStatus(1, new Orders { StatusId = 6, LastUpdatedByUserId = 1 });

            Assert.IsType<BadRequestObjectResult>(result.Result);
            var unchanged = context.Orders.First(o => o.Id == 1);
            Assert.Equal(9, unchanged.StatusId);
        }

        [Fact]
        public void UpdateOrderStatus_RejectsCancellation_WhenOrderAlreadyDelivered()
        {
            var (controller, context) = CreateController();
            SeedOrder(context, id: 1, statusId: 5); // Delivered

            var result = controller.UpdateOrderStatus(1, new Orders { StatusId = 8, LastUpdatedByUserId = 1 });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public void UpdateOrderStatus_RejectsCancellation_WhenOrderAlreadyPackaged()
        {
            // Customers may only cancel while the order is still pending (10, 2, or 3).
            // Once an admin has packaged it (4), it's past the pending phase and can no longer be cancelled.
            var (controller, context) = CreateController();
            SeedOrder(context, id: 1, statusId: 4); // Packaged In Warehouse

            var result = controller.UpdateOrderStatus(1, new Orders { StatusId = 6, LastUpdatedByUserId = 1 });

            Assert.IsType<BadRequestObjectResult>(result.Result);
            var unchanged = context.Orders.First(o => o.Id == 1);
            Assert.Equal(4, unchanged.StatusId);
        }

        [Fact]
        public void UpdateOrderStatus_AllowsCancellationCompletion_WhenAlreadyInCancellationRequestedState()
        {
            // Status 6/7 must remain reachable so an already-initiated cancellation (started while
            // pending) can still progress to its final Cancelled state, even though 6/7 themselves
            // are past the "pending" phase.
            var (controller, context) = CreateController();
            SeedOrder(context, id: 1, statusId: 6); // Cancellation Requested

            var result = controller.UpdateOrderStatus(1, new Orders { StatusId = 8, LastUpdatedByUserId = 1 });

            Assert.IsType<OkObjectResult>(result.Result);
            var updated = context.Orders.First(o => o.Id == 1);
            Assert.Equal(8, updated.StatusId);
        }

        [Fact]
        public void UpdateOrderStatus_AllowsCancellation_WhenOrderStillPending()
        {
            var (controller, context) = CreateController();
            SeedOrder(context, id: 1, statusId: 2); // New Order - COD

            var result = controller.UpdateOrderStatus(1, new Orders { StatusId = 8, LastUpdatedByUserId = 1 });

            Assert.IsType<OkObjectResult>(result.Result);
            var updated = context.Orders.First(o => o.Id == 1);
            Assert.Equal(8, updated.StatusId);
            Assert.False(string.IsNullOrEmpty(updated.CancellationPaidOn));
            Assert.False(string.IsNullOrEmpty(updated.CancelledOn));
        }

        // ---------- DeleteOrder ----------

        [Fact]
        public void DeleteOrder_ReturnsBadRequest_WhenIdInvalid()
        {
            var (controller, _) = CreateController();

            var result = controller.DeleteOrder(0);

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public void DeleteOrder_ReturnsNotFound_WhenOrderMissing()
        {
            var (controller, _) = CreateController();

            var result = controller.DeleteOrder(999);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public void DeleteOrder_RemovesOrder_WhenItExists()
        {
            var (controller, context) = CreateController();
            SeedOrder(context, id: 1);

            var result = controller.DeleteOrder(1);

            Assert.IsType<OkObjectResult>(result.Result);
            Assert.Empty(context.Orders);
        }
    }
}
