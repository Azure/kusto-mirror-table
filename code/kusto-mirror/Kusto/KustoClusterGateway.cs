﻿using Kusto.Data;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Kusto.Ingest;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kusto.Mirror.ConsoleApp.Kusto
{
    public class KustoClusterGateway
    {
        private readonly ICslQueryProvider _queryProvider;
        private readonly ICslAdminProvider _commandProvider;
        private readonly IKustoQueuedIngestClient _ingestionProvider;
        private readonly Uri _clusterQueryUri;
        private readonly string _application;
        private readonly IImmutableList<KeyValuePair<string, object>>? _requestOptions;

        public static async Task<KustoClusterGateway> CreateAsync(
            AuthenticationMode authenticationMode,
            Uri clusterIngestionUri,
            string version,
            string? requestDescription = null)
        {
            var clusterQueryUri = await GetQueryUriAsync(authenticationMode, clusterIngestionUri);

            return new KustoClusterGateway(
                authenticationMode,
                clusterQueryUri,
                clusterIngestionUri,
                version,
                requestDescription);
        }

        private KustoClusterGateway(
            AuthenticationMode authenticationMode,
            Uri clusterQueryUri,
            Uri clusterIngestionUri,
            string version,
            string? requestDescription = null)
        {
            var queryStringBuilder = CreateKustoConnectionStringBuilder(
                authenticationMode,
                clusterQueryUri);
            var ingestionStringBuilder = CreateKustoConnectionStringBuilder(
                authenticationMode,
                clusterIngestionUri);

            _queryProvider = KustoClientFactory.CreateCslQueryProvider(queryStringBuilder);
            _commandProvider = KustoClientFactory.CreateCslCmAdminProvider(queryStringBuilder);
            _ingestionProvider = KustoIngestFactory.CreateQueuedIngestClient(
                ingestionStringBuilder);
            _clusterQueryUri = clusterQueryUri;
            if (requestDescription != null)
            {
                _application = $"Kusto-Mirror;{version}";
                _requestOptions = ImmutableArray<KeyValuePair<string, object>>
                    .Empty
                    .Add(KeyValuePair.Create(
                        ClientRequestProperties.OptionRequestDescription,
                        (object)requestDescription));
            }
            else
            {
                _application = string.Empty;
            }
        }

        internal async Task IngestFromStorageAsync(
            Uri blobUrl,
            KustoQueuedIngestionProperties properties,
            CancellationToken ct)
        {
            var ingestionBlobUrl = $"{blobUrl};managed_identity=system";
            var result = await _ingestionProvider.IngestFromStorageAsync(
                ingestionBlobUrl,
                properties,
                //  Recommended so the DM sizes the file properly
                new StorageSourceOptions { Size = 10000 });
            var failureStatus = result.GetIngestionStatusCollection().First();

            if (failureStatus.Status != Status.Queued)
            {
                throw new MirrorException(
                    $"Blob '{blobUrl}' hasn't queued ; state '{failureStatus.Status}', "
                    + $"failure status '{failureStatus.FailureStatus}' & "
                    + $"error code '{failureStatus.ErrorCode}'");
            }
        }

        public async Task<IImmutableList<T>> ExecuteQueryAsync<T>(
            string database,
            string queryText,
            Func<IDataRecord, T> projection,
            CancellationToken ct)
        {
            using (var reader = await _queryProvider.ExecuteQueryAsync(
                database,
                queryText,
                _requestOptions != null
                ? new ClientRequestProperties(_requestOptions, null)
                {
                    Application = _application
                }
                : null))
            {
                var output = Project(reader, projection)
                    .ToImmutableArray();

                return output;
            }
        }

        public async Task<IImmutableList<T>> ExecuteCommandAsync<T>(
            string database,
            string commandText,
            Func<IDataRecord, T> projection,
            CancellationToken ct)
        {
            using (var reader = await _commandProvider.ExecuteControlCommandAsync(
                database,
                commandText,
                _requestOptions != null
                ? new ClientRequestProperties(_requestOptions, null)
                {
                    Application = _application
                }
                : null))
            {
                var output = Project(reader, projection)
                    .ToImmutableArray();

                return output;
            }
        }

        private static KustoConnectionStringBuilder CreateKustoConnectionStringBuilder(
            AuthenticationMode authenticationMode,
            Uri clusterUri)
        {
            var builder = new KustoConnectionStringBuilder(clusterUri.ToString());

            switch (authenticationMode)
            {
                case AuthenticationMode.AzCli:
                    return builder.WithAadAzCliAuthentication(false);
                case AuthenticationMode.Browser:
                    return builder.WithAadUserPromptAuthentication();

                default:
                    throw new NotSupportedException(
                        $"Unsupported authentication mode:  '{authenticationMode}'");
            }
        }

        private static async Task<Uri> GetQueryUriAsync(
            AuthenticationMode authenticationMode,
            Uri clusterIngestionUri)
        {
            try
            {
                var kustoConnectionStringBuilder = CreateKustoConnectionStringBuilder(
                    authenticationMode,
                    clusterIngestionUri);
                var dmCommandProvider =
                    KustoClientFactory.CreateCslCmAdminProvider(kustoConnectionStringBuilder);
                var dataReader =
                    await dmCommandProvider.ExecuteControlCommandAsync("", ".show query service uri");
                var clusterQueryUrl = Project(dataReader, r => r.GetString(0)).First();
                var clusterQueryUri = new Uri(clusterQueryUrl);

                return clusterQueryUri;
            }
            catch (Exception ex)
            {
                throw new MirrorException("Error retrieving the cluster query uri", ex);
            }
        }

        private static IEnumerable<T> Project<T>(
            IDataReader reader,
            Func<IDataRecord, T> projection)
        {
            while (reader.Read())
            {
                yield return projection(reader);
            }
        }
    }
}