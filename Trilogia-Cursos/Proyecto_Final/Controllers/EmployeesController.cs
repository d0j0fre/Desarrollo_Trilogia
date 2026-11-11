using Microsoft.AspNetCore.Mvc;
using System.Data;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Filters;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Controllers
{
    [AdminAuthorize("Empleados")]
    public class EmployeesController : Controller
    {
        private readonly IEmployeesService _employeesDbService;
        private readonly AdminDbService _adminDbService;
        private readonly ILogger<EmployeesController> _logger;

        public EmployeesController(IEmployeesService employeesDbService, AdminDbService adminDbService, ILogger<EmployeesController> logger)
        {
            _employeesDbService = employeesDbService;
            _adminDbService = adminDbService;
            _logger = logger;
        }

        [HttpGet]
        [AdminAuthorize("Empleados", "EMPLEADOS_VER")]
        public async Task<IActionResult> Index(string? buscar, string? estado)
        {
            var model = new EmployeeFilterViewModel
            {
                Buscar = buscar,
                Estado = estado,
                Empleados = await _employeesDbService.GetEmployeesAsync(buscar, estado)
            };

            return View(model);
        }

        [HttpGet]
        [AdminAuthorize("Empleados", "EMPLEADOS_CREAR")]
        public async Task<IActionResult> Create()
        {
            var model = new EmployeeFormViewModel
            {
                Activo = true,
                FechaContratacion = DateTime.Today,
                RolesDisponibles = await _employeesDbService.GetEmployeeRoleSelectListAsync(ActorRole)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Empleados", "EMPLEADOS_CREAR")]
        public async Task<IActionResult> Create(EmployeeFormViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Contrasena))
            {
                ModelState.AddModelError(nameof(model.Contrasena), "La contraseña es obligatoria para crear el empleado.");
            }

            if (model.Salario < 0)
            {
                ModelState.AddModelError(nameof(model.Salario), "El salario no puede ser negativo.");
            }

            // G2-22: el rol se valida en el servidor, no solo en la lista que ve el usuario.
            await ValidateAssignableRoleAsync(model.PerfilId);

            if (!ModelState.IsValid)
            {
                model.RolesDisponibles = await _employeesDbService.GetEmployeeRoleSelectListAsync(ActorRole, model.PerfilId);
                return View(model);
            }

            try
            {
                var empleadoId = await _employeesDbService.CreateEmployeeAsync(
                    model,
                    HttpContext.Session.GetInt32("UserId"),
                    HttpContext.Session.GetString("UserFullName"));

                await RegistrarAuditoriaAsync(
                    "Crear",
                    "Empleados",
                    $"Se registró el empleado {model.NombreCompleto} con identificador #{empleadoId}.");

                TempData["SuccessMessage"] = "Empleado registrado correctamente.";
                return RedirectToAction(nameof(Details), new { id = empleadoId });
            }
            catch (SqlException exception) when (exception.Number is 2601 or 2627)
            {
                _logger.LogWarning(exception, "Registro duplicado al crear empleado.");
                ModelState.AddModelError(string.Empty, "La identificación, el correo o la fecha de vigencia ya se encuentran registrados.");
            }
            catch (Exception exception) when (BusinessErrors.TryGetMessage(exception, out _) || BusinessErrors.IsApplicationRule(exception))
            {
                // G2-21: la causa («Ya existe un usuario registrado con ese correo.») se muestra tal cual.
                _logger.LogWarning(exception, "Regla de negocio al registrar el empleado.");
                ModelState.AddModelError(string.Empty, BusinessErrors.TryGetMessage(exception, out var message) ? message : exception.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No fue posible registrar el empleado.");
                ModelState.AddModelError(string.Empty, "Ocurrió un error al registrar el empleado. Intente nuevamente.");
            }

            model.RolesDisponibles = await _employeesDbService.GetEmployeeRoleSelectListAsync(ActorRole, model.PerfilId);
            return View(model);
        }

        [HttpGet]
        [AdminAuthorize("Empleados", "EMPLEADOS_EDITAR")]
        public async Task<IActionResult> Edit(int id)
        {
            var model = await _employeesDbService.GetEmployeeFormByIdAsync(id);

            if (model == null)
            {
                TempData["ErrorMessage"] = "No se encontró el empleado solicitado.";
                return RedirectToAction(nameof(Index));
            }

            if (!EmployeeRolePolicy.CanManage(ActorRole, model.Rol))
            {
                TempData["ErrorMessage"] = ManageDeniedMessage;
                return RedirectToAction(nameof(Index));
            }

            model.RolesDisponibles = await _employeesDbService.GetEmployeeRoleSelectListAsync(ActorRole, model.PerfilId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Empleados", "EMPLEADOS_EDITAR")]
        public async Task<IActionResult> Edit(EmployeeFormViewModel model)
        {
            if (model.FechaVigenciaSalario.HasValue && model.FechaVigenciaSalario.Value.Date <= new DateTime(1900, 1, 1))
                ModelState.AddModelError(nameof(model.FechaVigenciaSalario), "La fecha de vigencia debe ser posterior al 1 de enero de 1900.");
            if (string.IsNullOrWhiteSpace(model.RowVersionBase64))
            {
                ModelState.AddModelError(nameof(model.RowVersionBase64), "La versión del expediente es obligatoria para editar.");
            }

            if (model.Salario < 0)
            {
                ModelState.AddModelError(nameof(model.Salario), "El salario no puede ser negativo.");
            }

            // G2-22: la cuenta destino se lee de la base (no del formulario) y se valida quién puede tocarla.
            var current = await _employeesDbService.GetEmployeeDetailAsync(model.EmpleadoId);
            if (current == null)
            {
                TempData["ErrorMessage"] = "No se encontró el empleado solicitado.";
                return RedirectToAction(nameof(Index));
            }

            if (!EmployeeRolePolicy.CanManage(ActorRole, current.Rol))
            {
                TempData["ErrorMessage"] = ManageDeniedMessage;
                return RedirectToAction(nameof(Index));
            }

            if (current.UsuarioId == ActorUserId)
            {
                if (!model.Activo)
                    ModelState.AddModelError(string.Empty, SelfDeactivationMessage);
                if (model.PerfilId != current.PerfilId)
                    ModelState.AddModelError(string.Empty, "No puede cambiar su propio rol.");
            }

            await ValidateAssignableRoleAsync(model.PerfilId);

            if (!ModelState.IsValid)
            {
                model.Rol = current.Rol;
                model.RolesDisponibles = await _employeesDbService.GetEmployeeRoleSelectListAsync(ActorRole, model.PerfilId);
                return View(model);
            }

            try
            {
                await _employeesDbService.UpdateEmployeeAsync(
                    model,
                    HttpContext.Session.GetInt32("UserId"),
                    HttpContext.Session.GetString("UserFullName"));

                await RegistrarAuditoriaAsync(
                    "Editar",
                    "Empleados",
                    $"Se actualizó la información laboral del empleado {model.NombreCompleto}.");

                TempData["SuccessMessage"] = "Empleado actualizado correctamente.";
                return RedirectToAction(nameof(Details), new { id = model.EmpleadoId });
            }
            catch (SqlException exception) when (exception.Number is 2601 or 2627)
            {
                _logger.LogWarning(exception, "Registro duplicado al editar empleado.");
                ModelState.AddModelError(string.Empty, "La identificación o el correo ya se encuentran registrados.");
            }
            catch (DBConcurrencyException exception)
            {
                _logger.LogWarning(exception, "Conflicto de concurrencia al actualizar el empleado {EmployeeId}.", model.EmpleadoId);
                ModelState.AddModelError(string.Empty, "El expediente cambió mientras lo editaba. Recargue la página e intente nuevamente.");
            }
            catch (Exception exception) when (BusinessErrors.TryGetMessage(exception, out _) || BusinessErrors.IsApplicationRule(exception))
            {
                _logger.LogWarning(exception, "Regla de negocio al actualizar el empleado {EmployeeId}.", model.EmpleadoId);
                ModelState.AddModelError(string.Empty, BusinessErrors.TryGetMessage(exception, out var message) ? message : exception.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "No fue posible actualizar el empleado {EmployeeId}.", model.EmpleadoId);
                ModelState.AddModelError(string.Empty, "Ocurrió un error al actualizar el empleado. Intente nuevamente.");
            }

            model.RolesDisponibles = await _employeesDbService.GetEmployeeRoleSelectListAsync(ActorRole, model.PerfilId);
            return View(model);
        }

        [HttpGet]
        [AdminAuthorize("Empleados", "EMPLEADOS_VER")]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _employeesDbService.GetEmployeeDetailAsync(id);

            if (model == null)
            {
                TempData["ErrorMessage"] = "No se encontró el empleado solicitado.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Empleados", "EMPLEADOS_EDITAR")]
        public async Task<IActionResult> ToggleStatus(int empleadoId, string? buscar, string? estado)
        {
            if (empleadoId <= 0)
            {
                TempData["ErrorMessage"] = "La solicitud no es válida.";
                return RedirectToAction(nameof(Index), new { buscar, estado });
            }

            // G2-22: nadie se inactiva a sí mismo y solo el Administrador toca cuentas de Administrador o Gerente.
            var target = await _employeesDbService.GetEmployeeDetailAsync(empleadoId);
            if (target == null)
            {
                TempData["ErrorMessage"] = "No se encontró el empleado solicitado.";
                return RedirectToAction(nameof(Index), new { buscar, estado });
            }

            if (target.UsuarioId == ActorUserId)
            {
                TempData["ErrorMessage"] = SelfDeactivationMessage;
                return RedirectToAction(nameof(Index), new { buscar, estado });
            }

            if (!EmployeeRolePolicy.CanManage(ActorRole, target.Rol))
            {
                TempData["ErrorMessage"] = ManageDeniedMessage;
                return RedirectToAction(nameof(Index), new { buscar, estado });
            }

            try
            {
                var nuevoEstado = await _employeesDbService.ToggleEmployeeStatusAsync(empleadoId);

                await RegistrarAuditoriaAsync(
                    nuevoEstado ? "Activar" : "Inactivar",
                    "Empleados",
                    $"Se cambió el estado del empleado #{empleadoId}. Nuevo estado: {(nuevoEstado ? "Activo" : "Inactivo")}.");

                TempData["SuccessMessage"] = nuevoEstado
                    ? "Empleado reactivado correctamente."
                    : "Empleado inactivado correctamente.";
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "No fue posible cambiar el estado del empleado {EmployeeId}.", empleadoId);
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(exception, "Ocurrió un error al cambiar el estado del empleado.");
            }

            return RedirectToAction(nameof(Index), new { buscar, estado });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Empleados", "EMPLEADOS_TAREAS")]
        public async Task<IActionResult> CreateTask(EmployeeTaskFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Debe completar correctamente los datos de la tarea.";
                return RedirectToAction(nameof(Details), new { id = model.EmpleadoId });
            }

            try
            {
                var tareaId = await _employeesDbService.CreateEmployeeTaskAsync(
                    model,
                    HttpContext.Session.GetInt32("UserId"),
                    HttpContext.Session.GetString("UserFullName"));

                await RegistrarAuditoriaAsync(
                    "Asignar tarea",
                    "Empleados",
                    $"Se asignó la tarea #{tareaId} al empleado #{model.EmpleadoId}.");

                TempData["SuccessMessage"] = "Tarea asignada correctamente.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "No fue posible asignar la tarea.";
            }

            return RedirectToAction(nameof(Details), new { id = model.EmpleadoId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Empleados", "EMPLEADOS_TAREAS")]
        public async Task<IActionResult> UpdateTaskStatus(EmployeeTaskStatusViewModel model)
        {
            if (model.TareaId <= 0 || model.EmpleadoId <= 0 || string.IsNullOrWhiteSpace(model.Estado))
            {
                TempData["ErrorMessage"] = "La solicitud no es válida.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await _employeesDbService.UpdateEmployeeTaskStatusAsync(model.TareaId, model.Estado);

                await RegistrarAuditoriaAsync(
                    "Actualizar tarea",
                    "Empleados",
                    $"Se actualizó la tarea #{model.TareaId} al estado {model.Estado}.");

                TempData["SuccessMessage"] = "Estado de la tarea actualizado.";
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "No fue posible actualizar la tarea.";
            }

            return RedirectToAction(nameof(Details), new { id = model.EmpleadoId });
        }

        [HttpGet]
        public async Task<IActionResult> LeaveRequests(string? estado)
        {
            ViewBag.Estado = estado;
            var requests = await _employeesDbService.GetEmployeeLeaveRequestsAsync(estado);
            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminAuthorize("Empleados", "EMPLEADOS_SOLICITUDES")]
        public async Task<IActionResult> UpdateLeaveRequest(EmployeeLeaveRequestDecisionViewModel model, string? estadoFiltro)
        {
            if (model.SolicitudId <= 0 || string.IsNullOrWhiteSpace(model.Estado))
            {
                TempData["ErrorMessage"] = "La solicitud no es válida.";
                return RedirectToAction(nameof(LeaveRequests), new { estado = estadoFiltro });
            }

            try
            {
                await _employeesDbService.UpdateEmployeeLeaveRequestStatusAsync(
                    model,
                    HttpContext.Session.GetInt32("UserId"),
                    HttpContext.Session.GetString("UserFullName"));

                await RegistrarAuditoriaAsync(
                    "Resolver solicitud",
                    "Empleados",
                    $"Se actualizó la solicitud de días libres #{model.SolicitudId} al estado {model.Estado}.");

                TempData["SuccessMessage"] = "Solicitud actualizada correctamente.";
            }
            catch (Exception exception)
            {
                // G2-24: el procedimiento rechaza las transiciones no válidas y explica por qué.
                _logger.LogWarning(exception, "No fue posible actualizar la solicitud de licencia {SolicitudId}.", model.SolicitudId);
                TempData["ErrorMessage"] = BusinessErrors.MessageOr(exception, "No fue posible actualizar la solicitud.");
            }

            return RedirectToAction(nameof(LeaveRequests), new { estado = estadoFiltro });
        }

        private const string ManageDeniedMessage = "No tiene permiso para modificar esa cuenta: solo puede administrar cuentas de un rol inferior al suyo.";
        private const string SelfDeactivationMessage = "No puede inactivar su propia cuenta.";

        private string? ActorRole => HttpContext.Session.GetString("UserRole");
        private int ActorUserId => HttpContext.Session.GetInt32("UserId") ?? 0;

        /// <summary>El rol elegido debe existir y ser de nivel menor al de quien registra (el Administrador puede asignar cualquiera).</summary>
        private async Task ValidateAssignableRoleAsync(int perfilId)
        {
            var role = (await _employeesDbService.GetEmployeeRolesAsync()).FirstOrDefault(candidate => candidate.PerfilId == perfilId);
            if (role == null || !EmployeeRolePolicy.CanAssign(ActorRole, role.Nombre))
            {
                ModelState.AddModelError(nameof(EmployeeFormViewModel.PerfilId), "No tiene permiso para asignar ese rol.");
            }
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
