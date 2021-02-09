using Stratis.SmartContracts;

public struct PairCreatedEvent
{
    public Address Token;
    public Address Pair;
    public byte EventTypeId;
}