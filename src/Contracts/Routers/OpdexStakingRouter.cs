using Stratis.SmartContracts;

/// <summary>
/// 
/// </summary>
public class OpdexStakingRouter : OpdexRouter
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="state"></param>
    /// <param name="market"></param>
    public OpdexStakingRouter(ISmartContractState state, Address market) : base(state, market)
    {
    }
}