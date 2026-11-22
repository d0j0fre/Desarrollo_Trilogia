namespace Proyecto_Final.Tests;

/// <summary>
/// POST-qa1-06: al crear o editar un rol con un nombre repetido, la pantalla decía solo «No se pudo crear el rol…».
/// Ahora el controlador traduce los errores 50002 y 50007 del procedimiento a un mensaje claro en el campo Nombre.
/// </summary>
public sealed class RolNombreRepetidoMensajeTests
{
    private static string Controller() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Proyecto_Final", "Controllers", "RolesController.cs"));

    [Fact]
    public void CreateRole_ExplainsADuplicatedName()
    {
        var source = Controller();

        Assert.Contains("Number: 50002", source);
        Assert.Contains("Ya existe un rol con ese nombre.", source);
    }

    [Fact]
    public void EditRole_ExplainsADuplicatedName()
    {
        var source = Controller();

        Assert.Contains("Number: 50007", source);
        Assert.Contains("Ya existe otro rol con ese nombre.", source);
    }
}
