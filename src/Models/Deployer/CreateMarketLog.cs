using Stratis.SmartContracts;

public struct CreateMarketLog
{
    [Index] public Address Market;
    [Index] public Address Owner;
    public Address StakingToken;
    public bool AuthPoolCreators;
    public bool AuthProviders;
    public bool AuthTraders;
    public uint Fee;
}