﻿using System.Collections.Immutable;
using System.Data;

namespace Kusto.Mirror.ConsoleApp.Database
{
    public class DatabaseGateway
    {
        private readonly KustoClusterGateway _clusterGateway;

        public DatabaseGateway(KustoClusterGateway clusterGateway, string database)
        {
            _clusterGateway = clusterGateway;
            DatabaseName = database;
        }

        public string DatabaseName { get; }

        public async Task<IImmutableList<T>> ExecuteQueryAsync<T>(
            string queryText,
            Func<IDataRecord, T> projection,
            CancellationToken ct)
        {
            return await _clusterGateway.ExecuteQueryAsync(
                DatabaseName,
                queryText,
                projection,
                ct);
        }

        public async Task<IImmutableList<T>> ExecuteCommandAsync<T>(
            string commandText,
            Func<IDataRecord, T> projection,
            CancellationToken ct)
        {
            return await _clusterGateway.ExecuteCommandAsync(
                DatabaseName,
                commandText,
                projection,
                ct);
        }

        public async Task CreateMergeDatabaseObjectsAsync(CancellationToken ct)
        {
            await ExecuteCommandAsync(
                TableStatus.CreateStatusTableCommandText,
                r => 0,
                ct);
        }
    }
}