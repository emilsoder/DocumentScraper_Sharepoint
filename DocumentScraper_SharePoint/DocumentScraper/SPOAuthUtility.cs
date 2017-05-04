using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using DocumentScraper.Helpers;
using Newtonsoft.Json;
using DocumentScraper.ErrorHandling;

// ReSharper disable InconsistentNaming

namespace DocumentScraper
{
    public class SpoAuthUtility
    {
        private readonly string _username;
        private readonly string _password;
        private Uri _adfsIntegratedAuthUrl;
        private Uri _adfsAuthUrl;
        private readonly bool _useIntegratedWindowsAuth;
        private CookieContainer _cookieContainer;
        private SamlSecurityToken _stsAuthToken;

        public static string MsoStsUrl { get; } = "https://login.microsoftonline.com/extSTS.srf";
        public static string MsoLoginUrl { get; } = "https://login.microsoftonline.com/login.srf";
        public static string MsoHrdUrl { get; } = "https://login.microsoftonline.com/GetUserRealm.srf";
        public static string Wsse { get; } = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        public static string Wsu { get; } = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
        public static string Wst { get; } = "http://schemas.xmlsoap.org/ws/2005/02/trust";
        public static string Saml { get; } = "urn:oasis:names:tc:SAML:1.0:assertion";
        public static string SpowssigninUri { get; } = "_forms/default.aspx?wa=wsignin1.0";
        public static string ContextInfoQuery { get; } = "_api/contextinfo";

        public static SpoAuthUtility CurrentSpoAuthUtility { get; private set; }

        public Uri SiteUrl { get; }

        private SpoAuthUtility(Uri spSiteUrl, string username, string password, bool useIntegratedWindowsAuth)
        {
            SiteUrl = spSiteUrl;
            _username = username;
            _password = password;
            _useIntegratedWindowsAuth = useIntegratedWindowsAuth;

            _stsAuthToken = new SamlSecurityToken();
        }

        /// <summary>
        /// The method will request the SP ContextInfo and return its FormDigestValue as a String
        /// The FormDigestValue is a second layer of authentication required for several REST queries
        /// </summary>
        public static string GetRequestDigest()
        {
            var url = new Uri($"{CurrentSpoAuthUtility.SiteUrl}/{ContextInfoQuery}");
            var result = HttpHelper.SendODataJsonRequest(url, Method.POST, new byte[0], (HttpWebRequest)WebRequest.Create(url), CurrentSpoAuthUtility);

            return JsonConvert.DeserializeObject<ContextInfo>(Encoding.UTF8.GetString(result, 0, result.Length))?.FormDigestValue ?? string.Empty;
        }

        public static bool Create(Uri spSiteUrl, string username, string password, bool useIntegratedWindowsAuth)
        {
            var utility = new SpoAuthUtility(spSiteUrl, username, password, useIntegratedWindowsAuth);

            if (utility.GetCookieContainer().GetCookies(spSiteUrl).Cast<Cookie>().All(c => c.Name != "FedAuth"))
            {
                ErrorHandler.LogError("Could not retrieve Auth cookies");
                throw new Exception("Could not retrieve Auth cookies");
            }

            CurrentSpoAuthUtility = utility;
            return true;
        }

        public CookieContainer GetCookieContainer()
        {
            if (_stsAuthToken != null && DateTime.Now > _stsAuthToken.Expires)
            {
                _stsAuthToken = GetMsoStsSAMLToken();

                if (_stsAuthToken != null)
                {
                    var cookies = GetSPOAuthCookies(_stsAuthToken);
                    var cc = new CookieContainer();

                    var samlAuthCookie = new Cookie("FedAuth", cookies.FedAuth)
                    {
                        Path = "/",
                        Expires = _stsAuthToken.Expires,
                        Secure = cookies.Host.Scheme.Equals("https"),
                        HttpOnly = true,
                        Domain = cookies.Host.Host
                    };

                    cc.Add(SiteUrl, samlAuthCookie);

                    cc.Add(SiteUrl, new Cookie("rtFA", cookies.RtFa)
                    {
                        Path = "/",
                        Expires = _stsAuthToken.Expires,
                        Secure = cookies.Host.Scheme.Equals("https"),
                        HttpOnly = true,
                        Domain = cookies.Host.Host
                    });

                    _cookieContainer = cc;
                }
            }
            return _cookieContainer;
        }

