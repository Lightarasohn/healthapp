using healthapp.DTOs;
using healthapp.DTOs.AppointmentDTOs;
using healthapp.Models;

namespace healthapp.Interfaces
{
    public interface IAppointmentRepository
    {
        Task<ApiResponse<Appointment>> CreateAppointmentAsync(int patientId, CreateAppointmentDto dto);
        Task<ApiResponse<Appointment>> GetAppointmentDetailsAsync(int id);
        Task<ApiResponse<bool>> CancelAppointmentAsync(int userId, string role, int id);
        Task<ApiResponse<Appointment>> RescheduleAppointmentAsync(int userId, int id, RescheduleDto dto); // EKLENDI
        Task<ApiResponse<IEnumerable<Appointment>>> GetDoctorAppointmentsAsync(int userId);
        Task<ApiResponse<IEnumerable<Appointment>>> GetPatientAppointmentsAsync(int patientId);
        Task<ApiResponse<IEnumerable<Appointment>>> GetAllAppointmentsAsync();
        Task<ApiResponse<Appointment>> UpdateStatusAsync(int userId, int id, string status);
    }
}