using eComm_ms.DBA;
using eComm_ms.Models;
using eComm_ms.Models.DTOs;

namespace eComm_ms.Services
{
    /// <summary>
    /// Service to build enriched order details by joining order data with product, status, and user information.
    /// </summary>
    public class OrderDetailsService
    {
        private readonly ECommDbContext _context;

        public OrderDetailsService(ECommDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Builds a complete OrderDetailsDto with all related reference data.
        /// </summary>
        /// <param name="order">The order object to enrich.</param>
        /// <returns>OrderDetailsDto with product, status, and user details, or null if related data is missing.</returns>
        public OrderDetailsDto? BuildOrderDetails(Orders order)
        {
            if (order == null)
                return null;

            // Get Product details
            var product = _context.Products.FirstOrDefault(p => p.Id == order.ProductId);
            if (product == null)
                return null; // Product must exist

            // Get Status details
            var status = _context.StatusStates.FirstOrDefault(s => s.Id == order.StatusId);
            if (status == null)
                return null; // Status must exist

            // Get User details (person who placed the order)
            var user = _context.Users.FirstOrDefault(u => u.Id == order.UserId);
            if (user == null)
                return null; // User must exist

            // Get User details for who last updated the order
            var updatedByUser = _context.Users.FirstOrDefault(u => u.Id == order.LastUpdatedByUserId);

            return new OrderDetailsDto
            {
                OrderId = order.Id,
                UserId = order.UserId,
                ProductId = order.ProductId,
                StatusId = order.StatusId,
                LastUpdatedByUserId = order.LastUpdatedByUserId,
                PaymentMode = order.PaymentMode,
                OrderedFor = order.OrderedFor,
                DeliveryAddress = order.DeliveryAddress,
                LastUpdatedOn = order.LastUpdatedOn,
                AddedOn = order.AddedOn,
                OrderedOn = order.OrderedOn,
                PackagedOn = order.PackagedOn,
                DeliveredOn = order.DeliveredOn,
                CancelledOn = order.CancelledOn,
                CancellationPaidOn = order.CancellationPaidOn,
                Product = new ProductDetailsDto
                {
                    Id = product.Id,
                    Name = product.Name,
                    Price = product.Price,
                    Icon = product.Icon
                },
                Status = new StatusDetailsDto
                {
                    Id = status.Id,
                    Name = status.Name,
                    Description = status.Description,
                    Icon = status.Icon
                },
                User = new UserDetailsDto
                {
                    Id = user.Id,
                    UserId = user.UserId,
                    RoleId = user.RoleId
                },
                UpdatedByUser = updatedByUser != null ? new UserDetailsDto
                {
                    Id = updatedByUser.Id,
                    UserId = updatedByUser.UserId,
                    RoleId = updatedByUser.RoleId
                } : null!
            };
        }

        /// <summary>
        /// Builds a list of OrderDetailsDto objects.
        /// </summary>
        /// <param name="orders">Collection of orders to enrich.</param>
        /// <returns>List of OrderDetailsDto objects, excluding any orders with missing related data.</returns>
        public List<OrderDetailsDto> BuildOrderDetailsList(IEnumerable<Orders> orders)
        {
            var result = new List<OrderDetailsDto>();
            foreach (var order in orders)
            {
                var details = BuildOrderDetails(order);
                if (details != null)
                    result.Add(details);
            }
            return result;
        }
    }
}