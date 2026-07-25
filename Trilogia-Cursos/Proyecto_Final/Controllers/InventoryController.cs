using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Inventario")]
    public class InventoryController : Controller
    {
        private const long MaxProductImageBytes = 2 * 1024 * 1024;
        private static readonly IReadOnlyDictionary<string, string[]> PermittedImageContentTypes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = new[] { "image/jpeg" },
            [".jpeg"] = new[] { "image/jpeg" },
            [".png"] = new[] { "image/png" },
            [".webp"] = new[] { "image/webp" }
        };

        private readonly AdminDbService _adminDbService;
        private readonly IWebHostEnvironment _environment;

        public InventoryController(AdminDbService adminDbService, IWebHostEnvironment environment)
        {
            _adminDbService = adminDbService;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? filtro)
        {
            ViewBag.Filtro = filtro;
            var productos = await _adminDbService.GetProductsAsync(filtro);
            return View(productos);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Categorias = await _adminDbService.GetStoreCategoriesAsync();
            return View(new ProductFormViewModel
            {
                Activo = true,
                StockMinimo = 5
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductFormViewModel model)
        {
            ViewBag.Categorias = await _adminDbService.GetStoreCategoriesAsync();
            if (!ModelState.IsValid) return View(model);

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";

            try
            {
                model.ImagenUrl = await SaveProductImageAsync(model.ImagenArchivo, model.ImagenUrl);
            }
            catch (ProductImageValidationException ex)
            {
                ModelState.AddModelError(nameof(model.ImagenArchivo), ex.UserMessage);
                return View(model);
            }

            await _adminDbService.CreateProductAsync(model, usuarioId, usuarioNombre);

            await RegistrarAuditoriaAsync(
                "Crear",
                "Inventario",
                $"Se creó el producto {model.Nombre} con stock inicial {model.Stock} y stock mínimo {model.StockMinimo}.");

            TempData["SuccessMessage"] = "Producto creado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            ViewBag.Categorias = await _adminDbService.GetStoreCategoriesAsync();
            var model = await _adminDbService.GetProductByIdAsync(id);
            if (model == null) return RedirectToAction(nameof(Index));
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductFormViewModel model)
        {
            ViewBag.Categorias = await _adminDbService.GetStoreCategoriesAsync();
            if (!ModelState.IsValid) return View(model);

            var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
            var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";

            try
            {
                model.ImagenUrl = await SaveProductImageAsync(model.ImagenArchivo, model.ImagenUrl);
            }
            catch (ProductImageValidationException ex)
            {
                ModelState.AddModelError(nameof(model.ImagenArchivo), ex.UserMessage);
                return View(model);
            }

            await _adminDbService.UpdateProductAsync(model, usuarioId, usuarioNombre);

            await RegistrarAuditoriaAsync(
                "Editar",
                "Inventario",
                $"Se actualizó el producto {model.Nombre}. Stock actual: {model.Stock}. Stock mínimo: {model.StockMinimo}.");

            TempData["SuccessMessage"] = "Producto actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFeatured(int productoId, string? filtro)
        {
            await _adminDbService.ToggleFeaturedAsync(productoId);

            await RegistrarAuditoriaAsync(
                "Editar",
                "Inventario",
                $"Se cambió el estado destacado del producto #{productoId}.");

            return RedirectToAction(nameof(Index), new { filtro });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int productoId, string? filtro)
        {
            var activo = await _adminDbService.ToggleProductStatusAsync(productoId);

            await RegistrarAuditoriaAsync(
                activo ? "Activar" : "Inactivar",
                "Inventario",
                activo
                    ? $"Se reactivó el producto #{productoId}."
                    : $"Se inactivó el producto #{productoId} para ocultarlo del catálogo.");

            TempData["SuccessMessage"] = activo
                ? "Producto reactivado correctamente."
                : "Producto inactivado correctamente.";

            return RedirectToAction(nameof(Index), new { filtro });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int productoId, string? filtro)
        {
            // Compatibilidad con formularios anteriores: esta acción ahora solo inactiva/reactiva.
            return await ToggleStatus(productoId, filtro);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePermanent(int productoId, string? filtro)
        {
            try
            {
                var productoNombre = await _adminDbService.DeleteProductPermanentlyAsync(productoId);

                await RegistrarAuditoriaAsync(
                    "Eliminar",
                    "Inventario",
                    $"Se eliminó permanentemente el producto {productoNombre}.");

                TempData["SuccessMessage"] = "Producto eliminado permanentemente.";
            }
            catch (InvalidOperationException)
            {
                TempData["ErrorMessage"] = "No se pudo eliminar el producto.";
            }

            return RedirectToAction(nameof(Index), new { filtro });
        }

        private async Task<string?> SaveProductImageAsync(IFormFile? archivo, string? currentImageUrl)
        {
            if (archivo == null || archivo.Length == 0) return string.IsNullOrWhiteSpace(currentImageUrl) ? currentImageUrl : currentImageUrl.Trim();

            if (archivo.Length > MaxProductImageBytes)
            {
                throw new ProductImageValidationException("La imagen supera el tamano maximo permitido de 2 MB.");
            }

            var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || !PermittedImageContentTypes.TryGetValue(extension, out var allowedContentTypes))
            {
                throw new ProductImageValidationException("La imagen debe estar en formato JPG, JPEG, PNG o WEBP.");
            }

            if (string.IsNullOrWhiteSpace(archivo.ContentType) || !allowedContentTypes.Contains(archivo.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                throw new ProductImageValidationException("El tipo de archivo de la imagen no es valido.");
            }

            if (!await IsValidImageSignatureAsync(archivo, extension))
            {
                throw new ProductImageValidationException("El contenido del archivo no coincide con una imagen valida.");
            }

            var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "productos");
            Directory.CreateDirectory(uploadsRoot);
            var fileName = $"producto-{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsRoot, fileName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await archivo.CopyToAsync(stream);
            return $"~/uploads/productos/{fileName}";
        }

        private static async Task<bool> IsValidImageSignatureAsync(IFormFile archivo, string extension)
        {
            await using var stream = archivo.OpenReadStream();
            var header = new byte[12];
            var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));

            return extension switch
            {
                ".jpg" or ".jpeg" => bytesRead >= 3
                    && header[0] == 0xFF
                    && header[1] == 0xD8
                    && header[2] == 0xFF,
                ".png" => bytesRead >= 8
                    && header[0] == 0x89
                    && header[1] == 0x50
                    && header[2] == 0x4E
                    && header[3] == 0x47
                    && header[4] == 0x0D
                    && header[5] == 0x0A
                    && header[6] == 0x1A
                    && header[7] == 0x0A,
                ".webp" => bytesRead >= 12
                    && header[0] == 0x52
                    && header[1] == 0x49
                    && header[2] == 0x46
                    && header[3] == 0x46
                    && header[8] == 0x57
                    && header[9] == 0x45
                    && header[10] == 0x42
                    && header[11] == 0x50,
                _ => false
            };
        }

        private sealed class ProductImageValidationException : Exception
        {
            public ProductImageValidationException(string userMessage)
                : base(userMessage)
            {
                UserMessage = userMessage;
            }

            public string UserMessage { get; }
        }

        [HttpGet]
        public async Task<IActionResult> Movements()
        {
            var movimientos = await _adminDbService.GetInventoryMovementsAsync();
            return View(movimientos);
        }

        [HttpGet]
        public async Task<IActionResult> RegisterMovement()
        {
            var productos = await _adminDbService.GetActiveProductsForSelectAsync();
            ViewBag.Productos = productos.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = p.ProductoId.ToString(),
                Text = $"{p.Nombre} (Stock actual: {p.Stock})"
            }).ToList();
            return View(new InventoryMovementFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterMovement(InventoryMovementFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var productos = await _adminDbService.GetActiveProductsForSelectAsync();
                ViewBag.Productos = productos.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = p.ProductoId.ToString(),
                    Text = $"{p.Nombre} (Stock actual: {p.Stock})"
                }).ToList();
                return View(model);
            }

            try
            {
                var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
                var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";
                await _adminDbService.RegisterInventoryMovementAsync(model, usuarioId, usuarioNombre);

                await RegistrarAuditoriaAsync(
                    "Movimiento",
                    "Inventario",
                    $"Se registró movimiento de inventario tipo {model.TipoMovimiento} para el producto #{model.ProductoId} con cantidad {model.Cantidad}.");

                TempData["SuccessMessage"] = "Movimiento registrado correctamente.";
                return RedirectToAction(nameof(Movements));
            }
            catch (InvalidOperationException)
            {
                ModelState.AddModelError(string.Empty, "No se pudo registrar el movimiento de inventario. Revise los datos e intente nuevamente.");
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error al registrar el movimiento. Intente nuevamente.");
            }

            var productosRetry = await _adminDbService.GetActiveProductsForSelectAsync();
            ViewBag.Productos = productosRetry.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = p.ProductoId.ToString(),
                Text = $"{p.Nombre} (Stock actual: {p.Stock})"
            }).ToList();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> TransformStock()
        {
            var productos = await _adminDbService.GetActiveProductsForSelectAsync();
            ViewBag.Productos = productos.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = p.ProductoId.ToString(),
                Text = $"{p.Nombre} (Stock actual: {p.Stock})"
            }).ToList();
            return View(new StockTransformationFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransformStock(StockTransformationFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var productos = await _adminDbService.GetActiveProductsForSelectAsync();
                ViewBag.Productos = productos.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = p.ProductoId.ToString(),
                    Text = $"{p.Nombre} (Stock actual: {p.Stock})"
                }).ToList();
                return View(model);
            }

            try
            {
                var usuarioId = HttpContext.Session.GetInt32("UserId") ?? 0;
                var usuarioNombre = HttpContext.Session.GetString("UserFullName") ?? "Administrador";
                await _adminDbService.RegisterStockTransformationAsync(model, usuarioId, usuarioNombre);

                await RegistrarAuditoriaAsync(
                    "Transformacion",
                    "Inventario",
                    $"Se transformó stock del producto #{model.ProductoOrigenId} ({model.CantidadOrigen}u) al producto #{model.ProductoDestinoId} ({model.CantidadDestino}u).");

                TempData["SuccessMessage"] = "Transformación registrada correctamente.";
                return RedirectToAction(nameof(Movements));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
            catch (Exception)
            {
                ModelState.AddModelError(string.Empty, "Ocurrió un error al registrar la transformación. Intente nuevamente.");
            }

            var productosRetry = await _adminDbService.GetActiveProductsForSelectAsync();
            ViewBag.Productos = productosRetry.Select(p => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = p.ProductoId.ToString(),
                Text = $"{p.Nombre} (Stock actual: {p.Stock})"
            }).ToList();
            return View(model);
        }

        private async Task RegistrarAuditoriaAsync(string accion, string modulo, string descripcion)
        {
            await _adminDbService.CreateAuditLogAsync(
                HttpContext.Session.GetInt32("UserId"),
                HttpContext.Session.GetString("UserFullName"),
                HttpContext.Session.GetString("UserEmail"),
                HttpContext.Session.GetString("UserRole"),
                accion,
                modulo,
                descripcion,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString());
        }
    }
}
