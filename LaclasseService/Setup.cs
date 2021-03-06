﻿using System;

namespace Laclasse
{
    public class ServerSetup
    {
        public string name = "localhost";
        public int port = 4321;
        public bool stopOnException = false;
        public string publicUrl = "http://localhost:4321/";
        public string internalUrl = "http://localhost:4321/";
        public string publicFiles = "/usr/share/laclasse/";
        public string publicIcons = "/usr/share/laclasse/icons/";
        public string storage = "/var/lib/laclasse";
        public string log = "/var/log/laclasse";
        public string temporaryDirectory = "/var/lib/laclasse/tmp";
        public int maximumConcurrency = 2;
    }

    public class BonAppSetup
    {
        public string url = "https://bonapp-api.elior.com";
        public string apiKey = "password";
    }

    public class RestaurantSetup
    {
        public BonAppSetup bonApp = new BonAppSetup();
    }

    public class EduLibSetup
    {
        public string url = "https://test-service.edulib.fr/api/v1/catalog/laclasse";
        public string apiKey = "password";
    }

    public class TextbookSetup
    {
        public EduLibSetup eduLib = new EduLibSetup();
    }

    public class HttpSetup
    {
        public int defaultCacheDuration = 3600;
        public int keepAliveTimeout = 10;
        public int keepAliveMax = 0;
        public bool allowGZip = false;
    }

    public class DatabaseSetup
    {
        public string url = "server=localhost;userid=user;password=password;database=annuairev3";
    }

    public class EtherPadSetup
    {
        public string url = "/pads/";
        public string apiKey = "notdefined";
    }

    public class OnlyOfficeSetup
    {
        public string url = "/onlyoffice/";
    }

    public class DocSetup
    {
        public string url = "server=localhost;userid=user;password=password;database=docsv3";
        public string path = "/var/lib/laclasse-docs/";
        public EtherPadSetup etherpad = new EtherPadSetup();
        public OnlyOfficeSetup onlyoffice = new OnlyOfficeSetup();
    }

    public class LogSetup
    {
        public string alertEmail = null;
        public string requestLogFile = "/var/log/laclasse/access.log";
        public string errorLogFile = "/var/log/laclasse/error.log";
    }

    public class AafRun
    {
        public DayOfWeek day;
        public TimeSpan time;
    }

    public class AafSetup
    {
        public string path = "/var/lib/laclasse/aaf/";
        public string zipPath = "/var/lib/laclasse/aafZip/";
        public string logPath = "/var/lib/laclasse/aafLog/";
        public AafRun[] runs = new AafRun[] { };
    }

    public class StunSetup
    {
        public string host = "stun.services.mozilla.com";
        public int port = 3478;
    }

    public class WebRtcSetup
    {
        public StunSetup stun = new StunSetup();
    }

    public class MailServer
    {
        public string host = "localhost";
        public int port = 25;
        public string path = "/var/mail/";
    }

    public class MailSetup
    {
        public string from = "no-reply@laclasse.local";
        public MailServer server = new MailServer();
    }

    public class SmsSetup
    {
        public string url = "http://sen.laclasse.local/sms/";
        public string token = "the token here";
    }

    public class GrandLyonApiSetup
    {
        public string tokenUrl = "http://gdapi.laclasse.local/token";
        public string userInfoUrl = "http://glapi.laclasse.local/userinfo";
        public string authorization = "the token here";
    }

    public class AafSsoEndPointSetup
    {
        public string url;
        public string issuer;
        public string cert;
    }

