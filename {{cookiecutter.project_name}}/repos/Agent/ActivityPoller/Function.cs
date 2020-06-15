using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.StepFunctions;
using Amazon.StepFunctions.Model;
using Newtonsoft.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Amazon.S3;
using Amazon.S3.Model;
using System.Web;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ActivityPoller
{
  public class Function
  {
    private AmazonStepFunctionsClient stepFunctionsClient;
    private IAmazonDynamoDB dbClient;
    private IAmazonSimpleEmailService emailServiceClient;
    private IAmazonS3 s3client;
    public Function()
    {
      stepFunctionsClient = new AmazonStepFunctionsClient();
      dbClient = new AmazonDynamoDBClient();
      emailServiceClient = new AmazonSimpleEmailServiceClient();
      s3client = new AmazonS3Client();
    }

    public void FunctionHandler(ILambdaContext context)
    {
      Task<string> insufficientCreditHandlerrResult = InsufficientCreditHandler(Environment.GetEnvironmentVariable("StepFunctionActivityInsufficientCredit"), context);
      Task<string> unknownNumberPlateHandlerResult = UnknownNumberPlateHandler(Environment.GetEnvironmentVariable("StepFunctionActivityManualPlateInspection"), context);
      Task.WaitAll(new Task[]
      {
        insufficientCreditHandlerrResult,
        unknownNumberPlateHandlerResult
      });
    }

    private async Task<string> InsufficientCreditHandler(string insufficientCreditActivityARN, ILambdaContext context)
    {
      string result = "";
      GetActivityTaskResponse response = await stepFunctionsClient.GetActivityTaskAsync(
        new GetActivityTaskRequest() { ActivityArn = insufficientCreditActivityARN });
      if (HttpStatusCode.OK.Equals(response.HttpStatusCode) && !string.IsNullOrEmpty(response.TaskToken))
      {
        // Task is found
        context.Logger.LogLine($"InsufficientCreditHandler: Found a task. Input is: {response.Input}");
        NumberPlateTrigger input = JsonConvert.DeserializeObject<NumberPlateTrigger>(response.Input);
        
        if (!input.numberPlate.detected)
        {
          context.Logger.LogLine("TESTING::input.numberPlate.detected is false which means this must be a test");
          context.Logger.LogLine("Forcing number plate to test value");
          input.numberPlate.detected = true;
          input.numberPlate.numberPlateString = "TESTPLATE";
        }
        //Sign Image URL
        string imageLink = s3client.GetPreSignedURL(new GetPreSignedUrlRequest() { BucketName = input.bucket, Key = input.key, Expires = DateTime.Now.AddDays(1)});
        //
        // Query DynamoDB to get the owner email
        //
        Table table = Table.LoadTable(dbClient, Environment.GetEnvironmentVariable("DDBTableName"));
        Document document = await table.GetItemAsync(input.numberPlate.numberPlateString);
        if(document == null)
        {
          context.Logger.LogLine($"Could not find plate {input.numberPlate.numberPlateString} in our records");
        }
        var sendRequest = new SendEmailRequest
        {
          Source = Environment.GetEnvironmentVariable("TargetEmailAddress"),
          ReplyToAddresses = new List<string> { Environment.GetEnvironmentVariable("TargetEmailAddress") },
          Destination = new Destination
          {
            ToAddresses =
                       new List<string> { document["ownerEmail"] }
          },
          Message = new Message
          {
            Subject = new Content("[ACTION] - Your account credit is exhausted"),
            Body = new Body
            {
              Html = new Content
              {
                Charset = "UTF-8",
                Data = $"Hello {document["ownerFirstName"]} {document["ownerLastName"]},<br/><br/>Your vehicle with number plate <b>{document["numberPlate"]}</b> was recently detected on a toll road, but your account has insufficient credit to pay the toll.<br/><br/>" +
                  $"<img src='{imageLink}'/><br/><a href='{imageLink}'>Click here to see the original image</a><br/><br/>" +
                  "Please update your account balance immediately to avoid a fine. <br/>" +
                  $"<a href='{Environment.GetEnvironmentVariable("APIGWEndpoint")}topup/{document["numberPlate"]}?taskToken={HttpUtility.UrlEncode(response.TaskToken)}><b>Click this link to top up your account now.</b></a><br/>" +
                  "<br/><br/> Thanks<br/><b>Toll Road Administrator.</b><br/><br/>"
              },
              Text = new Content
              {
                Charset = "UTF-8",
                Data = $"Hello {document["ownerFirstName"]} {document["ownerLastName"]}, Your vehicle with number plate: {document["numberPlate"]} was recently detected on a toll road, but your account has insufficient credit to pay the toll." +
                  "Please update your account balance immediately to avoid a fine." +
                  $"Please access this link to top up: {Environment.GetEnvironmentVariable("APIGWEndpoint")}topup/{document["numberPlate"]}?taskToken={HttpUtility.UrlEncode(response.TaskToken)}" +
                  ".. Thanks. Toll Road Administrator."
              }
            }
          },
          // If you are not using a configuration set, comment
          // or remove the following line 
          //ConfigurationSetName = configSet
        };

        context.Logger.LogLine($"Sending email to ({Environment.GetEnvironmentVariable("TargetEmailAddress")})");
        SendEmailResponse sendEmailResponse = await emailServiceClient.SendEmailAsync(sendRequest);
        if (sendEmailResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
        {
          context.Logger.LogLine("The email was successfully sent.");
          result = "success";
        }
        else
        {
          context.Logger.LogLine("Internal Error: The email could not be sent.");
          result = "error";
        } 
      }
      return result;
    }

    private async Task<string> UnknownNumberPlateHandler(string unknowNumberActivityARN, ILambdaContext context)
    {
      string result = "";
      GetActivityTaskResponse response = await stepFunctionsClient.GetActivityTaskAsync(
        new GetActivityTaskRequest() { ActivityArn = unknowNumberActivityARN });
      if (HttpStatusCode.OK.Equals(response.HttpStatusCode) && !string.IsNullOrEmpty(response.Input))
      {
        context.Logger.LogLine($"ManualAdminTaskHandler: Found a task. Input is: {response.Input}");
        NumberPlateTrigger input = JsonConvert.DeserializeObject<NumberPlateTrigger>(response.Input);
        //Sign Image URL
        string imageLink = s3client.GetPreSignedURL(new GetPreSignedUrlRequest() { BucketName = input.bucket, Key = input.key, Expires = DateTime.Now.AddDays(1) });
        var sendRequest = new SendEmailRequest
        {
          Source = Environment.GetEnvironmentVariable("TargetEmailAddress"),
          ReplyToAddresses = new List<string> { Environment.GetEnvironmentVariable("TargetEmailAddress") },
          Destination = new Destination
          {
            ToAddresses =
                       new List<string> { Environment.GetEnvironmentVariable("TargetEmailAddress") }
          },
          Message = new Message
          {
            Subject = new Content("[ACTION] - Manual Decision Required!"),
            Body = new Body
            {
              Html = new Content
              {
                Charset = "UTF-8",
                Data = $"Hello {Environment.GetEnvironmentVariable("TargetEmailAddress")},< br />< br /> An image was captured at a toll booth, " +
                       "but the Number Plate Processor could not be confident that it could determine the actual number plate on the vehicle. We need your help to take a look at the image," +
                       "and make a determination.< br />< br />" +
                       $"<img src='{imageLink}'/><br/><a href=' {imageLink}'>Click here to see the original image if it is not appearing in the email correclty.</a><br/><br/>" +
                       $"<a href='{Environment.GetEnvironmentVariable("APIGWEndpoint")}parse/{input.bucket}/{input.key}/5?imageLink={HttpUtility.UrlEncode(imageLink)}&taskToken={HttpUtility.UrlEncode(response.TaskToken)}'><b>Click this link to help assess the image and provide the number plate.</b></a><br/>" +
                       "<br/><br/>Thanks<br/><b>Toll Road Administrator.</b><br/><br/>"
            },
              Text = new Content
              {
                Charset = "UTF-8",
                Data = $"Hello {Environment.GetEnvironmentVariable("TargetEmailAddress")}, An image was captured at a toll booth, " +
                       "but the Number Plate Processor could not be confident that it could determine the actual number plate on the vehicle. We need your help to take a look at the image," +
                       "and make a determination." +
                       $"Please access this link to take a decision: {Environment.GetEnvironmentVariable("APIGWEndpoint")}parse/{input.bucket}/{input.key}/5?imageLink={HttpUtility.UrlEncode(imageLink)}&taskToken={HttpUtility.UrlEncode(response.TaskToken)}" +
                       " .. Thanks. Toll Road Administrator"
              }
            }
          },
          // If you are not using a configuration set, comment
          // or remove the following line 
          //ConfigurationSetName = configSet
        };
        context.Logger.LogLine($"Sending email to ({Environment.GetEnvironmentVariable("TargetEmailAddress")})");
        SendEmailResponse sendEmailResponse = await emailServiceClient.SendEmailAsync(sendRequest);
        if (sendEmailResponse.HttpStatusCode.Equals(HttpStatusCode.OK))
        {
          context.Logger.LogLine("The email was successfully sent.");
          result = "success";
        }
        else
        {
          context.Logger.LogLine("Internal Error: The email could not be sent.");
          result = "error";
        }

      }
      return result;
    }

  }

  //
  // Data to be passed to the state machine
  //
  class NumberPlateTrigger
    {
      public string bucket { get; set; }
      public string key { get; set; }
      public string contentType { get; set; }
      public long contentLength { get; set; }
      public NumberPlate numberPlate { get; set; }
      public int charge { get; set; }
    }
    class NumberPlate
    {
      public bool detected { get; set; }
      public string numberPlateString { get; set; }
      public float confidence { get; set; }
      public string numberPlateRegEx { get; set; }
    }


}
