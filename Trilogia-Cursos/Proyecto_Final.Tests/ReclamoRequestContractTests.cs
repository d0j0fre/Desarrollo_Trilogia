using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Proyecto_Final.Models.Admin;
using Proyecto_Final.Services;

namespace Proyecto_Final.Tests;

public sealed class ReclamoRequestContractTests
{
    [Fact]
    public void Missing_request_is_invalid_and_is_not_regenerated_by_validation()
    {
        var model = Draft();
        model.RequestId = Guid.Empty;
        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(model, new ValidationContext(model), errors, true));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(model.RequestId)));
        Assert.Equal(Guid.Empty, model.RequestId);
    }

    [Fact]
    public void Form_round_trip_and_failed_validation_preserve_original_request_and_content()
    {
        var original = Draft();
        original.Asunto = string.Empty;
        var posted = JsonSerializer.Deserialize<ReclamoFormViewModel>(JsonSerializer.Serialize(original))!;
        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(posted, new ValidationContext(posted), errors, true));
        Assert.Equal(original.RequestId, posted.RequestId);
        Assert.Equal(original.Descripcion, posted.Descripcion);
        Assert.Equal(original.UsuarioId, posted.UsuarioId);
        Assert.Equal(original.FacturaId, posted.FacturaId);
    }

    [Fact]
    public void Create_command_uses_original_login_context_and_explicit_SQL_types()
    {
        using var connection = new SqlConnection();
        var login = Login();
        var model = Draft();
        using var command = ReclamosDbService.BuildCreateCommand(connection, model, login.UserId, login);

        Assert.Equal(ConnectionState.Closed, connection.State);
        Assert.Equal("dbo.sp_Reclamos_Create", command.CommandText);
        Assert.Equal(CommandType.StoredProcedure, command.CommandType);
        AssertParameter(command, "@RequestId", SqlDbType.UniqueIdentifier, model.RequestId);
        AssertParameter(command, "@LoginId", SqlDbType.UniqueIdentifier, login.LoginId);
        AssertParameter(command, "@UsuarioId", SqlDbType.Int, login.UserId);
        AssertParameter(command, "@ClienteUsuarioId", SqlDbType.Int, model.UsuarioId);
        AssertParameter(command, "@PerfilId", SqlDbType.Int, login.ProfileId);
        AssertParameter(command, "@SecurityStamp", SqlDbType.UniqueIdentifier, login.SecurityStamp);
        Assert.Equal(login.BindingHash.ToArray(), Assert.IsType<byte[]>(command.Parameters["@BindingHash"].Value));
        Assert.Equal(SqlDbType.Binary, command.Parameters["@BindingHash"].SqlDbType);
        Assert.Equal(32, command.Parameters["@BindingHash"].Size);
        foreach (var name in new[] { "@IssuedUtc", "@ExpiresUtc" })
        {
            Assert.Equal(SqlDbType.DateTime2, command.Parameters[name].SqlDbType);
            Assert.Equal(7, command.Parameters[name].Scale);
        }
        Assert.Equal(login.IssuedUtc, command.Parameters["@IssuedUtc"].Value);
        Assert.Equal(login.ExpiresUtc, command.Parameters["@ExpiresUtc"].Value);
        Assert.False(command.Parameters.Contains("@AgenteNombre"));
        Assert.False(command.Parameters.Contains("@AgenteId"));
        Assert.False(command.Parameters.Contains("@ExpectedSecurityStamp"));
    }

    [Fact]
    public void Unknown_result_retry_builds_the_same_request_and_payload_without_rotating_UUID()
    {
        using var connection = new SqlConnection();
        var login = Login();
        var model = Draft();
        using var first = ReclamosDbService.BuildCreateCommand(connection, model, login.UserId, login);
        using var retry = ReclamosDbService.BuildCreateCommand(connection, model, login.UserId, login);

        Assert.Equal(first.Parameters.Count, retry.Parameters.Count);
        foreach (SqlParameter parameter in first.Parameters)
        {
            var repeated = retry.Parameters[parameter.ParameterName];
            Assert.Equal(parameter.SqlDbType, repeated.SqlDbType);
            if (parameter.Value is byte[] bytes) Assert.Equal(bytes, Assert.IsType<byte[]>(repeated.Value));
            else Assert.Equal(parameter.Value, repeated.Value);
        }
        Assert.Equal(model.RequestId, retry.Parameters["@RequestId"].Value);
        Assert.Equal("Descripción del reclamo", retry.Parameters["@Descripcion"].Value);
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public void Missing_request_is_rejected_before_opening_SQL()
    {
        using var connection = new SqlConnection();
        var model = Draft();
        model.RequestId = Guid.Empty;
        var login = Login();

        Assert.Throws<ArgumentException>(() => ReclamosDbService.BuildCreateCommand(connection, model, login.UserId, login));
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public void Different_actor_is_rejected_before_opening_SQL()
    {
        using var connection = new SqlConnection();
        var login = Login();

        Assert.Throws<UnauthorizedAccessException>(() =>
            ReclamosDbService.BuildCreateCommand(connection, Draft(), login.UserId + 1, login));
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public void Noncanonical_login_lifetime_is_rejected_before_opening_SQL()
    {
        using var connection = new SqlConnection();
        var login = Login();

        Assert.Throws<UnauthorizedAccessException>(() => ReclamosDbService.BuildCreateCommand(connection,
            Draft(), login.UserId, login with { ExpiresUtc = login.ExpiresUtc.AddTicks(1) }));
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData("42")]
    [InlineData(42L)]
    public void Missing_or_unexpected_result_cannot_be_reported_as_created(object? result) =>
        Assert.Throws<InvalidOperationException>(() => ReclamosDbService.ReadCreatedId(result));

    [Fact]
    public void Database_null_result_cannot_be_reported_as_created() =>
        Assert.Throws<InvalidOperationException>(() => ReclamosDbService.ReadCreatedId(DBNull.Value));

    [Fact]
    public void A_committed_receipt_can_return_the_same_existing_complaint_identifier()
    {
        Assert.Equal(42, ReclamosDbService.ReadCreatedId(42));
        Assert.Equal(42, ReclamosDbService.ReadCreatedId(42));
    }

    private static ReclamoFormViewModel Draft() => new()
    {
        RequestId = Guid.NewGuid(), UsuarioId = 1042, FacturaId = 20, PedidoId = 30,
        Asunto = "Asunto QA", Descripcion = "  Descripción del reclamo  ", Categoria = "Servicio", Prioridad = "Media"
    };

    private static OriginalLoginContext Login()
    {
        var issued = new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
        return new(Guid.NewGuid(), 1040, 7, Guid.NewGuid(), issued, issued.AddHours(8), new byte[32]);
    }

    private static void AssertParameter(SqlCommand command, string name, SqlDbType type, object value)
    {
        Assert.Equal(type, command.Parameters[name].SqlDbType);
        Assert.Equal(value, command.Parameters[name].Value);
    }
}
