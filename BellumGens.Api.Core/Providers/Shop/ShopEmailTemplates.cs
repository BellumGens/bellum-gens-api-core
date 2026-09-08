using BellumGens.Api.Core.Models;
using System;
using System.Linq;
using System.Net;
using System.Text;

namespace BellumGens.Api.Core.Providers
{
    /// <summary>Customer emails for the shop in Bulgarian and English, selected by the order language.</summary>
    public static class ShopEmailTemplates
    {
        public static (string Subject, string Body) OrderConfirmation(ShopOrder order, ShopOptions options)
        {
            bool bg = IsBulgarian(order);
            string subject = bg
                ? $"Поръчка {order.OrderNumber} е платена успешно"
                : $"Order {order.OrderNumber} is confirmed";

            StringBuilder body = new();
            body.Append(Greeting(order, bg));
            body.Append(bg
                ? $"<p>Получихме плащането за поръчка <b>{order.OrderNumber}</b>. Благодарим ви!</p>"
                : $"<p>We received your payment for order <b>{order.OrderNumber}</b>. Thank you!</p>");
            body.Append(order.DeliveryMethod == DeliveryMethod.EventPickup
                ? (bg ? "<p>Поръчката може да бъде получена на място на следващото ни събитие. Ще се свържем с вас за подробности.</p>"
                      : "<p>Your order can be collected at our next event. We will contact you with the details.</p>")
                : (bg ? "<p>Ще ви уведомим по имейл, когато поръчката бъде изпратена с куриер.</p>"
                      : "<p>We will email you again when the order is handed to the courier.</p>"));
            AppendSummary(body, order, bg);
            if (order.DeliveryMethod == DeliveryMethod.Courier)
            {
                body.Append(bg ? "<p><b>Адрес за доставка:</b> " : "<p><b>Shipping address:</b> ");
                body.Append(Encode($"{order.StreetAddress}, {order.PostalCode} {order.City}, {order.Country}".Replace("  ", " ")));
                body.Append("</p>");
            }
            AppendFooter(body, order, options, bg);
            return (subject, body.ToString());
        }

        public static (string Subject, string Body) OrderShipped(ShopOrder order, ShopOptions options)
        {
            bool bg = IsBulgarian(order);
            string subject = bg
                ? $"Поръчка {order.OrderNumber} е изпратена"
                : $"Order {order.OrderNumber} has shipped";

            StringBuilder body = new();
            body.Append(Greeting(order, bg));
            body.Append(bg
                ? $"<p>Вашата поръчка <b>{order.OrderNumber}</b> беше предадена на куриера.</p>"
                : $"<p>Your order <b>{order.OrderNumber}</b> has been handed to the courier.</p>");
            if (!string.IsNullOrWhiteSpace(order.TrackingNumber))
            {
                body.Append(bg
                    ? $"<p>Номер за проследяване: <b>{Encode(order.TrackingNumber)}</b></p>"
                    : $"<p>Tracking number: <b>{Encode(order.TrackingNumber)}</b></p>");
            }
            AppendSummary(body, order, bg);
            AppendFooter(body, order, options, bg);
            return (subject, body.ToString());
        }

        private static string Greeting(ShopOrder order, bool bg)
        {
            string name = Encode($"{order.FirstName} {order.LastName}".Trim());
            return bg ? $"<p>Здравейте {name},</p>" : $"<p>Hello {name},</p>";
        }

        private static void AppendSummary(StringBuilder body, ShopOrder order, bool bg)
        {
            body.Append(bg ? "<p><b>Детайли за поръчката:</b></p><ul>" : "<p><b>Order details:</b></p><ul>");
            foreach (OrderItem item in order.Items.OrderBy(i => i.Id))
            {
                string variant = string.IsNullOrEmpty(item.VariantName) ? "" : $" ({Encode(item.VariantName)})";
                body.Append($"<li>{item.Quantity} × {Encode(item.ProductName)}{variant} — {Money(item.LineTotal, order.Currency)}</li>");
            }
            body.Append("</ul>");
            body.Append(bg ? "<p>Междинна сума: " : "<p>Subtotal: ").Append(Money(order.Subtotal, order.Currency)).Append("</p>");
            if (order.DiscountTotal > 0)
            {
                string promo = string.IsNullOrEmpty(order.PromoCode) ? "" : $" ({Encode(order.PromoCode)})";
                body.Append(bg ? $"<p>Отстъпка{promo}: -" : $"<p>Discount{promo}: -").Append(Money(order.DiscountTotal, order.Currency)).Append("</p>");
            }
            if (order.DeliveryMethod == DeliveryMethod.Courier)
            {
                body.Append(order.ShippingCost > 0
                    ? (bg ? "<p>Доставка: " : "<p>Shipping: ") + Money(order.ShippingCost, order.Currency) + "</p>"
                    : (bg ? "<p>Безплатна доставка</p>" : "<p>Free shipping</p>"));
            }
            body.Append(bg ? "<p><b>Обща сума: " : "<p><b>Total: ").Append(Money(order.Total, order.Currency)).Append("</b></p>");
        }

        private static void AppendFooter(StringBuilder body, ShopOrder order, ShopOptions options, bool bg)
        {
            string lang = bg ? "bg" : "en";
            string url = $"{options.StorefrontUrl?.TrimEnd('/')}/{lang}/shop/order/{order.Id}";
            body.Append($"<p><a href=\"{url}\" target=\"_blank\">{(bg ? "Преглед на поръчката" : "View your order")}</a></p>");
            body.Append(bg ? "<p>Поздрави от екипа на Bellum Gens!</p>" : "<p>Cheers from the Bellum Gens team!</p>");
        }

        private static string Money(decimal amount, string currency)
        {
            return $"{amount:0.00} {currency}";
        }

        private static string Encode(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static bool IsBulgarian(ShopOrder order)
        {
            return string.Equals(order.Language, "bg", StringComparison.OrdinalIgnoreCase);
        }
    }
}
