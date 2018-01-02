using System.Threading.Tasks;
using Autofac;
using MiningCore.Blockchain.Bitcoin;
using MiningCore.Blockchain.ZCash;
using MiningCore.Blockchain.ZCash.DaemonResponses;
using MiningCore.DaemonInterface;
using MiningCore.Notifications;
using MiningCore.Time;
using MiningCore.Contracts;
using System;
using MiningCore.Blockchain.Bitcoin.DaemonResponses;
using NBitcoin;

namespace MiningCore.Blockchain.BitcoinQuark
{
    public class BitcoinQuarkJobManager : ZCashJobManager<BitcoinQuarkJob>
    {
        public BitcoinQuarkJobManager(IComponentContext ctx,
            NotificationService notificationService,
            IMasterClock clock,
            IExtraNonceProvider extraNonceProvider) : base(ctx, notificationService, clock, extraNonceProvider)
        {
            getBlockTemplateParams = new object[]
            {
                new
                {
                    capabilities = new[] { "coinbasetxn", "workid", "coinbase/append" },
                    rules = new[] { "segwit" }
                }
            };
        }

        #region Overrides of ZCashJobManager<BitcoinQuarkJob>

        public override async Task<bool> ValidateAddressAsync(string address)
        {
            Contract.Requires<ArgumentException>(!string.IsNullOrEmpty(address), $"{nameof(address)} must not be empty");

            var result = await daemon.ExecuteCmdAnyAsync<ValidateAddressResponse>(
                BitcoinCommands.ValidateAddress, new[] { address });

            return result.Response != null && result.Response.IsValid;
        }

        protected override async Task<DaemonResponse<ZCashBlockTemplate>> GetBlockTemplateAsync()
        {
            var result = await daemon.ExecuteCmdAnyAsync<ZCashBlockTemplate>(
                BitcoinCommands.GetBlockTemplate, getBlockTemplateParams);

            if (result.Error == null && result.Response != null)
            {
                var height = result.Response.Height;
                var subsidyResponse = await daemon.ExecuteCmdAnyAsync<ZCashBlockSubsidy>(BitcoinCommands.GetBlockSubsidy, new[] { height });
                if(subsidyResponse.Error == null)
                {
                    result.Response.Subsidy = subsidyResponse.Response;
                }
            }

            return result;
        }

        protected override IDestination AddressToDestination(string address)
        {
            return BitcoinUtils.AddressToDestination(address);
        }


        #endregion
    }
}
