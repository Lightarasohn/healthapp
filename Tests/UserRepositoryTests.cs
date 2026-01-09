using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using healthapp.Repositories;
using healthapp.Context;
using healthapp.Interfaces;
using healthapp.Models;
using healthapp.DTOs.UserDTOs;
using healthapp.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace healthapp.Tests
{
    public class UserRepositoryTests
    {
        private readonly DbContextOptions<PostgresContext> _dbOptions;
        private readonly Mock<IPasswordService> _mockPasswordService;
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<IEmailService> _mockEmailService;

        public UserRepositoryTests()
        {
            _dbOptions = new DbContextOptionsBuilder<PostgresContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockPasswordService = new Mock<IPasswordService>();
            _mockTokenService = new Mock<ITokenService>();
            _mockConfig = new Mock<IConfiguration>();
            _mockEmailService = new Mock<IEmailService>();

            _mockConfig.Setup(c => c["CLIENT_URL"]).Returns("http://localhost:3000");
        }
        private UserRepository CreateRepository(PostgresContext context)
        {
            return new UserRepository(
                context,
                _mockPasswordService.Object,
                _mockTokenService.Object,
                _mockConfig.Object,
                _mockEmailService.Object
            );
        }

        [Fact]
        public async Task RegisterAsync_ShouldReturn201_WhenValidPatient()
        {
            using var context = new PostgresContext(_dbOptions);
            var repo = CreateRepository(context);

            var dto = new RegisterDto
            {
                Name = "Ahmet Yılmaz",
                Email = "ahmet@test.com",
                Password = "Password123",
                Role = "patient",
                Tc = "10000000146",
                Speciality = null,
                Hospital = null
            };

            _mockPasswordService.Setup(p => p.HashPassword(It.IsAny<string>())).Returns("hashed_password");

            var result = await repo.RegisterAsync(dto, null);

            Assert.Equal(201, result.StatusCode);
            
            var userInDb = await context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            Assert.NotNull(userInDb);
            Assert.Equal("hashed_password", userInDb!.PasswordHash);
            Assert.Equal("patient", userInDb.Role);
            
            _mockEmailService.Verify(e => e.SendEmailAsync(dto.Email, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RegisterAsync_ShouldReturn400_WhenEmailExists()
        {
            // Arrange
            using var context = new PostgresContext(_dbOptions);
            
            // Mevcut kullanıcıyı veritabanına ekle (TÜM ZORUNLU ALANLAR DOLU)
            context.Users.Add(new User 
            { 
                Name = "Existing User", 
                Email = "ahmet@test.com", 
                PasswordHash = "hash", 
                Role = "patient",
                Tc = "11111111111", // Unique constraint olabileceği için farklı bir TC
                IsVerified = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);
            
            // Aynı email ile kayıt denemesi
            var dto = new RegisterDto
            {
                Name = "Ahmet Yılmaz",
                Email = "ahmet@test.com", // Çakışacak Email
                Password = "123",
                Role = "patient",
                Tc = "22222222222"
            };

            // Act
            var result = await repo.RegisterAsync(dto, null);

            // Assert
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("zaten kayıtlı", result.Message);
        }

        [Fact]
        public async Task RegisterAsync_ShouldReturn400_WhenDoctorMissingDocument()
        {
            // Arrange
            using var context = new PostgresContext(_dbOptions);
            var repo = CreateRepository(context);

            var dto = new RegisterDto
            {
                Name = "Dr. Ali",
                Email = "ali@doctor.com",
                Password = "123",
                Role = "doctor",
                Tc = "52606108918",
                Speciality = 1,
                Hospital = "Sehir Hastanesi"
            };

            // Act - documentPath parametresini null gönderiyoruz
            var result = await repo.RegisterAsync(dto, null);

            // Assert
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("belge yüklemesi zorunludur", result.Message);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturn200_WhenCredentialsCorrectAndVerified()
        {
            // Arrange
            using var context = new PostgresContext(_dbOptions);
            
            // Kullanıcı oluştur (TÜM ZORUNLU ALANLAR DOLU)
            var user = new User
            {
                Name = "Login User",
                Email = "login@test.com",
                PasswordHash = "hashed_pw",
                Role = "patient",
                Tc = "44444444444",
                IsVerified = true, // Giriş için verified olmalı
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);
            var dto = new LoginDto 
            { 
                Email = "login@test.com", 
                Password = "plain_pw" 
            };

            // Mock setups
            _mockPasswordService.Setup(p => p.VerifyPassword("plain_pw", "hashed_pw")).Returns(true);
            _mockTokenService.Setup(t => t.GenerateToken(It.IsAny<User>(), true)).Returns("access_token");
            _mockTokenService.Setup(t => t.GenerateToken(It.IsAny<User>(), false)).Returns("refresh_token");

            // Act
            var result = await repo.LoginAsync(dto);

            // Assert
            Assert.Equal(200, result.StatusCode);
            
            var userInDb = await context.Users.FirstAsync();
            Assert.NotNull(userInDb.RefreshTokens);
            Assert.Contains("refresh_token", userInDb.RefreshTokens!);
        }

        [Fact]
        public async Task LoginAsync_ShouldReturn401_WhenNotVerified()
        {
            // Arrange
            using var context = new PostgresContext(_dbOptions);
            
            // Kullanıcı oluştur (Verified False)
            var user = new User
            {
                Name = "Unverified User",
                Email = "unverified@test.com",
                PasswordHash = "hashed_pw",
                Role = "patient",
                Tc = "55555555555",
                IsVerified = false, // Kritik nokta
                CreatedAt = DateTime.UtcNow
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);
            var dto = new LoginDto 
            { 
                Email = "unverified@test.com", 
                Password = "pw" 
            };
            
            _mockPasswordService.Setup(p => p.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

            // Act
            var result = await repo.LoginAsync(dto);

            // Assert
            Assert.Equal(401, result.StatusCode);
            Assert.Contains("doğrulayın", result.Message);
        }

        [Fact]
        public async Task VerifyIdentityAsync_ShouldReturnTrue_WhenTcMatches()
        {
            // Arrange
            using var context = new PostgresContext(_dbOptions);
            
            var targetTc = "52606108918";
            var user = new User 
            { 
                Id = 1, 
                Name = "TC User",
                Email = "tc@test.com",
                PasswordHash = "hash",
                Role = "patient",
                Tc = targetTc, // Eşleşecek TC
                IsVerified = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);

            // Act
            var result = await repo.VerifyIdentityAsync(1, targetTc);

            // Assert
            Assert.Equal(200, result.StatusCode);
            Assert.True(result.Data);
        }

        [Fact]
        public async Task VerifyIdentityAsync_ShouldReturnFalse_WhenTcDoesNotMatch()
        {
            // Arrange
            using var context = new PostgresContext(_dbOptions);
            
            var realTc = "12345678901";
            var wrongTc = "99999999999";

            var user = new User 
            { 
                Id = 1, 
                Name = "TC User",
                Email = "tc@test.com",
                PasswordHash = "hash",
                Role = "patient",
                Tc = realTc, 
                IsVerified = true
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);

            // Act
            var result = await repo.VerifyIdentityAsync(1, wrongTc);

            // Assert
            Assert.Equal(400, result.StatusCode);
            Assert.False(result.Data);
        }
    }
}