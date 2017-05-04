using System;

namespace DocumentScraper.Helpers
{
    internal class SPOAuthCookies
    {
        public string FedAuth { get; set; }
        public string RtFa { get; set; }
        public Uri Host { get; set; }
        public DateTime Expires { get; set; }
    }
}