        private SPOAuthCookies GetSPOAuthCookies(SamlSecurityToken stsToken)
        {
            var spoAuthCookies = new SPOAuthCookies();

            var siteUri = SiteUrl;
            var wsSigninUrl = new Uri($"{siteUri.Scheme}://{siteUri.Authority}/{SpowssigninUri}");

            var request = (HttpWebRequest)WebRequest.Create(wsSigninUrl);
            request.CookieContainer = new CookieContainer();


            if (HttpHelper.SendHttpRequest(wsSigninUrl, Method.POST, stsToken.Token, "application/x-www-form-urlencoded", request, null) == null) return spoAuthCookies;

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                spoAuthCookies.FedAuth = response.Cookies["FedAuth"]?.Value;
                spoAuthCookies.RtFa = response.Cookies["rtFA"]?.Value;
                spoAuthCookies.Expires = stsToken.Expires;
                spoAuthCookies.Host = wsSigninUrl;
            }
            return spoAuthCookies;
        }

        private Uri GetAdfsAuthUrl()
        {
            Uri corpAdfsProxyUrl = null;

            var request = (HttpWebRequest)WebRequest.Create(new Uri(MsoHrdUrl));

            var response = HttpHelper.SendHttpRequest(new Uri(MsoHrdUrl), Method.POST, Encoding.UTF8.GetBytes($"handler=1&login={_username}"), "application/x-www-form-urlencoded", request);

            var json = JsonConvert.DeserializeObject<STSInfo>(Encoding.UTF8.GetString(response, 0, response.Length));

            if (!string.IsNullOrEmpty(json.AuthUrl))
            {
                corpAdfsProxyUrl = new Uri(json.AuthUrl);
            }
            return corpAdfsProxyUrl;
        }

        private string GetAdfsSAMLTokenUsernamePassword()
        {
            string samlAssertion = null;
            var stsUsernameMixedUrl = $"https://{_adfsAuthUrl.Host}/adfs/services/trust/2005/usernamemixed/";
            var requestBody = Encoding.UTF8.GetBytes(ParameterizedSoapRequest("urn:federation:MicrosoftOnline", _username, _password, stsUsernameMixedUrl));

            try
            {
                var stsUrl = new Uri(stsUsernameMixedUrl);

                var responseData = HttpHelper.SendHttpRequest(stsUrl, Method.POST, requestBody, "application/soap+xml; charset=utf-8", (HttpWebRequest)WebRequest.Create(stsUrl), null);

                if (responseData != null)
                {
                    var sr = new StreamReader(new MemoryStream(responseData), Encoding.GetEncoding("utf-8"));
                    var nav = new XPathDocument(sr).CreateNavigator();
                    var nsMgr = new XmlNamespaceManager(nav.NameTable);
                    nsMgr.AddNamespace("t", "http://schemas.xmlsoap.org/ws/2005/02/trust");
                    var requestedSecurityToken = nav.SelectSingleNode("//t:RequestedSecurityToken", nsMgr);

                    var doc = new XmlDocument();
                    doc.LoadXml(requestedSecurityToken.InnerXml);
                    doc.PreserveWhitespace = true;
                    samlAssertion = doc.InnerXml;
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex);
            }
            return samlAssertion;
        }

