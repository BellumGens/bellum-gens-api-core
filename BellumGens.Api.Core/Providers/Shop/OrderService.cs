using BellumGens.Api.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BellumGens.Api.Core.Providers
{
	public class OrderService : IOrderService
	{
		private readonly BellumGensDbContext _db;
		private readonly ShopOptions _options;
		private readonly IShopSettingsService _settings;
		private readonly IEmailService _email;
		private readonly ILogger<OrderService> _logger;

		public OrderService(BellumGensDbContext db, IOptions<ShopOptions> options, IShopSettingsService settings, IEmailService email, ILogger<OrderService> logger)
		{
			_db = db;
			_options = options.Value;
			_settings = settings;
			_email = email;
			_logger = logger;
		}

		public async Task<QuoteResult> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken = default)
		{
			List<Product> products = await LoadProductsAsync(request.Lines, cancellationToken);
			return await BuildQuoteAsync(request, products, DateTimeOffset.Now, cancellationToken);
		}

		public async Task<ShopOrder> CreateOrderAsync(CreateOrderRequest request, string userId, CancellationToken cancellationToken = default)
		{
			DateTimeOffset now = DateTimeOffset.Now;
			// Released before pricing, so the new order can take the stock and promo use the replaced one held.
			ShopOrder replaced = await ReleaseReplacedOrderAsync(request, cancellationToken);
			List<Product> products = await LoadProductsAsync(request.Lines, cancellationToken);
			QuoteResult quote = await BuildQuoteAsync(request, products, now, cancellationToken);

			// Also fails for a promo the customer typed but that does not apply, otherwise they pay more than they saw.
			if (!quote.IsValid)
			{
				throw ShopValidationException.FromQuote(quote);
			}

			foreach (QuoteLine line in quote.Lines)
			{
				Product product = products.First(p => p.Id == line.ProductId);
				if (line.VariantId != null)
				{
					ProductVariant variant = product.Variants.First(v => v.Id == line.VariantId);
					if (variant.StockQuantity != null)
					{
						variant.StockQuantity -= line.Quantity;
					}
				}
				else if (product.StockQuantity != null)
				{
					product.StockQuantity -= line.Quantity;
				}
			}
			if (products.Any(p => p.StockQuantity < 0 || p.Variants.Any(v => v.StockQuantity < 0)))
			{
				throw new ShopValidationException("Not enough stock to fulfil the order.");
			}
			if (quote.PromoApplied)
			{
				// Reserved like stock and released on cancellation, so unpaid orders cannot push a code past its usage limit.
				Promo promo = await _db.PromoCodes.FindAsync(new object[] { quote.PromoCode }, cancellationToken);
				promo.TimesUsed++;
			}

			bool courier = request.DeliveryMethod == DeliveryMethod.Courier;
			// The courier collects a cash on delivery order, so it is final once placed and never expires unpaid.
			bool cashOnDelivery = courier && request.PaymentMethod == PaymentMethod.CashOnDelivery;
			ShopOrder order = new()
			{
				UserId = userId,
				Email = request.Email.Trim(),
				FirstName = request.FirstName.Trim(),
				LastName = request.LastName.Trim(),
				PhoneNumber = request.PhoneNumber.Trim(),
				// An order picked up at an event has no delivery address.
				City = courier ? request.City?.Trim() : null,
				StreetAddress = courier ? request.StreetAddress?.Trim() : null,
				PostalCode = courier ? request.PostalCode?.Trim() : null,
				Country = string.IsNullOrWhiteSpace(request.Country) ? "BG" : request.Country.Trim().ToUpperInvariant(),
				Language = NormalizeLanguage(request.Language),
				PromoCode = quote.PromoApplied ? quote.PromoCode : null,
				DeliveryMethod = request.DeliveryMethod,
				Subtotal = quote.Subtotal,
				DiscountTotal = quote.DiscountTotal,
				ShippingCost = quote.ShippingCost,
				Total = quote.Total,
				Currency = quote.Currency,
				Status = cashOnDelivery ? OrderStatus.Confirmed : OrderStatus.AwaitingPayment,
				PaymentMethod = cashOnDelivery ? PaymentMethod.CashOnDelivery : PaymentMethod.Revolut,
				CustomerNote = request.CustomerNote?.Trim(),
				OrderDate = now,
				ExpiresOn = now.AddMinutes(Math.Max(1, _options.PaymentExpiryMinutes))
			};
			foreach (QuoteLine line in quote.Lines)
			{
				order.Items.Add(new OrderItem
				{
					ProductId = line.ProductId,
					VariantId = line.VariantId,
					ProductName = line.ProductName,
					VariantName = line.VariantName,
					UnitPrice = line.UnitPrice,
					Quantity = line.Quantity,
					LineTotal = line.LineTotal
				});
			}

			_db.ShopOrders.Add(order);
			try
			{
				await _db.SaveChangesAsync(cancellationToken);
			}
			catch (DbUpdateConcurrencyException)
			{
				throw new ShopValidationException("Stock or the promo code changed while placing the order. Please try again.");
			}
			if (replaced != null)
			{
				replaced.AdminNote = $"Replaced by order {order.OrderNumber}.";
				await _db.SaveChangesAsync(cancellationToken);
			}
			if (cashOnDelivery)
			{
				await TrySendAsync(order, ShopEmailTemplates.OrderConfirmation(order, _options));
			}
			return order;
		}

		public async Task<bool> ApplyPaymentStatusAsync(Payment payment, PaymentStatus status, string eventType, string payload, CancellationToken cancellationToken = default)
		{
			ShopOrder order = payment.Order
				?? await _db.ShopOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == payment.OrderId, cancellationToken)
				?? throw new InvalidOperationException($"Payment {payment.Id} does not belong to an order.");

			DateTimeOffset now = DateTimeOffset.Now;
			bool changed = false;
			bool justPaid = false;

			switch (status)
			{
				case PaymentStatus.Completed:
					if (payment.Status != PaymentStatus.Completed)
					{
						payment.Status = PaymentStatus.Completed;
						changed = true;
					}
					bool reinstated = false;
					if (changed && order.Status == OrderStatus.Cancelled && await TryReserveAsync(order, cancellationToken))
					{
						// The payment landed after the order expired or was cancelled; the stock is still there, so honour it.
						order.AdminNote = "Payment completed after the order was cancelled; order reinstated.";
						reinstated = true;
					}
					if (order.Status == OrderStatus.AwaitingPayment || reinstated)
					{
						order.Status = OrderStatus.Paid;
						order.PaidOn = now;
						justPaid = true;
						changed = true;
					}
					else if (changed)
					{
						_logger.LogWarning("Payment {PaymentId} completed for order {OrderNumber} which is already {Status}. Check for a double charge or refund it.",
							payment.Id, order.OrderNumber, order.Status);
						if (order.Status == OrderStatus.Cancelled)
						{
							// Shown to the admin, who can refund it from the orders grid.
							order.AdminNote = "Payment completed after the order was cancelled and its stock was no longer available. Refund it.";
						}
					}
					break;
				case PaymentStatus.Authorised:
					if (payment.Status == PaymentStatus.Pending)
					{
						payment.Status = PaymentStatus.Authorised;
						changed = true;
					}
					break;
				case PaymentStatus.Failed:
					if (payment.Status is PaymentStatus.Pending or PaymentStatus.Authorised)
					{
						payment.Status = PaymentStatus.Failed;
						changed = true;
					}
					break;
				case PaymentStatus.Cancelled:
					if (payment.Status is PaymentStatus.Pending or PaymentStatus.Authorised or PaymentStatus.Failed)
					{
						payment.Status = PaymentStatus.Cancelled;
						changed = true;
					}
					break;
			}

			if (!string.IsNullOrEmpty(eventType))
			{
				payment.LastEventType = eventType;
			}
			if (payload != null)
			{
				payment.LastPayload = payload;
			}
			payment.UpdatedOn = now;

			// A concurrent webhook, reconcile or cancellation bumps the order's row version, so only one of them commits.
			await _db.SaveChangesAsync(cancellationToken);

			if (justPaid)
			{
				await TrySendAsync(order, ShopEmailTemplates.OrderConfirmation(order, _options));
			}
			return changed;
		}

		public async Task CancelAsync(ShopOrder order, string reason, CancellationToken cancellationToken = default)
		{
			if (order.Status != OrderStatus.AwaitingPayment)
			{
				return;
			}
			await ReleaseReservationsAsync(order, cancellationToken);
			order.Status = OrderStatus.Cancelled;
			order.AdminNote = reason;
			await _db.SaveChangesAsync(cancellationToken);
		}

		public async Task<ShopOrder> UpdateStatusAsync(ShopOrder order, OrderStatusUpdate update, CancellationToken cancellationToken = default)
		{
			DateTimeOffset now = DateTimeOffset.Now;
			if (!string.IsNullOrWhiteSpace(update.TrackingNumber))
			{
				order.TrackingNumber = update.TrackingNumber.Trim();
			}
			if (!string.IsNullOrWhiteSpace(update.Note))
			{
				order.AdminNote = update.Note.Trim();
			}

			bool shipped = false;
			bool cashOnDelivery = order.PaymentMethod == PaymentMethod.CashOnDelivery;
			if (update.Status != order.Status)
			{
				switch ((order.Status, update.Status))
				{
					case (OrderStatus.Paid, OrderStatus.Shipped):
					case (OrderStatus.Confirmed, OrderStatus.Shipped):
						order.Status = OrderStatus.Shipped;
						order.ShippedOn = now;
						shipped = true;
						break;
					case (OrderStatus.Paid, OrderStatus.Delivered):
					case (OrderStatus.Shipped, OrderStatus.Delivered):
					case (OrderStatus.Confirmed, OrderStatus.Delivered):
						order.Status = OrderStatus.Delivered;
						if (cashOnDelivery)
						{
							// The courier collected the payment on delivery.
							order.PaidOn ??= now;
						}
						break;
					case (OrderStatus.AwaitingPayment, OrderStatus.Cancelled):
					case (OrderStatus.Confirmed, OrderStatus.Cancelled):
						await ReleaseReservationsAsync(order, cancellationToken);
						order.Status = OrderStatus.Cancelled;
						break;
					case (OrderStatus.Shipped, OrderStatus.Cancelled) when cashOnDelivery:
						// The customer refused the parcel and the courier returned it unpaid; its items are back in stock.
						await ReleaseReservationsAsync(order, cancellationToken);
						order.Status = OrderStatus.Cancelled;
						break;
					case (_, OrderStatus.Refunded):
						throw new ShopValidationException("Use the refund action to refund an order.");
					default:
						throw new ShopValidationException($"An order cannot go from {order.Status} to {update.Status}.");
				}
			}

			await _db.SaveChangesAsync(cancellationToken);
			if (shipped)
			{
				await TrySendAsync(order, ShopEmailTemplates.OrderShipped(order, _options));
			}
			return order;
		}

		public bool IsPaymentInProgress(ShopOrder order, PaymentStatus status, DateTimeOffset now)
		{
			return status is PaymentStatus.Pending or PaymentStatus.Authorised
				&& now < order.ExpiresOn.AddMinutes(Math.Max(0, _options.PendingPaymentGraceMinutes));
		}

		public async Task MarkRefundedAsync(ShopOrder order, CancellationToken cancellationToken = default)
		{
			order.Status = OrderStatus.Refunded;
			await _db.SaveChangesAsync(cancellationToken);
		}

		public static decimal Round(decimal amount)
		{
			return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
		}

		public static string NormalizeCode(string code)
		{
			return string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
		}

		public static string NormalizeLanguage(string language)
		{
			return language != null && language.StartsWith("bg", StringComparison.OrdinalIgnoreCase) ? "bg" : "en";
		}

		private async Task<List<Product>> LoadProductsAsync(IEnumerable<CartLineRequest> lines, CancellationToken cancellationToken)
		{
			List<Guid> ids = (lines ?? Enumerable.Empty<CartLineRequest>()).Select(l => l.ProductId).Distinct().ToList();
			if (ids.Count == 0)
			{
				return new List<Product>();
			}
			return await _db.Products.Include(p => p.Variants).Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);
		}

		private async Task<QuoteResult> BuildQuoteAsync(QuoteRequest request, List<Product> products, DateTimeOffset now, CancellationToken cancellationToken)
		{
			List<CartLineRequest> lines = request.Lines ?? new List<CartLineRequest>();
			ShopSettings settings = await _settings.GetAsync(cancellationToken);
			QuoteResult result = new()
			{
				Currency = _options.Currency,
				PromoCode = NormalizeCode(request.PromoCode)
			};

			// The same product or variant may appear on several lines; stock is checked against the sum.
			Dictionary<Guid, int> requested = new();
			foreach (CartLineRequest line in lines)
			{
				Guid holder = line.VariantId ?? line.ProductId;
				requested[holder] = requested.GetValueOrDefault(holder) + line.Quantity;
			}

			List<Brand?> lineBrands = new();
			foreach (CartLineRequest line in lines)
			{
				QuoteLine quoteLine = new() { ProductId = line.ProductId, VariantId = line.VariantId, Quantity = line.Quantity };
				result.Lines.Add(quoteLine);

				Product product = products.FirstOrDefault(p => p.Id == line.ProductId);
				lineBrands.Add(product?.Brand);
				if (product == null || !product.Active)
				{
					quoteLine.ProductName = product?.Name;
					quoteLine.Problem = "This product is no longer available.";
					continue;
				}
				quoteLine.ProductName = product.Name;
				quoteLine.ImageUrl = product.ImageUrl;

				ProductVariant variant = null;
				if (line.VariantId != null)
				{
					variant = product.Variants.FirstOrDefault(v => v.Id == line.VariantId);
					if (variant == null || !variant.Active)
					{
						quoteLine.Problem = "The selected option is no longer available.";
						continue;
					}
					quoteLine.VariantName = variant.Name;
				}
				else if (product.Variants.Count > 0)
				{
					quoteLine.Problem = "Please choose an option for this product.";
					continue;
				}

				if (line.Quantity < 1 || line.Quantity > settings.MaxQuantityPerLine)
				{
					quoteLine.Problem = $"Quantity must be between 1 and {settings.MaxQuantityPerLine}.";
					continue;
				}

				int? stock = variant != null ? variant.StockQuantity : product.StockQuantity;
				quoteLine.Available = stock;
				quoteLine.UnitPrice = product.EffectivePrice(variant);
				quoteLine.LineTotal = Round(quoteLine.UnitPrice * line.Quantity);
				if (stock != null && stock < requested[line.VariantId ?? line.ProductId])
				{
					quoteLine.Problem = stock <= 0 ? "Sold out." : $"Only {stock} left in stock.";
				}
			}

			result.Subtotal = Round(result.Lines.Where(l => l.Problem == null).Sum(l => l.LineTotal));

			if (result.PromoCode != null)
			{
				Promo promo = await _db.PromoCodes.FindAsync(new object[] { result.PromoCode }, cancellationToken);
				if (promo == null)
				{
					result.PromoProblem = "Promo code is invalid.";
				}
				else if (!promo.IsUsable(now))
				{
					result.PromoProblem = "Promo code has expired.";
				}
				else if (promo.MinimumOrderTotal != null && result.Subtotal < promo.MinimumOrderTotal)
				{
					result.PromoProblem = $"Promo code requires a minimum order of {promo.MinimumOrderTotal:0.00} {result.Currency}.";
				}
				else
				{
					decimal eligible = 0;
					for (int i = 0; i < result.Lines.Count; i++)
					{
						QuoteLine line = result.Lines[i];
						if (line.Problem == null && (promo.Brand == null || lineBrands[i] == promo.Brand))
						{
							eligible += line.LineTotal;
						}
					}
					if (eligible <= 0)
					{
						result.PromoProblem = "Promo code does not apply to these products.";
					}
					else
					{
						result.DiscountTotal = Round(eligible * promo.Discount);
						result.PromoApplied = true;
					}
				}
			}

			decimal discounted = result.Subtotal - result.DiscountTotal;
			if (request.DeliveryMethod == DeliveryMethod.EventPickup || result.Subtotal <= 0)
			{
				result.ShippingCost = 0;
				result.FreeShipping = false;
			}
			else
			{
				result.FreeShipping = discounted >= settings.FreeShippingThreshold;
				result.ShippingCost = result.FreeShipping ? 0 : settings.ShippingCost;
			}
			result.Total = Round(discounted + result.ShippingCost);
			return result;
		}

		private async Task ReleaseReservationsAsync(ShopOrder order, CancellationToken cancellationToken)
		{
			await EnsureItemsLoadedAsync(order, cancellationToken);
			foreach (OrderItem item in order.Items)
			{
				if (item.VariantId != null)
				{
					ProductVariant variant = await _db.ProductVariants.FindAsync(new object[] { item.VariantId.Value }, cancellationToken);
					if (variant?.StockQuantity != null)
					{
						variant.StockQuantity += item.Quantity;
					}
				}
				else
				{
					Product product = await _db.Products.FindAsync(new object[] { item.ProductId }, cancellationToken);
					if (product?.StockQuantity != null)
					{
						product.StockQuantity += item.Quantity;
					}
				}
			}
			Promo promo = await FindOrderPromoAsync(order, cancellationToken);
			if (promo != null && promo.TimesUsed > 0)
			{
				promo.TimesUsed--;
			}
		}

		/// <summary>
		/// Cancels the unpaid order the new one replaces and releases its stock and promo use. The change is saved together
		/// with the new order, so a new order that fails validation leaves the old one as it was. Only an order still awaiting
		/// payment, with no payment authorised or completed, and placed with the same email is replaced; anything else is
		/// left alone (an order the customer did pay is honoured, an unrelated one expires on its own).
		/// </summary>
		private async Task<ShopOrder> ReleaseReplacedOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
		{
			if (request.ReplacesOrderId == null)
			{
				return null;
			}
			ShopOrder previous = await _db.ShopOrders
				.Include(o => o.Items)
				.Include(o => o.Payments)
				.FirstOrDefaultAsync(o => o.Id == request.ReplacesOrderId, cancellationToken);
			if (previous == null
				|| previous.Status != OrderStatus.AwaitingPayment
				|| !string.Equals(previous.Email?.Trim(), request.Email?.Trim(), StringComparison.OrdinalIgnoreCase)
				|| previous.Payments.Any(p => p.Status is PaymentStatus.Authorised or PaymentStatus.Completed))
			{
				return null;
			}
			await ReleaseReservationsAsync(previous, cancellationToken);
			previous.Status = OrderStatus.Cancelled;
			previous.AdminNote = "Replaced by a new order placed from the same cart.";
			return previous;
		}

		/// <summary>Takes the stock and promo use of a cancelled order again. Changes nothing and returns false when the stock is gone.</summary>
		private async Task<bool> TryReserveAsync(ShopOrder order, CancellationToken cancellationToken)
		{
			await EnsureItemsLoadedAsync(order, cancellationToken);
			Dictionary<ProductVariant, int> variants = new();
			Dictionary<Product, int> products = new();
			foreach (OrderItem item in order.Items)
			{
				if (item.VariantId != null)
				{
					ProductVariant variant = await _db.ProductVariants.FindAsync(new object[] { item.VariantId.Value }, cancellationToken);
					if (variant != null)
					{
						variants[variant] = variants.GetValueOrDefault(variant) + item.Quantity;
					}
				}
				else
				{
					Product product = await _db.Products.FindAsync(new object[] { item.ProductId }, cancellationToken);
					if (product != null)
					{
						products[product] = products.GetValueOrDefault(product) + item.Quantity;
					}
				}
			}
			if (variants.Any(v => v.Key.StockQuantity < v.Value) || products.Any(p => p.Key.StockQuantity < p.Value))
			{
				return false;
			}

			foreach ((ProductVariant variant, int quantity) in variants)
			{
				variant.StockQuantity -= quantity;
			}
			foreach ((Product product, int quantity) in products)
			{
				product.StockQuantity -= quantity;
			}
			// The customer already paid with the discount, so the use counts even past the limit.
			Promo promo = await FindOrderPromoAsync(order, cancellationToken);
			if (promo != null)
			{
				promo.TimesUsed++;
			}
			return true;
		}

		private async Task EnsureItemsLoadedAsync(ShopOrder order, CancellationToken cancellationToken)
		{
			if (!_db.Entry(order).Collection(o => o.Items).IsLoaded)
			{
				await _db.Entry(order).Collection(o => o.Items).LoadAsync(cancellationToken);
			}
		}

		private async Task<Promo> FindOrderPromoAsync(ShopOrder order, CancellationToken cancellationToken)
		{
			return string.IsNullOrEmpty(order.PromoCode)
				? null
				: await _db.PromoCodes.FindAsync(new object[] { order.PromoCode }, cancellationToken);
		}

		private async Task TrySendAsync(ShopOrder order, (string Subject, string Body) email)
		{
			try
			{
				await _email.SendEmailAsync(order.Email, email.Subject, email.Body);
			}
			catch (Exception e)
			{
				_logger.LogError(e, "Could not send the shop email for order {OrderNumber}.", order.OrderNumber);
			}
		}
	}
}
