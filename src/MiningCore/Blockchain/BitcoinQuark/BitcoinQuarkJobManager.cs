using System.Threading.Tasks;
using Autofac;
using MiningCore.Blockchain.Bitcoin;
using MiningCore.Blockchain.ZCash;
using MiningCore.Blockchain.ZCash.DaemonResponses;
using MiningCore.DaemonInterface;
using MiningCore.Notifications;
using MiningCore.Time;

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

        #endregion
    }
}
