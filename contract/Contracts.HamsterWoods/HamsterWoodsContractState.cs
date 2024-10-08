using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Contracts.HamsterWoods;

/// <summary>
///     The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type.
/// </summary>
public partial class HamsterWoodsContractState : ContractState
{
    public BoolState Initialized { get; set; }
    public SingletonState<Address> Admin { get; set; }
    public SingletonState<GameLimitSettings> GameLimitSettings { get; set; }
    public SingletonState<GameRules> GameRules { get; set; }
    public SingletonState<GridTypeList> GridTypeList { get; set; }
    
    public MappedState<Address, PlayerInformation> PlayerInformation { get; set; }
    
    public MappedState<Hash, BoutInformation> BoutInformation { get; set; }

    public MappedState<Address, int, long> UserWeeklyAcorns { get; set; }

    public SingletonState<int> CurrentWeek { get; set; }

    public SingletonState<PurchaseChanceConfig> PurchaseChanceConfig { get; set; }
    public SingletonState<RankingRules> RankingRules { get; set; }
    public MappedState<Address,LockedAcornsInfoList> LockedAcornsInfoList { get; set; }
    public SingletonState<ManagerList> ManagerList { get; set; } 
    public SingletonState<RaceConfig> RaceConfig { get; set; } 
    public SingletonState<RaceTimeInfo> RaceTimeInfo { get; set; } 
    
    // point
    public MappedState<Address, bool> JoinRecord { get; set; }
    public SingletonState<Hash> PointsContractDAppId { get; set; }
    public SingletonState<string> OfficialDomain { get; set; }

}