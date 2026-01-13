using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using healthapp.Context;
using healthapp.Repositories;
using healthapp.Interfaces;
using healthapp.DTOs.AppointmentDTOs;
using healthapp.Models;
using healthapp.DTOs;

namespace healthapp.Tests
{
    public class AppointmentRepositoryTests
    {

        private PostgresContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<PostgresContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new PostgresContext(options);
        }

        [Fact]
        public async Task CreateAppointment_ShouldFail_WhenDateIsPast()
        {

            using var context = GetInMemoryContext();
            var mockEmail = new Mock<IEmailService>();
            var repo = new AppointmentRepository(context, mockEmail.Object);


            var pastDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-1));

            var dto = new CreateAppointmentDto
            {
                DoctorId = 1,
                Date = pastDate,
                Start = new TimeOnly(10, 00)
            };


            var result = await repo.CreateAppointmentAsync(1, dto);


            Assert.Equal(400, result.StatusCode);
            Assert.Contains("Geçmiş bir tarihe", result.Message);
        }

        [Fact]
        public async Task CreateAppointment_ShouldFail_WhenOverlappingWithExisting()
        {

            using var context = GetInMemoryContext();
            var mockEmail = new Mock<IEmailService>();
            var repo = new AppointmentRepository(context, mockEmail.Object);


            var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
            var dayName = tomorrow.DayOfWeek.ToString().ToLower();


            var doctor = new Doctor
            {
                Id = 1,
                UserId = 10,

                Clocks = $"{{\"{dayName}\": {{\"start\": \"09:00\", \"end\": \"17:00\"}}}}",
                ConsultationFee = 500,
                User = new User { Id = 10, Name = "Dr. Test", Email = "doc@test.com", PasswordHash = "hash", Role = "doctor" }
            };


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


            var dto = new CreateAppointmentDto
            {
                DoctorId = 1,
                Date = tomorrow,
                Start = new TimeOnly(14, 00),
                End = new TimeOnly(15, 00),
                Notes = "Overlap Check"
            };

            var result = await repo.CreateAppointmentAsync(2, dto);


            Assert.Equal(400, result.StatusCode);
            Assert.Contains("başka bir randevu mevcut", result.Message);
        }

        [Fact]
        public async Task CreateAppointment_ShouldSuccess_WhenNoOverlap_AndValidDate()
        {

            using var context = GetInMemoryContext();
            var mockEmail = new Mock<IEmailService>();
            var repo = new AppointmentRepository(context, mockEmail.Object);

            var tomorrow = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
            var dayName = tomorrow.DayOfWeek.ToString().ToLower();


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


            var dto = new CreateAppointmentDto
            {
                DoctorId = 1,
                Date = tomorrow,
                Start = new TimeOnly(10, 00),

                Notes = "Valid Test"
            };

            var result = await repo.CreateAppointmentAsync(2, dto);


            Assert.Equal(201, result.StatusCode);
            Assert.NotNull(result.Data);
            Assert.Equal("booked", result.Data.Status);


            Assert.Equal(new TimeOnly(11, 00), result.Data.End);


            var dbRecord = await context.Appointments.FirstOrDefaultAsync();
            Assert.NotNull(dbRecord);
        }
    }
}