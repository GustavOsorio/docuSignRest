using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.APIGatewayEvents;
using System.Runtime.Serialization;

using Amazon.Lambda.Core;

using DocuSign.eSign.Api;
using DocuSign.eSign.Client;
using DocuSign.eSign.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.LambdaJsonSerializer))]

namespace docuSignRest
{
    public class Function
    {
        private string signerName = Environment.GetEnvironmentVariable("signerName");
        private string signerEmail = Environment.GetEnvironmentVariable("signerEmail");
        private const string basePath = "https://demo.docusign.net/restapi";
        private string returnUrl = "https://habx7eyy0e.execute-api.us-east-1.amazonaws.com/test-docu/";

        public async Task<APIGatewayProxyResponse> FunctionHandler(System.IO.Stream apiProxyEvent, ILambdaContext context)
        {
            var secretManager = new SecretManager();
            var secret = secretManager.GetSecret();

            SecretManager secretsKeys = JsonConvert.DeserializeObject<SecretManager>(secret);
            AppSettings appSettings = AppSettings.GetAppSettings(secretsKeys.accountId, secretsKeys.integratorKey, secretsKeys.docuSignAuthEmail, secretsKeys.docuSignAuthPass);

            Console.WriteLine("The secretsKeys: " + secretsKeys.integratorKey);

            string strBodyRequest;
            string response = "Invalid Request";
            string validateEnvelop = "No se pudo envíar el correo";
            int IntStatusCode = 400;
            using (var reader = new StreamReader(apiProxyEvent, Encoding.UTF8))
            {
                strBodyRequest = reader.ReadToEnd();
            }

            var dicBody = JsonConvert.DeserializeObject<Dictionary<string, string>>(strBodyRequest);
            RequestData ClientRequest = JsonConvert.DeserializeObject<RequestData>(dicBody["ClientRequest"]);
            Dictionary<string, string> dataPdfValues = JsonConvert.DeserializeObject<Dictionary<string, string>>(ClientRequest.pdfValues);

            string ds_signer1_name = ClientRequest.signerName ?? signerName;
            string ds_signer1_email = ClientRequest.signerEmail ?? signerEmail;
            string webhook_url = Environment.GetEnvironmentVariable("urlReturn") ?? returnUrl;
            string authHeader = "{\"Username\":\"" + secretsKeys.docuSignAuthEmail + "\", \"Password\":\"" + secretsKeys.docuSignAuthPass + "\", \"IntegratorKey\":\"" + secretsKeys.integratorKey + "\"}";

            Task<List<string>> readS3TXT = S3Data.ReadS3TXT("eSignature/FieldMapping/"+ClientRequest.fileName+".txt");
            List<string> mappingFileVarsList = await readS3TXT;
            
            Dictionary<string, SymitarVars> symitarVars = SymitarVars.getSymitarVars(mappingFileVarsList, dataPdfValues);
            
            if (!String.IsNullOrWhiteSpace(ds_signer1_email) && symitarVars.Count > 0 && !String.IsNullOrWhiteSpace(ClientRequest.fileName))
            {
                Task<Boolean> dateRequest = sendRequestSign(ds_signer1_name, ds_signer1_email, secretsKeys.accountId, webhook_url, authHeader, symitarVars, ClientRequest.fileName);
                Boolean activateFunction = await dateRequest;
                validateEnvelop = "Se envío el documento a " + ds_signer1_name + " " + ds_signer1_email;
            }

            switch (ClientRequest.requestEP)
            {
                //{'requestEP':'TEST'}
                case "TEST":
                    response = validateEnvelop;
                    IntStatusCode = 200;
                    break;
                case "ARRAY":
                    response = ClientRequest.requestData;
                    IntStatusCode = 200;
                    break;
                default:
                    break;
            }

            if (IntStatusCode == 400)
            {
                response = "Error";
            }
            return SendResponseToClient(response, IntStatusCode);
        }

