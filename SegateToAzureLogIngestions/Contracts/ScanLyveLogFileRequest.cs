using System;

namespace Contracts
{
    public class ScanLyveLogFileRequest
    {
        public string FileUrl { get; set; }
        public string BucketName { get; set; }
    }
}
