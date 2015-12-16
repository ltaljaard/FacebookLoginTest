using System;
using Xamarin.Auth;
using Xamarin.Forms.Platform.Android;
using Xamarin.Forms;
using FacebookLoginTest;
using FacebookLoginTest.Droid;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Android.App;

[assembly: ExportRenderer (typeof (LoginPage), typeof (LoginPageRenderer))]

namespace FacebookLoginTest.Droid
{
	public enum EAuthProvider : int
	{
		MicrosoftAccount            = 0,
		Google                      = 1,
		Twitter                     = 2,
		Facebook                    = 3,
		WindowsAzureActiveDirectory = 4,
	}

	public class LoginInfo
	{
		public enum ELoginStatus
		{
			NotSetYet       = 0,
			Success         = 1,
			Error           = 2,
			CancelledByUser = 3,
		}

		public LoginInfo(ELoginStatus? loginStatus, string loginErrorReason, string zumoToken, EAuthProvider? authProvider, string userId,
			string firstName, string lastName, string phoneNumber, string email, string pictureURI)
		{
			LoginStatus = loginStatus;
			LoginErrorReason = loginErrorReason;
			ZumoToken = zumoToken;
			AuthProvider = authProvider;
			UserId = userId;
			FirstName = firstName;
			LastName = lastName;
			Email = email;
			PhoneNumber = phoneNumber;
			PictureURI = pictureURI;
		}

		public LoginInfo() : this(null, null, null, null, null, null, null, null, null, null)
		{
		}

		public ELoginStatus? LoginStatus;
		public string LoginErrorReason;
		public string ZumoToken;
		public EAuthProvider? AuthProvider;
		public string UserId;
		public string FirstName;
		public string LastName;
		public string Email;
		public string PhoneNumber;
		public string PictureURI;
	}


	public class LoginPageRenderer : PageRenderer
	{

		bool done;

		private static EventHandler<AuthenticatorErrorEventArgs> LoginErrorDelegate;
		private static EventHandler<AuthenticatorCompletedEventArgs> LoginCompletedDelegate;

		EAuthProvider AuthProvider { get; set; }

