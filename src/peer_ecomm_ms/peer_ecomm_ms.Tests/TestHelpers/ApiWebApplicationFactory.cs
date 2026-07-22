using eComm_ms.DBA;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace eComm_ms.Tests.TestHelpers
{
    /// <summary>
    /// Boots the real ASP.NET Core pipeline (routing, DI, controllers, JSON, CORS — everything
    /// registered in Program.cs) exactly as it runs in production, but swaps the SQLite
    /// connection for an isolated, disposable EF Core InMemory database.
    ///
    /// This is what makes integration tests a meaningful "did my change break the wiring"
    /// check: they exercise real HTTP requests through the real pipeline instead of calling
    /// controller methods directly, without ever touching the real eCommDB.db file or depending
    /// on the hardcoded absolute path in Program.cs.
    /// </summary>
    public class ApiWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // EF Core registers more than just DbContextOptions<ECommDbContext> for a single
                // AddDbContext call — notably IDbContextOptionsConfiguration<ECommDbContext>, which
                // is additive (composed, not replaced) across multiple AddDbContext calls. Removing
                // only the options descriptor leaves Program.cs's UseSqlite() call still composed
                // alongside our UseInMemoryDatabase() call, which EF Core rejects as two providers
                // registered for one context. Strip every descriptor that references ECommDbContext
                // (directly or as a closed generic argument) before re-registering it.
                var descriptorsToRemove = services
                    .Where(d => d.ServiceType == typeof(ECommDbContext)
                        || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Contains(typeof(ECommDbContext))))
                    .ToList();

                foreach (var descriptor in descriptorsToRemove)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ECommDbContext>(options =>
                    options.UseInMemoryDatabase(_dbName));
            });
        }

        /// <summary>
        /// Opens a context against the same in-memory database the running app is using,
        /// so a test can seed reference data (products, status states, users) before making
        /// HTTP calls, or assert on rows written by an HTTP call afterward.
        /// </summary>
        public ECommDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ECommDbContext>()
                .UseInMemoryDatabase(_dbName)
                .Options;
            return new ECommDbContext(options);
        }
    }
}
