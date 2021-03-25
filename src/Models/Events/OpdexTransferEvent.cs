using Stratis.SmartContracts;

public struct OpdexTransferEvent
{
    [Index] public Address From;
    [Index] public Address To;
    public string Amount;
}