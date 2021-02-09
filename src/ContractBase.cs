using Stratis.SmartContracts;

public class ContractBase : SmartContract
{
    protected ContractBase(ISmartContractState contractState) : base(contractState)
    {
    }
    
    protected void SafeTransfer(Address to, ulong amount)
    {
        if (amount == 0) return;
        var result = Transfer(to, amount);
        Assert(result.Success, "OpdexV1: INVALID_TRANSFER");
    }
    
    protected void SafeTransferTo(Address token, Address to, UInt256 amount)
    {
        if (amount == 0) return;
        var result = Call(token, 0, "TransferTo", new object[] {to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_To");
    }
    
    protected void SafeTransferFrom(Address token, Address from, Address to, UInt256 amount)
    {
        var result = Call(token, 0, "TransferFrom", new object[] {from, to, amount});
        Assert(result.Success && (bool)result.ReturnValue, "OpdexV1: INVALID_TRANSFER_FROM");
    }
}