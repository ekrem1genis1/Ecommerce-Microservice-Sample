using Microsoft.AspNetCore.Mvc;

namespace Ecommerce.Services.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class CheckoutController : ControllerBase
    {
        private readonly IShoppingCartService _cartService;
        private readonly IPaymentProcessor _paymentProcessor;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            IShoppingCartService cartService,
            IPaymentProcessor paymentProcessor,
            ILogger<CheckoutController> logger)
        {
            _cartService = cartService;
            _paymentProcessor = paymentProcessor;
            _logger = logger;
        }

        // Sepetteki ürünleri doğrulayıp ödeme sürecini başlatan metot
        [HttpPost("complete")]
        public async Task<IActionResult> CompleteCheckout([FromBody] Guid cartId)
        {
            // 1. Sepet kontrolü ve ürünlerin güncelliği
            var cart = await _cartService.GetCartAsync(cartId);
            if (cart == null || !cart.Items.Any())
                return BadRequest("Sepet boş veya geçersiz.");

            _logger.LogInformation("{CartId} için ödeme işlemi başlatıldı.", cartId);

            // 2. Ödeme servisi ile dış entegrasyon (SOLID: Dependency Inversion)
            var paymentResult = await _paymentProcessor.ProcessAsync(cart.TotalAmount);

            if (!paymentResult.IsSuccess)
            {
                _logger.LogWarning("Ödeme başarısız: {Reason}", paymentResult.ErrorMessage);
                return UnprocessableEntity(paymentResult.ErrorMessage);
            }

            // 3. Başarılı işlem sonrası sepeti temizle ve yanıt dön
            await _cartService.ClearCartAsync(cartId);
            return Ok(new { TransactionId = paymentResult.TransactionId, Status = "Tamamlandı" });
        }
    }

    // Sepet yönetimi için iş mantığı arayüzü
    public interface IShoppingCartService
    {
        Task<CartDto> GetCartAsync(Guid cartId);
        Task ClearCartAsync(Guid cartId);
    }

    // Farklı ödeme sistemlerine (Iyzico, Stripe vb.) kolayca adapte edilebilir yapı
    public interface IPaymentProcessor
    {
        Task<PaymentResult> ProcessAsync(decimal amount);
    }

    // Veri modelleri ve DTO'lar
    public record CartDto(Guid Id, List<CartItem> Items, decimal TotalAmount);
    public record CartItem(int ProductId, string Name, int Quantity, decimal Price);
    public record PaymentResult(bool IsSuccess, string TransactionId, string ErrorMessage = null);
}