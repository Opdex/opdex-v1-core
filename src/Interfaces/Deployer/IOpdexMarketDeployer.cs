using Stratis.SmartContracts;

public interface IOpdexMarketDeployer
{
    /// <summary>
    /// The owner of the deployer, able to create new standard markets.
    /// </summary>
    Address Owner { get; }

    /// <summary>
    /// Promote an address to be the new owner of the deployer.
    /// </summary>
    /// <param name="address"></param>
    void SetOwner(Address address);
    
    /// <summary>
    /// Creates a configured standard market smart contract and assigns the transaction sender as the owner of the market.
    /// </summary>
    /// <param name="marketOwner">The address of the wallet that will be the owner of the market.</param>
    /// <param name="authPoolCreators">Flag to authorize liquidity pool creators or not.</param>
    /// <param name="authProviders">Flag to authorize liquidity providers or not.</param>
    /// <param name="authTraders">Flag to authorize traders or not.</param>
    /// <param name="fee">The market transaction fee 0-10 equal to 0-1%.</param>
    /// <returns>The contract address of the created market.</returns>
    Address CreateStandardMarket(Address marketOwner, bool authPoolCreators, bool authProviders, bool authTraders, uint fee);
}