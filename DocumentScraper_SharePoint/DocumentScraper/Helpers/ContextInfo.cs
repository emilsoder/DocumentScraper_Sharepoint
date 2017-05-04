using System;
using System.Collections.Generic;
using System.Linq;

namespace DocumentScraper.Helpers
{
    /// <summary>
    /// Helper class to handle ContextInfo JSON Deserialisation
    /// </summary>
    public class ContextInfo
    {
        public Dictionary<string, Dictionary<string, object>> FormInfoDictionary { get; set; }
        public string FormDigestValue => FormInfoDictionary?.FirstOrDefault().Value != null ? Convert.ToString(FormInfoDictionary.FirstOrDefault().Value["FormDigestValue"]) : null;
    }
}