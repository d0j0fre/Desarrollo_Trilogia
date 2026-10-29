namespace JJ.Commerce;

// Shared integrity metadata; contains no credentials, destinations or row data.
public static class QuoteSqlContract
{
    public static IReadOnlyDictionary<string, string> ObjectHashes { get; } = new Dictionary<string, string>
    {
        ["dbo.sp_Stage2_RequireOriginalLogin"] = "8C8515B2D2DD516A737A548EE7A03AB3E12D101BB8D6B27A1FBDC8846E31479C",
        ["CommercialSecurity.RequireAssignment"] = "53980BF5D5F8AE2020514A529082D5BE1289359A086B954F99188AAB1C1643A1",
        ["dbo.tr_CommerceQuote_ProductRevision"] = "3AB77A4F34AAA31269D5384A59757C4CFE1D39840EDC0F01520061DDFA5F4A13",
        ["dbo.tr_CommerceQuote_PromotionRevision"] = "1EF23A112EA4EF781C6FEE94E03D88403B54E190B9FE924E0104E50A38C334BD",
        ["dbo.tr_CommerceQuote_SegmentRevision"] = "8B5A772D6B027E7353429BEADCA60EEC7E8382568EED65B99C609E96A569DE2B",
        ["CommerceQuotes.OwnerImmutable"] = "A72666CFE5742A0D6CDF166DBF3F7053322C1D07E86B1395D013B66EE440BE56",
        ["CommerceQuotes.QuoteImmutable"] = "3F3D56A121B92D19A47E3E9B8B1847B4FA4F3D264B420A3CD3DEF6513478DA1D",
        ["CommerceQuotes.LinesImmutable"] = "44B96237E480881B02858836133783E2C209355A64128A149A0F840E8263A32E",
        ["CommerceQuotes.CaptureSource"] = "BC7A7EAFB8B856821003C872D8D83E874F46F74A7322A996A6F5EC373463E5CE",
        ["CommerceQuotes.RequireOwner"] = "E73F416D9B9ADFC5E248921622A6D04C25A979E7522B01161FE1C913E4884AB0",
        ["CommerceQuotes.Capture"] = "6BBD3545DF5459A779DF95140B45DC144806C136F68B1EDFB5D5DBBB1787E283",
        ["CommerceQuotes.Publish"] = "F96FDAC6555A72F0079199F0CB48D4DB0ED618B9039416BFC15CE8F9ECEE2DCF",
        ["CommerceQuotes.ReadOwn"] = "1FFC2F5794069F555382E97F288AA0A9DADED81C84C6B739129A5370619B98DF"
    };
    public static IReadOnlyList<string> Tables { get; } = new[] { "Owners", "Quotes", "Lines", "SourceRevision" };
    // DML trigger definitions are visible through their parent table.
    public static IReadOnlyList<string> MetadataObjects { get; } = new[]
    {
        "dbo.sp_Stage2_RequireOriginalLogin", "CommercialSecurity.RequireAssignment",
        "CommerceQuotes.CaptureSource", "CommerceQuotes.RequireOwner", "CommerceQuotes.Capture",
        "CommerceQuotes.Publish", "CommerceQuotes.ReadOwn", "dbo.Productos", "dbo.Promociones", "dbo.Usuarios",
        "CommerceQuotes.Owners", "CommerceQuotes.Quotes", "CommerceQuotes.Lines", "CommerceQuotes.SourceRevision"
    };
}