        public static APIGatewayProxyResponse SendResponseToClient(string strBody, int IntStatusCode)
        {
            return new APIGatewayProxyResponse
            {
                Body = strBody,
                StatusCode = IntStatusCode,
                Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
            };
        }
        public async Task<Boolean> sendRequestSign(string signerName, string signerEmail, string accountId, string returnUrl, string authHeader, Dictionary<string, SymitarVars> symitarVars, string fileName)
        {
            List<EnvelopeEvent> envelope_events = new List<EnvelopeEvent>();

            EnvelopeEvent envelope_event1 = new EnvelopeEvent();
            envelope_event1.EnvelopeEventStatusCode = "sent";
            envelope_events.Add(envelope_event1);
            EnvelopeEvent envelope_event2 = new EnvelopeEvent();
            envelope_event2.EnvelopeEventStatusCode = "delivered";
            envelope_events.Add(envelope_event2);
            EnvelopeEvent envelope_event3 = new EnvelopeEvent();
            envelope_event3.EnvelopeEventStatusCode = "completed";

            List<RecipientEvent> recipient_events = new List<RecipientEvent>();
            RecipientEvent recipient_event1 = new RecipientEvent();
            recipient_event1.RecipientEventStatusCode = "Sent";
            recipient_events.Add(recipient_event1);
            RecipientEvent recipient_event2 = new RecipientEvent();
            recipient_event2.RecipientEventStatusCode = "Delivered";
            recipient_events.Add(recipient_event2);
            RecipientEvent recipient_event3 = new RecipientEvent();
            recipient_event3.RecipientEventStatusCode = "Completed";

            EventNotification event_notification = new EventNotification();
            event_notification.Url = returnUrl;
            event_notification.LoggingEnabled = "true";
            event_notification.RequireAcknowledgment = "true";
            event_notification.UseSoapInterface = "false";
            event_notification.IncludeCertificateWithSoap = "false";
            event_notification.SignMessageWithX509Cert = "false";
            event_notification.IncludeDocuments = "true";
            event_notification.IncludeEnvelopeVoidReason = "true";
            event_notification.IncludeTimeZone = "true";
            event_notification.IncludeSenderAccountAsCustomField = "true";
            event_notification.IncludeDocumentFields = "true";
            event_notification.IncludeCertificateOfCompletion = "true";
            event_notification.EnvelopeEvents = envelope_events;
            event_notification.RecipientEvents = recipient_events;


            Tabs tabs = new Tabs();
            tabs.TextTabs = new List<Text>();
            tabs.SignHereTabs = new List<SignHere>();
            tabs.DateSignedTabs = new List<DateSigned>();
            int index = 1;
            foreach (KeyValuePair<string, SymitarVars> data in symitarVars)
            {
                 System.Console.WriteLine(data.Value.FieldLabel);
                 System.Console.WriteLine(data.Value.FieldValue);
                if (data.Value.FieldType.ToUpper() != "SETTING" && (data.Value.FieldType != "DocuSignField" || (data.Value.FieldType == "DocuSignField" && !String.IsNullOrWhiteSpace(data.Value.FieldValue))))
                {
                    switch (data.Value.FieldType)
                    {
                        case "DocuSignField":
                            Text text_tab = new Text();
                            text_tab.AnchorString = "/*"+data.Value.FieldLabel+"*/";
                            text_tab.AnchorYOffset = "-8";
                            text_tab.AnchorXOffset = "0";
                            text_tab.RecipientId = string.Concat("",index);
                            text_tab.TabLabel = data.Value.FieldLabel;
                            text_tab.Name = data.Value.FieldValue;
                            text_tab.Value = data.Value.FieldValue;
                            // text_tab.Required = data.Value.Required;
            
                            tabs.TextTabs.Add(text_tab);
                            break;
                        case "SignatureField":
                            SignHere sign_here_tab = new SignHere();
                            sign_here_tab.AnchorString = "/*"+data.Value.FieldLabel+"*/";
                            sign_here_tab.AnchorXOffset = "0";
                            sign_here_tab.AnchorYOffset = "0";
                            sign_here_tab.Name = "";
                            sign_here_tab.Optional = data.Value.Required;
                            sign_here_tab.ScaleValue = "1";
                            sign_here_tab.TabLabel = data.Value.FieldLabel;
                            
                            tabs.SignHereTabs.Add(sign_here_tab);
                            break;
                        case "DateSigned":                        
                            DateSigned date_signed_tab = new DateSigned();
                            date_signed_tab.AnchorString = "/*"+data.Value.FieldLabel+"*/";
                            date_signed_tab.AnchorYOffset = "-6";
                            date_signed_tab.RecipientId = string.Concat("",index);
                            date_signed_tab.Name = "";
                            date_signed_tab.TabLabel = data.Value.FieldLabel;
                            // date_signed_tab.Required = data.Value.Required;
                            
                            tabs.DateSignedTabs.Add(date_signed_tab);
                            break;
                        default:
                            break;
                        index++;
                    }
                }
            }

            Signer signer = new Signer();
            signer.Email = signerEmail;
            signer.Name = signerName;
            signer.RecipientId = "1";
            signer.RoutingOrder = "1";
            signer.Tabs = tabs;

            
            Task<Stream> getS3PDF = S3Data.ReadS3PDF("eSignature/PDFTemplate/"+fileName+".pdf");
            Stream newPdf = await getS3PDF;
            var bytes = S3Data.ReadByteStream(newPdf);

            Document document = new Document();
            document.DocumentId = "1";
            document.Name = fileName;

            // byte[] buffer = System.IO.File.ReadAllBytes("AUTOPYMT2.pdf");
            document.DocumentBase64 = Convert.ToBase64String(bytes);

            Recipients recipients = new Recipients();
            recipients.Signers = new List<Signer>();
            recipients.Signers.Add(signer);

            EnvelopeDefinition envelopeDefinition = new EnvelopeDefinition();
            envelopeDefinition.EmailSubject = "Please sign the " + "AUTOPYMT.pdf" + " document";
            envelopeDefinition.Documents = new List<Document>();
            envelopeDefinition.Documents.Add(document);
            envelopeDefinition.Recipients = recipients;
            envelopeDefinition.EventNotification = event_notification;
            envelopeDefinition.Status = "sent";

            ApiClient apiClient = new ApiClient(basePath);
            apiClient.Configuration.AddDefaultHeader("X-DocuSign-Authentication", authHeader);
            EnvelopesApi envelopesApi = new EnvelopesApi(apiClient.Configuration);

            EnvelopeSummary envelope_summary = envelopesApi.CreateEnvelope(accountId, envelopeDefinition);
            if (envelope_summary == null || envelope_summary.EnvelopeId == null)
            {
                return false;
            }

            // apiClient.Configuration.AddDefaultHeader("Authorization", "Bearer " + accessToken);
            // EnvelopesApi envelopesApi = new EnvelopesApi(WebhookLibrary.Configuration);
            // EnvelopeSummary envelope_summary = envelopesApi.CreateEnvelope(WebhookLibrary.AccountId, envelope_definition, null);

            // string envelope_id = envelope_summary.EnvelopeId;
            Console.WriteLine(envelope_summary);
            return true;
        }
        public class RequestData
        {
            [DataMember]
            public string requestEP { get; set; }
            [DataMember]
            public string requestData { get; set; }
            [DataMember]
            public string signerEmail { get; set; }
            [DataMember]
            public string signerName { get; set; }
            [DataMember]
            public string returnUrl { get; set; }
            [DataMember]
            public string pdfValues { get; set; }
            [DataMember]
            public string fileName { get; set; }

        }

