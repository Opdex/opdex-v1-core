using Stratis.SmartContracts;

public struct ClaimPendingDeployerOwnershipLog
{
    [Index] public Address From;
    [Index] public Address To;
}