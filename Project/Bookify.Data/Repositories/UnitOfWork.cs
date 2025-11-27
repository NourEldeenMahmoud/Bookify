using Bookify.Data.Data;
using Bookify.Data.Models;
using Bookify.Data.Repositories.Implementations;
using Bookify.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bookify.Data.Repositories
{
    public class UnitOfWork : IUnitOfWork, IAsyncDisposable
    {
        private readonly AppDbContext _context;
        private IDbContextTransaction? _transaction;

        private IRepository<RoomType>? _roomTypes;
        private IRoomRepository? _rooms;
        private IBookingRepository? _bookings;
        private IRepository<BookingPayment>? _bookingPayments;
        private IRepository<GalleryImage>? _galleryImages;
        private IRepository<BookingStatusHistory>? _bookingStatusHistory;
        
        public UnitOfWork(AppDbContext dbContext)
        {
            _context = dbContext;
        }

        public IRepository<RoomType> RoomTypes =>
                _roomTypes ??= new Repository<RoomType>(_context);

        public IRoomRepository Rooms =>
            _rooms ??= new RoomRepository(_context);

        public IBookingRepository Bookings =>
            _bookings ??= new BookingRepository(_context);

        public IRepository<BookingPayment> BookingPayments =>
            _bookingPayments ??= new Repository<BookingPayment>(_context);

        public IRepository<BookingStatusHistory> BookingStatusHistory =>
            _bookingStatusHistory ??= new Repository<BookingStatusHistory>(_context);

        public IRepository<GalleryImage> GalleryImages =>
            _galleryImages ??= new Repository<GalleryImage>(_context);


        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction == null) return;

            try
            {
                await SaveChangesAsync();
                await _transaction.CommitAsync();
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
            finally
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