        // public async Task<string> Handler(System.IO.Stream apigProxyEvent, ILambdaContext context)
        // {
        //     string strBody;
        //     using (var reader = new StreamReader(apigProxyEvent, Encoding.UTF8))
        //     {
        //         strBody = reader.ReadToEnd();
        //     }

        //     // Get HTML FORM values -> HtmlFields Dictionary
        //     var dicBody = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(strBody);
        //     Dictionary<string, string> HtmlFields = dicBody["body"];
        //     Task<List<string>> readS3TXT = S3Data.ReadS3TXT(DocuFilePath + HtmlFields["DocuFileName"] + ".txt");
        //     List<string> mappingFileVarsList = await readS3TXT;
        //     Dictionary<string, SymitarVars> symitarVars = SymitarVars.getSymitarVars(mappingFileVarsList, HtmlFields);
        //     AppSettings appSettings = AppSettings.GetAppSettings(HtmlFields["SYM"], HtmlFields["SymitarID"]);
        //     Task<XmlDocument> getSoapXML = CreateAndSendEnvelopeXML(appSettings, symitarVars);
        //     XmlDocument SoapXML = await getSoapXML;
        //     Task<string> sendDocuSign = SendDocuSign(SoapXML, appSettings);
        //     string EnvelopeID = await sendDocuSign;
        //     /*
        //         Task<string> getSoapTXT =  CreateAndSendEnvelopeTXT(appSettings, symitarVars);
        //         string EnvelopeID = await getSoapTXT;
        //     */
        //     return SendSymitarFormatResponse(EnvelopeID);
        // }

