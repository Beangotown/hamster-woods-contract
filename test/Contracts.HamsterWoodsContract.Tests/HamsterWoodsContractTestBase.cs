using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AElf.Boilerplate.TestBase;
using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.MultiToken;
using AElf.Cryptography.ECDSA;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Volo.Abp.Threading;

namespace Contracts.HamsterWoodsContract
{
    public class HamsterWoodsContractTestBase : DAppContractTestBase<HamsterWoodsContractTestModule>
    {
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);
        internal HamsterWoodsContractContainer.HamsterWoodsContractStub HamsterWoodsContractStub { get; set; }
        internal HamsterWoodsContractContainer.HamsterWoodsContractStub UserStub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
        internal AEDPoSContractImplContainer.AEDPoSContractImplStub AEDPoSContractStub { get; set; }
        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;
        
        protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;

        protected int SeedNum = 1;
        public HamsterWoodsContractTestBase()
        {
            HamsterWoodsContractStub = GetHamsterWoodsContractStub(DefaultKeyPair);
            UserStub = GetHamsterWoodsContractStub(UserKeyPair);
            TokenContractStub = GetTokenContractTester(DefaultKeyPair);
            AEDPoSContractStub = GetAEDPoSContractStub(DefaultKeyPair);
            AsyncHelper.RunSync(() => HamsterWoodsContractStub.Initialize.SendAsync(new Empty()));
            AsyncHelper.RunSync(() => CreateSeedNftCollection(TokenContractStub));
            AsyncHelper.RunSync(() => CreateNftCollectionAsync(TokenContractStub,new CreateInput
            {
                Symbol = "BEANPASS-0",
                TokenName = "BeanPassSymbol collection",
                TotalSupply = 10,
                Decimals = 0,
                Issuer = DefaultAddress,
                IsBurnable = true,
                Owner = DefaultAddress
            })
            );

            AsyncHelper.RunSync(() => CreateNftAsync(TokenContractStub, new CreateInput()
            {
                Symbol = HamsterWoodsContractConstants.HalloweenBeanPassSymbol,
                TokenName = "BeanPassSymbol",
                TotalSupply = 100,
                Decimals = 0,
                Issuer = DefaultAddress,
                IsBurnable = true,
                Owner = DefaultAddress
            }));
            AsyncHelper.RunSync(() => CreateNftCollectionAsync(TokenContractStub, new CreateInput()
            {
                Symbol = HamsterWoodsContractConstants.AcornSymbol,
                TokenName = "BeanSymbol",
                TotalSupply = 100000000000000,
                Decimals = 2,
                Issuer = DefaultAddress,
                IsBurnable = true,
                Owner = DefaultAddress
            }));
        }

        internal HamsterWoodsContractContainer.HamsterWoodsContractStub GetHamsterWoodsContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<HamsterWoodsContractContainer.HamsterWoodsContractStub>(DAppContractAddress, senderKeyPair);
        }
        
        internal TokenContractContainer.TokenContractStub GetTokenContractTester(ECKeyPair keyPair)
        {
            return GetTester<TokenContractContainer.TokenContractStub>(TokenContractAddress, keyPair);
        }
        
        internal AEDPoSContractImplContainer.AEDPoSContractImplStub GetAEDPoSContractStub(ECKeyPair keyPair)
        {
            return GetTester<AEDPoSContractImplContainer.AEDPoSContractImplStub>(ConsensusContractAddress, keyPair);
        }

        internal async Task CreateSeedNftCollection(TokenContractContainer.TokenContractStub stub)
        {
            var input = new CreateInput
            {
                Symbol = "SEED-0",
                Decimals = 0,
                IsBurnable = true,
                TokenName = "seed Collection",
                TotalSupply = 1,
                Issuer = DefaultAddress
            };
            await stub.Create.SendAsync(input);
        }
        
        internal async Task<CreateInput> CreateNftCollectionAsync(TokenContractContainer.TokenContractStub stub,
            CreateInput createInput)
        {
            var input = BuildSeedCreateInput(createInput);
            await stub.Create.SendAsync(input);
            await stub.Issue.SendAsync(new IssueInput
            {
                Symbol = input.Symbol,
                Amount = 1,
                Memo = "ddd",
                To = DefaultAddress
            });
            await stub.Approve.SendAsync(new ApproveInput()
                { Spender = TokenContractAddress, Symbol = "SEED-" + SeedNum, Amount = 1 });
            await stub.Create.SendAsync(createInput);
            return input;
        }

        private async Task CreateNftAsync(TokenContractContainer.TokenContractStub stub,
            CreateInput createInput)
        {
            await stub.Create.SendAsync(createInput);
            
        }

        internal CreateInput BuildSeedCreateInput(CreateInput createInput)
        {
            Interlocked.Increment(ref SeedNum);
            var input = new CreateInput
            {
                Symbol = "SEED-" + SeedNum,
                Decimals = 0,
                IsBurnable = true,
                TokenName = "seed token 1" ,
                TotalSupply = 1,
                Issuer = DefaultAddress,
               ExternalInfo = new ExternalInfo()
              { Value = { 
                      new Dictionary<string, string>()
                  {
                      ["__seed_owned_symbol"] = createInput.Symbol,
                      ["__seed_exp_time"] = "9992145642"
                  }
              }}
           };
           
            return input;
        }
    }
}