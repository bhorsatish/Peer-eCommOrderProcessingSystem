using eComm_ms.DBA;
using eComm_ms.Models;
using eComm_ms.Models.DTOs;
using eComm_ms.Services;
using Microsoft.AspNetCore.Mvc;

namespace eComm_ms.Controllers
{
    /// <summary>
    /// API controller that provides endpoints to query and manage orders.
    /// Orders can only be retrieved via: User ID, Status ID, or Order ID.
    /// No generic endpoint to retrieve all orders.
    /// </summary>
    [ApiController]
    [Route("/orders")]
    public class OrdersController : ControllerBase
    {
        /// <summary>
        /// Database context used to access order data.
        /// </summary>
        private readonly ECommDbContext _context;

        private readonly OrderDetailsService _orderDetailsService;

        public OrdersController(ECommDbContext context, OrderDetailsService orderDetailsService)
        {
            _context = context;
            _orderDetailsService = orderDetailsService;
        }

        /// <summary>
        /// Retrieves a single order by its ID with complete product, status, and user details.
        /// </summary>
        /// <param name="id">The order ID to retrieve.</param>
        /// <returns>
        /// 200 OK with the enriched OrderDetailsDto when found; otherwise 404 Not Found.
        /// </returns>
        [HttpGet("{id:long}", Name = "getorderdetailsbyid")]
        public ActionResult<OrderDetailsDto> GetOrderDetailsById(long id)
        {
            if (id <= 0)
            {
                return BadRequest(new { message = "Order ID must be greater than 0" });
            }

            var order = _context.Orders.FirstOrDefault(o => o.Id == id);

            if (order == null)
            {
                return NotFound(new { message = $"Order not found with ID: {id}" });
            }

            var orderDetails = _orderDetailsService.BuildOrderDetails(order);

            if (orderDetails == null)
            {
                return NotFound(new { message = "Order details could not be built due to missing related data" });
            }

            return Ok(orderDetails);
        }

        /// <summary>
        /// Retrieves orders by a specific user ID with complete details and pagination.
        /// </summary>
        /// <param name="userId">The user ID to filter orders.</param>
        /// <param name="pageNumber">The page number (1-based). Default is 1.</param>
        /// <param name="pageSize">The number of orders per page. Default is 10, max 100.</param>
        /// <returns>
        /// 200 OK with paginated enriched orders list for the specified user.
        /// 404 Not Found if user has no orders.
        /// </returns>
        [HttpGet("user/{userId:long}", Name = "getorderdetailsbyuserid")]
        public ActionResult<object> GetOrderDetailsByUserId(long userId, int pageNumber = 1, int pageSize = 10)
        {
            if (userId <= 0)
            {
                return BadRequest(new { message = "User ID must be greater than 0" });
            }

            pageNumber = ValidatePageNumber(pageNumber);
            pageSize = ValidatePageSize(pageSize);

            var skipCount = (pageNumber - 1) * pageSize;

            var totalCount = _context.Orders.Where(o => o.UserId == userId).Count();

            if (totalCount == 0)
            {
                return NotFound(new { message = $"No orders found for user ID: {userId}" });
            }

            var orders = _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.Id)
                .Skip(skipCount)
                .Take(pageSize)
                .ToList();

            var orderDetails = _orderDetailsService.BuildOrderDetailsList(orders);

