using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.IO.File;

namespace DocumentScraper.ErrorHandling
{
    internal class ErrorHandler
    {
        private static readonly string FolderName = "ErrorLogs";

        public static void LogError(Exception ex)
        {
            try
            {
                AppendAllLines($"{FolderName}/DocumentScraper_ErrorLog.txt", ListBuilder(ex));
            }
            catch (Exception exception)
            {
                AppendAllLines($"{FolderName}/DocumentScraper_ErrorLog.txt", ListBuilder(exception));
            }
        }

        public static void LogError(string exMessage)
        {
            try
            {
                AppendAllLines($"{FolderName}/DocumentScraper_ErrorLog.txt", ListBuilder(exMessage));
            }
            catch (Exception exception)
            {
                AppendAllLines($"{FolderName}/DocumentScraper_ErrorLog.txt", ListBuilder(exception));
            }
        }

        private static IEnumerable<string> ListBuilder(Exception ex) => new List<string>
        {
            $"\n----------------- {DateTime.Now} -----------------",
            ex?.Message,
            ex?.StackTrace,
            ex?.InnerException?.ToString()
        };

        private static IEnumerable<string> ListBuilder(string exMessage) => new List<string>
        {
            $"\n----------------- {DateTime.Now} -----------------",
            exMessage
        };
    }
}
