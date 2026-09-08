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
        private static readonly TimeSpan _productsCacheDuration = TimeSpan.FromMinutes(5);

        private readonly IOrderService _orders;
        private readonly IPaymentProvider _payments;
        private readonly IMemoryCache _cache;
        private readonly ShopOptions _options;

        public ShopController(IOrderService orders,
                              IPaymentProvider payments,
                              IMemoryCache cache,
                              IOptions<ShopOptions> options,
                              UserManager<ApplicationUser> userManager,
                              RoleManager<IdentityRole> roleManager,
                              SignInManager<ApplicationUser> signInManager,
                              EmailServiceProvider sender,
                              BellumGensDbContext context,
                              ILogger<ShopController> logger)
            : base(userManager, roleManager, signInManager, sender, context, logger)
        {
            _orders = orders;
            _payments = payments;
            _cache = cache;
            _options = options.Value;
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
            if (!_payments.IsConfigured)
            {
                _logger.LogError("Order refused: the payment provider is not configured.");
                return StatusCode(503, "Payments are temporarily unavailable.");
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
            await ReconcileAsync(order);
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
            await ReconcileAsync(order);

            if (order.Status != OrderStatus.AwaitingPayment || order.ExpiresOn <= DateTimeOffset.Now)
            {
                return Conflict("This order can no longer be paid.");
            }
            Payment latest = order.Payments.OrderByDescending(p => p.CreatedOn).FirstOrDefault();
            if (latest != null && (latest.Status == PaymentStatus.Pending || latest.Status == PaymentStatus.Authorised))
            {
                return Ok(ToResponse(order, latest));
            }
            if (!_payments.IsConfigured)
            {
                return StatusCode(503, "Payments are temporarily unavailable.");
            }

            OrderCreatedResponse response = await StartPaymentAsync(order);
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

        private Task<ShopOrder> LoadOrderAsync(Guid id)
        {
            return _dbContext.ShopOrders
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        /// <summary>
        /// Refreshes a pending payment from the provider when the webhook is late, and expires orders past their window.
        /// </summary>
        private async Task ReconcileAsync(ShopOrder order)
        {
            if (order.Status != OrderStatus.AwaitingPayment)
            {
                return;
            }
            DateTimeOffset now = DateTimeOffset.Now;
            Payment latest = order.Payments.OrderByDescending(p => p.CreatedOn).FirstOrDefault();
            bool pending = latest != null && (latest.Status == PaymentStatus.Pending || latest.Status == PaymentStatus.Authorised);
            if (pending && _payments.IsConfigured && !string.IsNullOrEmpty(latest.ProviderOrderId)
                && (now - latest.UpdatedOn).TotalSeconds >= _options.ReconcileAfterSeconds)
            {
                try
                {
                    PaymentOrderResult remote = await _payments.GetOrderAsync(latest.ProviderOrderId);
                    await _orders.ApplyPaymentStatusAsync(latest, remote.Status, "RECONCILE:" + remote.RawState, null);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Could not reconcile payment {PaymentId} with the provider.", latest.Id);
                }
            }
            if (order.Status == OrderStatus.AwaitingPayment && order.ExpiresOn <= now)
            {
                await _orders.CancelAsync(order, "Payment window expired.");
            }
        }

        private async Task<OrderCreatedResponse> StartPaymentAsync(ShopOrder order)
        {
            PaymentOrderResult result;
            try
            {
                result = await _payments.CreateOrderAsync(BuildPaymentRequest(order));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "The payment provider rejected order {OrderNumber}.", order.OrderNumber);
                return null;
            }

            Payment payment = new()
            {
                OrderId = order.Id,
                Provider = _payments.Provider,
                ProviderOrderId = result.ProviderOrderId,
                ProviderToken = result.Token,
                CheckoutUrl = result.CheckoutUrl,
                Amount = order.Total,
                Currency = order.Currency,
                Status = result.Status
            };
            order.Payments.Add(payment);
            await _dbContext.SaveChangesAsync();
            return ToResponse(order, payment);
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
                RedirectUrl = $"{_options.StorefrontUrl?.TrimEnd('/')}/{lang}/shop/order/{order.Id}",
                ExpirePendingAfter = $"PT{Math.Max(1, _options.PaymentExpiryMinutes)}M"
            };
        }

        private static OrderCreatedResponse ToResponse(ShopOrder order, Payment payment)
        {
            return new OrderCreatedResponse
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                Total = order.Total,
                Currency = order.Currency,
                CheckoutUrl = payment.CheckoutUrl,
                PaymentToken = payment.ProviderToken,
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
