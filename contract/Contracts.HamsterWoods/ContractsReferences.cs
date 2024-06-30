using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.MultiToken;

namespace Contracts.HamsterWoods
{
    public partial class HamsterWoodsContractState
    {
        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
        internal AEDPoSContractContainer.AEDPoSContractReferenceState ConsensusContract { get; set; }
    }
}