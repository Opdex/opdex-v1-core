using Stratis.SmartContracts;

public struct ApprovalEvent
{
    [Index] public Address Owner;
    [Index] public Address Spender;
    public UInt256 Amount;
    public byte EventTypeId;
}