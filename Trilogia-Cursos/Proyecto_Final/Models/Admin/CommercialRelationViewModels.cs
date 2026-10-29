using System.ComponentModel.DataAnnotations;

namespace Proyecto_Final.Models.Admin;

public sealed record CommercialPersonOption(int UserId, string Name);
public sealed record CommercialRelationRow(string Kind, int ParentUser, string ParentName,
    int ChildUser, string ChildName, bool Enabled, long Revision, DateTime? ExpiresUtc, bool WithinValidity);

public sealed class CommercialRelationForm
{
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una persona elegible.")]
    public int ParentUser { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná una persona elegible.")]
    public int ChildUser { get; set; }
    [Range(0, long.MaxValue)]
    public long ExpectedRevision { get; set; }
    public bool Enabled { get; set; } = true;
    [DataType(DataType.Date)]
    public DateTime? ValidThrough { get; set; }
    [Required(ErrorMessage = "Indicá el motivo de la asignación o revocación.")]
    [StringLength(250)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class CommercialRelationsViewModel
{
    public IReadOnlyList<CommercialPersonOption> Sellers { get; set; } = [];
    public IReadOnlyList<CommercialPersonOption> Customers { get; set; } = [];
    public IReadOnlyList<CommercialPersonOption> Supervisors { get; set; } = [];
    public IReadOnlyList<CommercialRelationRow> Relations { get; set; } = [];
    public long Total { get; set; }
    public int Page { get; set; } = 1;
    public bool HasNext => (long)Page * 50 < Total;
    public CommercialRelationForm CustomerRelation { get; set; } = new();
    public CommercialRelationForm SupervisorRelation { get; set; } = new();
}