        // public string SendSymitarFormatResponseTest(string str)
        // {
        //     string response = @"<html><body><p>" + str + "</p></body></html>";
        //     return response;
        // }

        // public string SendSymitarFormatResponse(string str)
        // {
        //     string response = @"<!DOCTYPE html><html xmlns='http://www.w3.org/1999/xhtml'>";
        //     response += @"<script type='text/javascript'>function submitform(){var elMsg = document.getElementById('PostResponse');";
        //     response += @"elMsg.style.display = 'block';document.getElementById('htmlrginputform').submit();}</script>"; response += @"<body onload='submitform()'><form name='htmlrginputform' id='htmlrginputform' method='post' ";
        //     response += @"action='symitar://HTMLView~Action=Post' ><input type='text' name='PostResponse' id='PostResponse' ";
        //     response += @"style='display:none' ";
        //     response += @"value='" + str + "' />";
        //     response += @"</form></body></html>";

        //     return response;
        // }

        // public async Task<string> SendDocuSign(XmlDocument SoapXML, AppSettings appSettings)
        // {
        //     string response = "Error Retrieving Docusign ID";
        //     string soapResult = "";
        //     System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        //     HttpWebRequest webRequest = CreateWebRequest(DocusignWSDL, CreateAndSendEnvelope, appSettings);
        //     InsertSoapEnvelopeIntoWebRequest(SoapXML, webRequest);
        //     try
        //     {
        //         WebResponse webResponse = await webRequest.GetResponseAsync();
        //         StreamReader rd = new StreamReader(webResponse.GetResponseStream());
        //         soapResult = rd.ReadToEnd();
        //         webResponse.Close();
        //         XmlDocument responseText = new XmlDocument();
        //         responseText.LoadXml(soapResult);
        //         XmlNodeList nList = responseText.GetElementsByTagName("EnvelopeID");

        //         for (int i = 0; i < nList.Count; i++)
        //         {
        //             response = nList[i].InnerText.ToString();
        //         }
        //     }
        //     catch (WebException wex)
        //     {
        //         response = "Error Sending Docusign: " + soapResult + " web:" + wex.ToString();
        //     }

        //     return response;
        // }

        // public HttpWebRequest CreateWebRequest(string url, string action, AppSettings appSettings)
        // {
        //     HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
        //     webRequest.Headers.Add("SOAPAction", action);
        //     webRequest.Headers.Add("X-DocuSign-Authentication", "<DocuSignCredentials><Username>" + appSettings.DocuSignAuthEmail + @"</Username><Password>" + appSettings.DocuSignAuthPass + @"</Password><IntegratorKey>" + appSettings._integratorKey + @"</IntegratorKey></DocuSignCredentials>");
        //     webRequest.ContentType = "text/xml;charset=\"UTF-8\"";
        //     webRequest.Method = "POST";

        //     return webRequest;
        // }

        // public void InsertSoapEnvelopeIntoWebRequest(XmlDocument soapEnvelopeXml, HttpWebRequest webRequest)
        // {
        //     using (Stream stream = webRequest.GetRequestStream())
        //     {
        //         soapEnvelopeXml.Save(stream);
        //     }
        // }

