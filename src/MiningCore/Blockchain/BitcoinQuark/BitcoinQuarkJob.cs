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
using System.Numerics;
using System.Globalization;

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
            bool BTQPremineEnforceWhitelist = false;
            uint BTQHeight = 520520;
            List<String[]> vPreminePubkeyList = new List<String[]>();
            if (this.networkType == BitcoinNetworkType.Main)
            {
                BTQPremineEnforceWhitelist = true;
                BTQHeight = 520520;
                String[] vPreminePubkeys1 = { "021dd3be6338f1842d1cf52bbcd3d408b4900ff3ecbdda77a7c19b955fcb7dd816", "03541b002b6e5c7eac7e22172475342c1095c8c012079d660ece960a13466be04e", "023372d26eb06f1c22aec0bef30f01020e911c7bf9dfcd1da59a81580de21d467b" };
                String[] vPreminePubkeys2 = { "02f4c682565b2c51e2850207b22ac068dac25b05e9efc903f272fcd68185961654", "02313c58f1c92d088f09a3c80169c56eab18e3b72e33d3400577374cc6c92f9c66", "029c25ddabd0c8d696f4e1484d5e25e7b4de902e36eb8178e86854cbd4edecc62b" };
                String[] vPreminePubkeys3 = { "034fa410a78864f0eb833685b9b549828f2457d48998a7dd6fcdc53c427ebae1ad", "037bd10863da072ec79a6af3de728e214107b43a4b4e11e823d30353e560e3abb7", "03a4fff6237b4429e15903625afcba1cf8d0388e44730e7c7de309345b3ecf3c0f" };
                String[] vPreminePubkeys4 = { "0368d47a3cc473685a4326820172df3ee9815303ae8508770ce8b98bf79880d6c8", "034d7ff8e71c45a9ad7d63cece2aef69b17d417a058acde9a0b74fd65e27fc6c14", "026ef51ad3da23632a8a7a7fba27fde1298bc92bf2bb6f48385270d9ea0ef6f706" };
                vPreminePubkeyList.Add(vPreminePubkeys1);
                vPreminePubkeyList.Add(vPreminePubkeys2);
                vPreminePubkeyList.Add(vPreminePubkeys3);
                vPreminePubkeyList.Add(vPreminePubkeys4);
            }
            else if (this.networkType == BitcoinNetworkType.Test)
            {
                BTQPremineEnforceWhitelist = true;
                BTQHeight = 1259790;
                String[] vPreminePubkeys1 = { "0330ac64a02530018aee75282511ab03ad14afded0de3a7631f859fcc95e7053f5", "02b1dd3a3d48bae5e8372e896c12fcf1a6a472df03a4e279f1228ea43eb72d7a76", "029afac47580783cd5e0fb7b9ef5eb70302e153c02b3880f254eed34e636228fb1" };
                String[] vPreminePubkeys2 = { "03408adf7846c306e9bb70b4943a28605195a7baf8f25aabd0d9cad703533ad154", "03e8b65f7dddd6747598747dba29f66874456e0182b6c4afaf92b01cf1c97ed333", "03b318bfec48b38094f5825b6d60d325df13386cb00742bf8e2b7798c7e19f5616" };
                String[] vPreminePubkeys3 = { "0330ac64a02530018aee75282511ab03ad14afded0de3a7631f859fcc95e7053f5", "02b1dd3a3d48bae5e8372e896c12fcf1a6a472df03a4e279f1228ea43eb72d7a76", "029afac47580783cd5e0fb7b9ef5eb70302e153c02b3880f254eed34e636228fb1" };
                String[] vPreminePubkeys4 = { "03408adf7846c306e9bb70b4943a28605195a7baf8f25aabd0d9cad703533ad154", "03e8b65f7dddd6747598747dba29f66874456e0182b6c4afaf92b01cf1c97ed333", "03b318bfec48b38094f5825b6d60d325df13386cb00742bf8e2b7798c7e19f5616" };
                vPreminePubkeyList.Add(vPreminePubkeys1);
                vPreminePubkeyList.Add(vPreminePubkeys2);
                vPreminePubkeyList.Add(vPreminePubkeys3);
                vPreminePubkeyList.Add(vPreminePubkeys4);
            }

            if (!BTQPremineEnforceWhitelist)
            {
                return null;
            }

            String[][] vPreminePubkeys = vPreminePubkeyList.ToArray();

            uint BTQPremineWindow = 100;
            uint nPowTargetSpacing = 10 * 60;

            if (!(BTQHeight <= height && height < (BTQHeight + BTQPremineWindow)))
            {
                return null;
            }

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
            
            Difficulty = GetDifficulty(BlockTemplate.Target);

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
            txHashes.AddRange(BlockTemplate.Transactions.Select(tx => new uint256(tx.TxId.HexToByteArray().ReverseArray())));

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


        protected double GetDifficulty(String starget)
        {
            NBitcoin.BouncyCastle.Math.BigInteger diff1b = new NBitcoin.BouncyCastle.Math.BigInteger(ZCashConstants.Diff1b.ToString());
            NBitcoin.BouncyCastle.Math.BigInteger btarget = new NBitcoin.BouncyCastle.Math.BigInteger(starget.HexToByteArray());

            Target targetDiff1b = new Target(diff1b);

            var qr = diff1b.DivideAndRemainder(btarget);
            var quotient = qr[0];
            var remainder = qr[1];
            var decimalPart = NBitcoin.BouncyCastle.Math.BigInteger.Zero;
            for (int i = 0; i < 12; i++)
            {
                var div = (remainder.Multiply(NBitcoin.BouncyCastle.Math.BigInteger.Ten)).Divide(btarget);

                decimalPart = decimalPart.Multiply(NBitcoin.BouncyCastle.Math.BigInteger.Ten);
                decimalPart = decimalPart.Add(div);

                remainder = remainder.Multiply(NBitcoin.BouncyCastle.Math.BigInteger.Ten).Subtract(div.Multiply(btarget));
            }
            double difficulty = double.Parse(quotient.ToString() + "." + decimalPart.ToString(), new NumberFormatInfo()
            {
                NegativeSign = "-",
                NumberDecimalSeparator = "."
            });

            return difficulty;
        }
        
        #endregion
    }
}

