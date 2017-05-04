using System;

namespace DocumentScraper.Helpers
{
    internal class SamlSecurityToken
    {
        public byte[] Token { get; set; }
        public DateTime Expires { get; set; }
    }
}