        // protected async Task<XmlDocument> CreateAndSendEnvelopeXML(AppSettings appSettings, Dictionary<string, SymitarVars> symitarVars)
        // {
        //     XmlDocument soapEnvelop = new XmlDocument();
        //     string SOAPRequest = @"<soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/' xmlns:ns='http://www.docusign.net/API/3.0' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema'>";
        //     SOAPRequest += @"<soapenv:Header/>";
        //     SOAPRequest += @"<soapenv:Body>";
        //     SOAPRequest += @"<ns:CreateAndSendEnvelope>";
        //     SOAPRequest += @"<ns:Envelope>";
        //     SOAPRequest += @"<TransactionID></TransactionID>";
        //     SOAPRequest += @"<ns:Asynchronous>false</ns:Asynchronous>";
        //     SOAPRequest += @"<ns:AccountId>" + appSettings._accountId + "</ns:AccountId>";
        //     SOAPRequest += @"<ns:Subject>" + appSettings.EnvelopeSubject + @"</ns:Subject>";
        //     SOAPRequest += @"<ns:EmailBlurb>" + appSettings.EnvelopeEmailBlurb + @"</ns:EmailBlurb>";
        //     SOAPRequest += @"<ns:Tabs>";
        //     foreach (KeyValuePair<string, SymitarVars> item in symitarVars)
        //     {
        //         if (item.Value.FieldType.ToUpper() != "SETTING")
        //         {
        //             SOAPRequest += @"<ns:Tab>";
        //             SOAPRequest += @"<ns:DocumentID>1</ns:DocumentID>";
        //             SOAPRequest += @"<ns:RecipientID>1</ns:RecipientID>";
        //             SOAPRequest += @"<ns:CustomTabRequired>false</ns:CustomTabRequired>";
        //             SOAPRequest += @"<CustomTabLocked>true</CustomTabLocked>";
        //             SOAPRequest += @"<ns:AnchorTabItem>";
        //             SOAPRequest += @"<ns:AnchorTabString>/*" + item.Value.FieldLabel + @"*/</ns:AnchorTabString>";
        //             SOAPRequest += @"<ns:IgnoreIfNotPresent>true</ns:IgnoreIfNotPresent>";
        //             SOAPRequest += @"</ns:AnchorTabItem>";
        //             switch (item.Value.FieldType)
        //             {
        //                 case "DocuSignField":
        //                     SOAPRequest += @"<ns:Type>Custom</ns:Type>";
        //                     SOAPRequest += @"<ns:TabLabel>" + item.Value.FieldLabel + @"</ns:TabLabel>";
        //                     SOAPRequest += @"<ns:Value>" + item.Value.FieldValue + @"</ns:Value>";
        //                     break;
        //                 case "SignatureField":
        //                     SOAPRequest += @"<ns:Type>SignHere</ns:Type>";
        //                     break;
        //                 case "SignatureFieldOptional":
        //                     SOAPRequest += @"<ns:Type>SignHereOptional</ns:Type>";
        //                     break;
        //                 case "DateSigned":
        //                     SOAPRequest += @"<ns:Type>DateSigned</ns:Type>";
        //                     break;
        //                 default:
        //                     SOAPRequest += @"<ns:Type>Custom</ns:Type>";
        //                     break;
        //             }
        //             SOAPRequest += @"</ns:Tab>";
        //         }
        //     }
        //     SOAPRequest += @"</ns:Tabs>";
        //     SOAPRequest += @"<ns:Documents>";
        //     SOAPRequest += @"<ns:Document>";
        //     SOAPRequest += @"<ns:ID>1</ns:ID>";
        //     SOAPRequest += @"<ns:Name>" + symitarVars["DocuFileName"].FieldValue + @".pdf</ns:Name>";
        //     SOAPRequest += @"<ns:PDFBytes>";
        //     Task<string> getS3 = S3Data.ReadS3PDF(DocuFilePath + symitarVars["DocuFileName"].FieldValue + ".pdf");
        //     SOAPRequest += await getS3;
        //     SOAPRequest += @"</ns:PDFBytes>";
        //     SOAPRequest += @"<FileExtension>pdf</FileExtension>";
        //     SOAPRequest += @"</ns:Document>";
        //     SOAPRequest += @"</ns:Documents>";
        //     SOAPRequest += @"<ns:Recipients>";
        //     SOAPRequest += @"<ns:Recipient>";
        //     SOAPRequest += @"<ns:ID>1</ns:ID>";
        //     SOAPRequest += @"<ns:UserName>" + symitarVars["MemberName"].FieldValue + @"</ns:UserName>";
        //     SOAPRequest += @"<ns:Email>";
        //     SOAPRequest += (appSettings.IsTesting) ? symitarVars["OperatorEmail"].FieldValue : symitarVars["DocuSignEmail"].FieldValue;
        //     SOAPRequest += @"</ns:Email>";
        //     SOAPRequest += @"<ns:Type>Signer</ns:Type>";
        //     SOAPRequest += @"<ns:AccessCode xsi:nil='true'></ns:AccessCode>";
        //     SOAPRequest += @"<ns:RequireIDLookup>true</ns:RequireIDLookup>";
        //     string[] arrDocuSignPhoneList;

