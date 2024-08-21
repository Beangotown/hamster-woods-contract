using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.MultiToken;
using Points.Contracts.Point;

namespace Contracts.HamsterWoods
{
    public partial class HamsterWoodsContractState
    {
        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
        internal AEDPoSContractContainer.AEDPoSContractReferenceState ConsensusContract { get; set; }
        internal PointsContractContainer.PointsContractReferenceState PointsContract { get; set; }
    }
}