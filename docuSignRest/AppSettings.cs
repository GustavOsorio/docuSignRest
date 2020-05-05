using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace docuSignRest
{
    [DataContract]
    public class AppSettings
    {
        [DataMember]
        public string EnvelopeSubject { get; set; }
        [DataMember]
        public string EnvelopeEmailBlurb { get; set; }
        [DataMember]
        public string ImagingPath { get; set; }
        [DataMember]
        public string EpisysPowerON { get; set; }
        [DataMember]
        public string EpisysMenu { get; set; }
        [DataMember]
        public string ProductionSYM { get; set; }
        [DataMember]
        public string BackOfficeEmail { get; set; }
        [DataMember]
        public string BackOfficeEmailSubject { get; set; }
        [DataMember]
        public string _integratorKey { get; set; }
        [DataMember]
        public string DocuSignAuthEmail { get; set; }
        [DataMember]
        public string DocuSignAuthPass { get; set; }
        [DataMember]
        public string _accountId { get; set; }
        [DataMember]
        public string CUName { get; set; }
        [DataMember]
        public string SymitarID { get; set; }
        [DataMember]
        public bool IsTesting { get; set; } = true;
 
        public static AppSettings GetAppSettings(string id, string integratorKey, string email, string pass)
        {
            AppSettings appSettings = new AppSettings();
            appSettings._integratorKey = integratorKey;
            appSettings.DocuSignAuthEmail = email;
            appSettings.DocuSignAuthPass = pass;
            appSettings._accountId = id;
            appSettings.IsTesting = true;

            // if (appSettings.ProductionSYM==SYM.Substring(0,3))
            // {
            //     appSettings.DocuSignAuthEmail= Function.DocuSignAuthEmail;
            //     appSettings.DocuSignAuthPass= Function.DocuSignAuthPass;
            //     appSettings._accountId= Function._accountId;               
            //     appSettings.IsTesting=false;
            // }
            // else
            // {
            //     appSettings.EnvelopeSubject="GFCU Env TEST- "+appSettings.EnvelopeSubject;
            // }

            return appSettings;
        }
    }
}