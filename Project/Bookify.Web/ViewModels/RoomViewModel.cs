using Bookify.Data.Models;

namespace Bookify.Web.ViewModels;

public class RoomViewModel
{
    public int Id { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public string? RoomTypeDescription { get; set; }
    public decimal PricePerNight { get; set; }
    public int MaxOccupancy { get; set; }
    public bool IsAvailable { get; set; }
    
    // List of image URLs from GalleryImages
    public List<string> ImageUrls { get; set; } = new List<string>();
    
    // Fallback image URL from RoomType (used if GalleryImages is empty)
    public string? FallbackImageUrl { get; set; }
    
    // Helper method to get the primary image (first from GalleryImages or fallback)
    public string GetPrimaryImageUrl()
    {
        if (ImageUrls != null && ImageUrls.Any())
        {
            return ImageUrls.First();
        }
        return FallbackImageUrl ?? "/images/G1.jpg";
    }
    
    // Helper method to get all images (GalleryImages + fallback if needed)
    public List<string> GetAllImageUrls()
    {
        var images = new List<string>();
        
        if (ImageUrls != null && ImageUrls.Any())
        {
            images.AddRange(ImageUrls);
        }
        else if (!string.IsNullOrEmpty(FallbackImageUrl))
        {
            images.Add(FallbackImageUrl);
        }
        else
        {
            images.Add("/images/G1.jpg");
        }
        
        return images;
    }
    
    // Static method to create RoomViewModel from Room entity
    public static RoomViewModel FromRoom(Room room)
    {
        var viewModel = new RoomViewModel
        {
            Id = room.Id,
            RoomNumber = room.RoomNumber,
            RoomTypeName = room.RoomType?.Name ?? string.Empty,
            RoomTypeDescription = room.RoomType?.Description,
            PricePerNight = room.RoomType?.PricePerNight ?? 0,
            MaxOccupancy = room.RoomType?.MaxOccupancy ?? 1,
            IsAvailable = room.IsAvailable,
            FallbackImageUrl = room.RoomType?.ImageUrl
        };
        
        // Extract image URLs from GalleryImages
        if (room.GalleryImages != null && room.GalleryImages.Any())
        {
            viewModel.ImageUrls = room.GalleryImages
                .Where(img => !string.IsNullOrEmpty(img.ImageUrl))
                .Select(img => img.ImageUrl)
                .ToList();
        }
        
        return viewModel;
    }
}