        //     if (appSettings.IsTesting)
        //     {
        //         arrDocuSignPhoneList = (symitarVars["OperatorPhone"].FieldValue.IndexOf(",") > -1) ? symitarVars["OperatorPhone"].FieldValue.Split(',') : new string[] { symitarVars["OperatorPhone"].FieldValue };
        //     }

        //     else
        //     {
        //         arrDocuSignPhoneList = (symitarVars["DocuSignPhoneList"].FieldValue.IndexOf(",") > -1) ? symitarVars["DocuSignPhoneList"].FieldValue.Split(',') : new string[] { symitarVars["DocuSignPhoneList"].FieldValue };
        //     }

        //     SOAPRequest += @"<ns:IDCheckConfigurationName>" + symitarVars["DocuSignAuthType"].FieldValue + "</ns:IDCheckConfigurationName>";
        //     if (symitarVars["DocuSignAuthType"].FieldValue == "Phone Auth $")
        //     {
        //         SOAPRequest += @"<ns:PhoneAuthentication>";
        //         SOAPRequest += @"<ns:RecipMayProvideNumber>false</ns:RecipMayProvideNumber>";
        //         SOAPRequest += @"<ns:SenderProvidedNumbers>";
        //         foreach (string str in arrDocuSignPhoneList)
        //         {
        //             SOAPRequest += @"<ns:SenderProvidedNumber>" + str + @"</ns:SenderProvidedNumber>";
        //         }

        //         SOAPRequest += @"</ns:SenderProvidedNumbers>";
        //         SOAPRequest += @"</ns:PhoneAuthentication>";
        //     }

        //     else
        //     {
        //         SOAPRequest += @"<ns:SMSAuthentication>";
        //         SOAPRequest += @"<ns:SenderProvidedNumbers>";
        //         foreach (string str in arrDocuSignPhoneList)
        //         {
        //             SOAPRequest += @"<ns:SenderProvidedNumber>" + str + @"</ns:SenderProvidedNumber>";
        //         }

        //         SOAPRequest += @"</ns:SenderProvidedNumbers>";
        //         SOAPRequest += @"</ns:SMSAuthentication>";
        //     }

        //     SOAPRequest += @"</ns:Recipient>";
        //     SOAPRequest += @"</ns:Recipients>";

        //     SOAPRequest += @"<ns:EmailSettings>";
        //     SOAPRequest += @"<ns:ReplyEmailAddressOverride>" + symitarVars["OperatorEmail"].FieldValue + @"</ns:ReplyEmailAddressOverride>";
        //     SOAPRequest += @"<ns:ReplyEmailNameOverride>" + appSettings.CUName + @"</ns:ReplyEmailNameOverride>";
        //     SOAPRequest += @"</ns:EmailSettings>";

        //     SOAPRequest += @"<ns:EventNotification>";
        //     SOAPRequest += @"<ns:URL>https://nt21igvv57.execute-api.us-east-1.amazonaws.com/test/test2</ns:URL>";
        //     SOAPRequest += @"<ns:loggingEnabled>true</ns:loggingEnabled>";
        //     SOAPRequest += @"<ns:requireAcknowledgment>false</ns:requireAcknowledgment>";
        //     SOAPRequest += @"<ns:useSoapInterface>false</ns:useSoapInterface>";
        //     SOAPRequest += @"<ns:includeCertificateWithSoap>false</ns:includeCertificateWithSoap>";
        //     SOAPRequest += @"<ns:signMessageWithX509Cert>false</ns:signMessageWithX509Cert>";
        //     SOAPRequest += @"<ns:includeDocuments>false</ns:includeDocuments>";
        //     SOAPRequest += @"<ns:includeEnvelopeVoidReason>false</ns:includeEnvelopeVoidReason>";
        //     SOAPRequest += @"<ns:includeTimeZone>false</ns:includeTimeZone>";
        //     SOAPRequest += @"<ns:includeSenderAccountAsCustomField>true</ns:includeSenderAccountAsCustomField>";
        //     SOAPRequest += @"<ns:includeDocumentFields>false</ns:includeDocumentFields>";
        //     SOAPRequest += @"<ns:includeCertificateOfCompletion>false</ns:includeCertificateOfCompletion>";
        //     SOAPRequest += @"<ns:EnvelopeEvents>";
        //     SOAPRequest += @"<ns:EnvelopeEvent xsi:nil='true'>Completed</ns:EnvelopeEvent>";
        //     SOAPRequest += @"</ns:EnvelopeEvents>";
        //     SOAPRequest += @"</ns:EventNotification>";

