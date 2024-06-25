using AElf;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Contracts.HamsterWoodsContract;

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
    
    public override PlayOutput Play(PlayInput input)
    {
        Assert(input.DiceCount <= 3, "Invalid diceCount");
        var playerInformation = InitPlayerInfo(input.ResetStart);
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

        // lock acorns
        playerInformation.LockedAcorns += boutInformation.Score;
        // State.TokenContract.Transfer.Send(new TransferInput
        // {
        //     To = Context.Sender,
        //     Symbol = HamsterWoodsContractConstants.BeanSymbol,
        //     Amount = boutInformation.Score
        // });
        Context.Fire(new Bingoed
        {
            GridType = boutInformation.GridType,
            GridNum = boutInformation.GridNum,
            Score = boutInformation.Score,
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
            WeeklyBeans = playerInformation.WeeklyBeans,
            TotalBeans = playerInformation.TotalAcorns,
            TotalChance = playerInformation.PurchasedChancesCount
        });
        return new PlayOutput { ExpectedBlockHeight = 0 };
    }

    public override Empty PurchaseChance(Int32Value input)
    {
        var acornsAmount = State.PurchaseChanceConfig.Value.AcornsAmount;
        Assert(acornsAmount > 0, "PurchaseChance is not allowed");
        var playerInformation = InitPlayerInfo(false);
        ReSetPlayerAcorns(playerInformation);
        Assert(playerInformation.TotalAcorns >= input.Value * acornsAmount, "Acorn is not enough");
        Assert(
            GetRemainingDailyPurchasedChanceCount(State.PurchaseChanceConfig.Value, playerInformation) <=
            input.Value, "Purchase chance is not enough");

        // if (IsWeekRanking())
        // {
        //     var currentWeek = State.CurrentWeek.Value;
        //     var realWeeklyBeans =
        //         Math.Min(playerInformation.TotalBeans, State.UserWeeklyBeans[Context.Sender][currentWeek]);
        //     playerInformation.WeeklyBeans = realWeeklyBeans > input.Value * beansAmount
        //         ? realWeeklyBeans.Sub(input.Value * beansAmount)
        //         : 0;
        //     State.UserWeeklyBeans[Context.Sender][currentWeek] = (int)playerInformation.WeeklyBeans;
        // }

        playerInformation.TotalAcorns -= input.Value * acornsAmount;
        playerInformation.PurchasedChancesCount += input.Value;
        State.PlayerInformation[Context.Sender] = playerInformation;
        State.TokenContract.TransferFrom.Send(new TransferFromInput
        {
            From = Context.Sender,
            To = Context.Self,
            Amount = input.Value * acornsAmount,
            Symbol = HamsterWoodsContractConstants.AcornSymbol,
            Memo = "PurchaseChance"
        });
        Context.Fire(new PurchasedChance
        {
            PlayerAddress = Context.Sender,
            BeansAmount = input.Value * acornsAmount,
            ChanceCount = input.Value,
            WeeklyBeans = playerInformation.WeeklyBeans,
            TotalBeans = playerInformation.TotalAcorns,
            TotalChance = playerInformation.PurchasedChancesCount
        });
        return new Empty();
    }
    
    // send locked money
}