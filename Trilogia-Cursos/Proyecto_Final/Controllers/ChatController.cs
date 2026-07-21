using Microsoft.AspNetCore.Mvc;
using Proyecto_Final.Services;
using Microsoft.AspNetCore.SignalR;
using Proyecto_Final.Hubs;

namespace Proyecto_Final.Controllers
{
    public class ChatController : Controller
    {
        private readonly AdminDbService _adminDbService;
        private readonly IHubContext<ChatHub> _hubContext;

        public ChatController(
            AdminDbService adminDbService,
            IHubContext<ChatHub> hubContext)
        {
            _adminDbService = adminDbService;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var usuarioIdActual = HttpContext.Session.GetInt32("UserId");

            if (usuarioIdActual == null || usuarioIdActual <= 0)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Debe iniciar sesión para utilizar el chat."
                });
            }

            var usuarios = await _adminDbService
                .GetChatUsersAsync(usuarioIdActual.Value);

            return Json(new
            {
                success = true,
                users = usuarios
            });
        }


        [HttpPost]
        public async Task<IActionResult> OpenConversation(int userId)
        {
            var usuarioActualId = HttpContext.Session.GetInt32("UserId");

            if (usuarioActualId == null || usuarioActualId <= 0)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Debe iniciar sesión para utilizar el chat."
                });
            }

            if (userId <= 0 || userId == usuarioActualId.Value)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El usuario seleccionado no es válido."
                });
            }

            try
            {
                var conversacionId =
                    await _adminDbService.GetOrCreateChatConversationAsync(
                        usuarioActualId.Value,
                        userId
                    );

                return Json(new
                {
                    success = true,
                    conversationId = conversacionId
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "No fue posible abrir la conversación."
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(
            int conversationId,
            string content)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId");

            if (usuarioId == null || usuarioId <= 0)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Debe iniciar sesión."
                });
            }

            if (conversationId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "La conversación no es válida."
                });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Escriba un mensaje."
                });
            }

            if (content.Length > 1000)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El mensaje no puede superar los 1000 caracteres."
                });
            }

            try
            {
                var mensaje = await _adminDbService.SendChatMessageAsync(
                    conversationId,
                    usuarioId.Value,
                    content.Trim()
                );

                if (mensaje == null)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "No fue posible guardar el mensaje."
                    });
                }

                await _hubContext.Clients
                    .Group($"chat-{conversationId}")
                    .SendAsync("ReceiveMessage", mensaje);

                return Json(new
                {
                    success = true,
                    message = mensaje
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "No fue posible enviar el mensaje.",
                    detail = ex.Message
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(int conversationId)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId");

            if (usuarioId == null || usuarioId <= 0)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Debe iniciar sesión."
                });
            }

            if (conversationId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "La conversación no es válida."
                });
            }

            try
            {
                var mensajes = await _adminDbService.GetChatMessagesAsync(
                    conversationId,
                    usuarioId.Value
                );

                return Json(new
                {
                    success = true,
                    currentUserId = usuarioId.Value,
                    messages = mensajes
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "No fue posible cargar los mensajes."
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDepartments()
        {
            var departamentos =
                await _adminDbService.GetChatDepartmentsAsync();

            return Json(new
            {
                success = true,
                departments = departamentos
            });
        }

        [HttpPost]
        public async Task<IActionResult> SendDepartmentMessage(
            int profileId,
            string content)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId");

            if (usuarioId == null || usuarioId <= 0)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Debe iniciar sesión."
                });
            }

            if (profileId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El departamento no es válido."
                });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Escriba un mensaje."
                });
            }

            if (content.Length > 1000)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El mensaje no puede superar los 1000 caracteres."
                });
            }

            try
            {
                var mensaje =
                    await _adminDbService.SendDepartmentMessageAsync(
                        profileId,
                        usuarioId.Value,
                        content.Trim()
                    );

                if (mensaje == null)
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = "No fue posible guardar el mensaje."
                    });
                }

                await _hubContext.Clients
                    .Group($"department-{profileId}")
                    .SendAsync("ReceiveDepartmentMessage", mensaje);

                return Json(new
                {
                    success = true,
                    message = mensaje
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "No fue posible enviar el mensaje.",
                    detail = ex.Message
                });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetDepartmentMessages(int profileId)
        {
            var usuarioId = HttpContext.Session.GetInt32("UserId");

            if (usuarioId == null || usuarioId <= 0)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Debe iniciar sesión."
                });
            }

            if (profileId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "El departamento no es válido."
                });
            }

            try
            {
                var mensajes =
                    await _adminDbService.GetDepartmentMessagesAsync(
                        profileId
                    );

                return Json(new
                {
                    success = true,
                    currentUserId = usuarioId.Value,
                    messages = mensajes
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "No fue posible cargar los mensajes del departamento.",
                    detail = ex.Message
                });
            }
        }



    }
}