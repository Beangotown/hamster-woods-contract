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
        Assert(input.DailyPurchaseCount > 0, "Invalid DailyPurchaseCount.");
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
}