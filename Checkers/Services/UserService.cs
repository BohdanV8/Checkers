using Checkers.Core;
using Checkers.Models;
using MongoDB.Driver;

namespace Checkers.Services
{
    public interface IUserService
    {
        public Task<User> GetUserByEmail(string email);
    }
    public class UserService: IUserService
    {
        private readonly IMongoCollection<User> _users;
        public UserService(AppDbContext context)
        {
            _users = context.Users;
        }
        public async Task<User> GetUserByEmail(string email)
        {
            var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
            return user;
        }
    }
}