        //     SOAPRequest += @"</ns:Envelope>";
        //     SOAPRequest += @"</ns:CreateAndSendEnvelope>";
        //     SOAPRequest += @"</soapenv:Body>";
        //     SOAPRequest += @"</soapenv:Envelope>";
        //     soapEnvelop.LoadXml(SOAPRequest);

        //     return soapEnvelop;
        // }

        // protected async Task<string> CreateAndSendEnvelopeTXT(AppSettings appSettings, Dictionary<string, SymitarVars> symitarVars)
        // {
        //     string SOAPRequest = @"<soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/' xmlns:ns='http://www.docusign.net/API/3.0' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema'>";

        //     SOAPRequest += @"<soapenv:Header/>";
        //     SOAPRequest += @"<soapenv:Body>";
        //     SOAPRequest += @"<ns:CreateAndSendEnvelope>";
        //     SOAPRequest += @"<ns:Envelope>";
        //     SOAPRequest += @"<TransactionID></TransactionID>";
        //     SOAPRequest += @"<ns:Asynchronous>false</ns:Asynchronous>";
        //     SOAPRequest += @"<ns:AccountId>" + appSettings._accountId + "</ns:AccountId>";
        //     SOAPRequest += @"<ns:Subject>" + appSettings.EnvelopeSubject + @"</ns:Subject>";
        //     SOAPRequest += @"<ns:EmailBlurb>" + appSettings.EnvelopeEmailBlurb + @"</ns:EmailBlurb>";
        //     SOAPRequest += @"<ns:Tabs>";
        //     foreach (KeyValuePair<string, SymitarVars> item in symitarVars)
        //     {
        //         if (item.Value.FieldType.ToUpper() != "SETTING")
        //         {
        //             SOAPRequest += @"<ns:Tab>";
        //             SOAPRequest += @"<ns:DocumentID>1</ns:DocumentID>";
        //             SOAPRequest += @"<ns:RecipientID>1</ns:RecipientID>";
        //             SOAPRequest += @"<ns:AnchorTabItem>";
        //             SOAPRequest += @"<ns:AnchorTabString>/*" + item.Value.FieldLabel + @"*/</ns:AnchorTabString>";
        //             SOAPRequest += @"</ns:AnchorTabItem>";
        //             switch (item.Value.FieldType)
        //             {
        //                 case "DocuSignField":
        //                     SOAPRequest += @"<ns:Type>Custom</ns:Type>";
        //                     SOAPRequest += @"<ns:TabLabel>" + item.Value.FieldLabel + @"</ns:TabLabel>";
        //                     SOAPRequest += @"<ns:Value>" + item.Value.FieldValue + @"</ns:Value>";
        //                     break;
        //                 case "SignatureField":
        //                     SOAPRequest += @"<ns:Type>SignHere</ns:Type>";
        //                     break;
        //                 case "SignatureFieldOptional":
        //                     SOAPRequest += @"<ns:Type>SignHereOptional</ns:Type>";
        //                     break;
        //                 case "DateSigned":
        //                     SOAPRequest += @"<ns:Type>DateSigned</ns:Type>";
        //                     break;
        //                 default:
        //                     SOAPRequest += @"<ns:Type>Custom</ns:Type>";
        //                     break;
        //             }
        //             SOAPRequest += @"</ns:Tab>";
        //         }
        //     }

        //     SOAPRequest += @"</ns:Tabs>";
        //     SOAPRequest += @"<ns:Documents>";
        //     SOAPRequest += @"<ns:Document>";
        //     SOAPRequest += @"<ns:ID>1</ns:ID>";
        //     SOAPRequest += @"<ns:Name>" + symitarVars["DocuFileName"].FieldValue + @".pdf</ns:Name>";
        //     SOAPRequest += @"<ns:PDFBytes>";
        //     Task<string> getS3 = S3Data.ReadS3PDF(DocuFilePath + symitarVars["DocuFileName"].FieldValue + ".pdf");

