using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;        private IUserRepository? _userRepository;
        private IProductRepository? _productRepository;
        private ICartRepository? _cartRepository;
        private IOrderRepository? _orderRepository;
        private IPaymentRepository? _paymentRepository;
        private IGenericRepository<Admin>? _adminsRepository;
        private IGenericRepository<Customer>? _customersRepository;
        private IDbContextTransaction? _currentTransaction;

        public UnitOfWork(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        #region Custom Repositories
 
        public IUserRepository Users => 
            _userRepository ??= new UserRepository(_context, _userManager, _signInManager);

        public IProductRepository Products =>
            _productRepository ??= new ProductRepository(_context);        public ICartRepository Carts =>
            _cartRepository ??= new CartRepository(_context);
              public IOrderRepository Orders =>
            _orderRepository ??= new OrderRepository(_context);
        
        public IPaymentRepository Payments =>
            _paymentRepository ??= new PaymentRepository(_context);
        
        #endregion

        #region Generic Repositories

        public IGenericRepository<Admin> Admins => 
            _adminsRepository ??= new GenericRepository<Admin>(_context);

        public IGenericRepository<Customer> Customers => 
            _customersRepository ??= new GenericRepository<Customer>(_context); 

        #endregion
        
        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
        
        #region Transaction Methods
        
        public async Task BeginTransactionAsync()
        {
            if (_currentTransaction != null)
            {
                throw new InvalidOperationException("A transaction is already in progress.");
            }
            
            _currentTransaction = await _context.Database.BeginTransactionAsync();
        }
        
        public async Task CommitTransactionAsync()
        {
            if (_currentTransaction == null)
            {
                throw new InvalidOperationException("No transaction in progress.");
            }
            
            try
            {
                await _context.SaveChangesAsync();
                await _currentTransaction.CommitAsync();
            }
            catch
            {
                await RollbackTransactionAsync();
                throw;
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
            }
        }
        
        public async Task RollbackTransactionAsync()
        {
            if (_currentTransaction == null)
            {
                throw new InvalidOperationException("No transaction in progress.");
            }
            
            try
            {
                await _currentTransaction.RollbackAsync();
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.DisposeAsync();
                    _currentTransaction = null;
                }
            }
        }
        
        #endregion
    }
}