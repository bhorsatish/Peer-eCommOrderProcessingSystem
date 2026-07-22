using eComm_ms.Models;
using eComm_ms.Services;
using eComm_ms.Tests.TestHelpers;

namespace eComm_ms.Tests.Services
{
    /// <summary>
    /// Tests for the background sweep logic in OrderStatusUpdateService. These call
    /// SweepPlacedOrdersAsync directly against an in-memory context — no real Timer, no
    /// waiting 5 minutes — matching the "extract the transition logic into a plain,
    /// directly-testable method" guidance the sweep was refactored to satisfy.
    /// </summary>
    public class OrderStatusUpdateServiceTests
    {
        private static Orders MakeOrder(long id, long statusId, long paymentMode = 0) => new()
        {
            Id = id,
            UserId = 1,
            ProductId = 1,
            StatusId = statusId,
            LastUpdatedByUserId = 1,
            PaymentMode = paymentMode,
            AddedOn = DateTime.Now.ToString("O"),
            LastUpdatedOn = DateTime.Now.ToString("O")
        };

        [Fact]
        public async Task SweepPlacedOrdersAsync_TransitionsOnlyPlacedOrders_LeavesOthersUntouched()
        {
            // Matches the exact scenario the execution plan calls for: seed 3 "pending"
            // (StatusId 10 = Placed) and 2 "shipped" (already past Placed), assert only the 3 flip.
            using var context = DbContextFactory.Create();
            context.Orders.AddRange(
                MakeOrder(1, statusId: 10),
                MakeOrder(2, statusId: 10),
                MakeOrder(3, statusId: 10),
                MakeOrder(4, statusId: 9),  // In Transit — must not be touched
                MakeOrder(5, statusId: 5)   // Delivered — must not be touched
            );
            context.SaveChanges();

            var updatedCount = await OrderStatusUpdateService.SweepPlacedOrdersAsync(context);

            Assert.Equal(3, updatedCount);
            Assert.All(new long[] { 1, 2, 3 }, id =>
                Assert.NotEqual(10, context.Orders.First(o => o.Id == id).StatusId));
            Assert.Equal(9, context.Orders.First(o => o.Id == 4).StatusId);
            Assert.Equal(5, context.Orders.First(o => o.Id == 5).StatusId);
        }

        [Fact]
        public async Task SweepPlacedOrdersAsync_SetsStatusToCod_WhenPaymentModeIsZero()
        {
            using var context = DbContextFactory.Create();
            context.Orders.Add(MakeOrder(1, statusId: 10, paymentMode: 0));
            context.SaveChanges();

            await OrderStatusUpdateService.SweepPlacedOrdersAsync(context);

            Assert.Equal(2, context.Orders.First(o => o.Id == 1).StatusId);
        }

        [Fact]
        public async Task SweepPlacedOrdersAsync_SetsStatusToPrepaid_WhenPaymentModeIsNonZero()
        {
            using var context = DbContextFactory.Create();
            context.Orders.Add(MakeOrder(1, statusId: 10, paymentMode: 1));
            context.SaveChanges();

            await OrderStatusUpdateService.SweepPlacedOrdersAsync(context);

            Assert.Equal(3, context.Orders.First(o => o.Id == 1).StatusId);
        }

        [Fact]
        public async Task SweepPlacedOrdersAsync_UpdatesLastUpdatedOn_ForEachTransitionedOrder()
        {
            using var context = DbContextFactory.Create();
            var order = MakeOrder(1, statusId: 10);
            order.LastUpdatedOn = DateTime.UtcNow.AddDays(-1).ToString("o");
            context.Orders.Add(order);
            context.SaveChanges();
            var before = order.LastUpdatedOn;

            await OrderStatusUpdateService.SweepPlacedOrdersAsync(context);

            Assert.NotEqual(before, context.Orders.First(o => o.Id == 1).LastUpdatedOn);
        }

        [Fact]
        public async Task SweepPlacedOrdersAsync_ReturnsZero_AndChangesNothing_WhenNoPlacedOrdersExist()
        {
            using var context = DbContextFactory.Create();
            context.Orders.Add(MakeOrder(1, statusId: 4)); // Packaged, not Placed
            context.SaveChanges();

            var updatedCount = await OrderStatusUpdateService.SweepPlacedOrdersAsync(context);

            Assert.Equal(0, updatedCount);
            Assert.Equal(4, context.Orders.First(o => o.Id == 1).StatusId);
        }

        [Fact]
        public async Task SweepPlacedOrdersAsync_IsIdempotent_SecondRunFindsNothingLeftToUpdate()
        {
            using var context = DbContextFactory.Create();
            context.Orders.Add(MakeOrder(1, statusId: 10));
            context.SaveChanges();

            var firstRun = await OrderStatusUpdateService.SweepPlacedOrdersAsync(context);
            var secondRun = await OrderStatusUpdateService.SweepPlacedOrdersAsync(context);

            Assert.Equal(1, firstRun);
            Assert.Equal(0, secondRun);
        }
    }
}
