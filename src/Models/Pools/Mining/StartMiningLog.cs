using Stratis.SmartContracts;

public struct StartMiningLog
{
    [Index] public Address Miner;
    public UInt256 Amount;
    public UInt256 TotalSupply;
    public UInt256 MinerBalance;
}