#r "Newtonsoft.Json"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Configuration;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    string body = await req.Content.ReadAsStringAsync();
    log.Info($"CM Data: {body}");

    var eventData = JsonConvert.DeserializeObject<JObject>(body);

    //Tells us if the callback was of type Job or Review.                   
    var callbackType = (string)eventData["CallBackType"];

    //Getting postid from the callback url
    var postId = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "fbpostid", true) == 0).Value;

    switch(callbackType.ToLower())
    {

        case "job":{

            //The callback contains the a Review Id if a manual review was created based on the criteria specified in the workflow
            var reviewId = (string)eventData["ReviewId"];                           

            //If you have posts hidden by default then you can make it visible if there was no Review created
            if(string.IsNullOrWhiteSpace(reviewId)){ 
                await UpdateFBPostVisibility(log, postId, false);
            }  

            break;
        }
        case "review": {
            var reviewerResult = eventData["ReviewerResultTags"];
            var isAnyTagTrue = false;
            foreach (var x in ((JObject)reviewerResult))
            {
                string name = x.Key;              
                string val = (string)reviewerResult[name];
                log.Info($"Tag: {name}, Value: {val}");
                if(val.ToLower() == "true"){
                   isAnyTagTrue = true; 
                   break;     
                }

            }
            
            //You can delete the POST if it has tags that do not meet your policies
            //Following code deletes the post if any tag came back True 
            if(isAnyTagTrue){
                await DeleteFBPost(log, postId);
            }

            break;
        }

    }

    //Respond to Content Moderator with http 200 OK
    return req.CreateResponse(HttpStatusCode.OK, "Callback Processed");
}

//This method updates the visibility of the FB Post
private static async Task UpdateFBPostVisibility(TraceWriter log, string postId, bool hide)
{
    log.Info($"FB Updating Post Visibility: {postId}, Hidden: {hide}");

    var fbPageAccessToken = GetEnvironmentVariable("fb:PageAccessToken");
    var fbUrl = $"https://graph.facebook.com/v2.9/{postId}?access_token={fbPageAccessToken}";               
    using (var client = new HttpClient())
    {
        using (var content = new MultipartFormDataContent())
        {
            content.Add(new StringContent(hide.ToString().ToLower()), "is_hidden");                                     
            var result = await client.PostAsync(fbUrl, content);
            log.Info($"FB Response: {result.ToString()}");
        }
    }    
}

//This method deletes the FB Post
private static async Task DeleteFBPost(TraceWriter log, string postId)
{
    log.Info($"FB Deleting Post: {postId}");

    var fbPageAccessToken = GetEnvironmentVariable("fb:PageAccessToken");
    var fbUrl = $"https://graph.facebook.com/v2.9/{postId}?access_token={fbPageAccessToken}";               
    using (var client = new HttpClient())
    {
        var result = await client.DeleteAsync(fbUrl);
        log.Info($"FB Response: {result.ToString()}");        
    }    
}

private static string GetEnvironmentVariable(string name)
{
    return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
}
