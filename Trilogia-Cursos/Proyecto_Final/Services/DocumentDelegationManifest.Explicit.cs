namespace Proyecto_Final.Services;
public static partial class DocumentDelegationManifest
{
    public const string ExplicitMigration = "0056_stage2_document_explicit_authorization";
    public const string ExplicitMigrationHash = "73ED2CEA48585FD7EA369262B88A176CEB03A91DA5A4EFD36F001A8DD5383455";
    public const string VersionOrderMigration = "0061_stage2_document_version_monotonic";
    public const string VersionOrderMigrationHash = "2BB5FCDCA013FAFA324788E2A3BBF9AC143ADEB4A32112E0C50226F743CE5364";
    private static IReadOnlyDictionary<string,string> ExplicitModules { get; } = new Dictionary<string,string>
    {
        ["DocumentSecurity.CanAccess"] = "CFD8943D94CC972CD30318EF490D0A79649102FCEAF827088BD77C7F78C9AD44",
        ["DocumentSecurity.SetGrant"] = "70BF43A1EC1A28FE3AF8F6B8BEA0EB30401EEE85B9DD9BB0A5061A9A4CB12A09",
        ["DocumentSecurity.GrantOptions"] = "72F923230EAB8F589BEFB78EDF615808A5FF758D67A909E5D37622047FBE439F",
        ["DocumentSecurity.ListGrants"] = "0CC48002A5CE259BB8C440902D4A0F0FF96EDCED6557AEC95F80936285A3A795",
        ["DocumentSecurity.Document_MarkVersionReady"] = "61EC40F559383122E4E662460F2E0024644CBEFDFBEF9C5C15FAEB8E1E13C22D",
    };
    public static IReadOnlyDictionary<string,string> Modules { get; } = BaseModules.Concat(ExplicitModules).GroupBy(x=>x.Key).ToDictionary(x=>x.Key,x=>x.Last().Value);
    public static IReadOnlyDictionary<string,string> Ledger { get; } = new Dictionary<string,string> { [Migration]=MigrationHash,[ExplicitMigration]=ExplicitMigrationHash,[VersionOrderMigration]=VersionOrderMigrationHash };
}
