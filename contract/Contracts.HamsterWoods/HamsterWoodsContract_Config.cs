using AElf;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Contracts.HamsterWoods;

public partial class HamsterWoodsContract
{
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

    public override Empty SetGameLimitSettings(GameLimitSettings input)
    {
        Assert(State.Admin.Value == Context.Sender, "No permission.");
        Assert(input.DailyPlayCountResetHours >= 0 && input.DailyPlayCountResetHours < 24,
            "Invalid dailyPlayCountResetHours.");
        Assert(input.DailyMaxPlayCount >= 0, "Invalid dailyMaxPlayCount.");
        State.GameLimitSettings.Value = input;
        return new Empty();
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
        Assert(input.AcornsAmount > 0, "Invalid AcornsAmount.");
        Assert(input.WeeklyPurchaseCount > 0, "Invalid WeeklyPurchaseCount.");
        State.PurchaseChanceConfig.Value = input;
        return new Empty();
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

    public override Empty SetUnlockManager(Address input)
    {
        Assert(State.Initialized.Value, "Not initialized.");
        Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid input.");
        Assert(State.Admin.Value == Context.Sender, "No permission.");

        State.ManagerList.Value.Value.Add(input);
        return new Empty();
    }

    public override Empty StartRace(Empty input)
    {
        Assert(State.Initialized.Value, "Not initialized.");
        Assert(State.Admin.Value == Context.Sender, "No permission.");
        State.RaceConfig.Value.IsRace = true;
        if (State.CurrentWeek.Value == 0)
        {
            State.CurrentWeek.Value = 1;
        }

        return new Empty();
    }

    public override Empty StopRace(Empty input)
    {
        Assert(State.Initialized.Value, "Not initialized.");
        Assert(State.Admin.Value == Context.Sender, "No permission.");
        State.RaceConfig.Value.IsRace = false;
        return new Empty();
    }

    public override Empty SetRaceConfig(RaceConfig input)
    {
        Assert(State.Initialized.Value, "Not initialized.");
        Assert(State.Admin.Value == Context.Sender, "No permission.");

        State.RaceConfig.Value = input;
        SetWeekNum(input.BeginTime, input.CalibrationTime, input.GameHours);
        return new Empty();
    }
}