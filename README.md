    

# Connect a Facebook Page to Content Moderator
This sample shows how you can connect a Facebook Page to Content Moderator and enable configurable workflows.

## Description
Following is what we will do at a high level:

1. Create a Content Moderator Team
2. Create Azure Functions that would listen for http events from Content Moderator and Facebook
3. Create a Facebook Page and App, and configure

Once the setup is done, all visitor posts on the Facebook Page would be sent to Content Moderator for executing the content workflow. Based on the thresholds specified in your Content Moderator workflow the posts would either be automatically un-hidden or go a through a human-review.

## Step by step guide
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
	 - Replace the contents of the run.csx with contents posted at [FbListener/run.csx](FbListener/run.csx)


 - *Create CMListener* - This function receives events from Content Moderator
	 - Click on the "**+**" add to create new function.
	 - Click on "**create your own custom function**"
	 - Click on the tile that says "HttpTrigger-CShrap"
	 - Enter a name "CMListener", the Authorization Level drop down should say **Function**
	 - Click Create
	 - Replace the contents of the run.csx with contents posted at [CmListener/run.csx](CmListener/run.csx)


----------

### Configure the Facebook Page and App:
#### Create a Facebook App
 - Navigate to [https://developers.facebook.com/]()
 - Click on My Apps
 - Add a New App
 - Webhooks -> Get Started
 - Select Page -> Subscribe to this topic
 - Provide the **FBListener** Url as **Callback URL** and the **Verify Token** you have configured on the Function App Setting
 - Once subscribed, scroll to feed and hit **subscribe**


#### Create a Facebook Page
 - Navigate to [https://www.facebook.com/bookmarks/pages]() and create a new Facebook Page
 - Giving the Facebook App access to this page:
	 - Navigate to [Graph API Explorer](https://developers.facebook.com/tools/explorer/)
	 - Select Application
	 - Select Page Access Token, Send Get
	 - **Click on the Id** in response (this is the Page Id)
	 - Now append **/subscribed_apps** to URL and Send Get (empty response)
	 - Send Post -> the response shall say **"success": true**

#### Create a non-expiring graph api access token
 - Use the [Graph API Explorer](https://developers.facebook.com/tools/explorer/) to create a short lived user access token for the app
	- Select Application
	- Select Get User Access Token
	- We will the access token (Short Lived Token) in the next step

 - We will use Postman for the next few steps:
	 - Open postman (or get it [here](https://www.getpostman.com/))
	 - Import these files 
 		- Postman Collection - [samples-fbPageModeration/Facebook Permanant Page Access Token.postman_collection.json]()
		- Postman Environment - [samples-fbPageModeration/FB Page Access Token Environment.postman_environment.json]()
	 - Update the Environment Variables:
		 - 
				




