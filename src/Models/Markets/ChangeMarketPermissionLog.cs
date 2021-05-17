using Stratis.SmartContracts;

public struct ChangeMarketPermissionLog
{
    [Index] public Address Address;
    public byte Permission;
    public bool IsAuthorized;
}