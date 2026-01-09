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
            // Her test için izole bir InMemory veritabanı
            _dbOptions = new DbContextOptionsBuilder<PostgresContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
        }

        private DoctorRepository CreateRepository(PostgresContext context)
        {
            return new DoctorRepository(context);
        }

        // --- UpdateDoctorScheduleAsync Testleri ---

        [Fact]
        public async Task UpdateDoctorScheduleAsync_ShouldReturn200_AndUnpdateClocksAndFee()
        {
            // Arrange
            using var context = new PostgresContext(_dbOptions);
            
            // 1. Kullanıcıyı oluştur (Zorunlu alanlar dolu)
            var user = new User
            {
                Id = 1,
                Name = "Dr. Schedule",
                Email = "schedule@doc.com",
                PasswordHash = "hash",
                Role = "doctor",
                Tc = "11111111111"
            };
            
            // 2. Doktor profilini oluştur
            var doctor = new Doctor
            {
                Id = 1,
                UserId = 1,
                Speciality = 1,
                Hospital = "Test Hospital",
                ConsultationFee = 500,
                Clocks = "{}" // Başlangıçta boş JSON
            };

            context.Users.Add(user);
            context.Doctors.Add(doctor);
            await context.SaveChangesAsync();

            var repo = CreateRepository(context);

            // Yeni saat verisi (Anonymous Object olarak gönderiyoruz, repo serialize edecek)
            var newClocks = new { Monday = new { start = "09:00", end = "17:00" } };
            
            var dto = new UpdateDoctorScheduleDto
            {
                Clocks = newClocks,
                ConsultationFee = 750 // Ücreti güncelliyoruz
            };

            // Act
            var result = await repo.UpdateDoctorScheduleAsync(1, dto);

            // Assert
            Assert.Equal(200, result.StatusCode);
            Assert.Equal("Çalışma saatleri güncellendi", result.Message);

            var updatedDoctor = await context.Doctors.FindAsync(1);
            Assert.Equal(750, updatedDoctor!.ConsultationFee);
            
            // Clocks JSON string olarak kaydedildiği için içerik kontrolü
            Assert.Contains("Monday", updatedDoctor.Clocks);
            Assert.Contains("09:00", updatedDoctor.Clocks);
        }

        [Fact]
        public async Task UpdateDoctorScheduleAsync_ShouldReturn404_WhenDoctorNotFound()
        {
            // Arrange
            using var context = new PostgresContext(_dbOptions);
            var repo = CreateRepository(context);

            var dto = new UpdateDoctorScheduleDto
            {
                Clocks = new { },
                ConsultationFee = 100
            };

            // Act (Olmayan UserId: 99)
            var result = await repo.UpdateDoctorScheduleAsync(99, dto);

            // Assert
            Assert.Equal(404, result.StatusCode);
            Assert.Contains("Doktor bulunamadı", result.Message);
        }

        // --- AddHealthHistoryAsync Testleri ---

        [Fact]
        public async Task AddHealthHistoryAsync_ShouldReturn200_AndAddRecord()
        {
            // Arrange
            using var context = new PostgresContext(_dbOptions);
            var repo = CreateRepository(context);

            var dto = new AddHealthHistoryDto
            {
                PatientId = 101, // Foreign key constraint olmadığı sürece InMemory'de User olmasa da ekler, ama pratikte User olması iyidir.
                Diagnosis = "Grippal Enfeksiyon",
                Treatment = "İstirahat ve Vitamin",
                Notes = "1 hafta rapor verildi"
            };

            // Act
            var result = await repo.AddHealthHistoryAsync(dto);

            // Assert
            Assert.Equal(200, result.StatusCode);
            Assert.True(result.Data);

            var historyInDb = await context.HealthHistories.FirstOrDefaultAsync(h => h.PatientId == 101);
            Assert.NotNull(historyInDb);
            Assert.Equal("Grippal Enfeksiyon", historyInDb.Diagnosis);
            Assert.Equal("İstirahat ve Vitamin", historyInDb.Treatment);
            Assert.False(historyInDb.Deleted); // Default değer kontrolü
        }

        // --- AddUnavailableDateAsync Testleri ---

        [Fact]
        public async Task AddUnavailableDateAsync_ShouldReturn200_WhenAddingToEmptyList()
        {
            // Arrange
            using var context = new PostgresContext(_dbOptions);

            var user = new User { Id = 2, Name = "Dr. Leave", Email = "leave@doc.com", PasswordHash = "hash", Role = "doctor", Tc = "22222222222" };
            var doctor = new Doctor
            {
                Id = 2,
                UserId = 2,
                Speciality = 2,
                Hospital = "City Hospital",
                UnavailableDates = null // Başlangıçta hiç izin yok
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

            // Act
            var result = await repo.AddUnavailableDateAsync(2, dto);

            // Assert
            Assert.Equal(200, result.StatusCode);

            var updatedDoctor = await context.Doctors.FindAsync(2);
            Assert.NotNull(updatedDoctor!.UnavailableDates);

            // JSON'ı deserialize edip içeriği kontrol et
            var dates = JsonSerializer.Deserialize<List<UnavailableDateDto>>(updatedDoctor.UnavailableDates);
            Assert.NotNull(dates);
            Assert.Single(dates); // 1 adet kayıt olmalı
            Assert.Equal("Yıllık İzin", dates[0].Reason);
        }

        [Fact]
        public async Task AddUnavailableDateAsync_ShouldReturn200_WhenAppendingToExistingList()
        {
            // Arrange
            using var context = new PostgresContext(_dbOptions);

            // Mevcut bir izin listesi oluştur
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
                UnavailableDates = JsonSerializer.Serialize(existingDates) // Mevcut data
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

            // Act
            var result = await repo.AddUnavailableDateAsync(3, newDateDto);

            // Assert
            Assert.Equal(200, result.StatusCode);

            var updatedDoctor = await context.Doctors.FindAsync(3);
            var dates = JsonSerializer.Deserialize<List<UnavailableDateDto>>(updatedDoctor!.UnavailableDates!);
            
            Assert.NotNull(dates);
            Assert.Equal(2, dates.Count); // Toplam 2 izin olmalı
            Assert.Contains(dates, d => d.Reason == "Konferans");
        }

        [Fact]
        public async Task AddUnavailableDateAsync_ShouldReturn404_WhenDoctorNotFound()
        {
            // Arrange
            using var context = new PostgresContext(_dbOptions);
            var repo = CreateRepository(context);

            var dto = new UnavailableDateDto
            {
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(1),
                Reason = "Test"
            };

            // Act (Olmayan ID)
            var result = await repo.AddUnavailableDateAsync(999, dto);

            // Assert
            Assert.Equal(404, result.StatusCode);
        }
    }
}