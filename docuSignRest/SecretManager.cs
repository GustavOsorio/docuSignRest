using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Extensions.Caching;
using Amazon.SecretsManager.Model;
using System.Runtime.Serialization;

namespace docuSignRest
{
    public class SecretManager
    {
        
        [DataMember]
        public string accountId { get; set; }
        [DataMember]
        public string integratorKey { get; set; }
        [DataMember]
        public string docuSignAuthPass { get; set; }
        [DataMember]
        public string docuSignAuthEmail { get; set; }

        public string GetSecret()
        {
            string secretName = Environment.GetEnvironmentVariable("secretName") ?? "secretsDocuSign";
            string region = "us-east-1";
            string secret = "";

            MemoryStream memoryStream = new MemoryStream();
            IAmazonSecretsManager client = new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region));
            GetSecretValueRequest request = new GetSecretValueRequest();

            request.SecretId = secretName;
            request.VersionStage = "AWSCURRENT";

            GetSecretValueResponse response = null;

            // In this sample we only handle the specific exceptions for the 'GetSecretValue' API.
            // See https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_GetSecretValue.html
            // We rethrow the exception by default.

            try
            {
                response = client.GetSecretValueAsync(request).Result;
            }
            catch (DecryptionFailureException e)
            {
                // Secrets Manager can't decrypt the protected secret text using the provided KMS key.
                // Deal with the exception here, and/or rethrow at your discretion.
                throw;
            }
            catch (InternalServiceErrorException e)
            {
                // An error occurred on the server side.
                // Deal with the exception here, and/or rethrow at your discretion.
                throw;
            }
            catch (InvalidParameterException e)
            {
                // You provided an invalid value for a parameter.
                // Deal with the exception here, and/or rethrow at your discretion
                throw;
            }
            catch (InvalidRequestException e)
            {
                // You provided a parameter value that is not valid for the current state of the resource.
                // Deal with the exception here, and/or rethrow at your discretion.
                throw;
            }
            catch (ResourceNotFoundException e)
            {
                // We can't find the resource that you asked for.
                // Deal with the exception here, and/or rethrow at your discretion.
                throw;
            }
            catch (System.AggregateException e)
            {
                // More than one of the above exceptions were triggered.
                // Deal with the exception here, and/or rethrow at your discretion.
                throw;
            }

            // Decrypts secret using the associated KMS CMK.
            // Depending on whether the secret is a string or binary, one of these fields will be populated.
            if (response.SecretString != null)
            {
                secret = response.SecretString;
            }
            else
            {
                memoryStream = response.SecretBinary;
                StreamReader reader = new StreamReader(memoryStream);
                string decodedBinarySecret = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(reader.ReadToEnd()));
            }

            return secret;
        }
    }
}
