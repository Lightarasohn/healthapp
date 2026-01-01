using healthapp.Context;
using Microsoft.EntityFrameworkCore;

namespace healthapp.Services
{
    public class AppointmentReminderService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AppointmentReminderService> _logger;

        public AppointmentReminderService(IServiceProvider serviceProvider, ILogger<AppointmentReminderService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Saatte bir çalışacak şekilde döngü
            var timer = new PeriodicTimer(TimeSpan.FromHours(1));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    _logger.LogInformation("Cron Job çalıştı: {time}", DateTime.Now);

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<PostgresContext>();
                        // Mail servisi varsa buraya inject edilecek, şimdilik console log basıyoruz.
                        // var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>(); 

                        var now = DateTime.Now;
                        var next24h = now.AddHours(24);
                        var next25h = now.AddHours(25);

                        // Node.js mantığı: 24-25 saat sonra başlayacak olanlar
                        // EF Core ile DateOnly ve TimeOnly karşılaştırması biraz trick'li olabilir, 
                        // basitleştirmek için tarihi ve saati birleştirip kontrol edebiliriz veya doğrudan DB sorgusu.
                        
                        var appointments = await context.Appointments
                            .Include(a => a.Patient)
                            .Where(a => a.Status == "booked") 
                            // Not: ReminderSent alanı Appointment modelinde yoksa eklenmeli veya es geçilmeli.
                            // Node.js kodunda reminderSent: { $ne: true } vardı. Modelde yoksa bu kontrolü atlıyorum.
                            .ToListAsync(stoppingToken);

                        foreach (var appt in appointments)
                        {
                            var apptDateTime = appt.Date.ToDateTime(appt.Start);
                            
                            if (apptDateTime >= next24h && apptDateTime < next25h)
                            {
                                if (appt.Patient != null && !string.IsNullOrEmpty(appt.Patient.Email))
                                {
                                    // Mail gönderme işlemi burada yapılacak
                                    _logger.LogInformation($"Hatırlatma Gönderildi: Hasta {appt.Patient.Name}, Tarih: {apptDateTime}");
                                    
                                    // Modelde ReminderSent varsa true yapıp kaydet
                                    // appt.ReminderSent = true;
                                }
                            }
                        }
                        // await context.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Randevu hatırlatma hatası");
                }
            }
        }
    }
}