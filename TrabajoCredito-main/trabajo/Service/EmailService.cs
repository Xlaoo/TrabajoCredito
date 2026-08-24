using System.Net;
using System.Net.Mail;

namespace trabajo.Service
{
    public class EmailService
    {
        private readonly string remitente = "rafaelhugorosalespapuico@gmail.com";
        private readonly string password = "gvzkraxpqxosvjxk";

        public async Task EnviarCodigoAsync(string destino, string codigo)
        {
            using var cliente = new SmtpClient("smtp.gmail.com", 587);

            cliente.UseDefaultCredentials = false;
            cliente.Credentials = new NetworkCredential(remitente, password);
            cliente.EnableSsl = true;
            cliente.DeliveryMethod = SmtpDeliveryMethod.Network;
            cliente.Timeout = 30000;

            using var mensaje = new MailMessage(remitente, destino)
            {
                Subject = "Código de verificación",
                Body = $"Tu código es: {codigo}"
            };

            await cliente.SendMailAsync(mensaje);
        }

        public async Task EnviarCorreoAsync(string destino, string asunto, string cuerpoHtml)
        {
            using var cliente = new SmtpClient("smtp.gmail.com", 587);

            cliente.UseDefaultCredentials = false;
            cliente.Credentials = new NetworkCredential(remitente, password);
            cliente.EnableSsl = true;
            cliente.DeliveryMethod = SmtpDeliveryMethod.Network;
            cliente.Timeout = 30000;

            using var mensaje = new MailMessage();
            mensaje.From = new MailAddress(remitente, "CrediPlus");
            mensaje.To.Add(destino);
            mensaje.Subject = asunto;
            mensaje.Body = cuerpoHtml;
            mensaje.IsBodyHtml = true;

            await cliente.SendMailAsync(mensaje);
        }
    }
}
