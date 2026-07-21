using eComm_ms.DBA;
using eComm_ms.Models;
using Microsoft.AspNetCore.Mvc;

namespace eComm_ms.Controllers
{
    [Controller]
    public class StatusStateController : ControllerBase
    {
        private readonly ECommDbContext _context;

        public StatusStateController(ECommDbContext context)
        {
            _context = context;
        }

        public StatusStates GetStatusState(int statusId)
        {
            return _context.StatusStates.FirstOrDefault(s => s.Id == statusId)!;
        }
    }
}
