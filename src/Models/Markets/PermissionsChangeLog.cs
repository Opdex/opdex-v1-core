using Stratis.SmartContracts;

public struct PermissionsChangeLog
{
    [Index] public Address Address;
    public byte Permission;
    public bool IsAuthorized;
}