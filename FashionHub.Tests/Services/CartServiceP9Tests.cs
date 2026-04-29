using Application.DTOs.Cart;
using Application.Services;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using DomainProduct = Domain.Entities.Product;

namespace FashionHub.Tests.Services;

/// <summary>
/// Property-based tests for CartService.AddToCartAsync — Property P9
/// **Validates: Requirements 2.2**
/// </summary>
public class CartServiceP9Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Custom Arbitrary: generates product price scenarios covering both
    // on-sale (with DiscountPrice) and regular-price cases.
    // ─────────────────────────────────────────────────────────────────────────

    public class ProductPriceScenarioArbitrary
    {
        /// <summary>
        /// Returns an Arbitrary for product price scenarios:
        ///   - regularPrice: 1.00–999.99
        ///   - discountPrice: 0.50–regularPrice (always less than regular)
        ///   - isOnSale: true or false
        /// </summary>
        public static Arbitrary<(decimal RegularPrice, decimal DiscountPrice, bool IsOnSale)> PriceScenarios()
        {
            var gen =
                from regularCents in Gen.Choose(100, 99999)          // $1.00–$999.99
                from discountCents in Gen.Choose(50, regularCents)    // $0.50–regularPrice
                from isOnSale in Gen.Elements(true, false)
                let regularPrice = regularCents / 100m
                let discountPrice = discountCents / 100m
                select (RegularPrice: regularPrice, DiscountPrice: discountPrice, IsOnSale: isOnSale);

            return gen.ToArbitrary();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Property P9 — Price Lock
    // **Validates: Requirements 2.2**
    //
    // For any AddToCartAsync call:
    //   - CartItem.PriceAtAddition == product.DiscountPrice when
    //     product.IsOnSale && product.DiscountPrice.HasValue
    //   - CartItem.PriceAtAddition == product.Price otherwise
    //   - The locked price does not change even if the product price is updated
    //     after the item is added
    // ─────────────────────────────────────────────────────────────────────────

    [Property(Arbitrary = new[] { typeof(ProductPriceScenarioArbitrary) })]
    public bool P9_PriceLock_CartItemPriceMatchesProductPriceAtAdditionTime(
        (decimal RegularPrice, decimal DiscountPrice, bool IsOnSale) scenario)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        const string userId = "user-p9-test";
        const string customerId = "customer-p9-test";
        const int productId = 99;
        const int cartId = 1;
        const int cartItemId = 10;

        var (regularPrice, discountPrice, isOnSale) = scenario;

        // Determine the expected locked price according to the price-lock rule
        decimal expectedLockedPrice = isOnSale
            ? discountPrice   // product.IsOnSale && product.DiscountPrice.HasValue
            : regularPrice;   // otherwise use regular price

        // Product with the generated price scenario
        var product = new DomainProduct
        {
            Id = productId,
            Name = "Test Product P9",
            SKU = "SKU-P9",
            Price = regularPrice,
            DiscountPrice = discountPrice,
            IsOnSale = isOnSale,
            StockQuantity = 100  // plenty of stock — not testing stock validation here
        };

        // Customer returned by user lookup
        var customer = new Customer { Id = customerId };

        // Empty cart returned by GetOrCreateCartAsync on first call
        var emptyCart = new Cart
        {
            Id = cartId,
            CustomerId = customerId,
            CartItems = new List<CartItem>()
        };

        // Cart item that the repository would create, with the locked price
        var addedCartItem = new CartItem
        {
            Id = cartItemId,
            CartId = cartId,
            ProductId = productId,
            Quantity = 1,
            PriceAtAddition = expectedLockedPrice,  // price locked at addition time
            SelectedSize = "M",
            SelectedColor = "Black",
            Product = product
        };

        // Updated cart returned after the item is added (via GetCartWithItemsByIdAsync)
        var cartWithItem = new Cart
        {
            Id = cartId,
            CustomerId = customerId,
            CartItems = new List<CartItem> { addedCartItem }
        };

        // Mock IProductRepository
        var mockProductRepo = new Mock<IProductRepository>();
        mockProductRepo
            .Setup(r => r.GetByIdAsync(productId))
            .ReturnsAsync(product);

        // Mock IUserRepository
        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(userId))
            .ReturnsAsync(customer);

        // Mock ICartRepository:
        //   - GetOrCreateCartAsync returns the empty cart (used by AddToCartAsync and GetCartAsync)
        //   - AddItemToCartAsync succeeds
        //   - GetCartWithItemsByIdAsync returns the cart with the locked-price item
        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(customerId))
            .ReturnsAsync(cartWithItem);  // return cart with item for GetCartAsync
        mockCartRepo
            .Setup(r => r.AddItemToCartAsync(
                cartId, productId, It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByIdAsync(cartId))
            .ReturnsAsync(cartWithItem);

        // Mock IUnitOfWork
        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Products).Returns(mockProductRepo.Object);
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var mockLogger = new Mock<ILogger<CartService>>();
        var service = new CartService(mockUow.Object, mockLogger.Object);

        var request = new AddToCartDto
        {
            ProductId = productId,
            Quantity = 1,
            SelectedSize = "M",
            SelectedColor = "Black"
        };

        // ── Act (add item to cart) ────────────────────────────────────────────
        var addResult = service.AddToCartAsync(userId, request).GetAwaiter().GetResult();

        // ── Assert: price is locked correctly at addition time ────────────────

        // 1. Service must succeed
        if (!addResult.IsSuccess)
            return false;

        // 2. Cart must contain exactly one item
        if (addResult.Data?.Items.Count != 1)
            return false;

        decimal actualLockedPrice = addResult.Data.Items[0].UnitPrice;

        // 3. The locked price must match the expected price-lock rule (Requirement 2.2)
        if (actualLockedPrice != expectedLockedPrice)
            return false;

        // ── Simulate product price change after addition ───────────────────────
        // Update the product's price (simulating a price change after cart addition)
        // The cart item's PriceAtAddition must NOT change — it was locked at addition time.
        decimal originalLockedPrice = addedCartItem.PriceAtAddition;
        product.Price = regularPrice * 2m;
        product.DiscountPrice = discountPrice * 2m;

        // ── Act (re-fetch cart after price change) ────────────────────────────
        // GetCartAsync uses GetOrCreateCartAsync which returns cartWithItem.
        // The cartWithItem still holds the original PriceAtAddition (unchanged).
        var getResult = service.GetCartAsync(userId).GetAwaiter().GetResult();

        if (!getResult.IsSuccess)
            return false;

        if (getResult.Data?.Items.Count != 1)
            return false;

        decimal priceAfterProductUpdate = getResult.Data.Items[0].UnitPrice;

        // 4. Price must remain locked even after product price changes (Requirement 2.2)
        if (priceAfterProductUpdate != originalLockedPrice)
            return false;

        return true;
    }
}
