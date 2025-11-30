using Bookify.Data.Data.Enums;
using Bookify.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Data.Seeding
{
    public static class SeedData
    {
        // Seed RoomTypes
        public static List<RoomType> SeedRoomTypes() => new List<RoomType>
        {
                new RoomType
                {
                    Id = 1,
                    Name = "Single Room",
                    Description = "Comfortable single room with one bed, perfect for solo travelers.",
                    PricePerNight = 2000.00m,
                    MaxOccupancy = 1,
                    ImageUrl = "/images/G1.jpg"


                },
                new RoomType
                {
                    Id = 2,
                    Name = "Double Room",
                    Description = "Spacious double room with two beds, ideal for couples or friends.",
                    PricePerNight = 3500.00m,
                    MaxOccupancy = 2,
                    ImageUrl = "/images/G2.jpg"
                },
                new RoomType
                {
                    Id = 3,
                    Name = "Deluxe Suite",
                    Description = "Luxurious suite with separate living area and premium amenities.",
                    PricePerNight = 10000.00m,
                    MaxOccupancy = 4,
                    ImageUrl = "/images/G3.jpg"

                },
                new RoomType
                {
                    Id = 4,
                    Name = "Family Room",
                    Description = "Large family room with multiple beds, perfect for families.",
                    PricePerNight = 50000.00m,
                    MaxOccupancy = 5,
                    ImageUrl = "/images/G4.jpg"


                },
                new RoomType
                {
                    Id = 5,
                    Name = "Presidential Suite",
                    Description = "Ultra-luxurious presidential suite with all premium features.",
                    PricePerNight = 20000.00m,
                    MaxOccupancy = 6,
                    ImageUrl = "/images/G5.jpg"
                }
        };

        // Seed Rooms(for Testing)
        public static List<Room> SeedRooms() => new List<Room>
        {
            new Room { Id = 1, RoomNumber = "101", RoomTypeId = 1, IsAvailable = true, Notes = "Ground floor, near elevator" },
            new Room { Id = 2, RoomNumber = "102", RoomTypeId = 1, IsAvailable = true, Notes = "Ground floor, quiet area" },
            new Room { Id = 3, RoomNumber = "103", RoomTypeId = 1, IsAvailable = false, Notes = "Under maintenance" },
            new Room { Id = 4, RoomNumber = "201", RoomTypeId = 1, IsAvailable = true, Notes = "Second floor, city view" },
            new Room { Id = 5, RoomNumber = "202", RoomTypeId = 1, IsAvailable = true, Notes = "Second floor, garden view" },

            // Double Rooms
            new Room { Id = 6, RoomNumber = "301", RoomTypeId = 2, IsAvailable = true, Notes = "Third floor, balcony" },
            new Room { Id = 7, RoomNumber = "302", RoomTypeId = 2, IsAvailable = true, Notes = "Third floor, sea view" },
            new Room { Id = 8, RoomNumber = "303", RoomTypeId = 2, IsAvailable = false, Notes = "Currently occupied" },
            new Room { Id = 9, RoomNumber = "304", RoomTypeId = 2, IsAvailable = true, Notes = "Third floor, corner room" },
            new Room { Id = 10, RoomNumber = "305", RoomTypeId = 2, IsAvailable = true, Notes = "Third floor, premium view" },

            // Deluxe Suites
            new Room { Id = 11, RoomNumber = "401", RoomTypeId = 3, IsAvailable = true, Notes = "Fourth floor, luxury suite" },
            new Room { Id = 12, RoomNumber = "402", RoomTypeId = 3, IsAvailable = true, Notes = "Fourth floor, jacuzzi included" },
            new Room { Id = 13, RoomNumber = "403", RoomTypeId = 3, IsAvailable = false, Notes = "Reserved for VIP" },
            new Room { Id = 14, RoomNumber = "501", RoomTypeId = 3, IsAvailable = true, Notes = "Fifth floor, panoramic view" },

            // Family Rooms
            new Room { Id = 15, RoomNumber = "601", RoomTypeId = 4, IsAvailable = true, Notes = "Sixth floor, family friendly" },
            new Room { Id = 16, RoomNumber = "602", RoomTypeId = 4, IsAvailable = true, Notes = "Sixth floor, connecting rooms available" },
            new Room { Id = 17, RoomNumber = "603", RoomTypeId = 4, IsAvailable = false, Notes = "Currently booked" },

            // Presidential Suites
            new Room { Id = 18, RoomNumber = "701", RoomTypeId = 5, IsAvailable = true, Notes = "Seventh floor, presidential suite" },
            new Room { Id = 19, RoomNumber = "702", RoomTypeId = 5, IsAvailable = true, Notes = "Seventh floor, exclusive access" },
            new Room { Id = 20, RoomNumber = "703", RoomTypeId = 5, IsAvailable = false, Notes = "Under renovation" }
            
        };



    }
}
