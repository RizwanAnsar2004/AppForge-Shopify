namespace shopify_saas_Core.Constants;

public sealed record SeedProduct(string Title, string ProductType, string ImageUrl);

public static class SeedProducts
{
    public static readonly IReadOnlyList<SeedProduct> Items = new[]
    {
        new SeedProduct("Medium Bangles — 12 Pcs Set", "Bangles",
            "https://cdn.shopify.com/s/files/1/0667/9606/0977/files/bangles-2-6-medium-12-pcs-bangles-0165-11000080-35102299193649.jpg?v=1772081583"),
        new SeedProduct("Classic Bracelet", "Accessories",
            "https://cdn.shopify.com/s/files/1/0667/9606/0977/files/bracelet-default-title-accessories-0199-11003559-35707831189809.jpg?v=1772081588"),
        new SeedProduct("Small Bangles 0328", "Bangles",
            "https://cdn.shopify.com/s/files/1/0667/9606/0977/products/bangles-2-4-small-bangles-0328-11003856-32105730736433.jpg?v=1772081579"),
        new SeedProduct("Small Bangles 0330", "Bangles",
            "https://cdn.shopify.com/s/files/1/0667/9606/0977/products/bangles-2-4-small-bangles-0330-11004238-32105716482353.jpg?v=1772081580"),
        new SeedProduct("Small Bangles 0333", "Bangles",
            "https://cdn.shopify.com/s/files/1/0667/9606/0977/files/bangles-2-4-small-bangles-0333-11004444-46146739011889.webp?v=1772098865"),
        new SeedProduct("Medium Bangles 0417", "Bangles",
            "https://cdn.shopify.com/s/files/1/0667/9606/0977/products/bangles-2-6-medium-bangles-0417-11006722-32176938942769.jpg?v=1772081580"),
        new SeedProduct("Golden Mint Green Bindiya", "Bindiya",
            "https://cdn.shopify.com/s/files/1/0667/9606/0977/products/bindiya-golden-mint-green-bindiya-0138-11005211-32111234777393.jpg?v=1772081566"),
        new SeedProduct("Champagne Bridal Set", "Bridal Set",
            "https://cdn.shopify.com/s/files/1/0667/9606/0977/files/bridal-set-champagne-bridal-0206-11003579-45369410158897.webp?v=1772098865"),
        new SeedProduct("Emerald Green Bridal Set", "Bridal Set",
            "https://cdn.shopify.com/s/files/1/0667/9606/0977/products/bridal-set-emerald-green-bridal-0247-11007336-32751105966385.jpg?v=1772081552"),
    };
}
