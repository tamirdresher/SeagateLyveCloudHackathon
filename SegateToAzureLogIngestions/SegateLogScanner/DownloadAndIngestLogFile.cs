using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Util;
using Contracts;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SegateLogScanner
{
    public class DownloadAndIngestLogFile
    {
        private AmazonS3Client _s3Client;
        private readonly ILogger<DownloadAndIngestLogFile> _logger;
        private readonly IAzureLogAnalyticsClient _logAnalyticsClient;
        private readonly string _logName;

        public DownloadAndIngestLogFile(IConfiguration config,
            ILogger<DownloadAndIngestLogFile> logger,
            IAzureLogAnalyticsClient logAnalyticsClient)
        {
            Config = config;
            _logger = logger;
            _logAnalyticsClient = logAnalyticsClient;
            _logName = config["LogAnalyticsLogName"];
            string accessKey = Config["LyveAccessKey"];
            string secretKey = Config["LyveSecretKey"];

            AmazonS3Config s3config = new AmazonS3Config();
            s3config.ServiceURL = Config["LyveUrl"];

            _s3Client = new AmazonS3Client(
                    accessKey,
                    secretKey,
                    s3config
                    );
        }

        IConfiguration Config { get; }

        [FunctionName(nameof(DownloadAndIngestLogFile))]
        public async Task Run([QueueTrigger("logFiles", Connection = "AzureWebJobsStorage")] ScanLyveLogFileRequest fileToScanRequest, ILogger log)
        {

            try
            {
                var s3Object = await _s3Client.GetObjectAsync(fileToScanRequest.BucketName, fileToScanRequest.FileUrl);

                var tempFile = Path.GetTempFileName();
                var extractedDir = tempFile + "_e";
                Directory.CreateDirectory(extractedDir);
                string extractedFile = Path.Combine(extractedDir, Path.GetFileName(fileToScanRequest.FileUrl));
                File.Delete(tempFile);
                await s3Object.WriteResponseStreamToFileAsync(tempFile, false, CancellationToken.None);

                ExtractLogFile(tempFile, extractedFile);

                using (var file = new StreamReader(extractedFile))
                {
                    var line = "";
                    while ((line = await file.ReadLineAsync()) != null)
                    {
                        await _logAnalyticsClient.WriteLog(_logName, line);
                    }
                }

                Directory.Delete(extractedDir, true);
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "Error while processing {file} {bucket}", fileToScanRequest.FileUrl, fileToScanRequest.BucketName);
            }
        }

        private static void ExtractLogFile(string compressedFile, string extractedFile)
        {
            byte[] dataBuffer = new byte[4096];
            using (System.IO.Stream fs = new FileStream(compressedFile, FileMode.Open, FileAccess.Read))
            {
                using (GZipInputStream gzipStream = new GZipInputStream(fs))
                {
                   
                    using (FileStream fsOut = File.Create(extractedFile))
                    {
                        StreamUtils.Copy(gzipStream, fsOut, dataBuffer);
                    }
                }
            }
        }
    }
}
