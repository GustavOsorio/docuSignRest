using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;

namespace docuSignRest
{
    [DataContract]
    public class SymitarVars
    {
        [DataMember]
        public string FieldType { get; set; }
        [DataMember]
        public string FieldValue { get; set; }
        [DataMember]
        public string FieldLabel { get; set; }
        [DataMember]
        public string PageNumber { get; set; } = "1";
        [DataMember]
        public string Required { get; set; } = "true";

        public static Dictionary<string, SymitarVars> getSymitarVars(List<string> mappingFileVarsList, Dictionary<string, string> pdfValues)
        {
            Dictionary<string, SymitarVars> symitarVars = new Dictionary<string, SymitarVars>();
            string[] myfields;

            foreach (string str in mappingFileVarsList)
            {
                myfields = str.Split('=');

                if (myfields[0] == "SymitarField")
                {
                    string[] myfieldsSub = myfields[1].Split('|');

                    if (myfieldsSub[0].ToUpper() == "SETTING")
                    {
                        symitarVars.Add(myfieldsSub[1], new SymitarVars() { FieldType = myfieldsSub[0], FieldLabel = myfieldsSub[1] });
                    }
                    else
                    {
                        if (!symitarVars.ContainsKey(myfieldsSub[1]))
                        {
                            symitarVars.Add(myfieldsSub[1], new SymitarVars() { FieldType = myfieldsSub[0], FieldLabel = myfieldsSub[1], PageNumber = myfieldsSub[2] });
                        }
                    }
                }
            }

            foreach (KeyValuePair<string, SymitarVars> item in symitarVars)
            {
                foreach (KeyValuePair<string, string> subitem in pdfValues)
                {
                    if (item.Value.FieldLabel == subitem.Key)
                    {
                        item.Value.FieldValue = subitem.Value;
                    }
                }
            }

            return symitarVars;
        }
    }
}