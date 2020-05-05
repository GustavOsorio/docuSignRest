# AWS Lambda with DocuSign API Rest

This starter project consists of:
* Function.cs - class file containing a class with a single function handler method
* aws-lambda-tools-defaults.json - default argument settings for use with Visual Studio and command line deployment tools for AWS
 

## Install  packages required from the command line:

```
    cd docuSignRest
```
Install The Amazon Lambda APIGatewayEvents SDK.
```
    dotnet add package Amazon.Lambda.APIGatewayEvents --version 1.2.0 
```

Install The Amazon Web Services SDK.
```
    dotnet add package AWSSDK.Core
```

Install The AWS Secrets Manager .
```
    dotnet add package AWSSDK.SecretsManager
```

Install The AWS Secrets Manager .NET caching.
```
    dotnet add package AWSSDK.SecretsManager.Caching
```

Install The DocuSign NuGet package.
```
    dotnet add package DocuSign.eSign.dll
```

Install System.Configuration.ConfigurationManager NuGet package.
```
    dotnet add package System.Configuration.ConfigurationManager
```

## Build from the command line:
```
    dotnet build
```

## Build from the command line:
```
    dotnet publish
```

## AWS Lambda config test:
```json
{
    "sub": "d381f00d-9dbf-4680-b8aa-7440f1bbd94f",
    "ClientRequest": "{ 'requestEP': 'TEST', 'fileName': 'AUTOPYMT', 'signerName': 'Gustavo Osorio K', 'signerEmail': 'gustavo.osorio@tekchoice.com', 'pdfValues': '{ \"AccountNumber\": \"3453455\", \"FirstPymtDate\": \"23-05-2019\" }' }"
}
```