        private string GetAdfsSAMLTokenWinAuth()
        {
            string samlAssertion = null;

            var request = (HttpWebRequest)WebRequest.Create(_adfsIntegratedAuthUrl);
            request.UseDefaultCredentials = true;

            var responseData = HttpHelper.SendHttpRequest(_adfsIntegratedAuthUrl, Method.GET, null, "text/html; charset=utf-8", request);

            if (responseData == null)
                return null;

            try
            {
                var sr = new StreamReader(new MemoryStream(responseData), Encoding.GetEncoding("utf-8"));
                var nav = new XPathDocument(sr).CreateNavigator();
                var wresult = nav.SelectSingleNode("/html/body/form/input[@name='wresult']");
                if (wresult != null)
                {
                    var requestSecurityTokenResponseText = wresult.GetAttribute("value", "");

                    sr = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(requestSecurityTokenResponseText)));
                    nav = new XPathDocument(sr).CreateNavigator();
                    var nsMgr = new XmlNamespaceManager(nav.NameTable);
                    nsMgr.AddNamespace("t", "http://schemas.xmlsoap.org/ws/2005/02/trust");
                    var requestedSecurityToken = nav.SelectSingleNode("//t:RequestedSecurityToken", nsMgr);

                    var doc = new XmlDocument();
                    doc.LoadXml(requestedSecurityToken.InnerXml);
                    doc.PreserveWhitespace = true;
                    samlAssertion = doc.InnerXml;
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError(ex);
            }
            return samlAssertion;
        }


        private SamlSecurityToken GetMsoStsSAMLToken()
        {
            var samlST = new SamlSecurityToken();
            byte[] saml11RTBytes = null;
            string logonToken = null;

            _adfsAuthUrl = GetAdfsAuthUrl();

            if (_adfsAuthUrl != null && _useIntegratedWindowsAuth)
            {
                var ub = new UriBuilder
                {
                    Scheme = _adfsAuthUrl.Scheme,
                    Host = _adfsAuthUrl.Host,
                    Path = $"{_adfsAuthUrl.LocalPath}auth/integrated/",
                    Query = $"{_adfsAuthUrl.Query.Remove(0, 1)}&wa=wsignin1.0&wtrealm=urn:federation:MicrosoftOnline".Replace("&username=", $"&username={_username}")
                };

                _adfsIntegratedAuthUrl = ub.Uri;

                logonToken = GetAdfsSAMLTokenWinAuth();

                if (!string.IsNullOrEmpty(logonToken))
                {
                    saml11RTBytes = Encoding.UTF8.GetBytes(ParameterizeSoapRequestAssertion(SiteUrl.ToString(), logonToken, MsoStsUrl));
                }
            }

            if (logonToken == null && _adfsAuthUrl != null && !string.IsNullOrEmpty(_password))
            {
                logonToken = GetAdfsSAMLTokenUsernamePassword();

                if (logonToken != null)
                {
                    saml11RTBytes = Encoding.UTF8.GetBytes(ParameterizeSoapRequestAssertion(SiteUrl.ToString(), logonToken, MsoStsUrl));
                }
            }

            if (logonToken == null && _adfsAuthUrl == null && !string.IsNullOrEmpty(_password))
            {
                saml11RTBytes = Encoding.UTF8.GetBytes(ParameterizedSoapRequest(SiteUrl.ToString(), _username, _password, MsoStsUrl));
            }

            if (saml11RTBytes != null)
            {
                var msoStsUri = new Uri(MsoStsUrl);

                var request = (HttpWebRequest)WebRequest.Create(msoStsUri);

                var responseData = HttpHelper.SendHttpRequest(msoStsUri, Method.POST, saml11RTBytes, "application/soap+xml; charset=utf-8", request, null);

                var sr = new StreamReader(new MemoryStream(responseData), Encoding.GetEncoding("utf-8"));
                var nav = new XPathDocument(sr).CreateNavigator();
                var nsMgr = new XmlNamespaceManager(nav.NameTable);

                nsMgr.AddNamespace("wsse", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
                var binarySecurityToken = nav.SelectSingleNode("//wsse:BinarySecurityToken", nsMgr);

                if (binarySecurityToken != null)
                {
                    nsMgr.AddNamespace("wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
                    var expires = nav.SelectSingleNode("//wsu:Expires", nsMgr);

                    if (!string.IsNullOrEmpty(binarySecurityToken.InnerXml) && !string.IsNullOrEmpty(expires?.InnerXml))
                    {
                        samlST.Token = Encoding.UTF8.GetBytes(binarySecurityToken.InnerXml);
                        samlST.Expires = DateTime.Parse(expires.InnerXml);
                    }
                }
                else
                {
                    ErrorHandler.LogError("binarySecurityToken == null");
                }
            }
            return samlST;
        }

        private string ParameterizedSoapRequest(string url, string username, string password, string toUrl)
        {
            var s = new StringBuilder();
            s.Append("<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:a=\"http://www.w3.org/2005/08/addressing\" xmlns:u=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd\">");
            s.Append("<s:Header>");
            s.Append("<a:Action s:mustUnderstand=\"1\">http://schemas.xmlsoap.org/ws/2005/02/trust/RST/Issue</a:Action>");
            s.Append("<a:ReplyTo>");
            s.Append("<a:Address>http://www.w3.org/2005/08/addressing/anonymous</a:Address>");
            s.Append("</a:ReplyTo>");
            s.Append("<a:To s:mustUnderstand=\"1\">[toUrl]</a:To>");
            s.Append("<o:Security s:mustUnderstand=\"1\" xmlns:o=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd\">");
            s.Append("<o:UsernameToken>");
            s.Append("<o:Username>[username]</o:Username>");
            s.Append("<o:Password>[password]</o:Password>");
            s.Append("</o:UsernameToken>");
            s.Append("</o:Security>");
            s.Append("</s:Header>");
            s.Append("<s:Body>");
            s.Append("<t:RequestSecurityToken xmlns:t=\"http://schemas.xmlsoap.org/ws/2005/02/trust\">");
            s.Append("<wsp:AppliesTo xmlns:wsp=\"http://schemas.xmlsoap.org/ws/2004/09/policy\">");
            s.Append("<a:EndpointReference>");
            s.Append("<a:Address>[url]</a:Address>");
            s.Append("</a:EndpointReference>");
            s.Append("</wsp:AppliesTo>");
            s.Append("<t:KeyType>http://schemas.xmlsoap.org/ws/2005/05/identity/NoProofKey</t:KeyType>");
            s.Append("<t:RequestType>http://schemas.xmlsoap.org/ws/2005/02/trust/Issue</t:RequestType>");
            s.Append("<t:TokenType>urn:oasis:names:tc:SAML:1.0:assertion</t:TokenType>");
            s.Append("</t:RequestSecurityToken>");
            s.Append("</s:Body>");
            s.Append("</s:Envelope>");

            var samlRtString = s.ToString();
            samlRtString = samlRtString.Replace("[username]", username);
            samlRtString = samlRtString.Replace("[password]", password);
            samlRtString = samlRtString.Replace("[url]", url);
            samlRtString = samlRtString.Replace("[toUrl]", toUrl);

            return samlRtString;
        }

        private string ParameterizeSoapRequestAssertion(string url, string samlAssertion, string toUrl)
        {
            var s = new StringBuilder();
            s.Append("<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:a=\"http://www.w3.org/2005/08/addressing\" xmlns:u=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd\">");
            s.Append("<s:Header>");
            s.Append("<a:Action s:mustUnderstand=\"1\">http://schemas.xmlsoap.org/ws/2005/02/trust/RST/Issue</a:Action>");
            s.Append("<a:ReplyTo>");
            s.Append("<a:Address>http://www.w3.org/2005/08/addressing/anonymous</a:Address>");
            s.Append("</a:ReplyTo>");
            s.Append("<a:To s:mustUnderstand=\"1\">[toUrl]</a:To>");
            s.Append("<o:Security s:mustUnderstand=\"1\" xmlns:o=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd\">[assertion]");
            s.Append("</o:Security>");
            s.Append("</s:Header>");
            s.Append("<s:Body>");
            s.Append("<t:RequestSecurityToken xmlns:t=\"http://schemas.xmlsoap.org/ws/2005/02/trust\">");
            s.Append("<wsp:AppliesTo xmlns:wsp=\"http://schemas.xmlsoap.org/ws/2004/09/policy\">");
            s.Append("<a:EndpointReference>");
            s.Append("<a:Address>[url]</a:Address>");
            s.Append("</a:EndpointReference>");
            s.Append("</wsp:AppliesTo>");
            s.Append("<t:KeyType>http://schemas.xmlsoap.org/ws/2005/05/identity/NoProofKey</t:KeyType>");
            s.Append("<t:RequestType>http://schemas.xmlsoap.org/ws/2005/02/trust/Issue</t:RequestType>");
            s.Append("<t:TokenType>urn:oasis:names:tc:SAML:1.0:assertion</t:TokenType>");
            s.Append("</t:RequestSecurityToken>");
            s.Append("</s:Body>");
            s.Append("</s:Envelope>");

            var samlRtString = s.ToString();
            samlRtString = samlRtString.Replace("[assertion]", samlAssertion);
            samlRtString = samlRtString.Replace("[url]", url);
            samlRtString = samlRtString.Replace("[toUrl]", toUrl);

            return samlRtString;
        }
    }
}



