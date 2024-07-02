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
        Assert(State.RaceConfig.Value !=null, "Invalid raceConfig");
        var playerInformation = SetPlayerInfo(input.ResetStart);
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

        // not include race. and settle end time not set
        SetLockedAcornsInfo(Context.Sender, boutInformation.Score);

        Context.Fire(new Picked
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
            WeeklyAcorns = playerInformation.WeeklyAcorns,
            TotalAcorns = playerInformation.TotalAcorns,
            TotalChance = playerInformation.PurchasedChancesCount,
            WeekNum = State.CurrentWeek.Value,
            IsRace = true//State.RaceConfig.Value.IsRace
        });
        return new Empty();
    }

    private void SetLockedAcornsInfo(Address address, int score)
    {
        var lockedAcornsInfoList = State.LockedAcornsInfoList[address];
        var weekNum = State.CurrentWeek.Value;
        if (lockedAcornsInfoList == null || lockedAcornsInfoList.Value == null || lockedAcornsInfoList.Value.Count == 0)
        {
            State.LockedAcornsInfoList[address] = new LockedAcornsInfoList
            {
                Value =
                {
                    new LockedAcornsInfo
                    {
                        Acorns = score,
                        Week = weekNum,
                        //SettleTime = State.RaceTimeInfo.Value.SettleEndTime,  //Value is null
                        IsUnlocked = false
                    }
                }
            };

            return;
        }

        var lockedInfo = lockedAcornsInfoList.Value.FirstOrDefault(t => t.Week == weekNum);
        if (lockedInfo == null)
        {
            lockedAcornsInfoList.Value.Add(new LockedAcornsInfo()
            {
                Acorns = score,
                Week = weekNum,
                SettleTime = State.RaceTimeInfo.Value.SettleEndTime,
                IsUnlocked = false
            });
        }
        else
        {
            lockedInfo.Acorns += score;
        }
    }

    public override Empty PurchaseChance(Int32Value input)
    {
        var acornsAmount = State.PurchaseChanceConfig.Value.AcornsAmount;
        Assert(acornsAmount > 0, "PurchaseChance is not allowed");
        var playerInformation = SetPlayerInfo(false);
        ReSetPlayerAcorns(playerInformation);
        Assert(playerInformation.TotalAcorns >= input.Value * acornsAmount, "Acorns is not enough");
        Assert(
            GetRemainingDailyPurchasedChanceCount(State.PurchaseChanceConfig.Value, playerInformation) <=
            input.Value, "Purchase chance is not enough");

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

        Context.Fire(new ChancePurchased
        {
            PlayerAddress = Context.Sender,
            AcornsAmount = input.Value * acornsAmount,
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
        Assert(input.Value.Count > 0 && input.Value.Count < 20, "Invalid input.");

        foreach (var address in input.Value)
        {
            UnlockAcorns(address);
        }

        return new Empty();
    }

    // send locked money
    private void UnlockAcorns(Address input)
    {
        var lockedAcornsInfoList = State.LockedAcornsInfoList[input];
        if (lockedAcornsInfoList == null || lockedAcornsInfoList.Value == null)
        {
            return;
        }

        // time judge
        var needUnlockInfoList = lockedAcornsInfoList.Value.Where(t => !t.IsUnlocked).ToList();
        if (needUnlockInfoList.Count == 0)
        {
            return;
        }

        var amount = needUnlockInfoList.Sum(t => t.Acorns);
        foreach (var item in needUnlockInfoList)
        {
            item.IsUnlocked = true;
        }

        State.TokenContract.Transfer.Send(new TransferInput
        {
            To = input,
            Symbol = HamsterWoodsContractConstants.AcornSymbol,
            Amount = amount
        });
        
        Context.Fire(new AcornsUnlocked
        {
            From = Context.Self,
            To = input,
            Symbol = HamsterWoodsContractConstants.AcornSymbol,
            Amount = amount
        });
    }
}