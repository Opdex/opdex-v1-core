# Opdex Controller Contract

A smart contract that acts as an entry point to Opdex protocol for most pool related interactions. This contract is primarily responsible for the following items:
- Management of available pools
- Quoting swap and pooling related methods. 
- Validating and completing prerequisite actions prior to providing or swapping in a pool.

## Overview 

Traders will approve the controller contract for an allowance per token they want to swap, provide or remove liquidity for. 

This controller contract will use `TransferFrom` amongst other validations prior to calling a pool to swap, add, or remove liquidity.

This contract only stores Pool addresses looked up by SRC token address so new versions can easily be deployed and used as long as existing pools are set in new versions.