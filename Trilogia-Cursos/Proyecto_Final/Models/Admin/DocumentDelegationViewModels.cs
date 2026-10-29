using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin;

public static class DocumentOperations
{
    public static IReadOnlyDictionary<string, string> Labels { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Read"] = "Ver ficha e historial", ["Edit"] = "Editar metadatos y estado", ["Version"] = "Publicar versiones",
        ["Download"] = "Descargar archivos", ["Alert"] = "Gestionar alertas", ["Create"] = "Clasificar documentos nuevos"
    };
    public static string Label(string value) => Labels.TryGetValue(value, out var label) ? label : "Operación no reconocida";
}
public sealed record DocumentDelegationRow(int Department, string DepartmentName, int Grantee, string GranteeName,
    string Operation, bool Enabled, long Revision, DateTime ExpiresUtc, bool Effective);
public sealed class DocumentDelegationForm
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná un departamento activo.")]
    public int Department { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una persona autorizable.")]
    public int Grantee { get; set; }
    [Required(ErrorMessage = "Seleccioná la operación que autorizás.")]
    public string Operation { get; set; } = "Read";
    public bool Enabled { get; set; } = true;
    [Range(0, long.MaxValue)]
    public long ExpectedRevision { get; set; }
    [DataType(DataType.Date)]
    public DateTime? ValidThrough { get; set; }
    [Required(ErrorMessage = "Indicá el motivo de la autorización o revocación."), StringLength(250)]
    public string Reason { get; set; } = string.Empty;
}
public sealed class DocumentDelegationsViewModel
{
    public IReadOnlyList<SelectOptionViewModel> Departments { get; set; } = [];
    public IReadOnlyList<SelectOptionViewModel> People { get; set; } = [];
    public IReadOnlyList<DocumentDelegationRow> Grants { get; set; } = [];
    public DocumentDelegationForm Form { get; set; } = new();
    public int Page { get; set; } = 1;
    public long Total { get; set; }
    public bool HasNext => (long)Page * 50 < Total;
}
