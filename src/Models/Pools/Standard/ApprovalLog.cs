using Stratis.SmartContracts;

public struct ApprovalLog
{
    [Index] public Address Owner;
    [Index] public Address Spender;
    public UInt256 Amount;
    public UInt256 OldAmount;
}