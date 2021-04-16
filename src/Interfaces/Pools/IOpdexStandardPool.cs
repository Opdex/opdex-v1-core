using Stratis.SmartContracts;

public interface IOpdexStandardPool : IOpdexPool
{
    /// <summary>
    /// The address of the market contract that created the pool.
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

    /// <summary>
    /// Checks if 
    /// </summary>s
    /// <param name="address"></param>
    /// <param name="authorization"></param>
    /// <returns></returns>
    bool IsAuthorizedFor(Address address, byte authorization);
    
    /// <summary>
    /// Allows direct transfers of CRS tokens through the standard Transfer method to this contract.
    /// </summary>
    void Receive();
}