using Stratis.SmartContracts;

public struct OpdexApprovalEvent
{
    [Index] public Address Owner;
    [Index] public Address Spender;
    public UInt256 Amount;
}