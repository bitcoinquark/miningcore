using Autofac;
using AutoMapper;
using MiningCore.Blockchain.Bitcoin;
using MiningCore.Blockchain.ZCash;
using MiningCore.Blockchain.ZCash.DaemonResponses;
using MiningCore.Configuration;
using MiningCore.Notifications;
using MiningCore.Persistence;
using MiningCore.Persistence.Repositories;
using MiningCore.Time;
using Newtonsoft.Json;

namespace MiningCore.Blockchain.BitcoinQuark
{
    [CoinMetadata(CoinType.BTQ)]
    public class BitcoinQuarkPool : ZCashPoolBase<BitcoinQuarkJob>
    {
        public BitcoinQuarkPool(IComponentContext ctx,
            JsonSerializerSettings serializerSettings,
            IConnectionFactory cf,
            IStatsRepository statsRepo,
            IMapper mapper,
            IMasterClock clock,
            NotificationService notificationService) :
            base(ctx, serializerSettings, cf, statsRepo, mapper, clock, notificationService)
        {
        }

        protected override BitcoinJobManager<BitcoinQuarkJob, ZCashBlockTemplate> CreateJobManager()
        {
            return ctx.Resolve<BitcoinQuarkJobManager>(
                new TypedParameter(typeof(IExtraNonceProvider), new ZCashExtraNonceProvider()));
        }
    }
}
