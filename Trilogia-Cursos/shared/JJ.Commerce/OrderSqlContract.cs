namespace JJ.Commerce;
public static class OrderSqlContract
{
    public static IReadOnlyDictionary<string,string> ObjectHashes { get; } = new Dictionary<string,string>
    {
        ["CommercialOrders.Result"] = "A8F920786CD6C2BF71295889D71A0F33128D76258AAFD4F89838B8EA316E3F71",
        ["CommercialOrders.Finalize"] = "F9787A059DFE46E84A94261B8E7AEEBE3063837A5B66BD146A55E014E55B18C2",
        ["CommercialOrders.Consume"] = "29CB6334044AFC6F0A4FDB32CAA26E817776D5BE0B862B69B18048F90D60B0A4",
        ["CommercialOrders.RequireSupervisor"] = "4A3F97D6FE3287554524A4D68C3F4D79BE4CC844606536D49ABAD5AFAE34442B",
        ["CommercialOrders.Decide"] = "BCC1741232619D9A32BE87AB407CE4BAEE287F87AAC445290A9B103D4E7754BA",
        ["CommercialOrders.Clients"] = "789FE735C0E3B28A3E1A00F774EC8B8D067FDA7F9A04DCD5069F4F3E188C9A16",
        ["CommercialOrders.History"] = "32CB16830A6405A36B4B80DDC859831BCFDB4CF46F4116EF6133543AE1E995D1",
        ["CommercialOrders.Details"] = "41F722E5B87BEC55E05F0929AD0CF10695D52D4CE34828807A496FB386BAAAAA",
        ["CommercialOrders.Retained"] = "B06393DC409A06F6FDB8C78FF4847701B024FF07830DA351B2A4E9CC218AE983",
        ["CommercialOrders.OrdersImmutable"] = "EA8511CB1954DB0D982C4313BB451D481F45C70BE98C5AE99AE516FAC3AD6A8D",
        ["CommercialOrders.MilestonesImmutable"] = "766D571164C0F67F6E7EEE1DF78F6DC28539D9B00A362A579E5D80F3EE058E97",
        ["CommercialOrders.DecisionsImmutable"] = "546621C5023BF891D20C7D9BDBAE7B6337C6D3C1CA626E6D7A51897EF16E25E0",
        ["CommercialOrders.HistoryV2"] = "CD5174E5909CDFBCB671C22B473DF3264CD23FC9BF32D53B16E26BA3119807B5",
        ["CommercialOrders.DetailsV2"] = "B2673EB109409EE59CFD89DEC91BFE350C481E4AA858D9A9019983505210E7AF",
        ["dbo.tr_Stage2OrderAmounts_Immutable"] = "7C2448BF5861FACA9BC22E91CA8B359788281D9E382596A0F66FFA1AED90AEFA",
        ["dbo.tr_Stage2OrderAmountLines_Immutable"] = "5538A43F3041D33E8F2B79AE385E232CF256276B9C893FAF0186F8FA9B404CE8",
    };
    public static IReadOnlyDictionary<string,string> Ledger { get; } = new Dictionary<string,string>
    {
        ["0039_stage2_aggregate_amounts_v2"] = "2CA5A1CC97629FD4AB65083D204DB47B37457894EEC74C0D5B247B1D6321CB96",
        ["0047_stage2_commercial_relationships"] = "9BC4DCB6D8F52B92AF666885F90BEF48ED07D178D15881F4C821889D9F60B50E",
        ["0048_stage2_order_approval_capability"] = "D747CE4AC82DAE4E5D5809FC7A69D95BF6C9E6DE2FDCB579009F3DE35AA2C89E",
        ["0049_stage2_server_quote_authority"] = "F8E08FCEAB1E8ED72DFDC30CF8F02DACEBD4303E3ECF292B72FB74F0520CEF7E",
        ["0050_stage2_quote_empty_promotions"] = "46616159907485A026785648A690727FECCF50CB0EAB17343A6D692406565BE5",
        ["0051_stage2_commercial_orders"] = "AB00E0743617C3E28877295F4BD69C747B2A3AE3A8D5BD370EC4E062710E4A31",
        ["0052_stage2_seller_history"] = "0B832E41114D9435975226F9DE695C532B785855F7B7C4BAE9BD92C84B1F72A6",
        ["0054_stage2_order_operation_receipts"] = "97174DD2644FE5C7981D452767B5D4C215489227D287344B4EDABC35CB496216",
    };
    public static IReadOnlyList<string> PublicOperations { get; } = new[] { "Consume", "Decide", "Clients", "HistoryV2", "DetailsV2", "Retained" };
    public static IReadOnlyList<string> InternalOperations { get; } = new[] { "Result", "Finalize", "RequireSupervisor", "History", "Details" };
    public static IReadOnlyList<string> Tables { get; } = new[] { "Orders", "Milestones", "Decisions" };
}
