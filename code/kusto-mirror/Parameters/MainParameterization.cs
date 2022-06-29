﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kusto.Mirror.ConsoleApp.Parameters
{
    internal class MainParameterization
    {
        public MainParameterization(
            AuthenticationMode authenticationMode,
            Uri clusterQueryUri,
            IEnumerable<DeltaTableParameterization> deltaTableParameterizations)
        {
            AuthenticationMode = authenticationMode;
            ClusterQueryUri = clusterQueryUri;
            DeltaTableParameterizations = deltaTableParameterizations.ToImmutableArray();
        }

        public static MainParameterization Create(CommandLineOptions options)
        {
            Uri? clusterQueryUri;

            if (Uri.TryCreate(options.ClusterQueryUrl, UriKind.Absolute, out clusterQueryUri))
            {
                if (!string.IsNullOrWhiteSpace(clusterQueryUri.Query))
                {
                    throw new MirrorException(
                        $"Cluster query URL can't contain query string:  "
                        + $"'{options.ClusterQueryUrl}'");
                }
            }
            else
            {
                throw new MirrorException(
                    $"Invalid cluster query URL:  '{options.ClusterQueryUrl}'");
            }

            Uri? deltaTableUrl;

            if (!Uri.TryCreate(options.DeltaTableUrl, UriKind.Absolute, out deltaTableUrl))
            {
                throw new MirrorException(
                    $"Invalid Delta Table URL:  '{options.DeltaTableUrl}'");
            }

            var deltaTable = new DeltaTableParameterization(
                deltaTableUrl,
                "Database not set",
                "Kusto table not set",
                true);

            return new MainParameterization(
                options.AuthenticationMode,
                clusterQueryUri,
                new[] { deltaTable });
        }

        public AuthenticationMode AuthenticationMode { get; }

        public Uri ClusterQueryUri { get; }

        public IImmutableList<DeltaTableParameterization> DeltaTableParameterizations { get; }
    }
}