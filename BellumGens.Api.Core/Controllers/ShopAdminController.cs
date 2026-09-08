using BellumGens.Api.Core.Models;
using BellumGens.Api.Core.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BellumGens.Api.Controllers
{
    /// <summary>Catalog, order, promo and payment administration. Every action requires the admin role.</summary>
    [Authorize(Roles = "admin")]
    public class ShopAdminController : BaseController
    {
        private readonly IStorageService _storage;
        private readonly IOrderService _orders;
        private readonly IPaymentProvider _payments;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;

        public ShopAdminController(IStorageService storage,
                                   IOrderService orders,
                                   IPaymentProvider payments,
                                   IMemoryCache cache,
                                   IConfiguration config,
                                   UserManager<ApplicationUser> userManager,
                                   RoleManager<IdentityRole> roleManager,
                                   SignInManager<ApplicationUser> signInManager,
                                   EmailServiceProvider sender,
                                   BellumGensDbContext context,
                                   ILogger<ShopAdminController> logger)
            : base(userManager, roleManager, signInManager, sender, context, logger)
        {
            _storage = storage;
            _orders = orders;
            _payments = payments;
            _cache = cache;
            _config = config;
        }

        // Products

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts()
        {
            List<Product> products = await _dbContext.Products
                .Include(p => p.Variants)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Name)
                .ToListAsync();
            foreach (Product product in products)
            {
                product.Variants = product.Variants.OrderBy(v => v.SortOrder).ThenBy(v => v.Name).ToList();
            }
            return Ok(products);
        }

        [HttpPost("products")]
        public async Task<IActionResult> CreateProduct(Product product)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            DateTimeOffset now = DateTimeOffset.Now;
            product.Id = Guid.Empty;
            product.RowVersion = null;
            product.Slug = await EnsureUniqueSlugAsync(string.IsNullOrWhiteSpace(product.Slug) ? product.Name : product.Slug, null);
            product.GalleryUrls ??= new List<string>();
            product.CreatedOn = now;
            product.UpdatedOn = now;
            foreach (ProductVariant variant in product.Variants)
            {
                variant.Id = Guid.Empty;
                variant.ProductId = Guid.Empty;
                variant.RowVersion = null;
                variant.Name = string.IsNullOrWhiteSpace(variant.Name) ? VariantName(variant) : variant.Name.Trim();
            }

            _dbContext.Products.Add(product);
            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {
                _logger.LogError(e, "Could not create product {Name}.", product.Name);
                return Conflict("The product could not be saved. Is the slug unique?");
            }
            InvalidateCatalogCache();
            return Ok(product);
        }

        [HttpPut("products/{id:guid}")]
        public async Task<IActionResult> UpdateProduct(Guid id, Product product)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            Product entity = await _dbContext.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id);
            if (entity == null)
            {
                return NotFound();
            }

            entity.Name = product.Name.Trim();
            entity.Slug = await EnsureUniqueSlugAsync(string.IsNullOrWhiteSpace(product.Slug) ? product.Name : product.Slug, id);
            entity.Description = product.Description;
            entity.Type = product.Type;
            entity.Brand = product.Brand;
            entity.Price = product.Price;
            entity.DiscountPercentage = product.DiscountPercentage;
            entity.ImageUrl = product.ImageUrl;
            entity.GalleryUrls = product.GalleryUrls ?? new List<string>();
            entity.StockQuantity = product.StockQuantity;
            entity.Active = product.Active;
            entity.SortOrder = product.SortOrder;
            entity.UpdatedOn = DateTimeOffset.Now;

            List<ProductVariant> incoming = product.Variants?.ToList() ?? new List<ProductVariant>();
            List<Guid> referenced = await _dbContext.OrderItems
                .Where(i => i.VariantId != null && i.Variant.ProductId == id)
                .Select(i => i.VariantId.Value)
                .Distinct()
                .ToListAsync();

            foreach (ProductVariant existing in entity.Variants.ToList())
            {
                ProductVariant match = incoming.FirstOrDefault(v => v.Id == existing.Id);
                if (match == null)
                {
                    // Variants that were ordered stay for history; unordered ones can go.
                    if (referenced.Contains(existing.Id))
                    {
                        existing.Active = false;
                    }
                    else
                    {
                        _dbContext.ProductVariants.Remove(existing);
                    }
                    continue;
                }
                existing.Name = string.IsNullOrWhiteSpace(match.Name) ? VariantName(match) : match.Name.Trim();
                existing.Cut = match.Cut;
                existing.Size = match.Size;
                existing.Sku = match.Sku;
                existing.PriceOverride = match.PriceOverride;
                existing.StockQuantity = match.StockQuantity;
                existing.Active = match.Active;
                existing.SortOrder = match.SortOrder;
            }
            foreach (ProductVariant added in incoming.Where(v => v.Id == Guid.Empty || entity.Variants.All(e => e.Id != v.Id)))
            {
                entity.Variants.Add(new ProductVariant
                {
                    Name = string.IsNullOrWhiteSpace(added.Name) ? VariantName(added) : added.Name.Trim(),
                    Cut = added.Cut,
                    Size = added.Size,
                    Sku = added.Sku,
                    PriceOverride = added.PriceOverride,
                    StockQuantity = added.StockQuantity,
                    Active = added.Active,
                    SortOrder = added.SortOrder
                });
            }

            try
            {
                await _dbContext.SaveChangesAsync();
            }
            catch (DbUpdateException e)
            {
                _logger.LogError(e, "Could not update product {Id}.", id);
                return Conflict("The product could not be saved. Is the slug unique?");
            }
            InvalidateCatalogCache();
            return Ok(entity);
        }

        [HttpDelete("products/{id:guid}")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            Product entity = await _dbContext.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id);
            if (entity == null)
            {
                return NotFound();
            }
            bool ordered = await _dbContext.OrderItems.AnyAsync(i => i.ProductId == id);
            if (ordered)
            {
                entity.Active = false;
                entity.UpdatedOn = DateTimeOffset.Now;
                foreach (ProductVariant variant in entity.Variants)
                {
                    variant.Active = false;
                }
            }
            else
            {
                _dbContext.Products.Remove(entity);
            }
            await _dbContext.SaveChangesAsync();
            InvalidateCatalogCache();
            return Ok(new { id, deactivated = ordered });
        }

        [HttpPost("products/{id:guid}/image")]
        public async Task<IActionResult> UploadProductImage(Guid id, ImageUploadRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            Product entity = await _dbContext.Products.FindAsync(id);
            if (entity == null)
            {
                return NotFound();
            }
            string container = _config["BlobService:ShopContainer"] ?? _config["BlobService:Container"];
            string url;
            try
            {
                url = await _storage.SaveImage(request.Image, $"shop-{id:N}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", container);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not upload the image for product {Id}.", id);
                return BadRequest("The image could not be uploaded.");
            }
            if (string.IsNullOrEmpty(url))
            {
                return BadRequest("The image must be a base64 encoded PNG data URL.");
            }
            entity.ImageUrl = url;
            entity.UpdatedOn = DateTimeOffset.Now;
            await _dbContext.SaveChangesAsync();
            InvalidateCatalogCache();
            return Ok(new { url });
        }

        // Orders

        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders(OrderStatus? status = null, DateTimeOffset? from = null, DateTimeOffset? to = null)
        {
            IQueryable<ShopOrder> query = _dbContext.ShopOrders.Include(o => o.Items).Include(o => o.Payments);
            if (status != null)
            {
                query = query.Where(o => o.Status == status);
            }
            if (from != null)
            {
                query = query.Where(o => o.OrderDate >= from);
            }
            if (to != null)
            {
                query = query.Where(o => o.OrderDate <= to);
            }
            return Ok(await query.OrderByDescending(o => o.OrderDate).Take(1000).ToListAsync());
        }

        [HttpGet("orders/{id:guid}")]
        public async Task<IActionResult> GetOrder(Guid id)
        {
            ShopOrder order = await _dbContext.ShopOrders
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.Id == id);
            return order == null ? NotFound() : Ok(order);
        }

        [HttpPut("orders/{id:guid}/status")]
        public async Task<IActionResult> UpdateOrderStatus(Guid id, OrderStatusUpdate update)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            ShopOrder order = await _dbContext.ShopOrders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }
            try
            {
                await _orders.UpdateStatusAsync(order, update);
            }
            catch (ShopValidationException e)
            {
                return BadRequest(e.Message);
            }
            return Ok(order);
        }

        [HttpPost("orders/{id:guid}/refund")]
        public async Task<IActionResult> RefundOrder(Guid id)
        {
            ShopOrder order = await _dbContext.ShopOrders.Include(o => o.Payments).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }
            if (order.Status is not (OrderStatus.Paid or OrderStatus.Shipped or OrderStatus.Delivered))
            {
                return BadRequest("Only paid orders can be refunded.");
            }
            Payment completed = order.Payments
                .Where(p => p.Status == PaymentStatus.Completed && !string.IsNullOrEmpty(p.ProviderOrderId))
                .OrderByDescending(p => p.UpdatedOn)
                .FirstOrDefault();
            if (completed == null)
            {
                return BadRequest("No completed payment was found for this order.");
            }
            try
            {
                await _payments.RefundAsync(completed.ProviderOrderId, ShopController.ToMinorUnits(completed.Amount), $"Refund for order {order.OrderNumber}");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "The payment provider rejected the refund for order {OrderNumber}.", order.OrderNumber);
                return StatusCode(502, "The payment provider rejected the refund.");
            }
            await _orders.MarkRefundedAsync(order);
            return Ok(order);
        }

        [HttpGet("orders/export.csv")]
        public async Task<IActionResult> ExportOrders(OrderStatus? status = null)
        {
            IQueryable<ShopOrder> query = _dbContext.ShopOrders.Include(o => o.Items);
            if (status != null)
            {
                query = query.Where(o => o.Status == status);
            }
            List<ShopOrder> orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();

            StringBuilder csv = new();
            csv.AppendLine("OrderNumber,OrderDate,Status,FirstName,LastName,Email,Phone,City,Street,PostalCode,Country,Delivery,Items,Total,Currency,TrackingNumber");
            foreach (ShopOrder order in orders)
            {
                string items = string.Join(" | ", order.Items.OrderBy(i => i.Id).Select(i =>
                    $"{i.Quantity}x {i.ProductName}{(string.IsNullOrEmpty(i.VariantName) ? "" : $" ({i.VariantName})")}"));
                csv.AppendLine(string.Join(",",
                    Csv(order.OrderNumber), Csv(order.OrderDate.ToString("yyyy-MM-dd HH:mm")), Csv(order.Status.ToString()),
                    Csv(order.FirstName), Csv(order.LastName), Csv(order.Email), Csv(order.PhoneNumber),
                    Csv(order.City), Csv(order.StreetAddress), Csv(order.PostalCode), Csv(order.Country),
                    Csv(order.DeliveryMethod.ToString()), Csv(items), Csv(order.Total.ToString("0.00")), Csv(order.Currency),
                    Csv(order.TrackingNumber)));
            }
            byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
            return File(bytes, "text/csv", $"orders-{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // Promo codes

        [HttpGet("promos")]
        public async Task<IActionResult> GetPromos()
        {
            return Ok(await _dbContext.PromoCodes.OrderBy(p => p.Code).ToListAsync());
        }

        [HttpPost("promos")]
        public async Task<IActionResult> CreatePromo(Promo promo)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            promo.Code = OrderService.NormalizeCode(promo.Code);
            if (promo.Code == null)
            {
                return BadRequest("A promo code is required.");
            }
            if (await _dbContext.PromoCodes.AnyAsync(p => p.Code == promo.Code))
            {
                return Conflict("This promo code already exists.");
            }
            promo.TimesUsed = 0;
            _dbContext.PromoCodes.Add(promo);
            await _dbContext.SaveChangesAsync();
            return Ok(promo);
        }

        [HttpPut("promos/{code}")]
        public async Task<IActionResult> UpdatePromo(string code, Promo promo)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            Promo entity = await _dbContext.PromoCodes.FindAsync(OrderService.NormalizeCode(code));
            if (entity == null)
            {
                return NotFound();
            }
            entity.Discount = promo.Discount;
            entity.Expiration = promo.Expiration;
            entity.Active = promo.Active;
            entity.UsageLimit = promo.UsageLimit;
            entity.MinimumOrderTotal = promo.MinimumOrderTotal;
            entity.Brand = promo.Brand;
            await _dbContext.SaveChangesAsync();
            return Ok(entity);
        }

        [HttpDelete("promos/{code}")]
        public async Task<IActionResult> DeletePromo(string code)
        {
            Promo entity = await _dbContext.PromoCodes.FindAsync(OrderService.NormalizeCode(code));
            if (entity == null)
            {
                return NotFound();
            }
            bool used = await _dbContext.ShopOrders.AnyAsync(o => o.PromoCode == entity.Code);
            if (used)
            {
                entity.Active = false;
            }
            else
            {
                _dbContext.PromoCodes.Remove(entity);
            }
            await _dbContext.SaveChangesAsync();
            return Ok(new { code = entity.Code, deactivated = used });
        }

        // Payments

        /// <summary>
        /// Registers the webhook URL with the payment provider. The returned signing secret must be stored as
        /// revolut:webhookSigningSecret in the App Service configuration; it is not shown again.
        /// </summary>
        [HttpPost("payments/webhook")]
        public async Task<IActionResult> RegisterWebhook(WebhookRegistrationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (!_payments.IsConfigured)
            {
                return StatusCode(503, "The payment provider is not configured.");
            }
            try
            {
                return Ok(await _payments.RegisterWebhookAsync(request.Url));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Webhook registration with {Provider} failed.", _payments.Provider);
                return StatusCode(502, "The payment provider rejected the webhook registration.");
            }
        }

        // Helpers

        public static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Guid.NewGuid().ToString("N")[..8];
            }
            StringBuilder slug = new();
            bool pendingDash = false;
            foreach (char c in value.Trim().ToLowerInvariant())
            {
                if (c < 128 && char.IsLetterOrDigit(c))
                {
                    if (pendingDash && slug.Length > 0)
                    {
                        slug.Append('-');
                    }
                    slug.Append(c);
                    pendingDash = false;
                }
                else
                {
                    pendingDash = true;
                }
            }
            return slug.Length == 0 ? Guid.NewGuid().ToString("N")[..8] : slug.ToString();
        }

        public static string VariantName(ProductVariant variant)
        {
            List<string> parts = new();
            if (variant.Cut != null)
            {
                parts.Add(variant.Cut.ToString());
            }
            if (variant.Size != null)
            {
                parts.Add(variant.Size.ToString());
            }
            return parts.Count == 0 ? "Default" : string.Join(" / ", parts);
        }

        private async Task<string> EnsureUniqueSlugAsync(string desired, Guid? excludeId)
        {
            string baseSlug = Slugify(desired);
            string candidate = baseSlug;
            int suffix = 2;
            while (await _dbContext.Products.AnyAsync(p => p.Slug == candidate && p.Id != excludeId))
            {
                candidate = $"{baseSlug}-{suffix++}";
            }
            return candidate;
        }

        private void InvalidateCatalogCache()
        {
            _cache.Remove(ShopController.ProductsCacheKey);
        }

        private static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            return value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
