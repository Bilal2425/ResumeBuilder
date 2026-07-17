using BaseApi.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace BaseApi.Data.Contexts
{
    public class UserDbContext : DbContext
    {
        // Constructor accepting DbContextOptions
        public UserDbContext(DbContextOptions<UserDbContext> options)
            : base(options)
        {
        }

        // DbSet for User entities
        public DbSet<User> Users { get; set; }

        // DbSet for PasswordResetToken entities
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    }


}
