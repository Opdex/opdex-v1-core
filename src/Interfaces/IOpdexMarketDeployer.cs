using Stratis.SmartContracts;

public interface IOpdexMarketDeployer
{
    /// <summary>
    /// Creates a configured standard market smart contract and assigns the transaction sender as the owner of the market.
    /// </summary>
    /// <param name="authPoolCreators">Flag to authorize liquidity pool creators or not.</param>
    /// <param name="authProviders">Flag to authorize liquidity providers or not.</param>
    /// <param name="authTraders">Flag to authorize traders or not.</param>
    /// <param name="fee">The market transaction fee 0-10 equal to 0-1%.</param>
    /// <returns>The contract address of the created market.</returns>
    Address CreateStandardMarket(bool authPoolCreators, bool authProviders, bool authTraders, uint fee);
}