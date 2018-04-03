using System;
using System.Collections.Generic;
using System.Text;

namespace MiningCore.Blockchain.Bitcoin.DaemonResponses
{
    public class GetAddressInfoResponse
    {
        public string Address { get; set; }

        public string ScriptPubKey { get; set; }

        public bool IsMine { get; set; }

        public bool IsWatchOnly { get; set; }

        public bool IsScript { get; set; }

        public bool IsWitness { get; set; }

        public string Script { get; set; }

        public string Hex { get; set; }

        public string PubKey { get; set; }

        public string Account { get; set; }
    }
}
