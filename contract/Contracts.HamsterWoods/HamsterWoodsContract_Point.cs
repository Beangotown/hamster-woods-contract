using System.Collections.Generic;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Points.Contracts.Point;

namespace Contracts.HamsterWoods;

public partial class HamsterWoodsContract
{
    public override Empty Join(JoinInput input)
    {
        Assert(input != null && !string.IsNullOrWhiteSpace(input.Domain), "Invalid input.");
        Assert(!State.JoinRecord[input.Address], "Already joined.");

        JoinPointsContract(input);

        return new Empty();
    }

    public override BoolValue GetJoinRecord(Address address)
    {
        return new BoolValue { Value = State.JoinRecord[address] };
    }

    private void JoinPointsContract(JoinInput input)
    {
        var registrant = input.Address;
        if (!IsHashValid(State.PointsContractDAppId.Value) || State.PointsContract.Value == null)
        {
            return;
        }

        if (State.JoinRecord[registrant]) return;

        var domain = input.Domain;
        domain ??= State.PointsContract.GetDappInformation.Call(new GetDappInformationInput
        {
            DappId = State.PointsContractDAppId.Value
        })?.DappInfo?.OfficialDomain;

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
        Assert(IsAddressValid(input.PointContractAddress), "Invalid point contract.");
        Assert(IsHashValid(input.DappId), "Invalid dapp id");

        State.PointsContract.Value = input.PointContractAddress;
        State.PointsContractDAppId.Value = input.DappId;
        return new Empty();
    }

    public override PointConfig GetPointConfig(Empty input)
    {
        return new PointConfig
        {
            DappId = State.PointsContractDAppId.Value,
            PointContractAddress = State.PointsContract.Value
        };
    }

    public override Empty Settle(SettleInput input)
    {
        Assert(!string.IsNullOrWhiteSpace(input.ActionName) && IsAddressValid(input.UserAddress), "Invalid input.");
        // JoinPointsContract(new JoinInput
        // {
        //     Domain = null,
        //     Address = input.UserAddress
        // });

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
        Assert(input.UserPointsList != null && input.UserPointsList.Count > 0, "Invalid input.");
        var userPointsList = new List<Points.Contracts.Point.UserPoints>();
        foreach (var userPoints in input.UserPointsList)
        {
            // JoinPointsContract(new JoinInput
            // {
            //     Domain = null,
            //     Address = userPoints.UserAddress
            // });
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