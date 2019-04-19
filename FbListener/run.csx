#r "Newtonsoft.Json"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Configuration;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    //This is the verification token you enter on the Facebook App Dashboard while subscribing your app for publishing events
    var verificationToken = GetEnvironmentVariable("fb:VerificationToken");

    //parse query parameter from Facebook
    string hubMode = req.Query["hub.mode"];
    string hubChallenge = req.Query["hub.challenge"];
    string hubverify_token = req.Query["hub.verify_token"];

    //This request is sent from FB when subscribing this endpoint
    if(hubMode == "subscribe"){
        if(hubverify_token == verificationToken)
        {
            return (ActionResult)new OkObjectResult(hubChallenge);
        }
        else
        {
            return new UnauthorizedObjectResult("Not Authorized");;
        }   
    }   

    //Parsing the request that was sent from FB        
    string body = await new StreamReader(req.Body).ReadToEndAsync();
    log.LogInformation($"FB Event Data: {body}");

    var eventData = JsonConvert.DeserializeObject<JObject>(body);
    var eventObject = eventData["entry"][0]["changes"][0]["value"];                 

    //This is the unique id that identifies the post on the FB graph
    var postId = (string)eventObject["post_id"];            

    //This tells us if this was image post or a Text post
    var itemType = (string)eventObject["item"];

    //Returning if it was not an Add
    var verb = (string)eventObject["verb"];
    if(verb.ToLower() != "add" ){
        return (ActionResult)new OkObjectResult("Received");
    }
    
    //Name of the person who sent this post
    var senderName = (string)eventObject["sender_name"];

    switch(itemType){
        case "photo":{
            log.LogInformation("Pushing Image for Moderation");
            var imageUrl = (string)eventObject["link"];
            var jobId = await CreateContentModerationJob(log, postId,"image", imageUrl);
            log.LogInformation($"CM Image JobId: {jobId}");
            return (ActionResult)new OkObjectResult($"Image JobId: {jobId}");
        }
        case "post":{            
            var text = (string)eventObject["message"];
            if(!string.IsNullOrWhiteSpace(text))
            {
                log.LogInformation("Pushing Text for Moderation");
                var jobId = await CreateContentModerationJob(log, postId, "text", text);
                log.LogInformation($"CM Text JobId: {jobId}");
            }
            
            var photos = eventObject["photos"];
            if(photos != null)
            {
                var photoCollection = (JArray)photos;
                foreach (var p in photoCollection)
                {
                    var jobId = await CreateContentModerationJob(log, postId,"image", p.Value<string>());
                    log.LogInformation($"CM Image JobId: {jobId}");
                }
            }

            break;
        }
        case "status":
        case "comment":
            var commentId = (string)eventObject["comment_id"];
            var comment = (string)eventObject["message"];
            if(!string.IsNullOrWhiteSpace(comment))
            {
                log.LogInformation("Pushing Text for Moderation");
                var jobId = await CreateContentModerationJob(log, commentId, "text", comment);
                log.LogInformation($"CM Text JobId: {jobId}");
            }
            break;        
    }

    //responding to FB with 200 OK
    return (ActionResult)new OkObjectResult("Received");    
}

//This method invokes the Content Moderator Job API to create a job with workflow specified for the content type
private static async Task<string> CreateContentModerationJob(ILogger log, string postId, string contentType, string contentValue)
{
    var subscriptionKey= GetEnvironmentVariable("cm:SubscriptionKey");
    var teamId = GetEnvironmentVariable("cm:TeamId");                   
    var callbackEndpoint =$"{GetEnvironmentVariable("cm:CallbackEndpoint")}%26fbpostid={postId}";
    var region = GetEnvironmentVariable("cm:Region");

    string workflowName = "";
    switch(contentType)
    {
        case "text": { workflowName = GetEnvironmentVariable("cm:TextWorkflow"); break;}
        case "image": { workflowName = GetEnvironmentVariable("cm:ImageWorkflow"); break;}
    }

    var cmUrl = $"https://{region}.api.cognitive.microsoft.com/contentmoderator/review/v1.0/teams/{teamId}/jobs?ContentType={contentType}&ContentId={postId}&WorkflowName={workflowName}&CallBackEndpoint={callbackEndpoint}";             
    log.LogInformation(cmUrl);

    var client = new HttpClient();    
    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
    var requestBodyObj = new { ContentValue = contentValue };
    string requestBody = JsonConvert.SerializeObject(requestBodyObj);
    log.LogInformation(requestBody);

    HttpResponseMessage response = null;
    string jobId = "";
    int tryCount = 0;
    do{    
        tryCount++;
        var content = new StringContent(requestBody,Encoding.UTF8,"application/json");
        response = await client.PostAsync(cmUrl, content);
        var cmResp = await response.Content.ReadAsStringAsync();
        var res = JsonConvert.DeserializeObject<JObject>(cmResp);
        jobId = (string)res["JobId"];
        log.LogInformation($"Response from CM: {res.ToString()}");  

        if(!response.IsSuccessStatusCode){
            System.Threading.Thread.Sleep(2000);
        }

    }while(!response.IsSuccessStatusCode && tryCount < 3);

    return jobId;
}

//Method to read app settings
public static string GetEnvironmentVariable(string name)
{
    return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
}
