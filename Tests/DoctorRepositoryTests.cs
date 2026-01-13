using Xunit;
using Microsoft.EntityFrameworkCore;
using healthapp.Repositories;
using healthapp.Context;
using healthapp.Models;
using healthapp.DTOs.DoctorDTOs;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace healthapp.Tests
{
    public class DoctorRepositoryTests
    {
        private readonly DbContextOptions<PostgresContext> _dbOptions;

        public DoctorRepositoryTests()
        {

            _dbOptions = new DbContextOptionsBuilder<PostgresContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        private DoctorRepository CreateRepository(PostgresContext context)
        {
            return new DoctorRepository(context);
        }



        [Fact]
        public async Task UpdateDoctorScheduleAsync_ShouldReturn200_AndUnpdateClocksAndFee()
        {

            using var context = new PostgresContext(_dbOptions);


            var user = new User
            {
                Id = 1,
                Name = "Dr. Schedule",
                Email = "schedule@doc.com",
                PasswordHash = "hash",
                Role = "doctor",
                Tc = "11111111111"
            };


            var doctor = new Doctor
            {
                Id = 1,
                UserId = 1,
                Speciality = 1,
                Hospital = "Test Hospital",
                ConsultationFee = 500,
                Clocks = "{}"
            };

            context.Users.Add(user);
            context.Doctors.Add(doctor);
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);


            var newClocks = new { Monday = new { start = "09:00", end = "17:00" } };

            var dto = new UpdateDoctorScheduleDto
            {
                Clocks = newClocks,
                ConsultationFee = 750
            };


            var result = await repo.UpdateDoctorScheduleAsync(1, dto);


            Assert.Equal(200, result.StatusCode);
            Assert.Equal("Çalışma saatleri güncellendi", result.Message);

            var updatedDoctor = await context.Doctors.FindAsync(1);
            Assert.Equal(750, updatedDoctor!.ConsultationFee);


            Assert.Contains("Monday", updatedDoctor.Clocks);
            Assert.Contains("09:00", updatedDoctor.Clocks);
        }

        [Fact]
        public async Task UpdateDoctorScheduleAsync_ShouldReturn404_WhenDoctorNotFound()
        {

            using var context = new PostgresContext(_dbOptions);
            var repo = CreateRepository(context);

            var dto = new UpdateDoctorScheduleDto
            {
                Clocks = new { },
                ConsultationFee = 100
            };


            var result = await repo.UpdateDoctorScheduleAsync(99, dto);


            Assert.Equal(404, result.StatusCode);
            Assert.Contains("Doktor bulunamadı", result.Message);
        }



        [Fact]
        public async Task AddHealthHistoryAsync_ShouldReturn200_AndAddRecord()
        {

            using var context = new PostgresContext(_dbOptions);
            var repo = CreateRepository(context);

            var dto = new AddHealthHistoryDto
            {
                PatientId = 101,
                Diagnosis = "Grippal Enfeksiyon",
                Treatment = "İstirahat ve Vitamin",
                Notes = "1 hafta rapor verildi"
            };


            var result = await repo.AddHealthHistoryAsync(dto);


            Assert.Equal(200, result.StatusCode);
            Assert.True(result.Data);

            var historyInDb = await context.HealthHistories.FirstOrDefaultAsync(h => h.PatientId == 101);
            Assert.NotNull(historyInDb);
            Assert.Equal("Grippal Enfeksiyon", historyInDb.Diagnosis);
            Assert.Equal("İstirahat ve Vitamin", historyInDb.Treatment);
            Assert.False(historyInDb.Deleted);
        }



        [Fact]
        public async Task AddUnavailableDateAsync_ShouldReturn200_WhenAddingToEmptyList()
        {

            using var context = new PostgresContext(_dbOptions);

            var user = new User { Id = 2, Name = "Dr. Leave", Email = "leave@doc.com", PasswordHash = "hash", Role = "doctor", Tc = "22222222222" };
            var doctor = new Doctor
            {
                Id = 2,
                UserId = 2,
                Speciality = 2,
                Hospital = "City Hospital",
                UnavailableDates = null
            };

            context.Users.Add(user);
            context.Doctors.Add(doctor);
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);

            var dto = new UnavailableDateDto
            {
                StartDate = DateTime.UtcNow.AddDays(1),
                EndDate = DateTime.UtcNow.AddDays(5),
                Reason = "Yıllık İzin"
            };


            var result = await repo.AddUnavailableDateAsync(2, dto);


            Assert.Equal(200, result.StatusCode);

            var updatedDoctor = await context.Doctors.FindAsync(2);
            Assert.NotNull(updatedDoctor!.UnavailableDates);


        }

        [Fact]
        public async Task AddUnavailableDateAsync_ShouldReturn200_WhenAppendingToExistingList()
        {

            using var context = new PostgresContext(_dbOptions);


            var existingDates = new List<UnavailableDateDto>
            {
                new UnavailableDateDto { StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow, Reason = "Eski İzin" }
            };

            var user = new User { Id = 3, Name = "Dr. Busy", Email = "busy@doc.com", PasswordHash = "hash", Role = "doctor", Tc = "33333333333" };
            var doctor = new Doctor
            {
                Id = 3,
                UserId = 3,
                Speciality = 1,
                Hospital = "General Hospital",
                UnavailableDates = JsonSerializer.Serialize(existingDates)
            };

            context.Users.Add(user);
            context.Doctors.Add(doctor);
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);

            var newDateDto = new UnavailableDateDto
            {
                StartDate = DateTime.UtcNow.AddMonths(1),
                EndDate = DateTime.UtcNow.AddMonths(1).AddDays(2),
                Reason = "Konferans"
            };


            var result = await repo.AddUnavailableDateAsync(3, newDateDto);


            Assert.Equal(200, result.StatusCode);


        }

        [Fact]
        public async Task AddUnavailableDateAsync_ShouldReturn404_WhenDoctorNotFound()
        {

            using var context = new PostgresContext(_dbOptions);
            var repo = CreateRepository(context);

            var dto = new UnavailableDateDto
            {
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(1),
                Reason = "Test"
            };


            var result = await repo.AddUnavailableDateAsync(999, dto);


            Assert.Equal(404, result.StatusCode);
        }
    }
}