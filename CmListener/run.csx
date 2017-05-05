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
			
			//Hide the post if a Review was created, else make post visible 
			await UpdateFBPostVisibility(log, postId, string.IsNullOrWhiteSpace(reviewId));  
			
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
							
			//Hide the post if any Tag came back as True, else make the post visbile 
			await UpdateFBPostVisibility(log, postId, isAnyTagTrue);
			
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

private static string GetEnvironmentVariable(string name)
{
	return System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
}