using Stratis.SmartContracts;

public struct ChangeDeployerOwnerLog
{
    [Index] public Address From;
    [Index] public Address To;
}