        //     SOAPRequest += await getS3;
        //     SOAPRequest += @"</ns:PDFBytes>";
        //     SOAPRequest += @"<FileExtension>pdf</FileExtension>";
        //     SOAPRequest += @"</ns:Document>";
        //     SOAPRequest += @"</ns:Documents>";
        //     SOAPRequest += @"<ns:Recipients>";
        //     SOAPRequest += @"<ns:Recipient>";
        //     SOAPRequest += @"<ns:ID>1</ns:ID>";
        //     SOAPRequest += @"<ns:UserName>" + symitarVars["MemberName"].FieldValue + @"</ns:UserName>";
        //     SOAPRequest += @"<ns:Email>";
        //     SOAPRequest += (appSettings.IsTesting) ? symitarVars["OperatorEmail"].FieldValue : symitarVars["DocuSignEmail"].FieldValue;
        //     SOAPRequest += @"</ns:Email>";
        //     SOAPRequest += @"<ns:Type>Signer</ns:Type>";
        //     SOAPRequest += @"<ns:AccessCode xsi:nil='true'></ns:AccessCode>";
        //     SOAPRequest += @"<ns:RequireIDLookup>true</ns:RequireIDLookup>";
        //     string[] arrDocuSignPhoneList;

        //     if (appSettings.IsTesting)
        //     {
        //         arrDocuSignPhoneList = (symitarVars["OperatorPhone"].FieldValue.IndexOf(",") > -1) ? symitarVars["OperatorPhone"].FieldValue.Split(',') : new string[] { symitarVars["OperatorPhone"].FieldValue };
        //     }
        //     else
        //     {
        //         arrDocuSignPhoneList = (symitarVars["DocuSignPhoneList"].FieldValue.IndexOf(",") > -1) ? symitarVars["DocuSignPhoneList"].FieldValue.Split(',') : new string[] { symitarVars["DocuSignPhoneList"].FieldValue };
        //     }
        //     SOAPRequest += @"<ns:IDCheckConfigurationName>" + symitarVars["DocuSignAuthType"].FieldValue + "</ns:IDCheckConfigurationName>";
        //     if (symitarVars["DocuSignAuthType"].FieldValue == "Phone Auth $")
        //     {
        //         SOAPRequest += @"<ns:PhoneAuthentication>";
        //         SOAPRequest += @"<ns:RecipMayProvideNumber>false</ns:RecipMayProvideNumber>";
        //         SOAPRequest += @"<ns:SenderProvidedNumbers>";
        //         foreach (string str in arrDocuSignPhoneList)
        //         {
        //             SOAPRequest += @"<ns:SenderProvidedNumber>" + str + @"</ns:SenderProvidedNumber>";
        //         }
        //         SOAPRequest += @"</ns:SenderProvidedNumbers>";
        //         SOAPRequest += @"</ns:PhoneAuthentication>";
        //     }
        //     else
        //     {
        //         SOAPRequest += @"<ns:SMSAuthentication>";
        //         SOAPRequest += @"<ns:SenderProvidedNumbers>";
        //         foreach (string str in arrDocuSignPhoneList)
        //         {
        //             SOAPRequest += @"<ns:SenderProvidedNumber>" + str + @"</ns:SenderProvidedNumber>";
        //         }
        //         SOAPRequest += @"</ns:SenderProvidedNumbers>";
        //         SOAPRequest += @"</ns:SMSAuthentication>";
        //     }
        //     SOAPRequest += @"</ns:Recipient>";
        //     SOAPRequest += @"</ns:Recipients>";

        //     SOAPRequest += @"<ns:EmailSettings>";
        //     SOAPRequest += @"<ns:ReplyEmailAddressOverride>" + symitarVars["OperatorEmail"].FieldValue + @"</ns:ReplyEmailAddressOverride>";
        //     SOAPRequest += @"<ns:ReplyEmailNameOverride>" + appSettings.CUName + @"</ns:ReplyEmailNameOverride>";
        //     SOAPRequest += @"</ns:EmailSettings>";

        //     SOAPRequest += @"</ns:Envelope>";
        //     SOAPRequest += @"</ns:CreateAndSendEnvelope>";
        //     SOAPRequest += @"</soapenv:Body>";
        //     SOAPRequest += @"</soapenv:Envelope>";
        //     return SOAPRequest;
        // }
    }
}
