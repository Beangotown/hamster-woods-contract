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

namespace Contracts.HamsterWoodsContract
{
    public class HamsterWoodsContract : HamsterWoodsContractContainer.HamsterWoodsContractBase
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

            ReSetPlayerBeans(playerInformation);
            return playerInformation;
        }

        private void ReSetPlayerBeans(PlayerInformation playerInformation)
        {
            var realBeanBalance = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Owner = Context.Sender,
                Symbol = HamsterWoodsContractConstants.BeanSymbol
            }).Balance;
            playerInformation.TotalBeans = realBeanBalance;
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
            State.TokenContract.Transfer.Send(new TransferInput
            {
                To = Context.Sender,
                Symbol = HamsterWoodsContractConstants.BeanSymbol,
                Amount = boutInformation.Score
            });
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
                TotalBeans = playerInformation.TotalBeans,
                TotalChance = playerInformation.PurchasedChancesCount
            });
            return new PlayOutput { ExpectedBlockHeight = 0 };
        }


        public override Empty PurchaseChance(Int32Value input)
        {
            var beansAmount = State.PurchaseChanceConfig.Value.BeansAmount;
            Assert(beansAmount > 0, "PurchaseChance is not allowed");
            var playerInformation = InitPlayerInfo(false);
            ReSetPlayerBeans(playerInformation);
            Assert(playerInformation.TotalBeans >= input.Value * beansAmount, "Bean is not enough");

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

            playerInformation.TotalBeans -= input.Value * beansAmount;
            playerInformation.PurchasedChancesCount += input.Value;
            State.PlayerInformation[Context.Sender] = playerInformation;
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                From = Context.Sender,
                To = Context.Self,
                Amount = input.Value * beansAmount,
                Symbol = HamsterWoodsContractConstants.BeanSymbol,
                Memo = "PurchaseChance"
            });
            Context.Fire(new PurchasedChance
            {
                PlayerAddress = Context.Sender,
                BeansAmount = input.Value * beansAmount,
                ChanceCount = input.Value,
                WeeklyBeans = playerInformation.WeeklyBeans,
                TotalBeans = playerInformation.TotalBeans,
                TotalChance = playerInformation.PurchasedChancesCount
            });
            return new Empty();
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
                    Math.Min(playerInformation.TotalBeans, State.UserWeeklyBeans[Context.Sender][currentWeek]);
                playerInformation.WeeklyBeans = realWeeklyBeans.Add(boutInformation.Score);
                State.UserWeeklyBeans[Context.Sender][currentWeek] = (int)playerInformation.WeeklyBeans;
            }

            playerInformation.TotalBeans = playerInformation.TotalBeans.Add(boutInformation.Score);
            State.PlayerInformation[boutInformation.PlayerAddress] = playerInformation;
        }

        private int GetPlayerCurGridNum(int preGridNum, int gridNum)
        {
            return (preGridNum + gridNum) %
                   State.GridTypeList.Value.Value.Count;
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

        public override BoolValue CheckBeanPass(Address owner)
        {
            var getBalanceOutput = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = HamsterWoodsContractConstants.BeanPassSymbol,
                Owner = owner
            });
            if (getBalanceOutput.Balance > 0)
            {
                return new BoolValue { Value = true };
            }

            getBalanceOutput = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = HamsterWoodsContractConstants.HalloweenBeanPassSymbol,
                Owner = owner
            });
            return new BoolValue { Value = getBalanceOutput.Balance > 0 };
        }

        public override Empty ChangeAdmin(Address newAdmin)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission.");

            if (State.Admin.Value == newAdmin)
            {
                return new Empty();
            }

            State.Admin.Value = newAdmin;
            return new Empty();
        }

        public override Address GetAdmin(Empty input)
        {
            return State.Admin.Value;
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
            var playerInformation = GetCurrentPlayerInformation(owner, CheckBeanPass(owner).Value);
            return playerInformation;
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

        public override GameLimitSettings GetGameLimitSettings(Empty input)
        {
            return State.GameLimitSettings.Value;
        }

        public override Empty SetGameLimitSettings(GameLimitSettings input)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission.");
            Assert(input.DailyPlayCountResetHours >= 0 && input.DailyPlayCountResetHours < 24,
                "Invalid dailyPlayCountResetHours.");
            Assert(input.DailyMaxPlayCount >= 0, "Invalid dailyMaxPlayCount.");
            State.GameLimitSettings.Value = input;
            return new Empty();
        }

        public override GameRules GetGameRules(Empty input)
        {
            return State.GameRules.Value;
        }

        public override Empty SetGameRules(GameRules input)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission.");
            Assert(input.BeginTime.CompareTo(input.EndTime) < 0,
                "Invalid EndTime.");
            Assert(input.MinScore > 0, "Invalid MinScore.");
            Assert(input.MaxScore >= input.MinScore, "Invalid MaxScore.");

            State.GameRules.Value = input;
            return new Empty();
        }

        public override Empty SetPurchaseChanceConfig(PurchaseChanceConfig input)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission.");
            Assert(input.BeansAmount > 0, "Invalid BeansAmount.");
            State.PurchaseChanceConfig.Value = input;
            return new Empty();
        }

        public override PurchaseChanceConfig GetPurchaseChanceConfig(Empty input)
        {
            return State.PurchaseChanceConfig.Value;
        }

        public override Empty SetRankingRules(RankingRules rankingRules)
        {
            Assert(State.Admin.Value == Context.Sender, "No permission.");
            Assert(rankingRules.WeeklyTournamentBeginNum > 0, "Invalid WeeklyTournamentBeginNum.");
            Assert(rankingRules.RankingHours > 0, "Invalid RankingHours.");
            Assert(rankingRules.RankingPlayerCount > 0, "Invalid RankingPlayerCount.");
            Assert(rankingRules.PublicityPlayerCount > 0, "Invalid PublicityPlayerCount.");

            State.RankingRules.Value = rankingRules;
            return new Empty();
        }

        public override RankingRules GetRankingRules(Empty input)
        {
            return State.RankingRules.Value;
        }
    }
}