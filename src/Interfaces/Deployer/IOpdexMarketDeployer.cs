using Stratis.SmartContracts;

public interface IOpdexMarketDeployer
{
    /// <summary>
    /// The owner of the market deployer contract. with the ability to create new markets.
    /// </summary>
    /// <remarks>
    /// The owner's privileges include the ability to create markets and setting a new pending owner.
    /// </remarks>
    Address Owner { get; }

    /// <summary>
    /// A pending wallet address that has been suggested to take ownership of the contract. This value
    /// acts as a whitelist for access to <see cref="ClaimPendingOwnership"/>.
    /// </summary>
    Address PendingOwner { get; }

    /// <summary>
    /// Public method allowing the current contract owner to whitelist a new pending owner. The newly pending owner
    /// will then call <see cref="ClaimPendingOwnership"/> to accept.
    /// </summary>
    /// <param name="pendingOwner">The address to set as the new pending owner.</param>
    void SetPendingOwnership(Address pendingOwner);

    /// <summary>
    /// Public method to allow the pending new owner to accept ownership replacing the current contract owner.
    /// </summary>
    void ClaimPendingOwnership();

    /// <summary>
    /// Creates a configured standard market smart contract.
    /// </summary>
    /// <param name="marketOwner">The address of the wallet that will be the owner of the market.</param>
    /// <param name="transactionFee">The market's transaction fee 0-10 equal to 0-1%.</param>
    /// <param name="authPoolCreators">Flag to authorize liquidity pool creators or not.</param>
    /// <param name="authProviders">Flag to authorize liquidity providers or not.</param>
    /// <param name="authTraders">Flag to authorize traders or not.</param>
    /// <param name="enableMarketFee">Flag to enable the market fee where 1/6 of transaction fees are collected by the market owner.</param>
    /// <returns>The contract address of the created standard market.</returns>
    Address CreateStandardMarket(Address marketOwner, uint transactionFee, bool authPoolCreators, bool authProviders, bool authTraders, bool enableMarketFee);

    /// <summary>
    /// Creates a public staking market contract with a .3% transaction fee.
    /// </summary>
    /// <param name="stakingToken">The address of the staking token to use throughout the market.</param>
    /// <returns>The contract address of the created staking market.</returns>
    Address CreateStakingMarket(Address stakingToken);
}