            return Ok(new
            {
                data = orderDetails,
                userId = userId,
                pageNumber = pageNumber,
                pageSize = pageSize,
                totalCount = totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        /// <summary>
        /// Retrieves orders by a specific status ID with complete details and pagination.
        /// </summary>
        /// <param name="statusId">The status ID to filter orders.</param>
        /// <param name="pageNumber">The page number (1-based). Default is 1.</param>
        /// <param name="pageSize">The number of orders per page. Default is 10, max 100.</param>
        /// <returns>
        /// 200 OK with paginated enriched orders list for the specified status.
        /// 404 Not Found if no orders exist for the status.
        /// </returns>
        [HttpGet("status/{statusId:long}", Name = "getorderdetailsbystatus")]
        public ActionResult<object> GetOrderDetailsByStatusId(long statusId, int pageNumber = 1, int pageSize = 10)
        {
            if (statusId <= 0)
            {
                return BadRequest(new { message = "Status ID must be greater than 0" });
            }

            pageNumber = ValidatePageNumber(pageNumber);
            pageSize = ValidatePageSize(pageSize);

            var skipCount = (pageNumber - 1) * pageSize;

            var totalCount = _context.Orders.Where(o => o.StatusId == statusId).Count();

            if (totalCount == 0)
            {
                return NotFound(new { message = $"No orders found for status ID: {statusId}" });
            }

            var orders = _context.Orders
                .Where(o => o.StatusId == statusId)
                .OrderByDescending(o => o.Id)
                .Skip(skipCount)
                .Take(pageSize)
                .ToList();

            var orderDetails = _orderDetailsService.BuildOrderDetailsList(orders);

            return Ok(new
            {
                data = orderDetails,
                statusId = statusId,
                pageNumber = pageNumber,
                pageSize = pageSize,
                totalCount = totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        /// <summary>
        /// Creates a new order.
        /// </summary>
        /// <param name="order">The order object containing order details.</param>
        /// <returns>
        /// 201 Created with the newly created order and its ID.
        /// 400 Bad Request if the order data is invalid.
        /// </returns>
        [HttpPost("add", Name = "createorder")]
        public ActionResult<object> CreateOrder([FromBody] Orders order)
        {
            // Validate input
            if (order == null)
            {
                return BadRequest(new { message = "Order data is required" });
            }

            if (order.UserId <= 0)
            {
                return BadRequest(new { message = "User ID must be greater than 0" });
            }

            if (order.ProductId <= 0)
            {
                return BadRequest(new { message = "Product ID must be greater than 0" });
            }

            if (order.StatusId <= 0)
            {
                return BadRequest(new { message = "Status ID must be greater than 0" });
            }

            if (order.LastUpdatedByUserId <= 0)
            {
                return BadRequest(new { message = "Last Updated By ID must be greater than 0" });
            }

            // Set timestamps
            order.AddedOn = DateTime.Now.ToString("O");
            order.LastUpdatedOn = DateTime.Now.ToString("O");

            try
            {
                // Add and save the order
                _context.Orders.Add(order);
                _context.SaveChanges();

                return CreatedAtRoute("getorderdetailsbyid", new { id = order.Id }, new
                {
                    message = "Order created successfully",
                    order = order
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"An error occurred while creating the order: {ex.Message}" });
            }
        }

        /// <summary>
        /// Updates the status of an existing order.
        /// </summary>
        /// <param name="id">The order ID to update.</param>
        /// <param name="order">The new status and related fields (StatusId and LastUpdatedByUserId are required).</param>
        /// <returns>
        /// 200 OK with the updated order.
        /// 400 Bad Request if the input is invalid.
        /// 404 Not Found if the order does not exist.
        /// </returns>
        [HttpPut("{id:long}/status", Name = "updateorderstatus")]
        public ActionResult<object> UpdateOrderStatus(long id, [FromBody] Orders order)
        {
            // Validate input
            if (id <= 0)
            {
                return BadRequest(new { message = "Order ID must be greater than 0" });
            }

            if (order == null)
            {
                return BadRequest(new { message = "Order data is required" });
            }

            if (order.StatusId <= 0)
            {
                return BadRequest(new { message = "Status ID must be greater than 0" });
            }

            if (order.LastUpdatedByUserId <= 0)
            {
                return BadRequest(new { message = "Updated By ID must be greater than 0" });
            }

            // Find the order
            var orderDetails = _context.Orders.FirstOrDefault(o => o.Id == id);

            if (orderDetails == null)
            {
                return NotFound(new { message = $"Order not found with ID: {id}" });
            }

            if ((order.StatusId == 6 || order.StatusId == 8)
                && !(orderDetails.StatusId == 10
                || orderDetails.StatusId == 2
                || orderDetails.StatusId == 3
                || orderDetails.StatusId == 6
                || orderDetails.StatusId == 7))
            {
                // Customers may only cancel while still pending (10, 2, or 3). Statuses 6/7 remain
                // allowed here so an already-in-progress cancellation can continue to completion.
                // Once packaged (4) or beyond, cancellation is no longer permitted.
                return BadRequest(new { message = "Orders can only be cancelled while still pending, before packaging has started" });
            }

            // Update status and timestamp
            orderDetails.StatusId = order.StatusId;
            orderDetails.LastUpdatedByUserId = order.LastUpdatedByUserId;
            orderDetails.LastUpdatedOn = DateTime.Now.ToString("O");

            switch (order.StatusId)
            {
                case 10:
                    orderDetails.PaymentMode = order.PaymentMode;
                    orderDetails.OrderedFor = order.OrderedFor;
                    orderDetails.DeliveryAddress = order.DeliveryAddress;
                    orderDetails.OrderedOn = DateTime.Now.ToString("O");
                    break;
                case 4:
                    orderDetails.PackagedOn = DateTime.Now.ToString("O");
                    break;
                case 5:
                    orderDetails.DeliveredOn = DateTime.Now.ToString("O");
                    break;
                case 6:
                    orderDetails.CancelledOn = DateTime.Now.ToString("O");
                    break;
                case 8:
                    orderDetails.CancellationPaidOn = DateTime.Now.ToString("O");
                    orderDetails.CancelledOn = string.IsNullOrWhiteSpace(orderDetails.CancelledOn) ? DateTime.Now.ToString("O") : orderDetails.CancelledOn;
                    break;
            }

            try
            {
                _context.SaveChanges();

                return Ok(new
                {
                    message = "Order status updated successfully",
                    order = order
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"An error occurred while updating the order status: {ex.Message}" });
            }
        }

        /// <summary>
        /// Deletes an existing order by its ID.
        /// </summary>
        /// <param name="id">The order ID to delete.</param>
        /// <returns>
        /// 200 OK with a success message when order is deleted.
        /// 400 Bad Request if the order ID is invalid.
        /// 404 Not Found if the order does not exist.
        /// </returns>
        [HttpDelete("{id:long}", Name = "deleteorder")]
        public ActionResult<object> DeleteOrder(long id)
        {
            // Validate input
            if (id <= 0)
            {
                return BadRequest(new { message = "Order ID must be greater than 0" });
            }

            // Find the order
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);

            if (order == null)
            {
                return NotFound(new { message = $"Order not found with ID: {id}" });
            }

            try
            {
                // Remove and save changes
                _context.Orders.Remove(order);
                _context.SaveChanges();

                return Ok(new
                {
                    message = "Order deleted successfully",
                    deletedOrderId = id
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"An error occurred while deleting the order: {ex.Message}" });
            }
        }

        /// <summary>
        /// Validates and normalizes the page number.
        /// </summary>
        private static int ValidatePageNumber(int pageNumber)
        {
            return pageNumber < 1 ? 1 : pageNumber;
        }

        /// <summary>
        /// Validates and normalizes the page size (max 100).
        /// </summary>
        private static int ValidatePageSize(int pageSize)
        {
            if (pageSize < 1) return 10;
            if (pageSize > 100) return 100;
            return pageSize;
        }
    }
}