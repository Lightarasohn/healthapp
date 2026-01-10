using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using healthapp.Context;
using healthapp.Repositories;
using healthapp.Interfaces;
using healthapp.DTOs.AppointmentDTOs;
using healthapp.Models;
using healthapp.DTOs; // ApiResponse için

namespace healthapp.Tests
{
    public class AppointmentRepositoryTests
    {
        // Her test için temiz bir Context oluşturmak adına yardımcı metot
        private PostgresContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<PostgresContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Her çağrıda benzersiz DB ismi
                .Options;
            return new PostgresContext(options);
        }

        [Fact]
        public async Task CreateAppointment_ShouldFail_WhenDateIsPast()
        {
            // Arrange (Hazırlık)
            using var context = GetInMemoryContext();
            var mockEmail = new Mock<IEmailService>();
            var repo = new AppointmentRepository(context, mockEmail.Object);

            // Dün için bir tarih ayarla
            var pastDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-1));
            
            var dto = new CreateAppointmentDto
            {
                DoctorId = 1,
                Date = pastDate,
                Start = new TimeOnly(10, 00)
            };

            // Act (Eylem)
            var result = await repo.CreateAppointmentAsync(1, dto);

            // Assert (Doğrulama)
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("Geçmiş bir tarihe", result.Message);
        }

        [Fact]
        public async Task CreateAppointment_ShouldFail_WhenOverlappingWithExisting()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var mockEmail = new Mock<IEmailService>();
            var repo = new AppointmentRepository(context, mockEmail.Object);

            // Testin çalışacağı günün ismini bul (Örn: "monday")
            var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
            var dayName = tomorrow.DayOfWeek.ToString().ToLower();

            // 1. Doktoru ve Çalışma Saatlerini Ekle (Mock Data)
            var doctor = new Doctor
            {
                Id = 1,
                UserId = 10,
                // Test günü için çalışma saati tanımla (09:00 - 17:00)
                Clocks = $"{{\"{dayName}\": {{\"start\": \"09:00\", \"end\": \"17:00\"}}}}",
                ConsultationFee = 500,
                User = new User { Id = 10, Name = "Dr. Test", Email = "doc@test.com", PasswordHash = "hash", Role = "doctor" }
            };
            
            // 2. Mevcut bir randevu ekle (14:00 - 15:00)
            var existingAppointment = new Appointment
            {
                Id = 100,
                DoctorId = 1,
                PatientId = 99,
                Date = tomorrow,
                Start = new TimeOnly(14, 00),
                End = new TimeOnly(15, 00),
                Status = "booked"
            };

            await context.Doctors.AddAsync(doctor);
            await context.Appointments.AddAsync(existingAppointment);
            await context.SaveChangesAsync();

            // Act -> Çakışan bir saat iste (14:30 - 15:30)
            // Mevcut randevu 15:00'te bitiyor ama biz 14:30'da başlatmak istiyoruz.
            var dto = new CreateAppointmentDto
            {
                DoctorId = 1,
                Date = tomorrow,
                Start = new TimeOnly(14, 00), 
                End = new TimeOnly(15, 00),
                Notes = "Overlap Check"
            };

            var result = await repo.CreateAppointmentAsync(2, dto);

            // Assert
            Assert.Equal(400, result.StatusCode);
            Assert.Contains("başka bir randevu mevcut", result.Message);
        }

        [Fact]
        public async Task CreateAppointment_ShouldSuccess_WhenNoOverlap_AndValidDate()
        {
            // Arrange
            using var context = GetInMemoryContext();
            var mockEmail = new Mock<IEmailService>();
            var repo = new AppointmentRepository(context, mockEmail.Object);

            var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
            var dayName = tomorrow.DayOfWeek.ToString().ToLower();

            // Doktor ve Hasta Ekle
            var doctor = new Doctor
            {
                Id = 1,
                UserId = 10,
                Clocks = $"{{\"{dayName}\": {{\"start\": \"09:00\", \"end\": \"17:00\"}}}}",
                ConsultationFee = 500,
                User = new User { Id = 10, Name = "Dr. Test", Email = "doctor@test.com", PasswordHash = "hash", Role = "doctor" }
            };
            var patient = new User { Id = 2, Name = "Patient Test", Email = "patient@test.com", PasswordHash = "hash", Role = "patient" };

            await context.Doctors.AddAsync(doctor);
            await context.Users.AddAsync(patient);
            await context.SaveChangesAsync();

            // Act -> Uygun bir saat (10:00 - 11:00)
            var dto = new CreateAppointmentDto
            {
                DoctorId = 1,
                Date = tomorrow,
                Start = new TimeOnly(10, 00),
                // End null gönderilirse kod otomatik 1 saat eklemeli
                Notes = "Valid Test"
            };

            var result = await repo.CreateAppointmentAsync(2, dto);

            // Assert
            Assert.Equal(201, result.StatusCode);
            Assert.NotNull(result.Data);
            Assert.Equal("booked", result.Data.Status);
            
            // Otomatik bitiş saati kontrolü (1 saat eklenmiş mi?)
            Assert.Equal(new TimeOnly(11, 00), result.Data.End);
            
            // Veritabanına gerçekten girdi mi?
            var dbRecord = await context.Appointments.FirstOrDefaultAsync();
            Assert.NotNull(dbRecord);
        }
    }
}