    

# Connect a Facbook Page to Content Moderator
This sample shows how you can connect a Facebook Page to Content Moderator and enable configurable workflows.

##Description
Following is what we will do at a high level:

1. Create a Content Moderator Team
2. Create Azure Functions that would listen for http events from Content Moderator and Facebook
3. Create a Facebook Page and App and configureo

Once the setup is done, all visitor posts on the Facebook Page would be sent to Content Moderator for executing the content workflow. Based on the thresholds specified in your Content Moderator workflow the posts would either be automatically un-hidden or go a through a human-review.

##Step by step guide
### Create a Content Moderator Team
- [Create the team](https://docs.microsoft.com/en-us/azure/cognitive-services/Content-Moderator/quick-start)
- [Configure connectors and workflows](https://docs.microsoft.com/en-us/azure/cognitive-services/Content-Moderator/review-tool-user-guide/workflows)

----------

###Create Azure Functions
For this step you will need to login to the [Azure Management Portal](https://portal.azure.com)

 - Create a new resource group - 
 - Add a Function App
 - Open the newly created function app
 - Navigate to Platform features -> Application Settings
 - Create the following application settings entries:
	 - cm:SubscriptionKey
	 - cm:TeamName
	 - cm:TextWorkflow
	 - cm:CallbackEndpoint
	 - cm:Region
	 - cm:ImageWorkflow
	 - fb:VerificationToken
	 - fb:PageAccessToken
	 

 - *Create FBListener* - This function receives events from Facebook 
	 - Click on the "**+**" add to create new function.
	 - Click on "**create your own custom function**"
	 - Click on the tile that says "HttpTrigger-CShrap"
	 - Enter a name "FBListener", the Authorization Level drop down should say **Function**
	 - Click Create
	 - Replace the contents of the run.csx with the following: 

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
			    //This is the verification token you enter on the Facebook App Dashboard while subscribing your app for publishing events
			    var verificationToken = GetEnvironmentVariable("fb:VerificationToken");
			
			    //parse query parameter from Facebook
			    string hubMode = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "hub.mode", true) == 0).Value;
			    string hubChallenge = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "hub.challenge", true) == 0).Value;
			    string hubverify_token = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "hub.verify_token", true) == 0).Value;
			
			    //This request is sent from FB when subscribing this endpoint
			    if(hubMode == "subscribe"){
			        if(hubverify_token == verificationToken)
			        {        
			            return new HttpResponseMessage(){ Content = new StringContent(hubChallenge)};
			        }else{
			            return new HttpResponseMessage(){ Content = new StringContent("Not Authorized"),StatusCode = HttpStatusCode.Unauthorized };
			        }   
			    }   
			
			
			    //Parsing the request that was sent from FB        
			    string body = await req.Content.ReadAsStringAsync();
			    log.Info($"FB Event Data: {body}");
			
			    var eventData = JsonConvert.DeserializeObject<JObject>(body);
			    var eventObject = eventData["entry"][0]["changes"][0]["value"];                 
			
			    //This is the unique id that identifies the post on the FB graph
			    var postId = (string)eventObject["post_id"];            
			
			    //This tells us if this was image post or a Text post
			    var itemType = (string)eventObject["item"];
			
			
			    //Returning if it was not an Add
			    var verb = (string)eventObject["verb"];
			    if(verb.ToLower() != "add" ){
			        return new HttpResponseMessage(){ Content = new StringContent("Received")};
			    }
			
			    
			    
			    //Name of the person who sent this post
			    var senderName = (string)eventObject["sender_name"];
			
			
			    switch(itemType){
			        case "photo":{
			            log.Info("Pushing Image for Moderation");
			            var imageUrl = (string)eventObject["link"];
			            var jobId = await CreateContentModerationJob(log, postId,"image", imageUrl);
			            log.Info($"CM Image JobId: {jobId}");
			            return new HttpResponseMessage(){ Content = new StringContent($"Image JobId: {jobId}")};    
			            break;
			        }
			        case "status":
			        case "post":{            
			            var text = (string)eventObject["message"];
			            if(!string.IsNullOrWhiteSpace(text))
			            {
			                log.Info("Pushing Text for Moderation");
			                var jobId = await CreateContentModerationJob(log, postId, "text", text);
			                log.Info($"CM Text JobId: {jobId}");
			            }
			            
			             var photos = eventObject["photos"];
			             if(photos != null)
			             {
			                 var photoCollection = (JArray)photos;
			                    foreach (var p in photoCollection)
			                    {
			                        var jobId = await CreateContentModerationJob(log, postId,"image", p.Value<string>());
			                        log.Info($"CM Image JobId: {jobId}");
			                    }
			             }
			
			            break;
			        }        
			    }
			
			    //responding to FB with 200 OK                  
			    return new HttpResponseMessage(){ Content = new StringContent("Received")};
			}
			
			//This method invokes the Content Moderator Job API to create a job with workflow specified for the content type
			private static async Task<string> CreateContentModerationJob(TraceWriter log, string postId, string contentType, string contentValue){
			
			    var subscriptionKey= GetEnvironmentVariable("cm:SubscriptionKey");
			    var teamName = GetEnvironmentVariable("cm:TeamName");                   
			    var callbackEndpoint =$"{GetEnvironmentVariable("cm:CallbackEndpoint")}%26fbpostid={postId}";
			    var region = GetEnvironmentVariable("cm:Region");
			
			    string workflowName = "";
			    switch(contentType)
			    {
			        case "text": { workflowName = GetEnvironmentVariable("cm:TextWorkflow"); break;}
			        case "image": { workflowName = GetEnvironmentVariable("cm:ImageWorkflow"); break;}
			    }
			
			    var cmUrl = $"https://{region}.api.cognitive.microsoft.com/contentmoderator/review/v1.0/teams/{teamName}/jobs?ContentType={contentType}&ContentId={postId}&WorkflowName={workflowName}&CallBackEndpoint={callbackEndpoint}";             
			    log.Info(cmUrl);
			
			    var client = new HttpClient();    
			    client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
			    var requestBodyObj = new { ContentValue = contentValue };    
			    string requestBody = JsonConvert.SerializeObject(requestBodyObj);
			    log.Info(requestBody);
			
			    
			
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
			        log.Info($"Response from CM: {res.ToString()}");  
			
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

 - *Create CMListener* - This function receives events from Content Moderator
	 - Click on the "**+**" add to create new function.
	 - Click on "**create your own custom function**"
	 - Click on the tile that says "HttpTrigger-CShrap"
	 - Enter a name "CMListener", the Authorization Level drop down should say **Function**
	 - Click Create
	 - Replace the contents of the run.csx with the following

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

----------

###Configure the Facebook Page and App:
####Create a Facebook App
 - Navigate to [https://developers.facebook.com/]()
 - Click on My Apps
 - Add a New App
 - Webhooks -> Get Started
 - Select Page -> Subscribe to this topic
 - Provide the **FBListener** Url as **Callback URL** and the **Verify Token** you have configured on the Function App Setting
 - Once subscribed, scroll to feed and hit **subscribe**


####Create a Facebook Page
 - Navigate to [https://www.facebook.com/bookmarks/pages]() and create a new Facebook Page
 - Giving the Facebook App access to this page:
	 - Navigate to [Graph API Explorer](https://developers.facebook.com/tools/explorer/)
	 - Select Application
	 - Select Page Access Token, Send Get
	 - **Click on the Id** in response (this is the Page Id)
	 - Now append **/subscribed_apps** to URL and Send Get (empty response)
	 - Send Post -> the response shall say **"success": true**

####Create a non-expiring graph api access token
 - Use the [Graph API Explorer](https://developers.facebook.com/tools/explorer/) to create a short lived user access token for the app
	- Select Application
	- Select Get User Access Token
	- We will the access token (Short Lived Token) in the next step

 - We will use Postman for the next few steps:
	 - Open postman (or get it [here](https://www.getpostman.com/))
	 - Import these files..
	 - Update the Environment Variables:
		 - 
				




