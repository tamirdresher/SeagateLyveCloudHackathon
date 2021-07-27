using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace SegateLogScanner
{
    public class LogScanCheckpoint:TableEntity
    {
        public const string CheckpointId = "Checkpoint";

        public string Id { get; set; } = CheckpointId;
        public DateTime LastModificationTime { get; set; }
        public string LastContinuationToken { get; set; }
    }
}
