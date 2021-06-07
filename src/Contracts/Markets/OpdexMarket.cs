using Stratis.SmartContracts;

/// <summary>
/// An abstract class of base market functionality.
/// </summary>
public abstract class OpdexMarket : SmartContract, IOpdexMarket
{
    /// <summary>
    /// Constructor to initialize the base of a market.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="transactionFee">The market transaction fee, 0-10 equal to 0-1%.</param>
    protected OpdexMarket(ISmartContractState state, uint transactionFee) : base(state)
    {
        Assert(transactionFee <= 10, "OPDEX: INVALID_TRANSACTION_FEE");
        TransactionFee = transactionFee;
    }

    /// <inheritdoc />
    public uint TransactionFee
    {
        get => State.GetUInt32(MarketStateKeys.TransactionFee);
        private set => State.SetUInt32(MarketStateKeys.TransactionFee, value);
    }

    /// <inheritdoc />
    public Address GetPool(Address token)
    {
        return State.GetAddress($"{MarketStateKeys.Pool}:{token}");
    }

    /// <inheritdoc />
    public abstract Address CreatePool(Address token);

    protected void SetPool(Address token, Address contract)
    {
        State.SetAddress($"{MarketStateKeys.Pool}:{token}", contract);
    }
}
