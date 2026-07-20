using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers.Admin
{
    [AdminAuthorize("Garantias")]
    public class WarrantyRequestsAdminController : Controller
    {
        private readonly AdminDbService _adminDbService;

        public WarrantyRequestsAdminController(AdminDbService adminDbService)
        {
            _adminDbService = adminDbService;
        }

        public async Task<IActionResult> Index()
        {
            var model = await _adminDbService.GetWarrantyRequestsAsync();
            return View(model);
        }
    }
}