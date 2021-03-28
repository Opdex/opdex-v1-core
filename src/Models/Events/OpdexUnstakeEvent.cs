using Stratis.SmartContracts;

public struct OpdexUnstakeEvent
{
    [Index] public Address Staker;
    public UInt256 Amount;
}