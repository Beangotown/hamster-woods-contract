using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Contracts.HamsterWoods;

public partial class HamsterWoodsContract : HamsterWoodsContractContainer.HamsterWoodsContractBase
{
    public override Empty Initialize(Empty input)
    {
        if (State.Initialized.Value)
        {
            return new Empty();
        }

        State.TokenContract.Value =
            Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
        State.ConsensusContract.Value =
            Context.GetContractAddressByName(SmartContractConstants.ConsensusContractSystemName);
        State.Admin.Value = Context.Sender;
        State.GameLimitSettings.Value = new GameLimitSettings()
        {
            DailyMaxPlayCount = HamsterWoodsContractConstants.DailyMaxPlayCount,
            DailyPlayCountResetHours = HamsterWoodsContractConstants.DailyPlayCountResetHours
        };
        State.ManagerList.Value = new ManagerList
        {
            Value = { }
        };
        State.PurchaseChanceConfig.Value = new PurchaseChanceConfig()
        {
            WeeklyPurchaseCount = 20,
            AcornsAmount = 25,
            WeeklyPurchaseCountResetHour = 0
        };
        State.GridTypeList.Value = new GridTypeList
        {
            Value =
            {
                GridType.Blue, GridType.Blue, GridType.Red, GridType.Blue, GridType.Gold, GridType.Red,
                GridType.Blue, GridType.Blue, GridType.Red, GridType.Gold, GridType.Blue, GridType.Red,
                GridType.Blue, GridType.Blue, GridType.Gold, GridType.Red, GridType.Blue, GridType.Red
            }
        };
        State.Initialized.Value = true;
        return new Empty();
    }

    public override Empty Play(PlayInput input)
    {
        Assert(input.DiceCount <= 3, "Invalid diceCount");
        Assert(State.RaceConfig.Value != null, "Invalid raceConfig");
        Assert(CheckHamsterPass(Context.Sender).Value, "HamsterPass Balance is not enough");
        var playerInformation = SetPlayerInfo(input.ResetStart);
        Assert(playerInformation.PlayableCount > 0 || playerInformation.PurchasedChancesCount > 0,
            "PlayableCount is not enough");

        var boutInformation = new BoutInformation
        {
            PlayId = Context.OriginTransactionId,
            PlayTime = Context.CurrentBlockTime,
            PlayerAddress = Context.Sender,
            DiceCount = input.DiceCount == 0 ? 1 : input.DiceCount
        };
        var randomHash = State.ConsensusContract.GetRandomHash.Call(new Int64Value
        {
            Value = Context.CurrentHeight
        });
        Assert(randomHash != null && !randomHash.Value.IsNullOrEmpty(),
            "Still preparing your game result, please wait for a while :)");
        SetBoutInformationBingoInfo(boutInformation.PlayId, randomHash, playerInformation, boutInformation);
        SetPlayerInformation(playerInformation, boutInformation);

        var score = boutInformation.Score * HamsterWoodsContractConstants.AcornsDecimalsValue;
        SetLockedAcornsInfo(Context.Sender, score);
        Context.Fire(new Picked
        {
            GridType = boutInformation.GridType,
            GridNum = boutInformation.GridNum,
            Score = score,
            PlayerAddress = boutInformation.PlayerAddress,
            BingoBlockHeight = boutInformation.BingoBlockHeight,
            DiceCount = boutInformation.DiceCount,
            DiceNumbers = new DiceList()
            {
                Value =
                {
                    boutInformation.DiceNumbers
                }
            },
            StartGridNum = boutInformation.StartGridNum,
            EndGridNum = boutInformation.EndGridNum,
            WeeklyAcorns = playerInformation.WeeklyAcorns,
            TotalAcorns = playerInformation.TotalAcorns,
            TotalChance = playerInformation.PurchasedChancesCount,
            WeekNum = State.CurrentWeek.Value,
            IsRace = State.RaceConfig.Value.IsRace,
            AcornsDecimals = HamsterWoodsContractConstants.AcornsDecimals
        });
        return new Empty();
    }

    public override Empty PurchaseChance(Int32Value input)
    {
        Assert(State.RaceConfig.Value != null, "Invalid raceConfig");
        Assert(State.PurchaseChanceConfig.Value != null, "Invalid purchaseChanceConfig");
        var acornsAmount = State.PurchaseChanceConfig.Value.AcornsAmount;
        Assert(acornsAmount > 0, "PurchaseChance is not allowed");

        var playerInformation = SetPlayerInfo(false);
        var costAmount = input.Value * acornsAmount;
        ;
        Assert(playerInformation.TotalAcorns >= costAmount, "Acorns is not enough");
        Assert(
            GeWeeklyPurchasedChanceCount(State.PurchaseChanceConfig.Value, playerInformation) >=
            input.Value, "Purchase chance is not enough");

        playerInformation.TotalAcorns -= costAmount;
        playerInformation.PurchasedChancesCount += input.Value;
        playerInformation.WeeklyPurchasedChancesCount -= input.Value;
        playerInformation.LastPurchaseChanceTime = Context.CurrentBlockTime;
        State.PlayerInformation[Context.Sender] = playerInformation;

        State.TokenContract.TransferFrom.Send(new TransferFromInput
        {
            From = Context.Sender,
            To = Context.Self,
            Amount = input.Value * acornsAmount,
            Symbol = HamsterWoodsContractConstants.AcornsSymbol,
            Memo = "PurchaseChance"
        });

        Context.Fire(new ChancePurchased
        {
            PlayerAddress = Context.Sender,
            AcornsAmount = costAmount,
            ChanceCount = input.Value,
            WeeklyAcorns = playerInformation.WeeklyAcorns,
            TotalAcorns = playerInformation.TotalAcorns,
            TotalChance = playerInformation.PurchasedChancesCount
        });
        return new Empty();
    }

    public override Empty BatchUnlockAcorns(UnlockAcornsInput input)
    {
        Assert(State.ManagerList.Value.Value.Contains(Context.Sender), "No permission.");
        Assert(input.Addresses.Count > 0 && input.Addresses.Count <= 20, "Invalid addresses.");
        Assert(input.WeekNum > 0 && input.WeekNum < State.CurrentWeek.Value, "Invalid weekNum.");

        foreach (var address in input.Addresses)
        {
            UnlockAcorns(address, input.WeekNum);
        }

        return new Empty();
    }

    private void UnlockAcorns(Address input, int weekNum)
    {
        var lockedAcornsInfoList = State.LockedAcornsInfoList[input];
        if (lockedAcornsInfoList == null || lockedAcornsInfoList.Value == null || lockedAcornsInfoList.Value.Count == 0)
        {
            return;
        }

        var needUnlockInfo = lockedAcornsInfoList.Value.FirstOrDefault(t => t.Week == weekNum && !t.IsUnlocked);
        if (needUnlockInfo == null)
        {
            return;
        }

        needUnlockInfo.IsUnlocked = true;
        var amount = needUnlockInfo.Acorns;

        State.TokenContract.Transfer.Send(new TransferInput
        {
            To = input,
            Symbol = HamsterWoodsContractConstants.AcornsSymbol,
            Amount = amount
        });

        Context.Fire(new AcornsUnlocked
        {
            From = Context.Self,
            To = input,
            Symbol = HamsterWoodsContractConstants.AcornsSymbol,
            Amount = amount,
            WeekNum = needUnlockInfo.Week
        });
    }
}