    public class AafSsoSetup
    {
        public AafSsoEndPointSetup agents = new AafSsoEndPointSetup
        {
            url = "http://localhost/aaf-sso/?idp=agents",
            issuer = "portail-agents",
            cert = "MIIDrTCCApWgAwIBAgIJAIm7+brrOLC5MA0GCSqGSIb3DQEBCwUAMG0xCzAJBgNVBAYTAkZSMQ4wDAYDVQQIDAVSaG9uZTENMAsGA1UEBwwETHlvbjEaMBgGA1UECgwRTWV0cm9wb2xlIGRlIEx5b24xDzANBgNVBAsMBkVSQVNNRTESMBAGA1UEAwwJbG9jYWxob3N0MB4XDTE3MDIyNDA5NDAzN1oXDTI3MDIyMjA5NDAzN1owbTELMAkGA1UEBhMCRlIxDjAMBgNVBAgMBVJob25lMQ0wCwYDVQQHDARMeW9uMRowGAYDVQQKDBFNZXRyb3BvbGUgZGUgTHlvbjEPMA0GA1UECwwGRVJBU01FMRIwEAYDVQQDDAlsb2NhbGhvc3QwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDYwGqmtjfcM5E9VI8ATrrZwDA96+mSwFPqXGqy91zHm4+4l97hGQxKdexzfBE1LEXMbMPxOrX4N4B4bIMp+9hL3yiA6NtfX9jXcLcPTXVtd7AfCvCLNZDO3z9AS4VLhbCMA4JRA57XWDppSPWA8poOn0mC8olO+YUnSobg1pHGmEF8wm4LR2jdM66Pu/N3YwNygGvcYn2/dZuZV2FWsU4TxMLzPQ/hFjchtwiTwlvZKiSfrME33ycFyW17wIh8fragbyz3E7jNlrtcclr17gfyPLS/Z+hz21jnsjWSiGu5/WKeoTATtLJ6zqFA2Kpmn+xuiO8oqQfbla5uGBOFwYAhAgMBAAGjUDBOMB0GA1UdDgQWBBSj41cEp0FJ2eduwD7oiLueHBY1dTAfBgNVHSMEGDAWgBSj41cEp0FJ2eduwD7oiLueHBY1dTAMBgNVHRMEBTADAQH/MA0GCSqGSIb3DQEBCwUAA4IBAQCi/5gJpXBLH1nvIh6q2CGXyuCcCJHvqXWukgHUicOLFspDkDCObBpLMzF7MVCQfCVcKtoMVATrh3jLT1rEQws0tWV1wSbktBRsLRz3xxS1Ta1ktXdAVldvqcIMsZNWnCI0Rr3lhovRsTEXPsfUL0w4JD8CY9WP+mL2eDgzHpkgj/sFo+mx+UEZHjlJoNwnR7pe6erGFQoii+6ZnC+dab7ww0UUOa6Fv44OOBxxhGHGR43RLiWVg+6UcpojlAJLokdE4q9Jx1H2fq+FityUmSPJesC0qGqyIaZKHqd4GDFNq0loJqmvUaxaX2NbDFhOWTmGsBQhTTz39Zvezti1E9+n"
        };
        public AafSsoEndPointSetup parents = new AafSsoEndPointSetup
        {
            url = "http://localhost/aaf-sso/?idp=parents",
            issuer = "portail-parents",
            cert = "MIIDrTCCApWgAwIBAgIJAIm7+brrOLC5MA0GCSqGSIb3DQEBCwUAMG0xCzAJBgNVBAYTAkZSMQ4wDAYDVQQIDAVSaG9uZTENMAsGA1UEBwwETHlvbjEaMBgGA1UECgwRTWV0cm9wb2xlIGRlIEx5b24xDzANBgNVBAsMBkVSQVNNRTESMBAGA1UEAwwJbG9jYWxob3N0MB4XDTE3MDIyNDA5NDAzN1oXDTI3MDIyMjA5NDAzN1owbTELMAkGA1UEBhMCRlIxDjAMBgNVBAgMBVJob25lMQ0wCwYDVQQHDARMeW9uMRowGAYDVQQKDBFNZXRyb3BvbGUgZGUgTHlvbjEPMA0GA1UECwwGRVJBU01FMRIwEAYDVQQDDAlsb2NhbGhvc3QwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDYwGqmtjfcM5E9VI8ATrrZwDA96+mSwFPqXGqy91zHm4+4l97hGQxKdexzfBE1LEXMbMPxOrX4N4B4bIMp+9hL3yiA6NtfX9jXcLcPTXVtd7AfCvCLNZDO3z9AS4VLhbCMA4JRA57XWDppSPWA8poOn0mC8olO+YUnSobg1pHGmEF8wm4LR2jdM66Pu/N3YwNygGvcYn2/dZuZV2FWsU4TxMLzPQ/hFjchtwiTwlvZKiSfrME33ycFyW17wIh8fragbyz3E7jNlrtcclr17gfyPLS/Z+hz21jnsjWSiGu5/WKeoTATtLJ6zqFA2Kpmn+xuiO8oqQfbla5uGBOFwYAhAgMBAAGjUDBOMB0GA1UdDgQWBBSj41cEp0FJ2eduwD7oiLueHBY1dTAfBgNVHSMEGDAWgBSj41cEp0FJ2eduwD7oiLueHBY1dTAMBgNVHRMEBTADAQH/MA0GCSqGSIb3DQEBCwUAA4IBAQCi/5gJpXBLH1nvIh6q2CGXyuCcCJHvqXWukgHUicOLFspDkDCObBpLMzF7MVCQfCVcKtoMVATrh3jLT1rEQws0tWV1wSbktBRsLRz3xxS1Ta1ktXdAVldvqcIMsZNWnCI0Rr3lhovRsTEXPsfUL0w4JD8CY9WP+mL2eDgzHpkgj/sFo+mx+UEZHjlJoNwnR7pe6erGFQoii+6ZnC+dab7ww0UUOa6Fv44OOBxxhGHGR43RLiWVg+6UcpojlAJLokdE4q9Jx1H2fq+FityUmSPJesC0qGqyIaZKHqd4GDFNq0loJqmvUaxaX2NbDFhOWTmGsBQhTTz39Zvezti1E9+n"
        };
    }

