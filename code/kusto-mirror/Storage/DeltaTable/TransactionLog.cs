﻿using System.Collections.Immutable;
using System.Text.Json;

namespace Kusto.Mirror.ConsoleApp.Storage.DeltaTable
{
    internal class TransactionLog
    {
        public static TransactionLog LoadLog(int txId, string blobText)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var lines = blobText.Split('\n');
            var entries = lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => JsonSerializer.Deserialize<TransactionLogEntry>(l, options)!)
                .ToImmutableArray();
            var metadata = entries
                .Where(e => e.Metadata != null)
                .Select(e => e.Metadata!)
                .ToImmutableArray();
            var add = entries.Where(e => e.Add != null).Select(e => e.Add!);
            var remove = entries.Where(e => e.Remove != null).Select(e => e.Remove!);

            if (metadata.Count() > 1)
            {
                throw new MirrorException("More than one meta data node in one transaction");
            }

            var transactionMetadata = metadata.Any()
                ? new TransactionMetadata(metadata.First())
                : null;
            var transactionAdds = add
                .Select(a => new TransactionAdd(a))
                .ToImmutableArray();
            var transactionRemoves = remove
                .Select(a => new TransactionRemove(a))
                .ToImmutableArray();

            return new TransactionLog(
                txId,
                txId,
                transactionMetadata,
                transactionAdds,
                transactionRemoves);
        }

        public TransactionLog(
            int startTxId,
            int endTxId,
            TransactionMetadata? transactionMetadata,
            IEnumerable<TransactionAdd> transactionAdds,
            IEnumerable<TransactionRemove> transactionRemoves)
        {
            StartTxId = startTxId;
            EndTxId = endTxId;
            TransactionMetadata = transactionMetadata;
            TransactionAdds = transactionAdds.ToImmutableArray();
            TransactionRemoves = transactionRemoves.ToImmutableArray();
        }

        public int StartTxId { get; }
        
        public int EndTxId { get; }

        public TransactionMetadata? TransactionMetadata { get; }

        public IImmutableList<TransactionAdd> TransactionAdds { get; }

        public IImmutableList<TransactionRemove> TransactionRemoves { get; }

        public TransactionLog Coalesce(TransactionLog second)
        {
            throw new NotImplementedException();
        }

        public static TransactionLog Coalesce(IEnumerable<TransactionLog> txLogs)
        {
            var span = new Span<TransactionLog>(txLogs.ToArray());

            if (span.Length == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(txLogs),
                    "Should contain at least of log");
            }
            else
            {
                return Coalesce(span[0], span.Slice(1));
            }
        }

        private static TransactionLog Coalesce(TransactionLog first, Span<TransactionLog> txLogs)
        {
            if (txLogs.Length == 0)
            {
                return first;
            }
            else
            {
                var second = txLogs[0];
                var merged = first.Coalesce(second);
                var remains = txLogs.Slice(1);

                return Coalesce(merged, remains);
            }
        }
    }
}