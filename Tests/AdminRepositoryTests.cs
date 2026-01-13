using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using healthapp.Repositories;
using healthapp.Context;
using healthapp.Interfaces;
using healthapp.Models;
using healthapp.DTOs.AdminDTOs;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace healthapp.Tests
{
    public class AdminRepositoryTests
    {
        private readonly DbContextOptions<PostgresContext> _dbOptions;
        private readonly Mock<IPasswordService> _mockPasswordService;

        public AdminRepositoryTests()
        {
            _dbOptions = new DbContextOptionsBuilder<PostgresContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _mockPasswordService = new Mock<IPasswordService>();
        }

        private AdminRepository CreateRepository(PostgresContext context)
        {
            return new AdminRepository(context, _mockPasswordService.Object);
        }



        [Fact]
        public async Task CreateAdminAsync_ShouldReturn201_WhenEmailIsUnique()
        {

            using var context = new PostgresContext(_dbOptions);
            var repo = CreateRepository(context);

            var dto = new CreateAdminDto
            {
                Name = "Admin User",
                Email = "admin@healthapp.com",
                Password = "SecretPassword123"
            };

            _mockPasswordService.Setup(p => p.HashPassword("SecretPassword123")).Returns("hashed_secret");


            var result = await repo.CreateAdminAsync(dto);


            Assert.Equal(201, result.StatusCode);
            Assert.Equal("Admin oluşturuldu", result.Message);

            var adminInDb = await context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            Assert.NotNull(adminInDb);
            Assert.Equal("admin", adminInDb.Role);
            Assert.True(adminInDb.IsVerified);
            Assert.Equal("hashed_secret", adminInDb.PasswordHash);
        }

        [Fact]
        public async Task CreateAdminAsync_ShouldReturn400_WhenEmailAlreadyExists()
        {

            using var context = new PostgresContext(_dbOptions);


            context.Users.Add(new User
            {
                Name = "Existing User",
                Email = "admin@healthapp.com",
                PasswordHash = "hash",
                Role = "patient",
                Tc = "11111111111",
                IsVerified = true
            });
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);
            var dto = new CreateAdminDto
            {
                Name = "New Admin",
                Email = "admin@healthapp.com",
                Password = "123"
            };


            var result = await repo.CreateAdminAsync(dto);


            Assert.Equal(400, result.StatusCode);
            Assert.Contains("Email kayıtlı", result.Message);
        }



        [Fact]
        public async Task UpdateUserRoleAsync_ShouldReturn200_AndAddDoctorRecord_WhenRoleIsDoctor()
        {

            using var context = new PostgresContext(_dbOptions);


            var user = new User
            {
                Id = 1,
                Name = "Test User",
                Email = "test@test.com",
                PasswordHash = "hash",
                Role = "patient",
                Tc = "22222222222"
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);


            var result = await repo.UpdateUserRoleAsync(1, "doctor");


            Assert.Equal(200, result.StatusCode);

            var updatedUser = await context.Users.FindAsync(1);
            Assert.Equal("doctor", updatedUser!.Role);


            var doctorRecord = await context.Doctors.FirstOrDefaultAsync(d => d.UserId == 1);
            Assert.NotNull(doctorRecord);
            Assert.Equal(1, doctorRecord.Speciality);
        }

        [Fact]
        public async Task UpdateUserRoleAsync_ShouldReturn404_WhenUserNotFound()
        {

            using var context = new PostgresContext(_dbOptions);
            var repo = CreateRepository(context);


            var result = await repo.UpdateUserRoleAsync(999, "admin");


            Assert.Equal(404, result.StatusCode);
            Assert.Contains("bulunamadı", result.Message);
        }



        [Fact]
        public async Task ApproveDoctorAsync_ShouldReturn200_AndSetApprovedTrue()
        {

            using var context = new PostgresContext(_dbOptions);

            var user = new User
            {
                Id = 1,
                Name = "Dr. Strange",
                Email = "strange@marvel.com",
                PasswordHash = "hash",
                Role = "doctor",
                Tc = "33333333333",
                IsDoctorApproved = false
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);


            var result = await repo.ApproveDoctorAsync(1);


            Assert.Equal(200, result.StatusCode);

            var updatedUser = await context.Users.FindAsync(1);
            Assert.True(updatedUser!.IsDoctorApproved);
        }

        [Fact]
        public async Task ApproveDoctorAsync_ShouldReturn404_WhenUserNotFound()
        {

            using var context = new PostgresContext(_dbOptions);
            var repo = CreateRepository(context);


            var result = await repo.ApproveDoctorAsync(999);


            Assert.Equal(404, result.StatusCode);
            Assert.Contains("Kullanıcı yok", result.Message);
        }



        [Fact]
        public async Task DeleteUserAsync_ShouldReturn200_AndRemoveUser()
        {

            using var context = new PostgresContext(_dbOptions);

            var user = new User
            {
                Id = 1,
                Name = "Deleted User",
                Email = "del@test.com",
                PasswordHash = "hash",
                Role = "patient",
                Tc = "44444444444"
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);


            var result = await repo.DeleteUserAsync(1);


            Assert.Equal(200, result.StatusCode);
            Assert.True(result.Data);


            var deletedUser = await context.Users.FindAsync(1);
            Assert.Null(deletedUser);
        }

        [Fact]
        public async Task DeleteUserAsync_ShouldReturn404_WhenUserNotFound()
        {

            using var context = new PostgresContext(_dbOptions);
            var repo = CreateRepository(context);

            var result = await repo.DeleteUserAsync(999);


            Assert.Equal(404, result.StatusCode);
            Assert.False(result.Data);
        }
    }
}