using Domain.Repositories.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        // Read
        public async Task<IEnumerable<T>> GetAllAsync()
        {
            var query = _dbSet.AsQueryable();
            if (HasIsDeletedProperty())
            {
                query = query.Where(e => EF.Property<bool>(e, "IsDeleted") == false);
            }
            return await query.ToListAsync();
        }

        public async Task<T> GetByIdAsync(int id)
        {
            var query = _dbSet.AsQueryable();
            if (HasIsDeletedProperty())
            {
                query = query.Where(e => EF.Property<bool>(e, "IsDeleted") == false);
            }
            return await query.FirstOrDefaultAsync(e => EF.Property<int>(e, "Id") == id);
        }

        // Add
        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbSet.AddRangeAsync(entities);
        }

        // Update
        public void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        // Delete
        public void Remove(T entity)
        {
            _dbSet.Remove(entity);
        }

        public void SoftDelete(T entity)
        {
            var propertyInfo = typeof(T).GetProperty("IsDeleted");
            if (propertyInfo != null && propertyInfo.PropertyType == typeof(bool))
            {
                propertyInfo.SetValue(entity, true);
                Update(entity);
            }
        }

        // Helper method to check if entity has IsDeleted property
        private bool HasIsDeletedProperty()
        {
            var propertyInfo = typeof(T).GetProperty("IsDeleted");
            return propertyInfo != null && propertyInfo.PropertyType == typeof(bool);
        }
    }
}