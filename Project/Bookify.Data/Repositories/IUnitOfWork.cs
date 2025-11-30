using Bookify.Data.Models;
using Bookify.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Bookify.Data.Repositories
{
    public interface IUnitOfWork : IDisposable, IAsyncDisposable
    {
        public IRepository<RoomType> RoomTypes { get; }
        public IRoomRepository Rooms { get; }
        public IBookingRepository Bookings { get; }
        public IRepository<BookingPayment> BookingPayments { get; }
        public IRepository<GalleryImage> GalleryImages { get; }
        public IRepository<BookingStatusHistory> BookingStatusHistory { get; }

        public Task<int> SaveChangesAsync();
        public Task BeginTransactionAsync();
        public Task CommitTransactionAsync();
        public Task RollbackTransactionAsync();
    }
}
