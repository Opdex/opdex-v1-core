using Stratis.SmartContracts;

/// <summary>
/// An abstract class of core market functionality.
/// </summary>
public abstract class OpdexMarket : SmartContract, IOpdexMarket
{
    /// <summary>
    /// Constructor to initialize the base of a market.
    /// </summary>
    /// <param name="state">Smart contract state.</param>
    /// <param name="fee">The market transaction fee, 0-10 equal to 0-1%.</param>
    protected OpdexMarket(ISmartContractState state, uint fee) : base(state)
    {
        Assert(fee <= 10, "OPDEX: INVALID_FEE");
        Fee = fee;
    }
    
    /// <inheritdoc />
    public uint Fee
    {
        get => State.GetUInt32(nameof(Fee));
        private set => State.SetUInt32(nameof(Fee), value);
    }

    /// <inheritdoc />
    public Address GetPool(Address token)
    {
        return State.GetAddress($"Pool:{token}");
    }

    /// <inheritdoc />
    public abstract Address CreatePool(Address token);
    
    protected void SetPool(Address token, Address contract)
    {
        State.SetAddress($"Pool:{token}", contract);
    }
}
