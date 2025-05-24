using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using System;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private IUserRepository? _userRepository;
        private IProductRepository? _productRepository;
        private ICartRepository? _cartRepository;
        private IOrderRepository? _orderRepository;
        private IGenericRepository<Admin>? _adminsRepository;
        private IGenericRepository<Customer>? _customersRepository;

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
            _productRepository ??= new ProductRepository(_context);
              public ICartRepository Carts =>
            _cartRepository ??= new CartRepository(_context);
            
        public IOrderRepository Orders =>
            _orderRepository ??= new OrderRepository(_context);
        
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
    }
}