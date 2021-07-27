using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Contracts;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace SegateLogScanner
{
    public class ScanLyveLogs
    {
        private AmazonS3Client _s3Client;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ScanLyveLogs> _log;
        public const string BucketNameKey = "BucketName";
        public const string LyveUrlKey = "LyveUrl";
        public const string LyveAccessKey = "LyveAccessKey";
        public const string LyveSecretKey = "LyveSecretKey";

        public ScanLyveLogs(IConfiguration configuration, ILogger<ScanLyveLogs> log)
        {
            _configuration = configuration;
            _log = log;
        }

        [FunctionName("ScanLyveLogs")]
        public async Task Run([Queue("logFiles"), StorageAccount("AzureWebJobsStorage")] ICollector<ScanLyveLogFileRequest> queue,
            [TimerTrigger("0 */5 * * * *")] TimerInfo timer,
            [Table("logrunscheckpoint", Connection = "AzureWebJobsStorage")] CloudTable logsCheckpoint)
        {

            var checkpoint = await GetLastCheckpointFileAsync(logsCheckpoint);
            _log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            ConnectLyveS3();
            _log.LogInformation($"Connected to Lyve S3: {DateTime.Now}");

            S3Bucket logsBucket = await GetLogsBucket();

            ListObjectsV2Request request = new ListObjectsV2Request { BucketName = logsBucket.BucketName };
            var lastModificationTime = DateTime.MinValue;
            if (checkpoint?.LastContinuationToken!=null)
            {
                request.ContinuationToken = checkpoint.LastContinuationToken;
                lastModificationTime = checkpoint.LastModificationTime;
            }
            var files = await _s3Client.ListObjectsV2Async(request);

            var nextContinuation = files.NextContinuationToken;
            List<S3Object> allFiles = files.S3Objects
                .Where(x => x.Key.EndsWith("gz"))
                .Where(x => x.LastModified > lastModificationTime)
                .ToList();
            while (true)
            {
                var results = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = logsBucket.BucketName, ContinuationToken = nextContinuation });

                allFiles.AddRange(results.S3Objects.Where(x => x.Key.EndsWith("gz")));
                
                if (results.NextContinuationToken == null)
                {
                    break;
                }
                else
                {
                    nextContinuation = results.NextContinuationToken;
                }
            }
            allFiles = allFiles.OrderByDescending(x => x.LastModified).ToList();

            _log.LogInformation($"Got {allFiles.Count()} new log files between {timer.ScheduleStatus.Last} to {DateTime.Now} ");

            foreach (var file in allFiles)
            {
                queue.Add(new ScanLyveLogFileRequest
                {
                    BucketName = logsBucket.BucketName,
                    FileUrl = file.Key
                });
            }

            await SetCheckpointAsync(logsCheckpoint, allFiles.FirstOrDefault(), nextContinuation);
        }

        private async Task SetCheckpointAsync(CloudTable logsCheckpoint, S3Object logFile, string continuationToken)
        {
            if (logFile==null)
            {
                return;
            }

            var checkpoint = new LogScanCheckpoint()
            {
                PartitionKey = LogScanCheckpoint.CheckpointId,
                RowKey = LogScanCheckpoint.CheckpointId,
                LastModificationTime = logFile.LastModified,
                LastContinuationToken=continuationToken,
                ETag="*"
            };
            var operation = TableOperation.InsertOrReplace(checkpoint);
            await logsCheckpoint.ExecuteAsync(operation);
        }

        private async Task<LogScanCheckpoint> GetLastCheckpointFileAsync(CloudTable logsCheckpoint)
        {
            var result = await logsCheckpoint.ExecuteAsync(TableOperation.Retrieve<LogScanCheckpoint>(LogScanCheckpoint.CheckpointId, LogScanCheckpoint.CheckpointId));
            var checkpoint = result.Result as LogScanCheckpoint;
            return checkpoint;
        }

        private async Task<S3Bucket> GetLogsBucket()
        {
            ListBucketsResponse response = await _s3Client.ListBucketsAsync();
            var logsBucket = response.Buckets.FirstOrDefault(b => b.BucketName == _configuration[BucketNameKey]);
            if (logsBucket == null)
            {
                throw new Exception($"Bucket named '{_configuration[BucketNameKey]}' doesn't exists. Can't get logs from Lyve S3");
            }

            return logsBucket;
        }

        private void ConnectLyveS3()
        {
            AmazonS3Config config = new AmazonS3Config();
            config.ServiceURL = _configuration[LyveUrlKey];
            _s3Client = new AmazonS3Client(
                    _configuration[LyveAccessKey],
                    _configuration[LyveSecretKey],
                    config
                    );
        }

    }
}
