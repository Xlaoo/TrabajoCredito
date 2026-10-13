using System.Net;
using System.Net.Mail;
using System.Text;

namespace trabajo.Service
{
    public class EmailService
    {
        private readonly string remitente = "rafaelhugorosalespapuico@gmail.com";
        private readonly string password = "oyozzfnehrsobbrg";

        private SmtpClient CrearClienteSmtp(int timeoutMs = 8000)
        {
            return new SmtpClient("smtp.gmail.com", 587)
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(remitente, password),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = timeoutMs
            };
        }

        public async Task EnviarCodigoAsync(string destino, string codigo)
        {
            using var cliente = CrearClienteSmtp(15000);

            using var mensaje = new MailMessage
            {
                From = new MailAddress(remitente, "CrediPlus Seguridad", Encoding.UTF8),
                Subject = "Código de verificación",
                Body = $"Tu código es: {codigo}",
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8
            };
            mensaje.To.Add(destino);

            await cliente.SendMailAsync(mensaje);
        }

        public async Task EnviarCorreoAsync(string destino, string asunto, string cuerpoHtml)
        {
            using var cliente = CrearClienteSmtp(15000);

            using var mensaje = new MailMessage();
            mensaje.From = new MailAddress(remitente, "CrediPlus", Encoding.UTF8);
            mensaje.To.Add(destino);
            mensaje.Subject = asunto;
            mensaje.Body = cuerpoHtml;
            mensaje.IsBodyHtml = true;
            mensaje.SubjectEncoding = Encoding.UTF8;
            mensaje.BodyEncoding = Encoding.UTF8;
            mensaje.Headers.Add("X-Mailer", "CrediPlus Mailer v2.0");

            await cliente.SendMailAsync(mensaje);
        }

