using Stratis.SmartContracts;

public struct SetPendingDeployerOwnershipLog
{
    [Index] public Address From;
    [Index] public Address To;
}