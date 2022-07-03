﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kusto.Mirror.ConsoleApp.Database
{
    internal enum BlobState
    {
        ToBeAdded,
        QueuedForIngestion,
        Staged,
        Loaded,
        ToBeDeleted,
        Deleted
    }
}