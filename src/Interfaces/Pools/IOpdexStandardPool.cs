using Stratis.SmartContracts;

public interface IOpdexStandardPool : IOpdexPool
{
    /// <summary>
    /// The address of the market the pool is assigned to.
    /// </summary>
    Address Market { get; }
    
    /// <summary>
    /// Flag to authorize liquidity providing type transactions.
    /// </summary>
    bool AuthProviders { get; }
    
    /// <summary>
    /// Flag to authorize swap type transactions.
    /// </summary>
    bool AuthTraders { get; }
}