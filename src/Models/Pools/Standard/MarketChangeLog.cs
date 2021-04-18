using Stratis.SmartContracts;

public struct MarketChangeLog
{
    [Index] public Address From;
    [Index] public Address To;
}