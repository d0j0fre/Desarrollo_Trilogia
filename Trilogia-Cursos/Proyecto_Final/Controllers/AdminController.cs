using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Admin")]
    public class AdminController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public AdminController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var model = await _adminDbService.GetDashboardSummaryAsync();
            return View(model);
        }
    }
}