    public class Saml2Setup
    {
        public string cert;
    }

    public class CasSetup
    {
        public int ticketTimeout = 60;
        public int rescueTicketTimeout = 600;
    }

    public class CUTSsoSetup
    {
        public string name;
        public string authorizeUrl;
        public string userInfoUrl;
        public string tokenUrl;
        public string logoutUrl;
        public string clientId;
        public string password;
    }

    public class SessionSetup
    {
        public int timeout = 43200;
        public int longTimeout = 1296000;
        public string header = "x-laclasse-authentication";
        public string cookie = "LACLASSE_AUTH";
    }

    public class AuthenticationSetup
    {
        public string masterPassword = "masterPassword";
        public Saml2Setup saml2Server = new Saml2Setup
        {
            cert = null
        };
        public AafSsoSetup aafSso = new AafSsoSetup();
        public CasSetup cas = new CasSetup();
        public SessionSetup session = new SessionSetup();
        public CUTSsoSetup cutSso = new CUTSsoSetup();
        public GrandLyonApiSetup grandLyonApi = new GrandLyonApiSetup();
    }

    public class GARSetup
    {
        public string idEnt = null;
        public string SFTPServer = null;
        public string SFTPLogin = null;
        public string listeRessourcesUrl = null;
        public string listeRessourcesCert = null;
        public string adminUrl = null;
    }

    public class Setup
    {
        public ServerSetup server = new ServerSetup();
        public HttpSetup http = new HttpSetup();
        public DatabaseSetup database = new DatabaseSetup();
        public LogSetup log = new LogSetup();
        public AafSetup aaf = new AafSetup();
        public WebRtcSetup webRTC = new WebRtcSetup();
        public MailSetup mail = new MailSetup();
        public SmsSetup sms = new SmsSetup();
        public AuthenticationSetup authentication = new AuthenticationSetup();
        public DocSetup doc = new DocSetup();
        public RestaurantSetup restaurant = new RestaurantSetup();
        public TextbookSetup textbook = new TextbookSetup();
        public GARSetup gar = new GARSetup();
    }
}
