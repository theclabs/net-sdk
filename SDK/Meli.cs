using System;
using System.Web;
using System.Net;
using System.IO;
using System.Runtime.Serialization.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Specialized;
using RestSharp;
using System.Collections.Generic;

namespace com.mercadolibre.sdk
{
	public class Meli
	{
		private RestClient client = new RestClient (ApiUrl);
		static private string apiUrl = "https://api.mercadolibre.com";

		static public string ApiUrl {
			get {
				return apiUrl;
			}
			set {
				apiUrl = value;
			}
		}

		public string ClientSecret { get; private set; }

		public int ClientId { get; private set; }

		public string AccessToken { get; private set; }

		public string RefreshToken { get; private set; }

		public Meli (int clientId, string clientSecret)
		{
			this.ClientId = clientId;
			this.ClientSecret = clientSecret;
		}

		public Meli (int clientId, string clientSecret, string accessToken)
		{
			this.ClientId = clientId;
			this.ClientSecret = clientSecret;
			this.AccessToken = accessToken;
		}

		public Meli (int clientId, string clientSecret, string accessToken, string refreshToken)
		{
			this.ClientId = clientId;
			this.ClientSecret = clientSecret;
			this.AccessToken = accessToken;
			this.RefreshToken = refreshToken;
		}

		public string getAuthUrl (string redirectUri)
		{

			return "https://auth.mercadolibre.com.ar/authorization?response_type=code&client_id=" + ClientId + "&redirect_uri=" + HttpUtility.UrlEncode (redirectUri);
		}

		public void authorize (string code, string redirectUri)
		{
			var request = new RestRequest ("/oauth/token?grant_type=authorization_code&client_id={client_id}&client_secret={client_secret}&code={code}&redirect_uri={redirect_uri}", Method.POST);

			request.AddParameter ("client_id", this.ClientId, ParameterType.UrlSegment);
			request.AddParameter ("client_secret", this.ClientSecret, ParameterType.UrlSegment);
			request.AddParameter ("code", code, ParameterType.UrlSegment);
			request.AddParameter ("redirect_uri", redirectUri, ParameterType.UrlSegment);

			request.AddHeader ("Accept", "application/json");

			var response = client.Execute (request);

			if (response.StatusCode.Equals (HttpStatusCode.OK)) {
				var token = JsonConvert.DeserializeAnonymousType (response.Content, new {refresh_token="", access_token = ""});
				this.AccessToken = token.access_token;
				this.RefreshToken = token.refresh_token;
			} else {
				throw new AuthorizationException ();
			}
		}

		public IRestResponse get (string resource)
		{
			return get (resource, new Parameter[0]);
		}

		void refreshToken ()
		{
			var request = new RestRequest ("/oauth/token?grant_type=refresh_token&client_id={client_id}&client_secret={client_secret}&refresh_token={refresh_token}", Method.POST);
			request.AddParameter ("client_id", this.ClientId, ParameterType.UrlSegment);
			request.AddParameter ("client_secret", this.ClientSecret, ParameterType.UrlSegment);
			request.AddParameter ("refresh_token", this.RefreshToken, ParameterType.UrlSegment);

			request.AddHeader ("Accept", "application/json");

			var response = client.Execute (request);

			if (response.StatusCode.Equals (HttpStatusCode.OK)) {
				var token = JsonConvert.DeserializeAnonymousType (response.Content, new {refresh_token="", access_token = ""});
				this.AccessToken = token.access_token;
				this.RefreshToken = token.refresh_token;
			} else {
				throw new AuthorizationException ();
			}
		}

		public IRestResponse get (string resource, params Parameter[] param)
		{
			bool containsAT = false;

			var request = new RestRequest (resource, Method.GET);
			List<string> names = new List<string> ();
			foreach (Parameter p in param) {
				names.Add (p.Name + "={" + p.Name + "}");
				if (p.Name.Equals ("access_token")) {
					containsAT = true;
				}
				p.Type = ParameterType.UrlSegment;
				request.AddParameter (p);
			}

			request.Resource = resource + "?" + string.Join ("&", names);

			request.AddHeader ("Accept", "application/json");

			var response = client.Execute (request);

			if (!string.IsNullOrEmpty (this.RefreshToken) && response.StatusCode == HttpStatusCode.NotFound && containsAT) {
				refreshToken ();

				request = new RestRequest (resource, Method.GET);
				names = new List<string> ();
				foreach (Parameter p in param) {
					if (p.Name.Equals ("access_token")) {
						p.Value = this.AccessToken;
					}
					names.Add (p.Name + "={" + p.Name + "}");
					p.Type = ParameterType.UrlSegment;
					request.AddParameter (p);
				}

				request.Resource = resource + "?" + string.Join ("&", names);

				request.AddHeader ("Accept", "application/json");

				response = client.Execute (request);
			}

			return response;
		}
	}
}