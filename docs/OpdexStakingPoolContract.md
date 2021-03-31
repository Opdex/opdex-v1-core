# Opdex Staking Pool Contract

Staking liquidity pool contract that inherits for OpdexStandardPool and IOpdexStakingPool. Used for staking pools that include all functionality from standard pools but adds staking abilities.

Transaction fees remain a total 0.3%, same as standard pools. Staking pools distribute the fee between providers and stakers. 
5/6 of transaction fees (0.25% tx fee) goes to liquidity providers. 1/6 of transaction fees (0.05%) goes to stakers.
