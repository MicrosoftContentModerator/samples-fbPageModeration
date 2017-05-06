    

# Connect a Facebook Page to Content Moderator
This sample shows how you can connect a Facebook Page to Content Moderator workflows.

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

### Create Azure Functions
For this step you will need to login to the [Azure Management Portal](https://portal.azure.com)

 - Add a Function App (refer this [link](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-function-app-portal))
 - Open the newly created function app
 - Navigate to Platform features -> Application Settings
 - Create the following application settings entries (refer this [link](https://docs.microsoft.com/en-us/azure/azure-functions/functions-how-to-use-azure-function-app-settings#settings)):
 
	 App setting|Description
	 ----------|----------
	 cm:TeamId|Your Content Moderator TeamId
	 cm:SubscriptionKey|Your Content Moderator subscription key. Get it on Settings-> Credentials	 
	 cm:Region|Your Content Moderator region
	 cm:ImageWorkflow|Name of the workflow to run on Images
	 cm:TextWorkflow|Name of the workflow to run on Texts
	 cm:CallbackEndpoint|Url for the CMListener Function App that you create later in this guide
	 fb:VerificationToken|This is secret that you put in and the same is used to subscribe to the facebook feed events
	 fb:PageAccessToken|This is a facebook graph api access token that does not expire and allows the function Hide/Delete posts on your behalf.
	 

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
		
	Key|Value
	----------|----------
	appId| Insert your Facebook App Identifier here
	appSecret| Insert your Facebook App's secret here
	short_lived_token| Insert the short lived user access token you generated in the previous step
	
	- Now run the 3 APIs listed in the collection one by one:
		- Select Generate Long-Lived Access Token and Click Send
		- Select Get User ID and Click Send
		- Select Get Permanent Page Access Token and Click Send
	- Copy the "access_token" from the response and set this as the value for app setting "fb:PageAccessToken"
	
Well that was the last step!!

Images and Text posted on your facebook page will now be sent to Content Moderator. Images that don't adhere to your policies would be taken down.
