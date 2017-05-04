// ReSharper disable InconsistentNaming
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Text;
using DocumentScraper.Helpers;
using Newtonsoft.Json;
using static System.IO.File;

namespace DocumentScraper.Tester
{
    internal class Program
    {
        #region Fields

        // File output folder path
        private const string FolderName = "ScraperOutputFiles";

        // User ID for POC
        private const int TargetUserId = 6;

        // Development site as target
        private static readonly Uri spSite = new Uri("<<< URL till sharepoint | ex: https://organization.sharepoint.com >>> ");

        // OData request path to SharePoint API
        private static readonly string RestQuery = "_api/Web/GetFolderByServerRelativePath(decodedurl=\'/sites/Test/Shared%20Documents\')/Files" 
            + $"?$expand=Author&$filter=Author/Id%20eq%20{TargetUserId}";
        #endregion 

        private static void Main(string[] args)
        {
            try
            {
                if (!SpoAuthUtility.Create(spSite, GetValueFromConfig("UserName"), WebUtility.HtmlEncode(GetValueFromConfig("Password")), false))
                    return;

                var url = new Uri($"{SpoAuthUtility.CurrentSpoAuthUtility.SiteUrl}/{RestQuery}");
                var result = HttpHelper.SendODataJsonRequest(url, Method.GET, null, (HttpWebRequest)WebRequest.Create(url), SpoAuthUtility.CurrentSpoAuthUtility);
                var jsonString = Encoding.UTF8.GetString(result, 0, result.Length);

                var newDes = JsonConvert.DeserializeObject<RootObject>(jsonString).d.Results.ToList();
                var fileTitle = newDes.Select(x => x.Author.Title).First()?.Replace(" ", "_");

                var author = newDes.Select(x => x.Author).First();
                var resultList = new List<string>
                {
                    $"Id:;{author.Id}",
                    $"Login name:;{author.LoginName.Remove(0, author.LoginName.LastIndexOf("|", StringComparison.Ordinal) + 1)}",
                    $"Name Id:;{author.UserId.NameId}",
                    "",
                    "Server path;File name"
                };
                resultList.AddRange(newDes.Select(item => $"{item.ServerRelativeUrl};{item.Name}").ToList());

                WriteAllText($"{FolderName}/{fileTitle}_QueryResult.json", jsonString);
                WriteAllLines($"{FolderName}/{fileTitle}_Files.csv", resultList);
            }
            catch (Exception ex)
            {
                AppendAllLines($"{FolderName}/DocumentScraper_Tester_ErrorLog.txt", ErrorMessageBuilder(ex));
            }
        }

        #region Helpers

        private static string GetValueFromConfig(string name) => ConfigurationManager.AppSettings[name];

        private static IEnumerable<string> ErrorMessageBuilder(Exception ex) => new List<string>
        {
            $"\n----------------- {DateTime.Now} -----------------",
            ex?.Message,
            ex?.StackTrace,
            ex?.InnerException?.ToString()
        };

        #endregion
    }
}