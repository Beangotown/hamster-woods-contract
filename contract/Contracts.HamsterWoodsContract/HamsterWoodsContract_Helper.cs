using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Contracts.HamsterWoodsContract;

public partial class HamsterWoodsContract
{
    private PlayerInformation InitPlayerInfo(bool resetStart)
    {
        Assert(CheckBeanPass(Context.Sender).Value, "BeanPass Balance is not enough");
        var playerInformation = GetCurrentPlayerInformation(Context.Sender, true);
        Assert(playerInformation.PlayableCount > 0 || playerInformation.PurchasedChancesCount > 0,
            "PlayableCount is not enough");
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
            Symbol = HamsterWoodsContractConstants.AcornSymbol
        }).Balance;
        playerInformation.TotalAcorns = acornBalance;
    }

    private bool IsWeekRanking()
    {
        var rankingRules = State.RankingRules.Value;
        if (rankingRules == null)
        {
            return false;
        }

        var beginWeekNum = Math.Max(State.CurrentWeek.Value - rankingRules.WeeklyTournamentBeginNum, 0);
        var tournamentHours = rankingRules.RankingHours.Add(rankingRules.PublicityHours);
        var beginTime = rankingRules.BeginTime.AddHours(tournamentHours.Mul(
            beginWeekNum));
        if (Context.CurrentBlockTime.CompareTo(beginTime) < 0)
        {
            return false;
        }

        var endTime = rankingRules.BeginTime.AddHours(tournamentHours.Mul(beginWeekNum + 1));
        while (Context.CurrentBlockTime.CompareTo(endTime) > 0)
        {
            beginWeekNum++;
            endTime = endTime.AddHours(tournamentHours);
        }

        State.CurrentWeek.Value = rankingRules.WeeklyTournamentBeginNum + beginWeekNum;
        if (Context.CurrentBlockTime.CompareTo(endTime.AddHours(-rankingRules.PublicityHours)) > 0)
        {
            return false;
        }

        return true;
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
        playerInformation.CurGridNum = boutInformation.EndGridNum;
        if (IsWeekRanking())
        {
            var currentWeek = State.CurrentWeek.Value;
            var realWeeklyBeans = (int)
                Math.Min(playerInformation.TotalAcorns, State.UserWeeklyBeans[Context.Sender][currentWeek]);
            playerInformation.WeeklyBeans = realWeeklyBeans.Add(boutInformation.Score);
            State.UserWeeklyBeans[Context.Sender][currentWeek] = (int)playerInformation.WeeklyBeans;
        }

        playerInformation.TotalAcorns = playerInformation.TotalAcorns.Add(boutInformation.Score);
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
                CurGridNum = 0
            };
        }

        var gameLimitSettings = State.GameLimitSettings.Value;
        playerInformation.PlayableCount = GetPlayableCount(gameLimitSettings, playerInformation, nftEnough);
        playerInformation.BeanPassOwned = nftEnough;
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

    private Int32 GetDailyPurchasedChanceCount(PurchaseChanceConfig purchaseChanceConfig,
        PlayerInformation playerInformation)
    {
        var now = Context.CurrentBlockTime.ToDateTime();
        var purchaseCountResetTime =
            new DateTime(now.Year, now.Month, now.Day, purchaseChanceConfig.DailyPurchaseCountResetHour, 0, 0,
                DateTimeKind.Utc).ToTimestamp();
        // LastPlayTime ,CurrentTime must not be same DayField
        if (playerInformation.LastPurchaseChanceTime == null || Context.CurrentBlockTime.CompareTo(
                                                                 playerInformation.LastPurchaseChanceTime.AddDays(1)
                                                             ) > -1
                                                             || (playerInformation.LastPurchaseChanceTime.CompareTo(
                                                                     purchaseCountResetTime) == -1 &&
                                                                 Context.CurrentBlockTime.CompareTo(
                                                                     purchaseCountResetTime) >
                                                                 -1))
        {
            return 0;
        }

        return playerInformation.DailyPurchasedChancesCount;
    }

    private Int32 GetRemainingDailyPurchasedChanceCount(PurchaseChanceConfig purchaseChanceConfig,
        PlayerInformation playerInformation)
    {
        var purchasedCount = GetDailyPurchasedChanceCount(purchaseChanceConfig, playerInformation);
        return purchaseChanceConfig.DailyPurchaseCount - purchasedCount;
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
}