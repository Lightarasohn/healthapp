using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace healthapp.Interfaces
{
    public interface ITokenService
    {
        public string GenerateToken(healthapp.Models.User user, bool isAccessToken = true);
    }
}