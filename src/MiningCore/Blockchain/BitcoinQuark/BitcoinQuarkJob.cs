using System;
using System.Collections.Generic;
using System.Linq;
using MiningCore.Blockchain.Bitcoin;
using MiningCore.Blockchain.ZCash;
using MiningCore.Blockchain.ZCash.DaemonResponses;
using MiningCore.Configuration;
using MiningCore.Contracts;
using MiningCore.Crypto;
using MiningCore.Extensions;
using MiningCore.Time;
using MiningCore.Util;
using NBitcoin;
using NBitcoin.DataEncoders;
using Transaction = NBitcoin.Transaction;

namespace MiningCore.Blockchain.BitcoinQuark
{
    public class BitcoinQuarkJob : ZCashJob
    {
        protected static Script CltvMultiSigScript(String[] pubkeys, UInt32 lock_time)
        {
            List<Op> ops = new List<Op>();
            if (lock_time > 0)
            {
                ops.Add(Op.GetPushOp(lock_time));
                ops.Add(OpcodeType.OP_CHECKLOCKTIMEVERIFY);
                ops.Add(OpcodeType.OP_DROP);
            }
            ops.Add(OpcodeType.OP_2);
            foreach (String pubkey in pubkeys)
            {
                PubKey key = new PubKey(pubkey);
                ops.Add(Op.GetPushOp(key.ToBytes()));
            }
            ops.Add(OpcodeType.OP_3);
            ops.Add(OpcodeType.OP_CHECKMULTISIG);

            Script redeem_script = new Script(ops.ToArray());
            return redeem_script;
        }

        protected virtual Script GetPremineAddressScript(uint height)
        {
            bool BTQPremineEnforceWhitelist = true;
            uint BTQHeight = 1259790;
            uint BTQPremineWindow = 100;
            uint nPowTargetSpacing = 10 * 60;

            if (!BTQPremineEnforceWhitelist)
            {
                return null;
            }

            if (!(BTQHeight <= height && height < (BTQHeight + BTQPremineWindow)))
            {
                return null;
            }

            String[] vPreminePubkeys1 = { "0330ac64a02530018aee75282511ab03ad14afded0de3a7631f859fcc95e7053f5", "02b1dd3a3d48bae5e8372e896c12fcf1a6a472df03a4e279f1228ea43eb72d7a76", "029afac47580783cd5e0fb7b9ef5eb70302e153c02b3880f254eed34e636228fb1" };
            String[] vPreminePubkeys2 = { "03408adf7846c306e9bb70b4943a28605195a7baf8f25aabd0d9cad703533ad154", "03e8b65f7dddd6747598747dba29f66874456e0182b6c4afaf92b01cf1c97ed333", "03b318bfec48b38094f5825b6d60d325df13386cb00742bf8e2b7798c7e19f5616" };
            String[] vPreminePubkeys3 = { "0330ac64a02530018aee75282511ab03ad14afded0de3a7631f859fcc95e7053f5", "02b1dd3a3d48bae5e8372e896c12fcf1a6a472df03a4e279f1228ea43eb72d7a76", "029afac47580783cd5e0fb7b9ef5eb70302e153c02b3880f254eed34e636228fb1" };
            String[] vPreminePubkeys4 = { "03408adf7846c306e9bb70b4943a28605195a7baf8f25aabd0d9cad703533ad154", "03e8b65f7dddd6747598747dba29f66874456e0182b6c4afaf92b01cf1c97ed333", "03b318bfec48b38094f5825b6d60d325df13386cb00742bf8e2b7798c7e19f5616" };

            String[][] vPreminePubkeys =
            {
                vPreminePubkeys1, vPreminePubkeys2, vPreminePubkeys3, vPreminePubkeys4
            };

            uint LOCK_STAGES = 18;  // 18 months
            uint LOCK_TIME = LOCK_STAGES * 30 * 24 * 3600;  // 18 months

            uint block = height - BTQHeight;
            uint num_unlocked = BTQPremineWindow * 46 / 100;  // 46% unlocked.
            uint num_locked = BTQPremineWindow - num_unlocked;  // 54% time-locked.
            uint stage_lock_time = LOCK_TIME / LOCK_STAGES / nPowTargetSpacing;
            uint stage_block_height = num_locked / LOCK_STAGES;
            String[] pubkeys = vPreminePubkeys[block % vPreminePubkeys.Length];  // Round robin.
            Script redeemScript;
            if (block < num_unlocked)
            {
                redeemScript = CltvMultiSigScript(pubkeys, 0);
            }
            else
            {
                uint locked_block = block - num_unlocked;
                uint stage = locked_block / stage_block_height;
                uint lock_time = BTQHeight + stage_lock_time * (1 + stage);
                redeemScript = CltvMultiSigScript(pubkeys, lock_time);
            }

            return redeemScript.PaymentScript;
        }

        #region Overrides of ZCashJob

