namespace Proyecto_Final.Services;
public static partial class DocumentDelegationManifest
{
    public const string Migration = "0055_stage2_document_delegation";
    public const string MigrationHash = "F5011FFEE0E4DAF7F053E7389C37699570361B8F4E9854928CE306BC80C229B9";
    public static IReadOnlyDictionary<string,string> BaseModules { get; } = new Dictionary<string,string>
    {
        ["DocumentSecurity.CanAccess"] = "E3661AADF166D4E8DDB7F71707F740E17982E64AE5649B5EBAC977EF6014D9E4",
        ["DocumentSecurity.SetGrant"] = "522C04B186DE3F0F2004834419B4E6CA3D19FA9CA828270C98EB5D1C15C923DF",
        ["DocumentSecurity.GrantOptions"] = "570F871037C23464135CB28B803ABFD2259176DC2CFAEBE82359802638E3C55D",
        ["DocumentSecurity.ListGrants"] = "CA95D736BE9937E0D9B4AFD1A5CF7D195F94A09FC68459E18F8441528DAAB919",
        ["DocumentSecurity.GrantAuditImmutable"] = "3EFE62B4C22299264BA432534C4FC14C81AB408163B57E4FF893A9AF833BE381",
        ["DocumentSecurity.Document_GetOptions"] = "9B2DFBBC0C65307A6AA1B81508028ADB9683E10331297114F3235094F9C0F7F1",
        ["DocumentSecurity.Document_List"] = "1BA1BD78D1D75F09D97976065794819E1316B01EF75B139B46C125B67EB7C8CC",
        ["DocumentSecurity.Document_Summary"] = "4FD50E651734CBB4288D38703D2D8166AFDEDE3FF77897EAD3A3426B2BFE8DBD",
        ["DocumentSecurity.Document_GetById"] = "EE0A3DFC0CAB721937F082395193D964CC02C509331E1D0BD7031A7FEEF6E591",
        ["DocumentSecurity.Document_GetForEdit"] = "4D51EC468ACC6A59EBEB08607AA11DE2096F5D71004C78F7EE4BFC84C2D54125",
        ["DocumentSecurity.Document_Create"] = "A5F4DC2BF902985D08E9B2E13BBB8E291DB41CC931AE3A6C5203FCD633918A74",
        ["DocumentSecurity.Document_MarkReady"] = "FF09218EC4D5559A1C905BC785ABE408D0A71566CD51BE9DD8C6968915FC3B6D",
        ["DocumentSecurity.Document_DeletePending"] = "0641F977B28AF96313B3945CFF1D98AD6AF3E48B1AC2AC7FB771A58724B39F5B",
        ["DocumentSecurity.Document_Update"] = "EA61FC63AF00A2A9A6B5215C1DF440C60AAA4B4871F9C168550E1F7CDA02AFEE",
        ["DocumentSecurity.Document_ReplaceFile"] = "CEC1BDA0A4C4A8B61AD643B6005DF75D206D89AFD046F41AD3014DE9F5E8CEB6",
        ["DocumentSecurity.Document_MarkVersionReady"] = "0802F6C0F5E2B13E6465C97CB614E14446D6374EC61DDC11FE81B9C76BE25A0F",
        ["DocumentSecurity.Document_DeletePendingVersion"] = "D8FCBF70A4BC0D3C25BEEA4608E719C7DFE826CB96BFF9DD2FE00329D5FADF5D",
        ["DocumentSecurity.Document_SetStatus"] = "BF9B73C4995F930FF9B37B33D84A5B062F4FBBD0495E23D9C6AADE1299D90D23",
        ["DocumentSecurity.Document_GetFileMetadata"] = "AA360F1CEE8CBD01F3B1B657AA11F748232C37DAA18C845F201AD36CFBD36079",
        ["DocumentSecurity.DocumentAlert_Generate"] = "4FBCAE5BDD1EE49BA00A8FE96EF4CDBC7BA338741A78BFFB5173060754116BAA",
        ["DocumentSecurity.DocumentAlert_List"] = "45FC1CF6C90EB95E0AFB1AEC82F383E5A4BFB7342F8C0CB2BD8FE7775A59D6E2",
        ["DocumentSecurity.DocumentAlert_Summary"] = "884078E52DBA57620DD8B1D602AAE20BC041FBB70D45B9E87D990D1B9499C07F",
        ["DocumentSecurity.DocumentAlert_MarkHandled"] = "2298FC3E0C45EA70E125F5D2FAF2623207FB65AAEE9CEF51CBEA80AFE20FC4B1",
        ["DocumentSecurity.DocumentAlert_RegisterNotification"] = "AA46C4E848E8C31370F5518312B833FEB4406C9E694F5B2498185E22B046E85B",
        ["DocumentSecurity.Document_AuthorizeDownload"] = "5A81F701EB7B01F566D3BD891B14CFA406670E29C13B4C918FF834365827F749",
        ["DocumentSecurity.Document_PublicationReceipt"] = "E20A5AFCF203E8D3DAB632988730463C4B941BBC841406B7E8FABA5AE399A739",
    };
    public static IReadOnlyList<string> Operations { get; } = new[] { "Document_GetOptions","Document_List","Document_Summary","Document_GetById","Document_GetForEdit","Document_Create","Document_MarkReady","Document_DeletePending","Document_Update","Document_ReplaceFile","Document_MarkVersionReady","Document_DeletePendingVersion","Document_SetStatus","Document_GetFileMetadata","DocumentAlert_Generate","DocumentAlert_List","DocumentAlert_Summary","DocumentAlert_MarkHandled","DocumentAlert_RegisterNotification","Document_AuthorizeDownload","Document_PublicationReceipt" };
    public static IReadOnlyList<string> PublicOperations { get; } = Operations.Concat(new[] { "SetGrant", "GrantOptions", "ListGrants" }).ToArray();
    public static IReadOnlyList<string> LegacyOperations { get; } = new[] { "dbo.sp_Stage2_Document_GetOptions","dbo.sp_Stage2_Document_List","dbo.sp_Stage2_Document_Summary","dbo.sp_Stage2_Document_GetById","dbo.sp_Stage2_Document_GetForEdit","dbo.sp_Stage2_Document_Create","dbo.sp_Stage2_Document_MarkReady","dbo.sp_Stage2_Document_DeletePending","dbo.sp_Stage2_Document_Update","dbo.sp_Stage2_Document_ReplaceFile","dbo.sp_Stage2_Document_MarkVersionReady","dbo.sp_Stage2_Document_DeletePendingVersion","dbo.sp_Stage2_Document_SetStatus","dbo.sp_Stage2_Document_GetFileMetadata","dbo.sp_Stage2_DocumentAlert_Generate","dbo.sp_Stage2_DocumentAlert_List","dbo.sp_Stage2_DocumentAlert_Summary","dbo.sp_Stage2_DocumentAlert_MarkHandled","dbo.sp_Stage2_DocumentAlert_RegisterNotification","dbo.sp_Stage2_AuthorizeDocumentDownload","dbo.sp_Document_GetOptions","dbo.sp_Document_List","dbo.sp_Document_Summary","dbo.sp_Document_GetById","dbo.sp_Document_GetForEdit","dbo.sp_Document_Create","dbo.sp_Document_MarkReady","dbo.sp_Document_DeletePending","dbo.sp_Document_Update","dbo.sp_Document_ReplaceFile","dbo.sp_Document_MarkVersionReady","dbo.sp_Document_DeletePendingVersion","dbo.sp_Document_SetStatus","dbo.sp_Document_GetFileMetadata","dbo.sp_DocumentAlert_Generate","dbo.sp_DocumentAlert_List","dbo.sp_DocumentAlert_Summary","dbo.sp_DocumentAlert_MarkHandled","dbo.sp_DocumentAlert_RegisterNotification" };
}