        public async Task EnviarAlertaAccesoCorrectoAsync(string destino, string nombre, string dispositivo, string fechaHora)
        {
            using var cliente = CrearClienteSmtp(6000);

            string cuerpo = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>Alerta de Acceso</title>
</head>
<body style=""margin:0;padding:0;background-color:#f1f5f9;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;-webkit-text-size-adjust:100%;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#f1f5f9;padding:30px 15px;"">
    <tr>
      <td align=""center"">
        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:560px;background-color:#ffffff;border-radius:18px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.07);"">
          <tr>
            <td style=""background:linear-gradient(135deg, #0F4C81 0%, #2F80ED 100%);padding:35px 30px;text-align:center;color:#ffffff;"">
              <div style=""font-size:26px;font-weight:800;letter-spacing:1px;"">CREDIPLUS</div>
              <div style=""font-size:12px;letter-spacing:2px;text-transform:uppercase;opacity:0.9;margin-top:4px;"">Centro de Seguridad</div>
              <div style=""display:inline-block;margin-top:16px;background:rgba(255,255,255,0.18);border:1px solid rgba(255,255,255,0.3);padding:6px 16px;border-radius:20px;font-size:13px;font-weight:600;"">
                🔐 ALERTA DE ACCESO
              </div>
            </td>
          </tr>
          <tr>
            <td style=""padding:35px 30px;color:#1e293b;"">
              <p style=""font-size:17px;font-weight:700;margin:0 0 14px 0;color:#0f172a;"">Hola, {WebUtility.HtmlEncode(nombre)}.</p>
              <p style=""font-size:15px;line-height:1.6;margin:0 0 20px 0;color:#334155;"">
                Detectamos que se han introducido <strong>correctamente las credenciales</strong> de tu cuenta CrediPlus.
              </p>
              <p style=""font-size:14px;line-height:1.6;margin:0 0 24px 0;color:#475569;background-color:#f8fafc;border-left:4px solid #2F80ED;padding:12px 16px;border-radius:4px;"">
                ℹ️ Para completar el inicio de sesión, debes finalizar el proceso de verificación de seguridad adicional (MFA).
              </p>
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;margin-bottom:26px;"">
                <tr>
                  <td style=""padding:14px 18px;border-bottom:1px solid #e2e8f0;font-size:13px;color:#64748b;font-weight:600;"">DETALLES DEL ACCESO</td>
                </tr>
                <tr>
                  <td style=""padding:12px 18px;font-size:14px;color:#1e293b;border-bottom:1px solid #f1f5f9;"">
                    <strong>Fecha y Hora:</strong> {WebUtility.HtmlEncode(fechaHora)}
                  </td>
                </tr>
                <tr>
                  <td style=""padding:12px 18px;font-size:14px;color:#1e293b;border-bottom:1px solid #f1f5f9;"">
                    <strong>Dispositivo / Navegador:</strong> {WebUtility.HtmlEncode(dispositivo)}
                  </td>
                </tr>
                <tr>
                  <td style=""padding:12px 18px;font-size:14px;color:#d97706;font-weight:600;"">
                    <strong>Estado:</strong> ⏳ Pendiente de verificación de seguridad
                  </td>
                </tr>
              </table>
              <div style=""background-color:#f1f5f9;border-radius:12px;padding:18px;font-size:13px;line-height:1.6;color:#475569;"">
                <strong style=""color:#0f172a;display:block;margin-bottom:6px;font-size:14px;"">¿FUISTE TÚ?</strong>
                Si estás intentando acceder, continúa normalmente con tu proceso de verificación.<br><br>
                <span style=""color:#b91c1c;font-weight:600;"">Si no reconoces esta actividad</span>, te recomendamos cambiar tu contraseña de inmediato y contactar con nuestro equipo de soporte para proteger tu cuenta.
              </div>
            </td>
          </tr>
          <tr>
            <td style=""background-color:#f8fafc;padding:24px 30px;text-align:center;border-top:1px solid #e2e8f0;font-size:12px;color:#94a3b8;"">
              <p style=""margin:0 0 6px 0;font-weight:600;color:#64748b;"">CREDIPLUS • Tu seguridad es nuestra prioridad</p>
              <p style=""margin:0;"">Este es un mensaje automático de seguridad preventiva.</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

            using var mensaje = new MailMessage
            {
                From = new MailAddress(remitente, "CrediPlus Seguridad", Encoding.UTF8),
                Subject = "🔐 CrediPlus | Detectamos un nuevo intento de acceso",
                Body = cuerpo,
                IsBodyHtml = true,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                Priority = MailPriority.High
            };
            mensaje.To.Add(destino);
            mensaje.Headers.Add("X-Mailer", "CrediPlus Security Mailer v2.0");

            await cliente.SendMailAsync(mensaje);
        }

        public async Task EnviarAlertaIntentoFallidoAsync(string destino, string nombre, string fechaHora)
        {
            using var cliente = CrearClienteSmtp(6000);

            string cuerpo = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>Intento de Acceso Fallido</title>
</head>
<body style=""margin:0;padding:0;background-color:#f1f5f9;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;-webkit-text-size-adjust:100%;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#f1f5f9;padding:30px 15px;"">
    <tr>
      <td align=""center"">
        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:560px;background-color:#ffffff;border-radius:18px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.07);"">
          <tr>
            <td style=""background:linear-gradient(135deg, #b91c1c 0%, #dc2626 100%);padding:35px 30px;text-align:center;color:#ffffff;"">
              <div style=""font-size:26px;font-weight:800;letter-spacing:1px;"">CREDIPLUS</div>
              <div style=""font-size:12px;letter-spacing:2px;text-transform:uppercase;opacity:0.9;margin-top:4px;"">Alerta de Seguridad</div>
              <div style=""display:inline-block;margin-top:16px;background:rgba(255,255,255,0.22);border:1px solid rgba(255,255,255,0.4);padding:6px 16px;border-radius:20px;font-size:13px;font-weight:600;"">
                ⚠️ ATENCIÓN
              </div>
            </td>
          </tr>
          <tr>
            <td style=""padding:35px 30px;color:#1e293b;"">
              <p style=""font-size:17px;font-weight:700;margin:0 0 14px 0;color:#0f172a;"">Hola, {WebUtility.HtmlEncode(nombre)}.</p>
              <p style=""font-size:15px;line-height:1.6;margin:0 0 20px 0;color:#334155;"">
                Detectamos un intento de acceso a tu cuenta CrediPlus utilizando una <strong>contraseña incorrecta</strong>. El acceso fue rechazado.
              </p>
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#fef2f2;border:1px solid #fecaca;border-radius:12px;margin-bottom:26px;"">
                <tr>
                  <td style=""padding:14px 18px;border-bottom:1px solid #fecaca;font-size:13px;color:#991b1b;font-weight:600;"">DETALLES DEL INTENTO</td>
                </tr>
                <tr>
                  <td style=""padding:12px 18px;font-size:14px;color:#1e293b;border-bottom:1px solid #fee2e2;"">
                    <strong>Fecha y Hora:</strong> {WebUtility.HtmlEncode(fechaHora)}
                  </td>
                </tr>
                <tr>
                  <td style=""padding:12px 18px;font-size:14px;color:#991b1b;border-bottom:1px solid #fee2e2;"">
                    <strong>Resultado:</strong> Contraseña incorrecta
                  </td>
                </tr>
                <tr>
                  <td style=""padding:12px 18px;font-size:14px;color:#dc2626;font-weight:700;"">
                    <strong>Estado:</strong> ⛔ Acceso rechazado
                  </td>
                </tr>
              </table>
              <div style=""background-color:#f8fafc;border-radius:12px;padding:18px;font-size:13px;line-height:1.6;color:#475569;"">
                <strong style=""color:#0f172a;display:block;margin-bottom:6px;font-size:14px;"">¿NO RECONOCES ESTA ACTIVIDAD?</strong>
                Si no reconoces esta actividad, te recomendamos revisar la seguridad de tu cuenta y actualizar tu contraseña si sospechas de un acceso no autorizado.<br><br>
                Si realizaste este intento, puedes volver a introducir tus credenciales.
              </div>
            </td>
          </tr>
          <tr>
            <td style=""background-color:#f8fafc;padding:24px 30px;text-align:center;border-top:1px solid #e2e8f0;font-size:12px;color:#94a3b8;"">
              <p style=""margin:0 0 6px 0;font-weight:600;color:#64748b;"">CREDIPLUS • Protegemos tu información</p>
              <p style=""margin:0;"">Mensaje de seguridad preventiva automática.</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

            using var mensaje = new MailMessage
            {
                From = new MailAddress(remitente, "CrediPlus Seguridad", Encoding.UTF8),
                Subject = "⚠️ CrediPlus | Intento de acceso fallido",
                Body = cuerpo,
                IsBodyHtml = true,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                Priority = MailPriority.High
            };
            mensaje.To.Add(destino);
            mensaje.Headers.Add("X-Mailer", "CrediPlus Security Mailer v2.0");

            await cliente.SendMailAsync(mensaje);
        }

        public async Task EnviarCodigoRegistroOtpAsync(string destino, string codigoOtp)
        {
            using var cliente = CrearClienteSmtp(10000);

            string cuerpo = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>Verificación de Correo</title>
</head>
<body style=""margin:0;padding:0;background-color:#f1f5f9;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;-webkit-text-size-adjust:100%;"">
  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#f1f5f9;padding:30px 15px;"">
    <tr>
      <td align=""center"">
        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width:540px;background-color:#ffffff;border-radius:18px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.07);"">
          <tr>
            <td style=""background:linear-gradient(135deg, #4361ee 0%, #7209b7 100%);padding:35px 30px;text-align:center;color:#ffffff;"">
              <div style=""font-size:26px;font-weight:800;letter-spacing:1px;"">CREDIPLUS</div>
              <div style=""font-size:13px;letter-spacing:1px;text-transform:uppercase;opacity:0.9;margin-top:4px;"">Verificación de Correo</div>
            </td>
          </tr>
          <tr>
            <td style=""padding:35px 30px;color:#1e293b;text-align:center;"">
              <p style=""font-size:18px;font-weight:700;margin:0 0 10px 0;color:#0f172a;"">Bienvenido a CrediPlus.</p>
              <p style=""font-size:15px;line-height:1.6;margin:0 0 24px 0;color:#475569;"">
                Para continuar con tu registro, introduce el siguiente código:
              </p>
              <div style=""background-color:#eef2ff;border:2px dashed #6366f1;border-radius:14px;padding:20px 15px;margin:0 auto 20px auto;max-width:320px;"">
                <span style=""font-size:38px;font-weight:800;letter-spacing:10px;color:#3730a3;font-family:'Consolas','Courier New',monospace;"">
                  {WebUtility.HtmlEncode(codigoOtp)}
                </span>
              </div>
              <p style=""font-size:14px;font-weight:700;color:#dc2626;margin:0 0 24px 0;"">
                ⏱️ Este código expira en 60 segundos.
              </p>
              <div style=""background-color:#f8fafc;border-radius:12px;padding:16px;font-size:13px;line-height:1.5;color:#64748b;text-align:left;"">
                🔒 <strong>Importante:</strong> Nunca compartas este código con nadie.<br><br>
                Si no solicitaste este registro, puedes ignorar este mensaje.
              </div>
            </td>
          </tr>
          <tr>
            <td style=""background-color:#f8fafc;padding:20px 30px;text-align:center;border-top:1px solid #e2e8f0;font-size:12px;color:#94a3b8;"">
              <p style=""margin:0 0 4px 0;font-weight:600;color:#64748b;"">CREDIPLUS • Tu seguridad es importante</p>
              <p style=""margin:0;"">Mensaje automático de confirmación de identidad.</p>
            </td>
          </tr>
        </table>
      </td>
    </tr>
  </table>
</body>
</html>";

            using var mensaje = new MailMessage
            {
                From = new MailAddress(remitente, "CrediPlus", Encoding.UTF8),
                Subject = "✉️ CrediPlus | Verifica tu correo electrónico",
                Body = cuerpo,
                IsBodyHtml = true,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                Priority = MailPriority.High
            };
            mensaje.To.Add(destino);
            mensaje.Headers.Add("X-Mailer", "CrediPlus Security Mailer v2.0");

            await cliente.SendMailAsync(mensaje);
        }
    }
}
