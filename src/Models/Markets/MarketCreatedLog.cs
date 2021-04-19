using Stratis.SmartContracts;

public struct MarketCreatedLog
{
    [Index] public Address Market;
    public bool AuthPoolCreators;
    public bool AuthProviders;
    public bool AuthTraders;
    public uint Fee;
}