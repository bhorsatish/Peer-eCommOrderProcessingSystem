using eComm_ms.DBA;
using Microsoft.EntityFrameworkCore;

namespace eComm_ms.Services
{
    public class OrderStatusUpdateService : BackgroundService
    {
        private readonly ILogger<OrderStatusUpdateService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private Timer? _timer;

        public OrderStatusUpdateService(ILogger<OrderStatusUpdateService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OrderStatusUpdateService is starting.");

            // Run immediately on startup. Must not let an exception escape here: BackgroundService's
            // default StopHost exception behavior means an unhandled throw takes down the entire app,
            // not just this job.
            try
            {
                await UpdateOrderStatuses(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating order statuses on startup.");
            }

            // Then run every 5 minutes (first tick after 5 minutes, since we already ran once above)
            _timer = new Timer(async (state) =>
            {
                try
                {
                    await UpdateOrderStatuses(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while updating order statuses.");
                }
            }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

            await Task.CompletedTask;
        }

        private async Task UpdateOrderStatuses(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ECommDbContext>();

                try
                {
                    var updatedCount = await SweepPlacedOrdersAsync(dbContext, stoppingToken);

                    if (updatedCount > 0)
                    {
                        _logger.LogInformation($"Successfully updated {updatedCount} orders.");
                    }
                    else
                    {
                        _logger.LogDebug("No orders with status 10 found.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while updating order statuses.");
                    throw;
                }
            }
        }

        /// <summary>
        /// Finds every order still at status 10 (Placed) and advances it to 2 (New Order - COD)
        /// or 3 (New Order - Prepaid) based on PaymentMode. Pulled out of the timer/scope plumbing
        /// above so it can be unit-tested directly against any ECommDbContext (including the
        /// EF Core InMemory provider) without waiting on a real timer.
        /// </summary>
        /// <returns>The number of orders transitioned.</returns>
        public static async Task<int> SweepPlacedOrdersAsync(ECommDbContext dbContext, CancellationToken cancellationToken = default)
        {
            var ordersToUpdate = await dbContext.Orders
                .Where(o => o.StatusId == 10)
                .ToListAsync(cancellationToken);

            if (ordersToUpdate.Count == 0)
            {
                return 0;
            }

            foreach (var order in ordersToUpdate)
            {
                // Set status to 2 if PaymentMode is 0, otherwise set to 3
                order.StatusId = order.PaymentMode == 0 ? 2 : 3;
                order.LastUpdatedOn = DateTime.UtcNow.ToString("o");
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return ordersToUpdate.Count;
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("OrderStatusUpdateService is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }
    }
}