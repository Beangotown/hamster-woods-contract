using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Contracts.HamsterWoods;

public partial class HamsterWoodsContract
{
    public override Address GetAdmin(Empty input)
    {
        return State.Admin.Value;
    }
    
    public override RankingRules GetRankingRules(Empty input)
    {
        return State.RankingRules.Value;
    }
    
    public override PurchaseChanceConfig GetPurchaseChanceConfig(Empty input)
    {
        return State.PurchaseChanceConfig.Value;
    }
    
    public override GameRules GetGameRules(Empty input)
    {
        return State.GameRules.Value;
    }
    
    public override GameLimitSettings GetGameLimitSettings(Empty input)
    {
        return State.GameLimitSettings.Value;
    }
    
    public override BoutInformation GetBoutInformation(GetBoutInformationInput input)
    {
        Assert(input!.PlayId != null && !input.PlayId.Value.IsNullOrEmpty(), "Invalid playId.");

        var boutInformation = State.BoutInformation[input.PlayId];

        Assert(boutInformation != null, "Bout not found.");

        return boutInformation;
    }

    public override PlayerInformation GetPlayerInformation(Address owner)
    {
        var playerInformation = GetCurrentPlayerInformation(owner, CheckHamsterPass(owner).Value);
        return playerInformation;
    }
    
    public override BoolValue CheckHamsterPass(Address owner)
    {
        var getBalanceOutput = State.TokenContract.GetBalance.Call(new GetBalanceInput
        {
            Symbol = HamsterWoodsContractConstants.HamsterPassSymbol,
            Owner = owner
        });

        return new BoolValue { Value = getBalanceOutput.Balance > 0 };
    }

    public override RaceConfig GetRaceConfig(Empty input)
    {
        return State.RaceConfig.Value;
    }

    public override LockedAcornsInfoList GetLockedAcornsInfoList(Address owner)
    {
        return State.LockedAcornsInfoList[owner];
    }

    public override CurrentRaceInfo GetCurrentRaceInfo(Empty input)
    {
        return GetCurrentRaceInfo();
    }
    
    public override ManagerList GetManagers(Empty input)
    {
        return State.ManagerList.Value;
    }
}