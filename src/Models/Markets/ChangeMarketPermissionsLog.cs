using Stratis.SmartContracts;

public struct ChangeMarketPermissionsLog
{
    [Index] public Address Address;
    public byte Permission;
    public bool IsAuthorized;
}