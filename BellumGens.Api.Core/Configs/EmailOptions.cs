using System.Collections.Generic;

namespace BellumGens.Api.Core
{
	/// <summary>
	/// Bound from the "email" configuration section. Mail is sent through Microsoft Graph as an Entra ID application
	/// (app-only auth, Mail.Send application permission), so no mailbox password is stored.
	/// ClientSecret must come from App Service settings or user-secrets, never from appsettings.json.
	/// Credentials, in order of precedence: ClientSecret, CertificateThumbprint, or (when neither is set) the host's
	/// managed identity as a federated credential on the app registration, which needs no secret at all.
	/// TenantId and ClientId are always required; they are identifiers, not secrets.
	/// </summary>
	public class EmailOptions
	{
		public const string Section = "email";

		/// <summary>
		/// The mailbox the application sends as when no entry in <see cref="DomainSenders"/> matches the request's source
		/// domain, including mail sent outside a request (webhooks, background jobs). Its display name is what recipients see.
		/// </summary>
		public string Sender { get; set; }

		/// <summary>
		/// Site name → sender mailbox. The site name is taken from the host of the request's Origin header (or Referer):
		/// the label before the top-level domain, with hyphens removed. So "ebleague" covers eb-league.com and its
		/// subdomains (www., staging.). Keys have no dots or hyphens, so they are valid App Service setting names on Linux
		/// (email__domainSenders__ebleague).
		/// </summary>
		public Dictionary<string, string> DomainSenders { get; set; } = [];

		/// <summary>Directory (tenant) ID of the Entra ID app registration.</summary>
		public string TenantId { get; set; }

		/// <summary>Application (client) ID of the Entra ID app registration.</summary>
		public string ClientId { get; set; }

		public string ClientSecret { get; set; }

		/// <summary>
		/// Thumbprint of a certificate uploaded to the app registration, looked up in the CurrentUser and LocalMachine
		/// "My" stores (on Windows App Service, list it in the WEBSITE_LOAD_CERTIFICATES app setting).
		/// </summary>
		public string CertificateThumbprint { get; set; }

		/// <summary>
		/// Used when neither ClientSecret nor CertificateThumbprint is set: the client ID of the user-assigned managed identity
		/// that is a federated credential on the app registration. Leave empty to use the host's system-assigned identity.
		/// </summary>
		public string ManagedIdentityClientId { get; set; }

		public bool SaveToSentItems { get; set; } = true;
	}
}
