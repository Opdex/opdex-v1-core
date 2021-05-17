using Stratis.SmartContracts;

public struct ChangeMarketOwnerLog
{
    [Index] public Address From;
    [Index] public Address To;
}