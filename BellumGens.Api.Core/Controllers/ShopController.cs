using BellumGens.Api.Core;
using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BellumGens.Api.Controllers
{
	/// <summary>Public storefront API: catalog, cart quotes, order creation and order status.</summary>
	public class ShopController : BaseController
	{
		public const string ProductsCacheKey = "shop:products";
		private const string PaymentStartingMessage = "The payment is already being started. Please try again in a moment.";
		private static readonly TimeSpan _productsCacheDuration = TimeSpan.FromMinutes(5);

		private readonly IOrderService _orders;
		private readonly IPaymentProvider _payments;
		private readonly IMemoryCache _cache;
		private readonly ShopOptions _options;
		private readonly IShopSettingsService _settings;

		public ShopController(IOrderService orders,
							  IPaymentProvider payments,
							  IMemoryCache cache,
							  IOptions<ShopOptions> options,
							  IShopSettingsService settings,
							  UserManager<ApplicationUser> userManager,
							  RoleManager<IdentityRole> roleManager,
							  SignInManager<ApplicationUser> signInManager,
							  IEmailService sender,
							  BellumGensDbContext context,
							  ILogger<ShopController> logger)
			: base(userManager, roleManager, signInManager, sender, context, logger)
		{
			_orders = orders;
			_payments = payments;
			_cache = cache;
			_options = options.Value;
			_settings = settings;
		}

		[HttpGet("settings")]
		[AllowAnonymous]
		public async Task<IActionResult> GetSettings()
		{
			ShopSettings settings = await _settings.GetAsync();
			return Ok(new ShopSettingsView
			{
				ShippingCost = settings.ShippingCost,
				FreeShippingThreshold = settings.FreeShippingThreshold,
				MaxQuantityPerLine = settings.MaxQuantityPerLine,
				Currency = _options.Currency
			});
		}

		[HttpGet("products")]
		[AllowAnonymous]
		public async Task<IActionResult> GetProducts(Brand? brand = null, ProductType? type = null)
		{
			IEnumerable<Product> products = await GetActiveProductsAsync();
			if (brand != null)
			{
				products = products.Where(p => p.Brand == brand);
			}
			if (type != null)
			{
				products = products.Where(p => p.Type == type);
			}
			return Ok(products.ToList());
		}

		[HttpGet("products/{slug}")]
		[AllowAnonymous]
		public async Task<IActionResult> GetProduct(string slug)
		{
			List<Product> products = await GetActiveProductsAsync();
			Product product = products.FirstOrDefault(p => string.Equals(p.Slug, slug, StringComparison.OrdinalIgnoreCase));
			return product == null ? NotFound() : Ok(product);
		}

		[HttpGet("promo")]
		[AllowAnonymous]
		public async Task<IActionResult> CheckPromo(string code)
		{
			if (string.IsNullOrWhiteSpace(code))
			{
				return BadRequest("Promo code is required.");
			}
			Promo promo = await _dbContext.PromoCodes.FindAsync(OrderService.NormalizeCode(code));
			if (promo == null || !promo.IsUsable(DateTimeOffset.Now))
			{
				return NotFound();
			}
			return Ok(new PromoView
			{
				Code = promo.Code,
				Discount = promo.Discount,
				Expiration = promo.Expiration,
				MinimumOrderTotal = promo.MinimumOrderTotal,
				Brand = promo.Brand
			});
		}

		[HttpPost("quote")]
		[AllowAnonymous]
		public async Task<IActionResult> Quote(QuoteRequest request)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			return Ok(await _orders.QuoteAsync(request));
		}

		[HttpPost("orders")]
		[AllowAnonymous]
		[EnableRateLimiting(ShopOptions.OrdersRateLimitPolicy)]
		public async Task<IActionResult> CreateOrder(CreateOrderRequest request)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			bool payOnline = request.PaymentMethod != PaymentMethod.CashOnDelivery;
			if (payOnline && !_payments.IsConfigured)
			{
				_logger.LogError("Order refused: the payment provider is not configured.");
				return StatusCode(503, "Payments are temporarily unavailable.");
			}

			if (request.ReplacesOrderId is Guid replacesId && await LoadOrderAsync(replacesId) is ShopOrder previous)
			{
				// Picks up a payment that went through after all, so a paid order is never cancelled as replaced.
				await ReconcileAsync(previous);
			}

			ApplicationUser user = await GetAuthUserOrNull();
			ShopOrder order;
			try
			{
				order = await _orders.CreateOrderAsync(request, user?.Id);
			}
			catch (ShopValidationException e)
			{
				return Conflict(new { problems = e.Problems, quote = e.Quote });
			}

			if (user != null && request.SaveAddress && order.DeliveryMethod == DeliveryMethod.Courier)
			{
				await TrySaveOrderAddressAsync(user.Id, order);
			}

			if (!payOnline)
			{
				// Paid to the courier on delivery: there is no checkout to open, the storefront shows the order instead.
				return Ok(ToResponse(order, null));
			}

			OrderCreatedResponse response = await StartPaymentAsync(order);
			if (response == null)
			{
				await _orders.CancelAsync(order, "The payment could not be started.");
				return StatusCode(502, "The payment could not be started. Please try again.");
			}
			return Ok(response);
		}

		[HttpGet("orders/{id:guid}")]
		[AllowAnonymous]
		public async Task<IActionResult> GetOrder(Guid id)
		{
			ShopOrder order = await LoadOrderAsync(id);
			if (order == null)
			{
				return NotFound();
			}
			order = await ReconcileAsync(order);
			return Ok(OrderStatusView.FromOrder(order, DateTimeOffset.Now));
		}

		[HttpPost("orders/{id:guid}/retry-payment")]
		[AllowAnonymous]
		[EnableRateLimiting(ShopOptions.OrdersRateLimitPolicy)]
		public async Task<IActionResult> RetryPayment(Guid id)
		{
			ShopOrder order = await LoadOrderAsync(id);
			if (order == null)
			{
				return NotFound();
			}
			order = await ReconcileAsync(order);

			if (order.Status != OrderStatus.AwaitingPayment || order.ExpiresOn <= DateTimeOffset.Now)
			{
				return Conflict("This order can no longer be paid.");
			}
			Payment latest = order.Payments.OrderByDescending(p => p.CreatedOn).FirstOrDefault();
			if (latest != null && (latest.Status == PaymentStatus.Pending || latest.Status == PaymentStatus.Authorised))
			{
				// Without a provider order id the attempt is still being opened by a concurrent request.
				return string.IsNullOrEmpty(latest.ProviderOrderId)
					? Conflict(PaymentStartingMessage)
					: Ok(ToResponse(order, latest));
			}
			if (!_payments.IsConfigured)
			{
				return StatusCode(503, "Payments are temporarily unavailable.");
			}

			OrderCreatedResponse response;
			try
			{
				response = await StartPaymentAsync(order);
			}
			catch (DbUpdateConcurrencyException)
			{
				return Conflict(PaymentStartingMessage);
			}
			return response == null
				? StatusCode(502, "The payment could not be started. Please try again.")
				: Ok(response);
		}

		[HttpGet("orders/mine")]
		[Authorize]
		public async Task<IActionResult> MyOrders()
		{
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return Unauthorized();
			}
			List<ShopOrder> orders = await _dbContext.ShopOrders
				.Include(o => o.Items)
				.Include(o => o.Payments)
				.Where(o => o.UserId == user.Id)
				.OrderByDescending(o => o.OrderDate)
				.ToListAsync();
			DateTimeOffset now = DateTimeOffset.Now;
			return Ok(orders.Select(o => OrderStatusView.FromOrder(o, now)).ToList());
		}

		/// <summary>The signed-in customer's saved delivery addresses, the most recently used first.</summary>
		[HttpGet("addresses")]
		[Authorize]
		public async Task<IActionResult> GetAddresses()
		{
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return Unauthorized();
			}
			return Ok(await _dbContext.ShippingAddresses
				.Where(a => a.UserId == user.Id)
				.OrderByDescending(a => a.LastUsedOn)
				.ToListAsync());
		}

		/// <summary>
		/// Saves a delivery address to the signed-in customer's address book. Saving one that is already there only marks
		/// it as the most recently used, so the book holds no duplicates.
		/// </summary>
		[HttpPost("addresses")]
		[Authorize]
		public async Task<IActionResult> SaveAddress(ShippingAddressRequest request)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest(ModelState);
			}
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return Unauthorized();
			}
			ShippingAddress address = await SaveAddressAsync(user.Id, request);
			return address == null
				? Conflict($"You can save up to {ShopOptions.MaxSavedAddresses} addresses. Remove one first.")
				: Ok(address);
		}

		[HttpDelete("addresses/{id:guid}")]
		[Authorize]
		public async Task<IActionResult> DeleteAddress(Guid id)
		{
			ApplicationUser user = await GetAuthUser();
			if (user == null)
			{
				return Unauthorized();
			}
			// Another customer's address is reported as missing, so ids cannot be probed.
			ShippingAddress address = await _dbContext.ShippingAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id);
			if (address == null)
			{
				return NotFound();
			}
			_dbContext.ShippingAddresses.Remove(address);
			await _dbContext.SaveChangesAsync();
			return NoContent();
		}

		/// <summary>Converts a decimal amount to minor units (cents) the way payment providers expect.</summary>
		public static long ToMinorUnits(decimal amount)
		{
			return (long)Math.Round(amount * 100, 0, MidpointRounding.AwayFromZero);
		}

		private async Task<List<Product>> GetActiveProductsAsync()
		{
			if (_cache.TryGetValue(ProductsCacheKey, out List<Product> cached))
			{
				return cached;
			}
			List<Product> products = await _dbContext.Products
				.AsNoTracking()
				.Include(p => p.Variants)
				.Where(p => p.Active)
				.OrderBy(p => p.SortOrder)
				.ThenBy(p => p.Name)
				.ToListAsync();
			foreach (Product product in products)
			{
				product.Variants = product.Variants
					.Where(v => v.Active)
					.OrderBy(v => v.SortOrder)
					.ThenBy(v => v.Name)
					.ToList();
			}
			_cache.Set(ProductsCacheKey, products, _productsCacheDuration);
			return products;
		}

		/// <summary>
		/// Adds the address to the customer's book, or marks the matching saved one as just used. Returns null when the
		/// book is full.
		/// </summary>
		private async Task<ShippingAddress> SaveAddressAsync(string userId, ShippingAddressRequest request)
		{
			DateTimeOffset now = DateTimeOffset.Now;
			string firstName = request.FirstName.Trim();
			string lastName = request.LastName.Trim();
			string phoneNumber = request.PhoneNumber.Trim();
			string city = request.City.Trim();
			string streetAddress = request.StreetAddress.Trim();
			string postalCode = request.PostalCode.Trim();

			List<ShippingAddress> saved = await _dbContext.ShippingAddresses.Where(a => a.UserId == userId).ToListAsync();
			ShippingAddress existing = saved.FirstOrDefault(a =>
				Same(a.FirstName, firstName) && Same(a.LastName, lastName) && Same(a.PhoneNumber, phoneNumber)
				&& Same(a.City, city) && Same(a.StreetAddress, streetAddress) && Same(a.PostalCode, postalCode));
			if (existing != null)
			{
				existing.LastUsedOn = now;
				await _dbContext.SaveChangesAsync();
				return existing;
			}
			if (saved.Count >= ShopOptions.MaxSavedAddresses)
			{
				return null;
			}
			ShippingAddress address = new()
			{
				UserId = userId,
				FirstName = firstName,
				LastName = lastName,
				PhoneNumber = phoneNumber,
				City = city,
				StreetAddress = streetAddress,
				PostalCode = postalCode,
				Country = ShopOptions.DeliveryCountry,
				CreatedOn = now,
				LastUsedOn = now
			};
			_dbContext.ShippingAddresses.Add(address);
			await _dbContext.SaveChangesAsync();
			return address;
		}

		/// <summary>Saving the address is a convenience: the order stands even when it fails or the book is full.</summary>
		private async Task TrySaveOrderAddressAsync(string userId, ShopOrder order)
		{
			try
			{
				ShippingAddress saved = await SaveAddressAsync(userId, new ShippingAddressRequest
				{
					FirstName = order.FirstName,
					LastName = order.LastName,
					PhoneNumber = order.PhoneNumber,
					City = order.City,
					StreetAddress = order.StreetAddress,
					PostalCode = order.PostalCode
				});
				if (saved == null)
				{
					_logger.LogInformation("The address of order {OrderNumber} was not saved: the customer's address book is full.", order.OrderNumber);
				}
			}
			catch (Exception e)
			{
				_logger.LogWarning(e, "Could not save the address of order {OrderNumber}.", order.OrderNumber);
				// Otherwise the failed change would be saved again, and fail again, with the payment that follows.
				foreach (var entry in _dbContext.ChangeTracker.Entries<ShippingAddress>().Where(e => e.State != EntityState.Unchanged).ToList())
				{
					entry.State = EntityState.Detached;
				}
			}
		}

		private static bool Same(string a, string b)
		{
			return string.Equals(a?.Trim() ?? "", b?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);
		}

		private Task<ShopOrder> LoadOrderAsync(Guid id)
		{
			return _dbContext.ShopOrders
				.Include(o => o.Items)
				.Include(o => o.Payments)
				.FirstOrDefaultAsync(o => o.Id == id);
		}

		/// <summary>
		/// Refreshes a pending payment from the provider when the webhook is late, and expires orders past their window.
		/// An expired order with a pending payment is checked with the provider first, and kept while the provider still
		/// reports the payment in progress (up to the grace period), so a payment completing at the last moment is honoured.
		/// Returns the order as currently stored, which is a fresh copy when a webhook updated it concurrently.
		/// </summary>
		private async Task<ShopOrder> ReconcileAsync(ShopOrder order)
		{
			if (order.Status != OrderStatus.AwaitingPayment)
			{
				return order;
			}
			DateTimeOffset now = DateTimeOffset.Now;
			bool expired = order.ExpiresOn <= now;
			Payment latest = order.Payments.OrderByDescending(p => p.CreatedOn).FirstOrDefault();
			bool pending = latest != null && (latest.Status == PaymentStatus.Pending || latest.Status == PaymentStatus.Authorised);
			try
			{
				if (pending && !string.IsNullOrEmpty(latest.ProviderOrderId)
					&& (expired || (now - latest.UpdatedOn).TotalSeconds >= _options.ReconcileAfterSeconds))
				{
					if (!_payments.IsConfigured)
					{
						return order;
					}
					PaymentOrderResult remote;
					try
					{
						remote = await _payments.GetOrderAsync(latest.ProviderOrderId);
					}
					catch (Exception e)
					{
						_logger.LogWarning(e, "Could not reconcile payment {PaymentId} with the provider.", latest.Id);
						return order;
					}
					await _orders.ApplyPaymentStatusAsync(latest, remote.Status, "RECONCILE:" + remote.RawState, null);
					if (_orders.IsPaymentInProgress(order, remote.Status, now))
					{
						return order;
					}
				}
				if (order.Status == OrderStatus.AwaitingPayment && expired)
				{
					await _orders.CancelAsync(order, "Payment window expired.");
				}
				return order;
			}
			catch (DbUpdateConcurrencyException e)
			{
				_logger.LogInformation(e, "Order {OrderNumber} changed while reconciling it; reloading.", order.OrderNumber);
				_dbContext.ChangeTracker.Clear();
				return await LoadOrderAsync(order.Id);
			}
		}

		/// <summary>
		/// Opens a provider checkout for the order. The attempt is recorded, and the order's row version bumped, before the
		/// provider is called: a concurrent retry either fails that claim with <see cref="DbUpdateConcurrencyException"/>
		/// or finds this attempt as the active payment, so one order never gets two open checkouts.
		/// The checkout stays open for the full payment window, so the order's hold is extended to cover it; otherwise a
		/// retry late in the window would leave the customer paying for an order that expires under them.
		/// </summary>
		private async Task<OrderCreatedResponse> StartPaymentAsync(ShopOrder order)
		{
			DateTimeOffset checkoutCloses = DateTimeOffset.Now.AddMinutes(PaymentWindowMinutes);
			if (order.ExpiresOn < checkoutCloses)
			{
				order.ExpiresOn = checkoutCloses;
			}
			Payment payment = new()
			{
				OrderId = order.Id,
				Provider = _payments.Provider,
				Amount = order.Total,
				Currency = order.Currency,
				Status = PaymentStatus.Pending
			};
			order.Payments.Add(payment);
			_dbContext.Entry(order).Property(o => o.ExpiresOn).IsModified = true;
			await _dbContext.SaveChangesAsync();

			PaymentOrderResult result;
			try
			{
				result = await _payments.CreateOrderAsync(BuildPaymentRequest(order));
			}
			catch (Exception e)
			{
				_logger.LogError(e, "The payment provider rejected order {OrderNumber}.", order.OrderNumber);
				payment.Status = PaymentStatus.Failed;
				payment.UpdatedOn = DateTimeOffset.Now;
				await _dbContext.SaveChangesAsync();
				return null;
			}

			payment.ProviderOrderId = result.ProviderOrderId;
			payment.ProviderToken = result.Token;
			payment.CheckoutUrl = result.CheckoutUrl;
			payment.Status = result.Status;
			payment.UpdatedOn = DateTimeOffset.Now;
			await _dbContext.SaveChangesAsync();
			return ToResponse(order, payment);
		}

		/// <summary>
		/// Where Revolut sends the customer after paying: the order page. Revolut refuses the whole order when the redirect
		/// host is localhost or an IP address, so a local storefront (development) gets no redirect: the customer ends on
		/// Revolut's own confirmation page, and the order page picks up the payment when opened.
		/// </summary>
		private string RedirectUrlFor(ShopOrder order, string lang)
		{
			string url = $"{_options.StorefrontUrl?.TrimEnd('/')}/{lang}/shop/order/{order.Id}";
			if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || IsLocalHost(uri))
			{
				_logger.LogWarning("No payment redirect for order {OrderNumber}: Revolut does not accept the storefront URL {StorefrontUrl}.",
					order.OrderNumber, _options.StorefrontUrl);
				return null;
			}
			return url;
		}

		private static bool IsLocalHost(Uri uri)
		{
			return uri.IsLoopback
				|| uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
				|| string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
				|| uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
		}

		private PaymentOrderRequest BuildPaymentRequest(ShopOrder order)
		{
			string lang = OrderService.NormalizeLanguage(order.Language);
			return new PaymentOrderRequest
			{
				AmountMinor = ToMinorUnits(order.Total),
				Currency = order.Currency,
				Description = $"Bellum Gens order {order.OrderNumber}",
				CustomerEmail = order.Email,
				Reference = order.OrderNumber,
				RedirectUrl = RedirectUrlFor(order, lang),
				ExpirePendingAfter = $"PT{PaymentWindowMinutes}M"
			};
		}

		private int PaymentWindowMinutes => Math.Max(1, _options.PaymentExpiryMinutes);

		private static OrderCreatedResponse ToResponse(ShopOrder order, Payment payment)
		{
			return new OrderCreatedResponse
			{
				OrderId = order.Id,
				OrderNumber = order.OrderNumber,
				Total = order.Total,
				Currency = order.Currency,
				CheckoutUrl = payment?.CheckoutUrl,
				PaymentToken = payment?.ProviderToken,
				ExpiresOn = order.ExpiresOn
			};
		}

		private async Task<ApplicationUser> GetAuthUserOrNull()
		{
			if (User?.Identity == null || !User.Identity.IsAuthenticated)
			{
				return null;
			}
			try
			{
				return await GetAuthUser();
			}
			catch (Exception e)
			{
				_logger.LogWarning(e, "Could not resolve the logged in user for a shop order; continuing as guest.");
				return null;
			}
		}
	}
}
