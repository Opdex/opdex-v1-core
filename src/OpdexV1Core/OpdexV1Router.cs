using Stratis.SmartContracts;

public class OpdexV1Router : SmartContract
{
    public OpdexV1Router(ISmartContractState smartContractState, Address factory, Address wcrs) : base(smartContractState)
    {
        Factory = factory;
        WCRS = wcrs;
    }

    public override void Receive()
    {
        Assert(Message.Sender == WCRS, "OpdexV2: UNACCEPTED_CRS");
        base.Receive();
    }

    public Address Factory
    {
        get => PersistentState.GetAddress(nameof(Factory));
        set => PersistentState.SetAddress(nameof(Factory), value);
    }
    
    public Address WCRS
    {
        get => PersistentState.GetAddress(nameof(WCRS));
        set => PersistentState.SetAddress(nameof(WCRS), value);
    }

    # region Liquidity
    public void AddLiquidity()
    {
        
    }

    public void AddLiquidityCRS()
    {
        
    }

    public void RemoveLiquidity()
    {
        
    }

    public void RemoveLiquidityCRS()
    {
        
    }
    #endregion
    
    #region Swaps

    public void SwapExactTokensForTokens()
    {
        
    }

    public void SwapTokensForExactTokens()
    {
        
    }

    public void SwapExactCRSForTokens()
    {
        
    }

    public void SwapTokensForExactCRS()
    {
        
    }

    public void SwapExactTokensForCRS()
    {
        
    }

    public void swapCRSForExactTokens()
    {
        
    }
    
    #endregion
}