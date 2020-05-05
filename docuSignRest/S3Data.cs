using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace docuSignRest
{
    public class S3Data
    {
        public readonly Amazon.RegionEndpoint bucketRegion= Amazon.RegionEndpoint.USEast1;
        private static IAmazonS3 client;
        private static string bucketName = "demo-staff.tekchoice.com";

        public static async Task<List<string>> ReadS3TXT(string TXTFile)
        {
            List<string> responseList = new List<string>();           

            client = new AmazonS3Client();

            string line=string.Empty;
            try
            {
                GetObjectRequest objReq = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = TXTFile,
                };

                using(GetObjectResponse objResp = await client.GetObjectAsync(objReq))
                using (Stream responseStream = objResp.ResponseStream)
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        responseList.Add(line);
                    }

                    reader.Close();
                }
            }
            catch
            {
               responseList.Add("Error Getting TXT File");
            }

            return responseList;
        }
        public static async Task<Stream> ReadS3PDF(string PDFFile)
        {
            client = new AmazonS3Client();
            Stream responseStream = new MemoryStream();

            try
            {
                GetObjectRequest objReq = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = PDFFile
                };

                GetObjectResponse objResp = await client.GetObjectAsync(objReq); 
                responseStream = objResp.ResponseStream;
                // using (Stream responseStream = objResp.ResponseStream)
                // {
                //     var bytes = ReadByteStream(responseStream);
                //     response= Convert.ToBase64String(bytes);
                // }
            }
            catch
            {
                throw;
            }

           return responseStream;
        }

        public static byte[] ReadByteStream(Stream responseStream)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = responseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }
    }
}