using eComm_ms.DBA;
using eComm_ms.Models;
using Microsoft.AspNetCore.Mvc;

namespace eComm_ms.Controllers
{
    /// <summary>
    /// API controller that provides endpoints to query and manage products.
    /// </summary>
    [ApiController]
    [Route("/products")]
    public class ProductsController : ControllerBase
    {
        /// <summary>
        /// Database context used to access product data.
        /// </summary>
        private readonly ECommDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProductsController"/> class.
        /// </summary>
        /// <param name="context">The database context to be used by the controller.</param>
        public ProductsController(ECommDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves all products.
        /// </summary>
        /// <returns>
        /// An <see cref="ActionResult{IEnumerable}"/> containing the list of products.
        /// Returns 200 OK with the product list.
        /// </returns>
        [HttpGet(Name = "getallproducts")]
        public ActionResult<IEnumerable<Products>> Get()
        {
            var products = _context.Products.ToList();
            return Ok(products);
        }

        /// <summary>
        /// Retrieves a single product by its identifier.
        /// </summary>
        /// <param name="id">The identifier of the product to retrieve.</param>
        /// <returns>
        /// 200 OK with the <see cref="Products"/> when found; otherwise 404 Not Found.
        /// </returns>
        [HttpGet("{id:int}", Name = "getproductbyid")]
        public ActionResult<Products> Get(int id)
        {
            var product = _context.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }
            return Ok(product);
        }

        /// <summary>
        /// Adds a new product. The product identifier is assigned by taking the current maximum id
        /// in the store and incrementing it by one.
        /// </summary>
        /// <param name="product">The product to add. Must not be null.</param>
        /// <returns>
        /// 201 Created with the created product and a Location header pointing to the new resource.
        /// Returns 400 Bad Request when <paramref name="product"/> is null.
        /// </returns>
        [HttpPost("add", Name = "addproduct")]
        public ActionResult<Products> Post([FromBody] Products product)
        {
            if (product == null)
            {
                return BadRequest("Product cannot be null");
            }

            // Get the max ID and increment by 1
            var maxId = _context.Products.Any() ? _context.Products.Max(p => p.Id) : 0;
            product.Id = maxId + 1;

            _context.Products.Add(product);
            _context.SaveChanges();

            return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
        }
    }
}