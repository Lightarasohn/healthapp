using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using healthapp.Interfaces;

namespace healthapp.Services
{
    public class PasswordService : IPasswordService
    {
        public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);
        public bool VerifyPassword(string password, string hashedPassword) => BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }
}