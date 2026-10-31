using System.Data;
using Proyecto_FinalAPI.Models;

namespace Proyecto_FinalAPI.Services.Mobile
{
    public interface INotificationsMobileDbService
    {
        Task<MobileNotificationsResponse> GetAsync(int userId, CancellationToken cancellationToken = default);
        Task<int> MarkReadAsync(int userId, int? notificationId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Bandeja de avisos del usuario (migración 0221). Los procedimientos
    /// filtran por usuario: un identificador ajeno no lee ni marca nada.
    /// </summary>
    public sealed class NotificationsMobileDbService : MobileDbServiceBase, INotificationsMobileDbService
    {
        public NotificationsMobileDbService(IConfiguration configuration) : base(configuration) { }

        public async Task<MobileNotificationsResponse> GetAsync(int userId, CancellationToken cancellationToken = default)
        {
            var items = await QueryAsync("dbo.sp_Notificaciones_Listar",
                p => p.Add("@UsuarioId", SqlDbType.Int).Value = userId,
                r => new MobileNotification
                {
                    NotificacionId = r.Int("NotificacionId"),
                    Tipo = r.Str("Tipo"),
                    Titulo = r.Str("Titulo"),
                    Mensaje = r.Str("Mensaje"),
                    EntidadTipo = r.Str("EntidadTipo"),
                    EntidadId = r.Int("EntidadId"),
                    Leida = r.Bool("Leida"),
                    FechaCreacion = r.Date("FechaCreacion")
                }, cancellationToken);

            var unread = 0;
            await ReadAsync("dbo.sp_Notificaciones_ContarNoLeidas",
                p => p.Add("@UsuarioId", SqlDbType.Int).Value = userId,
                async r => { if (await r.ReadAsync(cancellationToken)) unread = r.Int("NoLeidas"); },
                cancellationToken);

            return new MobileNotificationsResponse { NoLeidas = unread, Items = items };
        }

        public async Task<int> MarkReadAsync(int userId, int? notificationId, CancellationToken cancellationToken = default)
        {
            var marked = 0;
            await ReadAsync("dbo.sp_Notificaciones_MarcarLeidas", p =>
            {
                p.Add("@UsuarioId", SqlDbType.Int).Value = userId;
                p.Add("@NotificacionId", SqlDbType.Int).Value = notificationId.HasValue ? notificationId.Value : DBNull.Value;
            }, async r => { if (await r.ReadAsync(cancellationToken)) marked = r.Int("Marcadas"); }, cancellationToken);
            return marked;
        }
    }
}