		// Use OnElementChanged() as a substitute for a "constructor" for LoginPageRenderer: if we could simply call LoginPage directly
		// we could have passed the AuthProvider into the constructor as a parameter, but seeing that the LoginPageRenderer takes over
		// for the device-specific rendering, it's not so simple. Luckily OnElementChanged() can be used for this purpose.
		//		protected override void OnElementChanged(VisualElementChangedEventArgs e)
		protected async override void OnElementChanged (ElementChangedEventArgs<Page> e)
		{
			base.OnElementChanged (e);

			if (!done)
			{

				LoginPage page = (LoginPage)e.NewElement;
				AuthProvider = EAuthProvider.Facebook;


				// this is a ViewGroup - so should be able to load an AXML file and FindView<>
				var activity = this.Context as Activity;

				done = true;

				LoginInfo loginInfo = new LoginInfo();

				var auth = new OAuth2Authenticator (
					clientId: "xxxxxxxxxxxxxxx", // This is the App ID from https://developers.facebook.com/apps
					scope: "public_profile,email",
					authorizeUrl: new Uri ("https://m.facebook.com/dialog/oauth/"),
					redirectUrl: new Uri ("https://xxxxxxxxxxxxxxxx.azurewebsites.net/signin-facebook")) // your Azure site
				{	AllowCancel = true,
					ShowUIErrors = false
				};

				// Note: see the following proposed pattern for handling login on iOS and Android respectively: http://www.codeproject.com/Tips/783576/Using-Xamarin-Auth-OAuth-Authenticator-with-async
				// Also see:
				// - https://github.com/saramgsilva/Xamarin.Auth/blob/master/samples/Xamarin.Auth.Sample.Android/MainActivity.cs
				// - https://github.com/EdHubbell/OAuthTwoDemo.XForms/blob/master/Android/Renderers/LoginPageRenderer.cs
				LoginPageRenderer.LoginErrorDelegate =
					(sender, eventArgs) =>
				{
					((OAuth2Authenticator)sender).Error -= LoginPageRenderer.LoginErrorDelegate;

					//..Do some error handling here - this particular case doesn't occur in the error that
					//  this demo app is trying to illustrate - so just ignore this part of the code.

				};

				LoginPageRenderer.LoginCompletedDelegate =
					(sender, eventArgs) =>
				{
					((OAuth2Authenticator)sender).Completed -= LoginPageRenderer.LoginCompletedDelegate;

					if (eventArgs.IsAuthenticated)
					{
						var request = new OAuth2Request ("GET", new Uri ("https://graph.facebook.com/me?fields=id,first_name,last_name,email,picture{url}"), null, eventArgs.Account);

						try
						{	Response result = Task.Run (request.GetResponseAsync).Result;

							//							loginInfo = JObject.Parse (result.GetResponseText ()); // note: the the user's unique "id" from the auth proider (e.g. Facebook ID) is already in result and is transferred to loginInfo here.
							//							loginInfo.Add (new JProperty ("zumoToken", eventArgs.Account.Properties ["access_token"].ToString ()));
							//							loginInfo.Add (new JProperty ("status", "Login_Success"));
							//							loginInfo.Add (new JProperty ("authProvider", AuthProvider.ToString ()));
							JObject response = JObject.Parse (result.GetResponseText ()); // note: the the user's unique "id" from the auth proider (e.g. Facebook ID) is in result
							loginInfo.UserId = response ["id"].ToString ();
							loginInfo.ZumoToken = eventArgs.Account.Properties ["access_token"].ToString ();
							loginInfo.LoginStatus = LoginInfo.ELoginStatus.Success;
							loginInfo.AuthProvider = AuthProvider;
							loginInfo.FirstName = response ["first_name"].ToString ();
							loginInfo.LastName = response ["last_name"].ToString ();
							loginInfo.Email = response ["email"].ToString ();
							//...????????......
							//							loginInfo.PictureURI = response ["picture"]["data"][0].Value.Value;
							loginInfo.PictureURI = response ["picture"]["data"]["url"].ToString();							


						} catch (Exception ex)
						{	
							loginInfo.LoginStatus = LoginInfo.ELoginStatus.Error;
							loginInfo.LoginErrorReason = "Unable to retrieve user details from authorization provider '" + AuthProvider.ToString () + "'";


							//=======> THIS IS THE PROBLEM: UNDER ANDROID LOLLIPOP (API 22) THERE IS NO EXCEPTION THROWN, BUT
							//         UNDER ANDROID MARSHMALLOW (API 23) AN EXCEPTION IS THROWN HERE...............................
							int a = 1; // you can put a breakpoint on this line while testing this sample app...

							throw ex; // just throw the exception again for purposes of this demo app...

						}

					} else
					{	// Auth failed - The only way to get to this branch on Google (?and others?) is to hit the 'Cancel' button.
						loginInfo.LoginStatus = LoginInfo.ELoginStatus.CancelledByUser;
					}

					// Carry on with the post-login app logic...


					//=======> Put another breakpoint here while testing: if this point is reached without the above
					//         exception being thrown then it means the authentication worked as it should. This only
					//         happens under Lollipop (API 22) - under Marshmallow API 23), the above exception occurs...
					int b = 1;


				};

				auth.Completed += LoginPageRenderer.LoginCompletedDelegate;
				auth.Error += LoginPageRenderer.LoginErrorDelegate; // note: there seems to be some kind of bug where (on iOS 8 at least) it goes into an endless loop when the Error handler is called. To fix this, we created a static member variable for the error handler (LoginPageRenderer.LoginErrorDelegate), and then at the top of the error handler we do a "auth.Error -= LoginPageRenderer.LoginErrorDelegate;", and that fixes the problem.

				activity.StartActivity (auth.GetUI (activity));
				done = true;
			}
		}




	}
}


