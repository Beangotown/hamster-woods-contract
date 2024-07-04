using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Contracts.HamsterWoods;

public partial class HamsterWoodsContract
{
    private PlayerInformation SetPlayerInfo(bool resetStart)
    {
        var playerInformation = GetCurrentPlayerInformation(Context.Sender, true);
        if (resetStart)
        {
            playerInformation.CurGridNum = 0;
        }

        ReSetPlayerAcorns(playerInformation);
        return playerInformation;
    }

    private void ReSetPlayerAcorns(PlayerInformation playerInformation)
    {
        var acornBalance = State.TokenContract.GetBalance.Call(new GetBalanceInput
        {
            Owner = Context.Sender,
            Symbol = HamsterWoodsContractConstants.AcornsSymbol
        }).Balance;
        playerInformation.TotalAcorns = acornBalance;
    }


    private void SetPlayerInformation(PlayerInformation playerInformation, BoutInformation boutInformation)
    {
        if (playerInformation.PlayableCount > 0)
        {
            playerInformation.PlayableCount--;
        }
        else
        {
            playerInformation.PurchasedChancesCount--;
        }

        playerInformation.LastPlayTime = Context.CurrentBlockTime;
        playerInformation.LastBingoBlockHeight = Context.CurrentHeight;
        playerInformation.CurGridNum = boutInformation.EndGridNum;

        var currentWeek = GetWeekNum();
        var weeklyAcorns = State.UserWeeklyAcorns[Context.Sender][currentWeek];
        var acornsAmount = boutInformation.Score * HamsterWoodsContractConstants.AcornsDecimalsValue;

        playerInformation.WeeklyAcorns = weeklyAcorns.Add(acornsAmount);
        State.UserWeeklyAcorns[Context.Sender][currentWeek] = playerInformation.WeeklyAcorns;

        playerInformation.LockedAcorns = playerInformation.LockedAcorns.Add(acornsAmount);
        playerInformation.SumScores = playerInformation.SumScores.Add(boutInformation.Score);
        State.PlayerInformation[boutInformation.PlayerAddress] = playerInformation;
    }

    private void SetBoutInformationBingoInfo(Hash playId, Hash randomHash, PlayerInformation playerInformation,
        BoutInformation boutInformation)
    {
        var usefulHash = HashHelper.XorAndCompute(randomHash, playId);
        var dices = GetDices(usefulHash, boutInformation.DiceCount);
        var randomNum = dices.Sum();
        var curGridNum = GetPlayerCurGridNum(playerInformation.CurGridNum, randomNum);
        var gridType = State.GridTypeList.Value.Value[curGridNum];
        boutInformation.Score = GetScoreByGridType(gridType, usefulHash);
        boutInformation.DiceNumbers.AddRange(dices);
        boutInformation.GridNum = randomNum;
        boutInformation.StartGridNum = playerInformation.CurGridNum;
        boutInformation.EndGridNum = curGridNum;
        boutInformation.GridType = gridType;
        boutInformation.BingoBlockHeight = Context.CurrentHeight;
        State.BoutInformation[playId] = boutInformation;
    }

    private Int32 GetScoreByGridType(GridType gridType, Hash usefulHash)
    {
        int score;
        if (gridType == GridType.Blue)
        {
            score = HamsterWoodsContractConstants.BlueGridScore;
        }
        else if (gridType == GridType.Red)
        {
            score = HamsterWoodsContractConstants.RedGridScore;
        }
        else
        {
            var gameRules = State.GameRules.Value;
            var minScore = 30;
            var maxScore = 50;
            if (gameRules != null)
            {
                if (Context.CurrentBlockTime.CompareTo(gameRules.BeginTime) >= 0 &&
                    Context.CurrentBlockTime.CompareTo(gameRules.EndTime) <= 0)
                {
                    minScore = gameRules.MinScore;
                    maxScore = gameRules.MaxScore;
                }
            }

            score = Convert.ToInt32(Math.Abs(usefulHash.ToInt64() % (maxScore - minScore + 1)) + minScore);
        }

        return score;
    }

    private PlayerInformation GetCurrentPlayerInformation(Address playerAddress, bool nftEnough)
    {
        var playerInformation = State.PlayerInformation[playerAddress];
        if (playerInformation == null)
        {
            playerInformation = new PlayerInformation
            {
                PlayerAddress = playerAddress,
                CurGridNum = 0,
                AcornsDecimals = HamsterWoodsContractConstants.AcornsDecimals,
                DailyPlayableCount = State.GameLimitSettings.Value.DailyMaxPlayCount,
                WeeklyPurchasedChancesCount = State.PurchaseChanceConfig.Value.WeeklyPurchaseCount
            };
        }

        var gameLimitSettings = State.GameLimitSettings.Value;
        playerInformation.PlayableCount = GetPlayableCount(gameLimitSettings, playerInformation, nftEnough);
        playerInformation.HamsterPassOwned = nftEnough;
        return playerInformation;
    }

