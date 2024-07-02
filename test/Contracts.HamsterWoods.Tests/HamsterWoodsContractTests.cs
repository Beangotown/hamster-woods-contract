using System;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Contracts.HamsterWoods;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Contracts.HamsterWoods.Tests
{
    public class HamsterWoodsContractTests : HamsterWoodsContractTestBase
    {
        [Fact]
        public async Task InitializeTests()
        {
            await HamsterWoodsContractStub.Initialize.SendAsync(new Empty());
        }

        [Fact]
        public async Task Play_FailTests()
        {
            var tx = await HamsterWoodsContractStub.Play.SendWithExceptionAsync(new PlayInput
            {
                ResetStart = true
            });
            tx.TransactionResult.Error.ShouldContain("Invalid raceConfig");

            await HamsterWoodsContractStub.SetRaceConfig.SendAsync(new RaceConfig()
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                GameHours = 7 * 24,
                IsRace = true,
                CalibrationTime = DateTime.UtcNow.AddDays(-1).ToTimestamp()
            });
            
            var tx2 = await HamsterWoodsContractStub.Play.SendWithExceptionAsync(new PlayInput
            {
                ResetStart = true
            });
            tx2.TransactionResult.Error.ShouldContain("HamsterPass Balance is not enough");
        }

        private async Task SetConfig()
        {
            await HamsterWoodsContractStub.SetRaceConfig.SendAsync(new RaceConfig()
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                GameHours = 7 * 24,
                IsRace = true,
                CalibrationTime = DateTime.UtcNow.AddDays(-1).ToTimestamp()
            });
        }


        private async Task<Hash> PlayAsync(bool resetStart)
        {
            await SetConfig();
            var tx = await HamsterWoodsContractStub.Play.SendAsync(new PlayInput
            {
                ResetStart = resetStart,
                DiceCount = 3
            });
            return tx.TransactionResult.TransactionId;
        }

        [Fact]
        public async void ChangeAdmin_WithValidInput_ShouldUpdateAdmin()
        {
            var newAdminAddress = new Address { Value = HashHelper.ComputeFrom("NewAdmin").Value };
            await HamsterWoodsContractStub.ChangeAdmin.SendAsync(newAdminAddress);
            var getAdminAddress = await HamsterWoodsContractStub.GetAdmin.CallAsync(new Empty());
            Assert.Equal(newAdminAddress, getAdminAddress);
        }

        [Fact]
        public async void ChangeAdmin_Fail()
        {
            var newAdminAddress = new Address { Value = HashHelper.ComputeFrom("NewAdmin").Value };
            var checkRe = await UserStub.ChangeAdmin.SendWithExceptionAsync(newAdminAddress);
            checkRe.TransactionResult.Error.ShouldContain("No permission.");
        }

        [Fact]
        public async void GetAdmin_ShouldReturnAdminAddress()
        {
            var getAdminAddress = await HamsterWoodsContractStub.GetAdmin.CallAsync(new Empty());
            Assert.Equal(DefaultAddress, getAdminAddress);
        }

        [Fact]
        public async Task GetBoutInformationTests_Fail_InvalidInput()
        {
            var result =
                await HamsterWoodsContractStub.GetBoutInformation.SendWithExceptionAsync(new GetBoutInformationInput());
            result.TransactionResult.Error.ShouldContain("Invalid playId");

            result = await HamsterWoodsContractStub.GetBoutInformation.SendWithExceptionAsync(
                new GetBoutInformationInput
                {
                    PlayId = Hash.Empty
                });
            result.TransactionResult.Error.ShouldContain("Bout not found.");
        }

        [Fact]
        public async Task SetLimitSettingsTests()
        {
            var settings = await HamsterWoodsContractStub.GetGameLimitSettings.CallAsync(new Empty());
            settings.DailyMaxPlayCount.ShouldBe(HamsterWoodsContractConstants.DailyMaxPlayCount);
            settings.DailyPlayCountResetHours.ShouldBe(HamsterWoodsContractConstants.DailyPlayCountResetHours);
            var dailyMaxPlayCount = 4;
            var dailyPlayCountResetHours = 8;
            await HamsterWoodsContractStub.SetGameLimitSettings.SendAsync(new GameLimitSettings()
            {
                DailyMaxPlayCount = dailyMaxPlayCount,
                DailyPlayCountResetHours = dailyPlayCountResetHours
            });

            settings = await HamsterWoodsContractStub.GetGameLimitSettings.CallAsync(new Empty());
            settings.DailyMaxPlayCount.ShouldBe(dailyMaxPlayCount);
            settings.DailyPlayCountResetHours.ShouldBe(dailyPlayCountResetHours);
            await PlayInitAsync();
            await PlayAsync(true);
            var playerInformation = await HamsterWoodsContractStub.GetPlayerInformation.CallAsync(DefaultAddress);
            playerInformation.PlayableCount.ShouldBe(dailyMaxPlayCount - 1);
        }

        [Fact]
        public async Task SetLimitSettingsTests_Fail_NoPermission()
        {
            var result = await UserStub.SetGameLimitSettings.SendWithExceptionAsync(new GameLimitSettings()
            {
                DailyMaxPlayCount = 6,
                DailyPlayCountResetHours = 8
            });

            result.TransactionResult.Error.ShouldContain("No permission");
        }


        [Fact]
        public async Task SetLimitSettingsTests_Fail_InvalidInput()
        {
            var result = await HamsterWoodsContractStub.SetGameLimitSettings.SendWithExceptionAsync(
                new GameLimitSettings()
                {
                    DailyMaxPlayCount = -1
                });
            result.TransactionResult.Error.ShouldContain("Invalid DailyMaxPlayCount");

            result = await HamsterWoodsContractStub.SetGameLimitSettings.SendWithExceptionAsync(new GameLimitSettings
            {
                DailyMaxPlayCount = 1,
                DailyPlayCountResetHours = 80
            });
            result.TransactionResult.Error.ShouldContain("Invalid DailyPlayCountResetHours");
        }

        private async Task PlayInitAsync()
        {
            await SetConfig();
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Symbol = HamsterWoodsContractConstants.HamsterPassSymbol,
                Amount = 1,
                Memo = "ddd",
                To = DefaultAddress
            });
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Symbol = HamsterWoodsContractConstants.AcornSymbol,
                Amount = 100000000000000,
                Memo = "Issue",
                To = DAppContractAddress
            });
        }


        [Fact]
        public async Task CheckHamsterPass_Test()
        {
            await PlayInitAsync();
            var BalanceRe = await HamsterWoodsContractStub.CheckHamsterPass.CallAsync(DefaultAddress);
            BalanceRe.Value.ShouldBe(true);
        }

        [Fact]
        public async Task PlayNewTests()
        {
            await PlayInitAsync();
            int sumScore = 0;
            int sumGridNum = 0;
            for (int i = 0; i < 5; i++)
            {
                var boutInformation = await PlayNewTest();
                sumScore += boutInformation.Score;
                sumGridNum = (sumGridNum + boutInformation.GridNum) % 18;
            }

            var playerInfo = await HamsterWoodsContractStub.GetPlayerInformation.CallAsync(DefaultAddress);
            playerInfo.LockedAcorns.ShouldBe(sumScore);
            playerInfo.TotalAcorns.ShouldBe(0);
            playerInfo.SumScores.ShouldBe(sumScore);
            playerInfo.CurGridNum.ShouldBe(sumGridNum);
        }

        private async Task<BoutInformation> PlayNewTest()
        {
            var result = await HamsterWoodsContractStub.Play.SendAsync(new PlayInput()
            {
                DiceCount = 2,
                ResetStart = false
            });
            var boutInformation = await HamsterWoodsContractStub.GetBoutInformation.CallAsync(
                new GetBoutInformationInput
                {
                    PlayId = result.TransactionResult.TransactionId
                });
            boutInformation.BingoBlockHeight.ShouldNotBeNull();
            boutInformation.GridNum.ShouldBeInRange(1, 18);
            if (boutInformation.GridType == GridType.Blue)
            {
                boutInformation.Score.ShouldBe(1);
            }
            else if (boutInformation.GridType == GridType.Red)
            {
                boutInformation.Score.ShouldBe(5);
            }
            else
            {
                var gameRules = await HamsterWoodsContractStub.GetGameRules.CallAsync(new Empty());
                var minScore = 30;
                var maxScore = 50;
                if (gameRules != null)
                {
                    if (DateTime.UtcNow.ToTimestamp().CompareTo(gameRules.BeginTime) >= 0 &&
                        DateTime.UtcNow.ToTimestamp().CompareTo(gameRules.EndTime) <= 0)
                    {
                        minScore = gameRules.MinScore;
                        maxScore = gameRules.MaxScore;
                    }
                }

                boutInformation.Score.ShouldBeInRange(minScore, maxScore);
            }

            return boutInformation;
        }

        [Fact]
        public async Task SetGameRules_Test()
        {
            var result = await UserStub.SetGameRules.SendWithExceptionAsync(new GameRules()
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                EndTime = DateTime.UtcNow.AddDays(2).ToTimestamp(),
                MinScore = 1,
                MaxScore = 10
            });

            result.TransactionResult.Error.ShouldContain("No permission");
            result = await HamsterWoodsContractStub.SetGameRules.SendWithExceptionAsync(new GameRules()
            {
                BeginTime = DateTime.UtcNow.AddDays(2).ToTimestamp(),
                EndTime = DateTime.UtcNow.AddDays(1).ToTimestamp(),
                MinScore = 1,
                MaxScore = 10
            });
            result.TransactionResult.Error.ShouldContain("Invalid EndTime");
            result = await HamsterWoodsContractStub.SetGameRules.SendWithExceptionAsync(new GameRules()
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                EndTime = DateTime.UtcNow.AddDays(2).ToTimestamp(),
                MinScore = 0,
                MaxScore = 10
            });
            result.TransactionResult.Error.ShouldContain("Invalid MinScore");
            result = await HamsterWoodsContractStub.SetGameRules.SendWithExceptionAsync(new GameRules()
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                EndTime = DateTime.UtcNow.AddDays(2).ToTimestamp(),
                MinScore = 10,
                MaxScore = 1
            });
            result.TransactionResult.Error.ShouldContain("Invalid MaxScore");
            result = await HamsterWoodsContractStub.SetGameRules.SendAsync(new GameRules()
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                EndTime = DateTime.UtcNow.AddDays(2).ToTimestamp(),
                MinScore = 1,
                MaxScore = 10
            });
            result.TransactionResult.TransactionId.ShouldNotBeNull();
            await PlayNewTests();
        }

        [Fact]
        public async Task SetRankingRules_Test()
        {
            var beginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp();
            var result = await UserStub.SetRankingRules.SendWithExceptionAsync(new RankingRules
            {
                BeginTime = beginTime,
                WeeklyTournamentBeginNum = 1,
                RankingHours = 10,
                PublicityHours = 2,
                RankingPlayerCount = 10,
                PublicityPlayerCount = 2,
            });

            result.TransactionResult.Error.ShouldContain("No permission");
            result = await HamsterWoodsContractStub.SetRankingRules.SendWithExceptionAsync(new RankingRules
            {
                BeginTime = beginTime,
                WeeklyTournamentBeginNum = 0,
                RankingHours = 10,
                PublicityHours = 2,
                RankingPlayerCount = 10,
                PublicityPlayerCount = 2,
            });
            result.TransactionResult.Error.ShouldContain("Invalid WeeklyTournamentBeginNum");
            result = await HamsterWoodsContractStub.SetRankingRules.SendWithExceptionAsync(new RankingRules
            {
                BeginTime = beginTime,
                WeeklyTournamentBeginNum = 1,
                RankingHours = 0,
                PublicityHours = 2,
                RankingPlayerCount = 10,
                PublicityPlayerCount = 2,
            });
            result.TransactionResult.Error.ShouldContain("Invalid RankingHours");
            result = await HamsterWoodsContractStub.SetRankingRules.SendAsync(new RankingRules
            {
                BeginTime = beginTime,
                WeeklyTournamentBeginNum = 1,
                RankingHours = 10,
                PublicityHours = 2,
                RankingPlayerCount = 10,
                PublicityPlayerCount = 2,
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var rankingRules = await UserStub.GetRankingRules.CallAsync(new Empty());
            rankingRules.BeginTime.ShouldBe(beginTime);
            rankingRules.WeeklyTournamentBeginNum.ShouldBe(1);
            rankingRules.RankingHours.ShouldBe(10);
            rankingRules.PublicityHours.ShouldBe(2);
            rankingRules.RankingPlayerCount.ShouldBe(10);
            rankingRules.PublicityPlayerCount.ShouldBe(2);
        }

        //[Fact]
        public async Task SetPurchaseChanceConfig_Test()
        {
            var result = await UserStub.SetPurchaseChanceConfig.SendWithExceptionAsync(new PurchaseChanceConfig
            {
                AcornsAmount = 10
            });

            result.TransactionResult.Error.ShouldContain("No permission");
            result = await HamsterWoodsContractStub.SetPurchaseChanceConfig.SendWithExceptionAsync(
                new PurchaseChanceConfig
                {
                    AcornsAmount = 0
                });
            result.TransactionResult.Error.ShouldContain("Invalid AcornsAmount");
            result = await HamsterWoodsContractStub.SetPurchaseChanceConfig.SendWithExceptionAsync(
                new PurchaseChanceConfig
                {
                    AcornsAmount = 10
                });
            result.TransactionResult.Error.ShouldContain("Invalid DailyPurchaseCount");
            result = await HamsterWoodsContractStub.SetPurchaseChanceConfig.SendAsync(new PurchaseChanceConfig
            {
                AcornsAmount = 10,
                WeeklyPurchaseCount = 10
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var purchaseChanceConfig = await HamsterWoodsContractStub.GetPurchaseChanceConfig.CallAsync(new Empty());
            purchaseChanceConfig.AcornsAmount.ShouldBe(10);
        }

        [Fact]
        public async Task PlayWithRanking()
        {
            await PlayInitAsync();
            var result = await HamsterWoodsContractStub.SetRankingRules.SendAsync(new RankingRules
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                WeeklyTournamentBeginNum = 1,
                RankingHours = 10,
                PublicityHours = 0,
                RankingPlayerCount = 10,
                PublicityPlayerCount = 2,
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var setRaceConfigResult = await HamsterWoodsContractStub.SetRaceConfig.SendAsync(new RaceConfig
            {
                BeginTime = DateTime.UtcNow.AddDays(-1).ToTimestamp(),
                GameHours = 7 * 24,
                IsRace = true
            });
            setRaceConfigResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            int sumScore = 0;
            int sumGridNum = 0;
            for (int i = 0; i < 5; i++)
            {
                var boutInformation = await PlayNewTest();
                sumScore += boutInformation.Score;
                sumGridNum = (sumGridNum + boutInformation.GridNum) % 18;
            }

            var playerInfo = await HamsterWoodsContractStub.GetPlayerInformation.CallAsync(DefaultAddress);
            playerInfo.TotalAcorns.ShouldBe(0);
            playerInfo.LockedAcorns.ShouldBe(sumScore);
            playerInfo.WeeklyAcorns.ShouldBe(sumScore);
            playerInfo.PlayableCount.ShouldBe(HamsterWoodsContractConstants.DailyMaxPlayCount - 5);
        }

        [Fact]
        public async Task PurchaseChance()
        {
            await PurchaseChanceInit();
            var result = await HamsterWoodsContractStub.SetPurchaseChanceConfig.SendAsync(new PurchaseChanceConfig
            {
                AcornsAmount = 25,
                WeeklyPurchaseCount = 20
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            result = await HamsterWoodsContractStub.PurchaseChance.SendAsync(new Int32Value
            {
                Value = 10
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var playerInfo = await HamsterWoodsContractStub.GetPlayerInformation.CallAsync(DefaultAddress);
            playerInfo.PurchasedChancesCount.ShouldBe(10);
        }

        private async Task PurchaseChanceInit()
        {
            await SetConfig();
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Symbol = HamsterWoodsContractConstants.HamsterPassSymbol,
                Amount = 1,
                Memo = "ddd",
                To = DefaultAddress
            });
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Symbol = HamsterWoodsContractConstants.AcornSymbol,
                Amount = 100000000000000,
                Memo = "Issue",
                To = DefaultAddress
            });

            await TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = HamsterWoodsContractConstants.AcornSymbol,
                Amount = 1000000000000,
                Spender = DAppContractAddress,
            });
        }
    }
}