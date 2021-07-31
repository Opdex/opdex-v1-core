using Stratis.SmartContracts;

public struct ClaimPendingMarketOwnershipLog
{
    [Index] public Address From;
    [Index] public Address To;
}