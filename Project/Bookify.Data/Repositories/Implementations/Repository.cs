using Bookify.Data.Data;
using Bookify.Data.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Serilog;
namespace Bookify.Data.Repositories.Implementations
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly AppDbContext _context;
        protected readonly DbSet<T> _dbSet;
        protected readonly ILogger _logger;

        public Repository(AppDbContext Context)
        {
            _context = Context;
            _dbSet = _context.Set<T>();
            _logger = Log.ForContext<Repository<T>>();
        }

        public async Task<T> AddAsync(T entity)
        {
            try
            {
                _logger.Information("Adding new {EntityType} entity", typeof(T).Name);
                await _dbSet.AddAsync(entity);
                _logger.Debug("Successfully added {EntityType} entity", typeof(T).Name);
                return entity;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error adding {EntityType} entity", typeof(T).Name);
                throw;
            }
        }

        public async Task AddRangeAsync(IEnumerable<T> entities) 
        {
            try
            {
                var entitiesList = entities.ToList();
                _logger.Information("Adding {Count} {EntityType} entities in batch", entitiesList.Count, typeof(T).Name);
                await _dbSet.AddRangeAsync(entitiesList);
                _logger.Debug("Successfully added {Count} {EntityType} entities", entitiesList.Count, typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error adding range of {EntityType} entities", typeof(T).Name);
                throw;
            }
        }

        public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {

            try
            {
                _logger.Debug("Checking if any {EntityType} exists with predicate", typeof(T).Name);
                var result = await _dbSet.AnyAsync(predicate);
                _logger.Debug("{EntityType} exists check result: {Result}", typeof(T).Name, result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error checking if any {EntityType} exists with predicate", typeof(T).Name);
                throw;
            }
        }

        public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        {
            try
            {
                _logger.Debug("Counting {EntityType} entities", typeof(T).Name);
                int count;

                if (predicate == null)
                    count = await _dbSet.CountAsync();
                else
                    count = await _dbSet.CountAsync(predicate);

                _logger.Debug("Count of {EntityType} entities: {Count}", typeof(T).Name, count);
                return count;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error counting {EntityType} entities", typeof(T).Name);
                throw;
            }
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            try
            {
                _logger.Debug("Finding {EntityType} entities with predicate", typeof(T).Name);
                var result = await _dbSet.Where(predicate).ToListAsync();
                _logger.Debug("Found {Count} {EntityType} entities matching predicate", result.Count(), typeof(T).Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error finding {EntityType} entities with predicate", typeof(T).Name);
                throw;
            }
        }

        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            try
            {
                _logger.Debug("Getting first or default {EntityType} with predicate", typeof(T).Name);
                var result = await _dbSet.FirstOrDefaultAsync(predicate);

                if (result == null)
                    _logger.Debug("No {EntityType} found matching predicate", typeof(T).Name);
                else
                    _logger.Debug("Successfully retrieved {EntityType}", typeof(T).Name);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting first or default {EntityType} with predicate", typeof(T).Name);
                throw;
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            try
            {
                _logger.Information("Getting all {EntityType} entities", typeof(T).Name);
                var result = await _dbSet.ToListAsync();
                _logger.Debug("Successfully retrieved {Count} {EntityType} entities", result.Count(), typeof(T).Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting all {EntityType} entities", typeof(T).Name);
                throw;
            }
        } 

        public async Task<T?> GetByIdAsync(int id)
        {
            try
            {
                _logger.Information("Getting {EntityType} by ID: {Id}", typeof(T).Name, id);
                var result = await _dbSet.FindAsync(id);

                if (result == null)
                    _logger.Warning("{EntityType} with ID {Id} not found", typeof(T).Name, id);
                else
                    _logger.Debug("Successfully retrieved {EntityType} with ID {Id}", typeof(T).Name, id);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting {EntityType} by ID {Id}", typeof(T).Name, id);
                throw;
            }
        }
        public async Task<T?> GetByIdAsync(string id)
        {
            try
            {
                _logger.Information("Getting {EntityType} by ID: {Id}", typeof(T).Name, id);
                var result = await _dbSet.FindAsync(id);
                if (result == null)
                {
                    _logger.Warning("{EntityType} with ID: {Id} is nto found", typeof(T).Name, id);
                }
                else
                {
                    _logger.Debug("Successfully retrieved {EntityType} with ID {Id}", typeof(T).Name, id);
                }
                return result;

            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error getting {EntityType} by ID {Id}", typeof(T).Name, id);
                throw;
            }
            
        }

        public void Remove(T entity)
        {
            try
            {
                _logger.Information("Removing {EntityType} entity", typeof(T).Name);
                _dbSet.Remove(entity);
                _logger.Debug("Successfully removed {EntityType} entity", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error removing {EntityType} entity", typeof(T).Name);
                throw;
            }
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            try
            {
                var entitiesList = entities.ToList();
                _logger.Information("Removing {Count} {EntityType} entities in batch", entitiesList.Count, typeof(T).Name);
                _dbSet.RemoveRange(entitiesList);
                _logger.Debug("Successfully removed {Count} {EntityType} entities", entitiesList.Count, typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error removing range of {EntityType} entities", typeof(T).Name);
                throw;
            }
        }

        public void Update(T entity)
        {
            try
            {
                _logger.Information("Updating {EntityType} entity", typeof(T).Name);
                _dbSet.Update(entity);
                _logger.Debug("Successfully updated {EntityType} entity", typeof(T).Name);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error updating {EntityType} entity", typeof(T).Name);
                throw;
            }
        }
    }
}
