﻿namespace OAuthConsumer {
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Net;
	using System.Net.Http;
	using System.ServiceModel;
	using System.ServiceModel.Channels;
	using System.ServiceModel.Security;
	using System.Threading.Tasks;
	using System.Web;
	using System.Web.UI.WebControls;
	using DotNetOpenAuth;
	using DotNetOpenAuth.ApplicationBlock;
	using DotNetOpenAuth.Messaging;
	using DotNetOpenAuth.OAuth;
	using DotNetOpenAuth.OAuth.ChannelElements;
	using OAuthConsumer.SampleServiceProvider;

	/// <summary>
	/// Sample consumer of our Service Provider sample's WCF service.
	/// </summary>
	public partial class SampleWcf : System.Web.UI.Page {
		protected async void Page_Load(object sender, EventArgs e) {
			if (!IsPostBack) {
				if (Session["WcfTokenManager"] != null) {
					WebConsumer consumer = this.CreateConsumer();
					var accessTokenMessage = await consumer.ProcessUserAuthorizationAsync(new HttpRequestWrapper(Request), Response.ClientDisconnectedToken);
					if (accessTokenMessage != null) {
						Session["WcfAccessToken"] = accessTokenMessage.AccessToken;
						this.authorizationLabel.Text = "Authorized!  Access token: " + accessTokenMessage.AccessToken;
					}
				}
			}
		}

		protected async void getAuthorizationButton_Click(object sender, EventArgs e) {
			WebConsumer consumer = this.CreateConsumer();
			UriBuilder callback = new UriBuilder(Request.Url);
			callback.Query = null;
			string[] scopes = (from item in this.scopeList.Items.OfType<ListItem>()
							   where item.Selected
							   select item.Value).ToArray();
			string scope = string.Join("|", scopes);
			var requestParams = new Dictionary<string, string> {
			{ "scope", scope },
		};
			var response = await consumer.PrepareRequestUserAuthorizationAsync(callback.Uri, requestParams, null, Response.ClientDisconnectedToken);
			var responseMessage = await consumer.Channel.PrepareResponseAsync(response, Response.ClientDisconnectedToken);
			await responseMessage.SendAsync(new HttpResponseWrapper(Response), Response.ClientDisconnectedToken);
		}

		protected async void getNameButton_Click(object sender, EventArgs e) {
			try {
				this.nameLabel.Text = await this.CallServiceAsync(client => client.GetName());
			} catch (SecurityAccessDeniedException) {
				this.nameLabel.Text = "Access denied!";
			}
		}

		protected async void getAgeButton_Click(object sender, EventArgs e) {
			try {
				int? age = await this.CallServiceAsync(client => client.GetAge());
				this.ageLabel.Text = age.HasValue ? age.Value.ToString(CultureInfo.CurrentCulture) : "not available";
			} catch (SecurityAccessDeniedException) {
				this.ageLabel.Text = "Access denied!";
			}
		}

		protected async void getFavoriteSites_Click(object sender, EventArgs e) {
			try {
				string[] favoriteSites = await this.CallServiceAsync(client => client.GetFavoriteSites());
				this.favoriteSitesLabel.Text = string.Join(", ", favoriteSites);
			} catch (SecurityAccessDeniedException) {
				this.favoriteSitesLabel.Text = "Access denied!";
			}
		}

		private async Task<T> CallServiceAsync<T>(Func<DataApiClient, T> predicate) {
			DataApiClient client = new DataApiClient();
			var serviceEndpoint = new MessageReceivingEndpoint(client.Endpoint.Address.Uri, HttpDeliveryMethods.AuthorizationHeaderRequest | HttpDeliveryMethods.PostRequest);
			var accessToken = Session["WcfAccessToken"] as string;
			if (accessToken == null) {
				throw new InvalidOperationException("No access token!");
			}

			var httpRequest = new HttpRequestMessage(HttpMethod.Post, client.Endpoint.Address.Uri);
			using (WebConsumer consumer = this.CreateConsumer()) {
				using (var handler = consumer.CreateMessageHandler(accessToken)) {
					handler.ApplyAuthorization(httpRequest);
				}
			}

			HttpRequestMessageProperty httpDetails = new HttpRequestMessageProperty();
			httpDetails.Headers[HttpRequestHeader.Authorization] = httpRequest.Headers.Authorization.ToString();
			using (OperationContextScope scope = new OperationContextScope(client.InnerChannel)) {
				OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = httpDetails;
				return predicate(client);
			}
		}

		private WebConsumer CreateConsumer() {
			string consumerKey = "sampleconsumer";
			string consumerSecret = "samplesecret";
			var tokenManager = Session["WcfTokenManager"] as InMemoryTokenManager;
			if (tokenManager == null) {
				tokenManager = new InMemoryTokenManager(consumerKey, consumerSecret);
				Session["WcfTokenManager"] = tokenManager;
			}
			MessageReceivingEndpoint oauthEndpoint = new MessageReceivingEndpoint(
				new Uri("http://localhost:65169/OAuth.ashx"),
				HttpDeliveryMethods.PostRequest);
			WebConsumer consumer = new WebConsumer(
				new ServiceProviderDescription {
					RequestTokenEndpoint = oauthEndpoint,
					UserAuthorizationEndpoint = oauthEndpoint,
					AccessTokenEndpoint = oauthEndpoint,
					TamperProtectionElements = new DotNetOpenAuth.Messaging.ITamperProtectionChannelBindingElement[] {
					new HmacSha1SigningBindingElement(),
				},
				},
				tokenManager);

			return consumer;
		}
	}
}