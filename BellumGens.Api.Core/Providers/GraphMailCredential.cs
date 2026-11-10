using System;
using System.Security.Cryptography.X509Certificates;
using Azure.Core;
using Azure.Identity;

namespace BellumGens.Api.Core.Providers
{
	/// <summary>
	/// Singleton holder for the Entra ID credential used to send mail through Microsoft Graph. Azure.Identity caches the
	/// access token inside the credential instance, so it must outlive the transient typed HttpClient that uses it.
	/// </summary>
	public sealed class GraphMailCredential
	{
		/// <summary>Audience of the managed identity token that Entra ID accepts for a federated credential (public cloud).</summary>
		internal const string TokenExchangeScope = "api://AzureADTokenExchange/.default";

		public GraphMailCredential(TokenCredential credential)
		{
			Credential = credential;
		}

		/// <summary>Null when the "email" section does not configure an app registration.</summary>
		public TokenCredential Credential { get; }

		public static GraphMailCredential FromOptions(EmailOptions options)
		{
			if (string.IsNullOrWhiteSpace(options.TenantId) || string.IsNullOrWhiteSpace(options.ClientId))
			{
				return new GraphMailCredential(null);
			}

			if (!string.IsNullOrWhiteSpace(options.ClientSecret))
			{
				return new GraphMailCredential(new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret));
			}

			if (!string.IsNullOrWhiteSpace(options.CertificateThumbprint))
			{
				X509Certificate2 certificate = FindCertificate(options.CertificateThumbprint)
					?? throw new InvalidOperationException($"Email certificate with thumbprint {options.CertificateThumbprint} was not found in the CurrentUser or LocalMachine certificate store.");
				return new GraphMailCredential(new ClientCertificateCredential(options.TenantId, options.ClientId, certificate));
			}

			return new GraphMailCredential(FederatedManagedIdentityCredential(options));
		}

		/// <summary>
		/// Workload identity federation: the host's managed identity is a federated credential on the app registration,
		/// so its token for the token-exchange audience is accepted as the client assertion in place of a secret.
		/// </summary>
		private static ClientAssertionCredential FederatedManagedIdentityCredential(EmailOptions options)
		{
			ManagedIdentityCredential managedIdentity = new(string.IsNullOrWhiteSpace(options.ManagedIdentityClientId)
				? ManagedIdentityId.SystemAssigned
				: ManagedIdentityId.FromUserAssignedClientId(options.ManagedIdentityClientId));
			TokenRequestContext exchangeAudience = new([TokenExchangeScope]);

			return new ClientAssertionCredential(options.TenantId, options.ClientId,
				async cancellationToken => (await managedIdentity.GetTokenAsync(exchangeAudience, cancellationToken).ConfigureAwait(false)).Token);
		}

		private static X509Certificate2 FindCertificate(string thumbprint)
		{
			foreach (StoreLocation location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
			{
				using X509Store store = new(StoreName.My, location);
				store.Open(OpenFlags.ReadOnly);
				X509Certificate2Collection found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint.Replace(" ", string.Empty), validOnly: false);
				if (found.Count > 0)
				{
					return found[0];
				}
			}
			return null;
		}
	}
}
