using eComm_ms.DBA;
using Microsoft.EntityFrameworkCore;

namespace eComm_ms.Tests.TestHelpers
{
    /// <summary>
    /// Creates an isolated in-memory ECommDbContext for each test so tests never share state.
    /// </summary>
    public static class DbContextFactory
    {
        public static ECommDbContext Create()
        {
            var options = new DbContextOptionsBuilder<ECommDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ECommDbContext(options);
        }
    }
}
