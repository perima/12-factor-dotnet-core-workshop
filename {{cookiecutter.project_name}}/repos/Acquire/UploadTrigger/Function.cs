using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.XRay.Recorder.Core;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace UploadTrigger
{
    public class Function
    {
        IAmazonRekognition rekognitionClient { get; set; }
        string regExNumberPlate { get; set; }
        AWSXRayRecorder recorder = AWSXRayRecorder.Instance;
        public Function()
        {
            rekognitionClient = new AmazonRekognitionClient();
        }
        public async Task<NumberPlateTrigger> FunctionHandler(S3Event evnt, ILambdaContext context)
        {
            var s3Event = evnt.Records?[0].S3;
            context.Logger.LogLine("EVENT Received: " + JsonConvert.SerializeObject(s3Event));

            if (regExNumberPlate == null)
            {
                context.Logger.LogLine("regExNumberPlate is not yet populated. Calling getNumberPlateFromSecretsManager()...");
                regExNumberPlate = await getNumberPlateFromSecretsManager(context);
                context.Logger.LogLine("regExNumberPlate is " + regExNumberPlate);
            }

            //var response = await this.S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key);
            //return response.Headers.ContentType;

            NumberPlateTrigger result = new NumberPlateTrigger
            {
                bucket = s3Event.Bucket.Name,
                key = s3Event.Object.Key,
                contentType = "",
                contentLength = s3Event.Object.Size,
                charge = int.Parse(Environment.GetEnvironmentVariable("TollgateCharge")),
                numberPlate = new NumberPlate()
                {
                    numberPlateRegEx = this.regExNumberPlate,
                    detected = false
                }
            };

            recorder.BeginSubsegment("TollGantry::Detect Number Plate in Captured Image");
            recorder.AddMetadata("bucket", s3Event.Bucket.Name);
            recorder.AddMetadata("key", s3Event.Object.Key);
            recorder.AddMetadata("regex", this.regExNumberPlate);

            S3Object s3Object = new S3Object();
            s3Object.Bucket = s3Event.Bucket.Name;
            s3Object.Name = s3Event.Object.Key;
            DetectTextRequest detectTextReq = new DetectTextRequest { Image = new Image { S3Object = s3Object } };

            context.Logger.LogLine("Calling Rekognition ... ");
            DetectTextResponse detectTextResponse = await rekognitionClient.DetectTextAsync(detectTextReq);
            context.Logger.LogLine($"Response from Rekognition: {JsonConvert.SerializeObject(detectTextResponse)}");

            // Check if the a valid number was detected...
            foreach (var textItem in detectTextResponse.TextDetections)
            {
                if (!result.numberPlate.detected && textItem.Type.Value == "LINE" && textItem.Confidence > float.Parse(Environment.GetEnvironmentVariable("RekognitionTextMinConfidence")))
                {
                    Regex regex = new Regex(regExNumberPlate);
                    MatchCollection matches = regex.Matches(textItem.DetectedText);
                    context.Logger.LogLine($"Matches collection: {matches.Count}");
                    string plateNumber = "";
                    foreach (Match match in matches)
                    {
                        plateNumber += ( match.Groups[1].Value + match.Groups[2].Value);
                    }
                    if (!string.IsNullOrEmpty(plateNumber))
                    {
                        result.numberPlate.detected = true;
                        result.numberPlate.confidence = textItem.Confidence;
                        result.numberPlate.numberPlateString = plateNumber;
                        context.Logger.LogLine($"A valid plate number was detected ({result.numberPlate.numberPlateString})");
                    }
                }
            }

            recorder.EndSubsegment();

            //
            // At this point, we either know it is a valid number plate
            // or it couldn't be determined with adequate confidence
            // so we need manual intervention 
            //

            //
            // Kick off the step function 
            //

            context.Logger.LogLine("Starting the state machine");
            //TODO: add code to start the state machine
            context.Logger.LogLine("State machine started");

            return result;
        }
        private async Task<string> getNumberPlateFromSecretsManager(ILambdaContext context)
        {
            //TODO: Call secrets manager to retrieve the plate number regex
            return ".*";
        }

    }

    //
    // Data to be passed to the state machine
    //
    public class NumberPlateTrigger
    {
        public string bucket { get; set; }
        public string key { get; set; }
        public string contentType { get; set; }
        public long contentLength { get; set; }
        public NumberPlate numberPlate { get; set; }
        public int charge { get; set; }
    }
    public class NumberPlate
    {
        public bool detected { get; set; }
        public string numberPlateString { get; set; }
        public float confidence { get; set; }
        public string numberPlateRegEx { get; set; }
    }
}
