using Stratis.SmartContracts;

public struct MineLog
{
    [Index] public Address Miner;
    public UInt256 Amount;
    public UInt256 TotalSupply;
    [Index] public byte EventType;
}