    private Int32 GetPlayableCount(GameLimitSettings gameLimitSettings, PlayerInformation playerInformation,
        bool nftEnough)
    {
        if (!nftEnough) return 0;
        var now = Context.CurrentBlockTime.ToDateTime();
        var playCountResetDateTime =
            new DateTime(now.Year, now.Month, now.Day, gameLimitSettings.DailyPlayCountResetHours, 0, 0,
                DateTimeKind.Utc).ToTimestamp();
        // LastPlayTime ,CurrentTime must not be same DayField
        if (playerInformation.LastPlayTime == null || Context.CurrentBlockTime.CompareTo(
                                                       playerInformation.LastPlayTime.AddDays(1)
                                                   ) > -1
                                                   || (playerInformation.LastPlayTime.CompareTo(
                                                           playCountResetDateTime) == -1 &&
                                                       Context.CurrentBlockTime.CompareTo(playCountResetDateTime) >
                                                       -1))
        {
            return gameLimitSettings.DailyMaxPlayCount;
        }

        return playerInformation.PlayableCount;
    }

    private Int32 GeWeeklyPurchasedChanceCount(PurchaseChanceConfig purchaseChanceConfig,
        PlayerInformation playerInformation)
    {
        GetWeekNum();
        if (playerInformation.LastPurchaseChanceTime == null ||
            playerInformation.LastPurchaseChanceTime.CompareTo(State.RaceTimeInfo.Value.BeginTime) < 0)
        {
            return 0;
        }

        return playerInformation.PurchasedChancesCount;
    }

    private Int32 GetRemainingWeeklyPurchasedChanceCount(PurchaseChanceConfig purchaseChanceConfig,
        PlayerInformation playerInformation)
    {
        var purchasedCount = GeWeeklyPurchasedChanceCount(purchaseChanceConfig, playerInformation);
        return purchaseChanceConfig.WeeklyPurchaseCount - purchasedCount;
    }

    private List<int> GetDices(Hash hashValue, int diceCount)
    {
        var hexString = hashValue.ToHex();
        var dices = new List<int>();

        for (int i = 0; i < diceCount; i++)
        {
            var startIndex = i * 8;
            var intValue = int.Parse(hexString.Substring(startIndex, 8),
                System.Globalization.NumberStyles.HexNumber);
            var dice = (intValue % 6 + 5) % 6 + 1;
            dices.Add(dice);
        }

        return dices;
    }

    private int GetPlayerCurGridNum(int preGridNum, int gridNum)
    {
        return (preGridNum + gridNum) %
               State.GridTypeList.Value.Value.Count;
    }

    private void SetWeekNum(Timestamp beginTime, Timestamp calibrationTime, int gameHours)
    {
        if (State.RaceTimeInfo.Value == null)
        {
            State.RaceTimeInfo.Value = new RaceTimeInfo
            {
                BeginTime = beginTime,
                EndTime = calibrationTime.AddHours(gameHours)
            };

            State.RaceTimeInfo.Value.SettleBeginTime = State.RaceTimeInfo.Value.EndTime;
            State.RaceTimeInfo.Value.SettleEndTime = State.RaceTimeInfo.Value.EndTime.AddDays(1);

            return;
        }

        State.RaceTimeInfo.Value.BeginTime = State.RaceConfig.Value.BeginTime;
        State.RaceTimeInfo.Value.EndTime =
            State.RaceConfig.Value.CalibrationTime.AddHours(State.RaceConfig.Value.GameHours);
        State.RaceTimeInfo.Value.SettleBeginTime = State.RaceTimeInfo.Value.EndTime;
        State.RaceTimeInfo.Value.SettleEndTime = State.RaceTimeInfo.Value.EndTime.AddDays(1);
    }

    private int GetWeekNum()
    {
        var raceTimeInfo = State.RaceTimeInfo.Value;
        while (Context.CurrentBlockTime.CompareTo(raceTimeInfo.EndTime) > 0)
        {
            raceTimeInfo.BeginTime = raceTimeInfo.EndTime;
            raceTimeInfo.EndTime = raceTimeInfo.EndTime.AddHours(State.RaceConfig.Value.GameHours);
            raceTimeInfo.SettleBeginTime = raceTimeInfo.EndTime;
            raceTimeInfo.SettleEndTime = raceTimeInfo.EndTime.AddDays(1);

            State.RaceTimeInfo.Value = raceTimeInfo;
            State.CurrentWeek.Value.Add(1);
        }

        if (State.CurrentWeek.Value == 0)
        {
            State.CurrentWeek.Value = HamsterWoodsContractConstants.StartWeekNum;
        }

        return State.CurrentWeek.Value;
    }

    private bool IsBegin()
    {
        var beginTime = State.RaceConfig.Value.BeginTime;
        if (Context.CurrentBlockTime.CompareTo(beginTime) < 0)
        {
            return false;
        }

        return true;
    }
}