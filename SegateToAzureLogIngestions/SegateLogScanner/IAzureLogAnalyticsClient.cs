using System.Threading.Tasks;

namespace SegateLogScanner
{
    public interface IAzureLogAnalyticsClient
    {
        Task WriteLog(string logName, string logMessage);
    }
}