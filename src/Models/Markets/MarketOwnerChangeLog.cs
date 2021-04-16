using Stratis.SmartContracts;

public struct MarketOwnerChangeLog
{
    [Index] public Address From;
    [Index] public Address To;
}