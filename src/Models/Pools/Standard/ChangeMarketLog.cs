using Stratis.SmartContracts;

public struct ChangeMarketLog
{
    [Index] public Address From;
    [Index] public Address To;
}