using BellumGens.Api.Core.Common;
using BellumGens.Api.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading.Tasks;

namespace BellumGens.Api.Controllers
{
    public class ShopController : BaseController
    {
        private const int baseJerseyPrice = 60;
        private const decimal baseDiscount = .3M;
        public ShopController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, SignInManager<ApplicationUser> signInManager, IEmailSender sender, BellumGensDbContext context, ILogger<ShopController> logger)
            : base(userManager, roleManager, signInManager, sender, context, logger)
        {
        }

        [HttpGet]
        [Route("Promo")]
        public async Task<IActionResult> CheckPromo(string code)
        {
            return Ok(await _dbContext.PromoCodes.FindAsync(code.ToUpperInvariant()));
        }

        [Authorize]
        [Route("Orders")]
        public async Task<IActionResult> GetOrders()
        {
            if (await UserIsInRole("admin"))
            {
                return Ok(await _dbContext.JerseyOrders.Include(o => o.Jerseys).ToListAsync());
            }
            return Unauthorized();
        }

        [Authorize]
        [HttpPut]
        [Route("Edit")]
        public async Task<IActionResult> EditOrder(Guid orderId, JerseyOrder order)
        {
            if (await UserIsInRole("admin"))
            {
                JerseyOrder entity = await _dbContext.JerseyOrders.FindAsync(orderId);
                if (entity == null)
                {
                    return NotFound();
                }

                _dbContext.Entry(order).State = EntityState.Modified;

                try
                {
                    await _dbContext.SaveChangesAsync();
                }
                catch (DbUpdateException e)
                {
                    System.Diagnostics.Trace.TraceError("Order update exception: " + e.Message);
                    return BadRequest("Something went wrong...");
                }

                return Ok(order);
            }
            return Unauthorized();
        }

        [HttpPost]
        [Route("Order")]
        public async Task<IActionResult> SubmitOrder(JerseyOrder order)
        {
            if (ModelState.IsValid)
            {
                if (order.PromoCode != null)
                {
                    order.PromoCode = order.PromoCode.ToUpperInvariant();
                }

                _dbContext.JerseyOrders.Add(order);

                try
                {
                    await _dbContext.SaveChangesAsync();
                }
                catch (DbUpdateException e)
                {
                    System.Diagnostics.Trace.TraceError("Order submit exception: " + e.Message);
                    return BadRequest("Something went wrong...");
                }

                try
                {
                    await _dbContext.Entry(order).Reference(o => o.Promo).LoadAsync();

                    decimal discount = baseDiscount;
                    if (order.Promo != null)
                    {
                        discount += order.Promo.Discount;
                    }

                    StringBuilder builder = new();
                    builder.Append($@"Здравейте {order.FirstName} {order.LastName},
                                <p>Успешно получихме вашата поръчка. Очаквайте обаждане на посоченият от вас телефонен номер за потвърждение!</p>
                                <p>Детайли за вашата поръчка:</p>");
                    foreach (JerseyDetails jersey in order.Jerseys)
                    {
                        builder.Append($"<p>{Util.JerseyCutNames[jersey.Cut]} тениска, размер {Util.JerseySizeNames[jersey.Size]}</p>");
                    }
                    decimal price = order.Jerseys.Count * baseJerseyPrice * (1 - discount);
                    if (price >= 100)
                    {
                        builder.Append($"Безплатна доставка за поръчка над 100лв.! ");
                    }
                    builder.Append($"Обща цена: {price}лв.");
                    builder.Append(@"<p>Поздрави от екипа на Bellum Gens!</p>
                                <a href='https://eb-league.com' target='_blank'>https://eb-league.com</a>");

                    await _sender.SendEmailAsync(order.Email, "Поръчката ви е получена", builder.ToString());
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.TraceError("Order submit exception: " + e.Message);
                }
                return Ok(order);
            }
            return BadRequest("Order couldn't be validated...");
        }

        [HttpDelete]
        [Route("Order")]
        public async Task<IActionResult> DeleteOrder(Guid orderId)
        {
            if (await UserIsInRole("admin"))
            {
                var order = await _dbContext.JerseyOrders.FindAsync(orderId);
                
                if (order != null)
                {
                    _dbContext.JerseyOrders.Remove(order);
                }
                else
                {
                    return NotFound();
                }

                try
                {
                    await _dbContext.SaveChangesAsync();
                }
                catch (DbUpdateException e)
                {
                    System.Diagnostics.Trace.TraceError("Order submit exception: " + e.Message);
                    return BadRequest("Something went wrong...");
                }

                return Ok(orderId);
            }
            return Unauthorized();
        }
    }
}
