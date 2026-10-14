using System;

namespace trabajo.Service
{
    /// <summary>
    /// Helper oficial para la gestión uniforme, robusta y consistente de fechas y horas
    /// en la zona horaria oficial de Perú (America/Lima, UTC-05:00).
    /// Garantiza compatibilidad 100% entre Windows (localhost) y Linux (Northflank/Docker),
    /// independientemente de la zona horaria del sistema operativo host.
    /// </summary>
    public static class PeruTimeHelper
    {
        public static readonly TimeZoneInfo PeruTimeZone = ObtenerZonaHorariaPeru();

        private static TimeZoneInfo ObtenerZonaHorariaPeru()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
            }
            catch
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
                }
                catch
                {
                    return TimeZoneInfo.CreateCustomTimeZone("America/Lima", TimeSpan.FromHours(-5), "Hora de Perú", "Hora de Perú");
                }
            }
        }

        /// <summary>
        /// Obtiene el instante actual expresado en la hora oficial de Perú (UTC-05:00).
        /// </summary>
        public static DateTime NowPeru => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, PeruTimeZone);

        /// <summary>
        /// Obtiene la fecha civil actual en Perú a las 00:00:00 (inicio del día).
        /// </summary>
        public static DateTime TodayPeru => NowPeru.Date;

        /// <summary>
        /// Convierte un instante UTC a hora oficial de Perú.
        /// </summary>
        public static DateTime ConvertirUtcAPeru(DateTime utcDateTime)
        {
            var utc = utcDateTime.Kind == DateTimeKind.Utc 
                ? utcDateTime 
                : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(utc, PeruTimeZone);
        }

        /// <summary>
        /// Convierte de forma segura una fecha leída de la base de datos a hora de Perú.
        /// Regla crítica de compatibilidad y preservación:
        /// Las fechas de solo calendario (medianoche 00:00:00) no se desplazan
        /// para evitar que un día específico salte al día anterior.
        /// </summary>
        public static DateTime ConvertirFechaBaseDatosAPeru(DateTime dbDateTime, bool esInstanteUtc = true)
        {
            // PRUEBA F y PRUEBA I: Fechas de solo calendario (00:00:00) no deben desplazarse 5 horas al día anterior
            if (dbDateTime.TimeOfDay == TimeSpan.Zero)
            {
                return dbDateTime.Date;
            }

            if (dbDateTime.Kind == DateTimeKind.Utc)
            {
                return TimeZoneInfo.ConvertTimeFromUtc(dbDateTime, PeruTimeZone);
            }

            if (dbDateTime.Kind == DateTimeKind.Local)
            {
                return TimeZoneInfo.ConvertTime(dbDateTime, PeruTimeZone);
            }

            if (esInstanteUtc)
            {
                var utc = DateTime.SpecifyKind(dbDateTime, DateTimeKind.Utc);
                return TimeZoneInfo.ConvertTimeFromUtc(utc, PeruTimeZone);
            }

            return dbDateTime;
        }

        /// <summary>
        /// Calcula la fecha de vencimiento civil a partir de la fecha base de aprobación en Perú.
        /// Maneja correctamente meses de 28, 29, 30 y 31 días y años bisiestos.
        /// Retorna siempre una fecha civil pura (00:00:00).
        /// </summary>
        public static DateTime CalcularVencimientoCivil(DateTime fechaBaseCivil, int numeroMes)
        {
            DateTime basePura = fechaBaseCivil.Date;
            return basePura.AddMonths(numeroMes);
        }

        /// <summary>
        /// Evalúa si una cuota se encuentra vencida según la regla contractual peruana:
        /// Una cuota con FechaLimitePago sólo se considera vencida una vez culminado
        /// en su totalidad el día de pago en Perú (a partir de las 00:00:00 del día siguiente).
        /// </summary>
        public static bool EsCuotaVencida(DateTime fechaLimitePago)
        {
            return fechaLimitePago.Date < TodayPeru;
        }

        public static string FormatoFecha(DateTime dt) => dt.ToString("dd/MM/yyyy");
        public static string FormatoHora(DateTime dt) => dt.ToString("HH:mm");
        public static string FormatoFechaHora(DateTime dt) => dt.ToString("dd/MM/yyyy HH:mm");
        public static string FormatoIsoFecha(DateTime dt) => dt.ToString("yyyy-MM-dd");
    }
}
