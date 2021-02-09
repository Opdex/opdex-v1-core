using Stratis.SmartContracts;

public struct TransferEvent
{
    [Index] public Address From;
    [Index] public Address To;
    public UInt256 Amount;        
    public byte EventTypeId;
}