using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Proyecto_Final.Controllers;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

// Seguridad / propiedad del kilometraje (migración 0312): un chofer no cierra ni ve la jornada de otro.
public sealed class MileageOwnershipTests
{
    [Fact]
    public async Task CloseMileage_WithoutAnAuthenticatedActorIsForbiddenAndNeverReachesTheDatabase()
    {
        var permissions = new Mock<IRolePermissionService>(MockBehavior.Strict);
        var controller = BuildController(permissions, userId: null);

        var result = await controller.CloseMileage(10, 500);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, view.StatusCode);
        permissions.VerifyNoOtherCalls();
    }

    [Fact]
    public void Mileage_ListAndCloseDecideOwnershipWithTheSameCapability()
    {
        var controller = File.ReadAllText(SourcePath("Proyecto_Final", "Controllers", "FleetController.cs"));
        var service = File.ReadAllText(SourcePath("Proyecto_Final", "Services", "FleetDbService.cs"));

        // La lista y el cierre dependen de la misma capacidad: Administrador o FLOTA_KILOMETRAJE_ADMIN.
        Assert.Contains("FLOTA_KILOMETRAJE_ADMIN", controller, StringComparison.Ordinal);
        Assert.Contains("await CanAdministerMileageAsync() ? null : HttpContext.Session.GetInt32(\"UserId\")", controller, StringComparison.Ordinal);
        Assert.Contains("_fleet.CloseMileageAsync(kilometrajeId, kmFinal, actorUsuarioId, await CanAdministerMileageAsync())", controller, StringComparison.Ordinal);
        // El actor viaja siempre a la base; ya no hay una sobrecarga de dos parámetros que lo omita.
        Assert.Contains("\"@ActorUsuarioId\"", service, StringComparison.Ordinal);
        Assert.Contains("\"@PuedeAdministrar\"", service, StringComparison.Ordinal);
        Assert.DoesNotContain("CloseMileageAsync(int kilometrajeId, int kmFinal)", service, StringComparison.Ordinal);
        // El filtro de la lista ya no depende del nombre del perfil «Chofer».
        Assert.DoesNotContain("esChofer", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration0312_ChecksOwnershipInsideAnAtomicTransaction()
    {
        var sql = File.ReadAllText(SourcePath("database", "migrations", "0312_kilometraje_propiedad.sql"));
        var procedure = sql[sql.IndexOf("CREATE OR ALTER PROCEDURE dbo.sp_Kilometraje_Cerrar", StringComparison.Ordinal)..];

        // La transacción empieza antes de leer la fila y la lectura toma UPDLOCK + HOLDLOCK.
        Assert.True(procedure.IndexOf("BEGIN TRANSACTION", StringComparison.Ordinal) < procedure.IndexOf("WITH (UPDLOCK, HOLDLOCK)", StringComparison.Ordinal));
        // Sin actor se rechaza; la jornada ajena responde «no encontrada»; el segundo cierre responde 53044.
        Assert.Contains("THROW 53047", procedure, StringComparison.Ordinal);
        Assert.Contains("ISNULL(@ChoferUsuarioId, 0) <> @ActorUsuarioId", procedure, StringComparison.Ordinal);
        Assert.Contains("AND KmFinal IS NULL", procedure, StringComparison.Ordinal);
        Assert.Contains("IF @@ROWCOUNT <> 1 THROW 53044", procedure, StringComparison.Ordinal);
        Assert.Contains("FLOTA_KILOMETRAJE_ADMIN", sql, StringComparison.Ordinal);
    }

    private static FleetController BuildController(Mock<IRolePermissionService> permissions, int? userId)
    {
        var controller = new FleetController(null!, null!, null!, permissions.Object, NullLogger<FleetController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = new FakeSession() } }
        };
        if (userId.HasValue) controller.HttpContext.Session.SetInt32("UserId", userId.Value);
        return controller;
    }

    private static string SourcePath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Proyecto_Final.slnx")))
            directory = directory.Parent;
        var root = directory?.FullName ?? throw new DirectoryNotFoundException("No se encontró la raíz del repositorio.");
        return Path.Combine([root, .. parts]);
    }

    private sealed class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = new();
        public bool IsAvailable => true;
        public string Id => "test";
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
