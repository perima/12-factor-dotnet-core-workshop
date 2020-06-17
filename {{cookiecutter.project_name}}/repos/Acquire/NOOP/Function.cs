using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.XRay.Recorder.Core;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace UploadTrigger
{
    public class Function
    {
        string regExNumberPlate { get; set; }
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

            AWSXRayRecorder recorder = AWSXRayRecorder.Instance;
            recorder.BeginSubsegment("TollGantry::Detect Number Plate in Captured Image");
            recorder.AddMetadata("bucket", s3Event.Bucket.Name);
            recorder.AddMetadata("key", s3Event.Object.Key);
            recorder.AddMetadata("regex", this.regExNumberPlate);
            //
            // TODO: Call Rekognition to detect text in the captured image
            //
            recorder.EndSubsegment();

            //
            // Kick off the step function 
            //
            context.Logger.LogLine("Starting the state machine");
            IAmazonStepFunctions stepFunctionsClient = new AmazonStepFunctionsClient();
            await stepFunctionsClient.StartExecutionAsync(new StartExecutionRequest() { StateMachineArn = Environment.GetEnvironmentVariable("NumberPlateProcessStateMachine"), Input = JsonConvert.SerializeObject(result) });
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