        protected override Transaction CreateOutputTransaction()
        {
            rewardToPool = new Money(BlockTemplate.CoinbaseValue, MoneyUnit.Satoshi);

            var tx = new Transaction();

            Script premineAddressScript = GetPremineAddressScript(BlockTemplate.Height);

            // pool reward
            var amount = new Money(blockReward + rewardFees, MoneyUnit.Satoshi);

            if (premineAddressScript != null)
            {
                tx.AddOutput(amount, premineAddressScript);
            }
            else
            {
                tx.AddOutput(amount, poolAddressDestination);
            }


            return tx;
        }

        protected override byte[] SerializeHeader(uint nTime, string nonce)
        {
            // BTQ requires the blockheight to be encoded in the first 4 bytes of the hashReserved field
            var heightAndReserved = BitConverter.GetBytes(BlockTemplate.Height)
                .Concat(Enumerable.Repeat((byte)0, 28))
                .ToArray();

            var blockHeader = new ZCashBlockHeader
            {
                Version = (int)BlockTemplate.Version,
                Bits = new Target(Encoders.Hex.DecodeData(BlockTemplate.Bits)),
                HashPrevBlock = uint256.Parse(BlockTemplate.PreviousBlockhash),
                HashMerkleRoot = new uint256(merkleRoot),
                HashReserved = heightAndReserved,
                NTime = nTime,
                Nonce = nonce
            };

            return blockHeader.ToBytes();
        }

        public override void Init(ZCashBlockTemplate blockTemplate, string jobId,
            PoolConfig poolConfig, ClusterConfig clusterConfig, IMasterClock clock,
            IDestination poolAddressDestination, BitcoinNetworkType networkType,
            bool isPoS, double shareMultiplier,
            IHashAlgorithm coinbaseHasher, IHashAlgorithm headerHasher, IHashAlgorithm blockHasher)
        {
            Contract.RequiresNonNull(blockTemplate, nameof(blockTemplate));
            Contract.RequiresNonNull(poolConfig, nameof(poolConfig));
            Contract.RequiresNonNull(clusterConfig, nameof(clusterConfig));
            Contract.RequiresNonNull(clock, nameof(clock));
            Contract.RequiresNonNull(poolAddressDestination, nameof(poolAddressDestination));
            Contract.RequiresNonNull(coinbaseHasher, nameof(coinbaseHasher));
            Contract.RequiresNonNull(headerHasher, nameof(headerHasher));
            Contract.RequiresNonNull(blockHasher, nameof(blockHasher));
            Contract.Requires<ArgumentException>(!string.IsNullOrEmpty(jobId), $"{nameof(jobId)} must not be empty");

            this.poolConfig = poolConfig;
            this.clusterConfig = clusterConfig;
            this.clock = clock;
            this.poolAddressDestination = poolAddressDestination;
            this.networkType = networkType;

            BlockTemplate = blockTemplate;
            JobId = jobId;
            Difficulty = (double)new BigRational(ZCashConstants.Diff1b, BlockTemplate.Target.HexToByteArray().ToBigInteger());

            this.isPoS = isPoS;
            this.shareMultiplier = shareMultiplier;

            this.headerHasher = headerHasher;
            this.blockHasher = blockHasher;

            if (!string.IsNullOrEmpty(BlockTemplate.Target))
                blockTargetValue = new uint256(BlockTemplate.Target);
            else
            {
                var tmp = new Target(BlockTemplate.Bits.HexToByteArray());
                blockTargetValue = tmp.ToUInt256();
            }

            previousBlockHashReversedHex = BlockTemplate.PreviousBlockhash
                .HexToByteArray()
                .ReverseArray()
                .ToHexString();
            
            blockReward = blockTemplate.Subsidy.Miner;
            rewardFees = blockTemplate.Transactions.Sum(x => x.Fee);

            BuildCoinbase();

            // build tx hashes
            var txHashes = new List<uint256> { new uint256(coinbaseInitialHash) };
            txHashes.AddRange(BlockTemplate.Transactions.Select(tx => new uint256(tx.Hash.HexToByteArray().ReverseArray())));

            // build merkle root
            merkleRoot = MerkleNode.GetRoot(txHashes).Hash.ToBytes().ReverseArray();
            merkleRootReversed = merkleRoot.ReverseArray();
            merkleRootReversedHex = merkleRootReversed.ToHexString();

            jobParams = new object[]
            {
                JobId,
                BlockTemplate.Version.ReverseByteOrder().ToStringHex8(),
                previousBlockHashReversedHex,
                merkleRootReversedHex,
                BlockTemplate.Height.ReverseByteOrder().ToStringHex8() + sha256Empty.Take(28).ToHexString(), // height + hashReserved
                BlockTemplate.CurTime.ReverseByteOrder().ToStringHex8(),
                BlockTemplate.Bits.HexToByteArray().ReverseArray().ToHexString(),
                false
            };
        }

        
        public override object GetJobParams(bool isNew)
        {
            jobParams[jobParams.Length - 1] = true;
            return jobParams;
        }

        #endregion
    }
}

