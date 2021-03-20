using Stratis.SmartContracts;

public interface IOpdexStandardPool
{
    Address Token { get; }
    ulong ReserveCrs { get; }
    UInt256 ReserveSrc { get; }
    UInt256 KLast { get; }
    bool Locked { get; }
    byte[][] Reserves { get; }
    
    UInt256 Mint(Address to);
    void Swap(ulong amountCrsOut, UInt256 amountSrcOut, Address to, byte[] data);
    UInt256[] Burn(Address to);
    void Skim(Address to);
    void Sync();
}