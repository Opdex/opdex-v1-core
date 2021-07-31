using Stratis.SmartContracts;

public struct SetPendingMarketOwnershipLog
{
    [Index] public Address From;
    [Index] public Address To;
}