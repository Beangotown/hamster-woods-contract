using System.Collections.Generic;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Contracts.HamsterWoods;

public partial class HamsterWoodsContract
{
    public override Empty Join(JoinInput input)
    {
        Assert(input != null && !string.IsNullOrWhiteSpace(input.Domain), "Invalid input.");
        Assert(!State.JoinRecord[input.Address], "Already joined.");
        Assert(State.ManagerList.Value.Value.Contains(Context.Sender), "No permission.");
        
        JoinPointsContract(input.Domain, input.Address);

        return new Empty();
    }

    public override BoolValue GetJoinRecord(Address address)
    {
        return new BoolValue { Value = State.JoinRecord[address] };
    }

    private void JoinPointsContract(string domain, Address registrant)
    {
        if (!IsHashValid(State.PointsContractDAppId.Value) || State.PointsContract.Value == null)
        {
            return;
        }

        if (State.JoinRecord[registrant]) return;

        if (string.IsNullOrWhiteSpace(domain))
        {
            domain = State.OfficialDomain.Value;
        }

        State.JoinRecord[registrant] = true;

        State.PointsContract.Join.Send(new Points.Contracts.Point.JoinInput
        {
            DappId = State.PointsContractDAppId.Value,
            Domain = domain,
            Registrant = registrant
        });

        Context.Fire(new Joined
        {
            Domain = domain,
            Registrant = registrant
        });
    }

    public override Empty AcceptReferral(AcceptReferralInput input)
    {
        Assert(State.ManagerList.Value.Value.Contains(Context.Sender), "No permission.");
        Assert(input != null, "Invalid input.");
        Assert(IsAddressValid(input.Referrer) && State.JoinRecord[input.Referrer], "Invalid referrer.");
        Assert(!State.JoinRecord[input.Invitee], "Already joined.");

        var invitee = input.Invitee;
        State.JoinRecord[invitee] = true;

        State.PointsContract.AcceptReferral.Send(new Points.Contracts.Point.AcceptReferralInput
        {
            DappId = State.PointsContractDAppId.Value,
            Referrer = input.Referrer,
            Invitee = invitee
        });

        Context.Fire(new ReferralAccepted
        {
            Invitee = invitee,
            Referrer = input.Referrer
        });

        return new Empty();
    }

    public override Empty SetPointConfig(PonitConfigInput input)
    {
        Assert(State.ManagerList.Value.Value.Contains(Context.Sender), "No permission.");
        Assert(IsAddressValid(input.PointContractAddress), "Invalid PointContractAddress.");
        Assert(IsHashValid(input.DappId), "Invalid DappId.");
        Assert(!string.IsNullOrWhiteSpace(input.OfficialDomain), "Invalid OfficialDomain.");

        State.PointsContract.Value = input.PointContractAddress;
        State.PointsContractDAppId.Value = input.DappId;
        State.OfficialDomain.Value = input.OfficialDomain;
        return new Empty();
    }

    public override PointConfig GetPointConfig(Empty input)
    {
        return new PointConfig
        {
            DappId = State.PointsContractDAppId.Value,
            PointContractAddress = State.PointsContract.Value,
            OfficialDomain = State.OfficialDomain.Value
        };
    }

    public override Empty Settle(SettleInput input)
    {
        Assert(State.ManagerList.Value.Value.Contains(Context.Sender), "No permission.");
        Assert(!string.IsNullOrWhiteSpace(input.ActionName) && IsAddressValid(input.UserAddress), "Invalid input.");

        JoinPointsContract(null, input.UserAddress);
        State.PointsContract.Settle.Send(new Points.Contracts.Point.SettleInput
        {
            DappId = State.PointsContractDAppId.Value,
            ActionName = input.ActionName,
            UserAddress = input.UserAddress,
            UserPointsValue = input.UserPointsValue,
            UserPoints = input.UserPoints
        });
        return new Empty();
    }

    public override Empty BatchSettle(BatchSettleInput input)
    {
        Assert(State.ManagerList.Value.Value.Contains(Context.Sender), "No permission.");
        Assert(input.UserPointsList != null && input.UserPointsList.Count > 0, "Invalid input.");
        var userPointsList = new List<Points.Contracts.Point.UserPoints>();
        foreach (var userPoints in input.UserPointsList)
        {
            JoinPointsContract(null, userPoints.UserAddress);
            userPointsList.Add(new Points.Contracts.Point.UserPoints
            {
                UserAddress = userPoints.UserAddress,
                UserPointsValue = userPoints.UserPointsValue
            });
        }

        State.PointsContract.BatchSettle.Send(new Points.Contracts.Point.BatchSettleInput
        {
            ActionName = input.ActionName,
            DappId = State.PointsContractDAppId.Value,
            UserPointsList = { userPointsList }
        });
        return new Empty();
    }
}