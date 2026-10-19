using System;
using Xunit;
using Xunit.Abstractions;
using trabajo.Service;

namespace TestTrabajoCredito
{
    public class PeruTimeHelperTests
    {
        private readonly ITestOutputHelper _output;

        public PeruTimeHelperTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ProbarTodasLasReglasTemporalesYVencimientos()
        {
            var runner = new EscenarioRunner(_output, "Reglas Temporales, Zona Horaria Perú (UTC-5) y Vencimientos Civiles");

            // 1. Verificación de la zona horaria de Perú (UTC -5)
            runner.Evaluar(
                nombreEscenario: "Zona horaria oficial de Perú (Offset UTC -5 horas)",
                datoEntrada: "PeruTimeHelper.PeruTimeZone",
                esperado: "-05:00:00",
                accion: () => PeruTimeHelper.PeruTimeZone.BaseUtcOffset.ToString());

            // 2. Conversión horaria de UTC a hora local de Perú
            runner.Evaluar(
                nombreEscenario: "Conversión de instante UTC a hora local peruana (-5h exactas)",
                datoEntrada: "2026-06-15 15:00:00 UTC",
                esperado: "2026-06-15 10:00:00",
                accion: () =>
                {
                    var utc = new DateTime(2026, 6, 15, 15, 0, 0, DateTimeKind.Utc);
                    var peru = PeruTimeHelper.ConvertirUtcAPeru(utc);
                    return peru.ToString("yyyy-MM-dd HH:mm:ss");
                });

            // 3. Conversión de fecha pura de base de datos (00:00:00) sin desplazamiento de día
            runner.Evaluar(
                nombreEscenario: "Fecha pura de base de datos (00:00:00) preserva día calendario",
                datoEntrada: "2026-10-20 00:00:00",
                esperado: "2026-10-20 00:00:00",
                accion: () =>
                {
                    var fechaPura = new DateTime(2026, 10, 20, 0, 0, 0);
                    var conv = PeruTimeHelper.ConvertirFechaBaseDatosAPeru(fechaPura);
                    return conv.ToString("yyyy-MM-dd HH:mm:ss");
                });

            // 4. Conversión de fecha y hora UTC almacenada en base de datos
            runner.Evaluar(
                nombreEscenario: "Fecha y hora con componente horario aplica desplazamiento UTC a Perú",
                datoEntrada: "2026-10-20 03:00:00 UTC",
                esperado: "2026-10-19 22:00:00",
                accion: () =>
                {
                    var conHora = new DateTime(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc);
                    var conv = PeruTimeHelper.ConvertirFechaBaseDatosAPeru(conHora);
                    return conv.ToString("yyyy-MM-dd HH:mm:ss");
                });

            // 5-8. Vencimiento civil a fin de mes y años bisiestos (Código Civil Peruano)
            (int anio, int mes, int dia, int sumar, string esperado, string desc)[] casosVencimiento = new[]
            {
                (2024, 1, 31, 1, "2024-02-29", "Año bisiesto 2024: 31 de enero + 1 mes ajusta al 29 de febrero"),
                (2023, 1, 31, 1, "2023-02-28", "Año no bisiesto 2023: 31 de enero + 1 mes ajusta al 28 de febrero"),
                (2026, 3, 31, 1, "2026-04-30", "Mes de 30 días: 31 de marzo + 1 mes ajusta al 30 de abril"),
                (2026, 5, 15, 3, "2026-08-15", "Plazo de 3 meses exactos: 15 de mayo + 3 meses al 15 de agosto")
            };

            foreach (var caso in casosVencimiento)
            {
                runner.Evaluar(
                    nombreEscenario: $"Vencimiento civil: {caso.desc}",
                    datoEntrada: $"{caso.anio}-{caso.mes:D2}-{caso.dia:D2} + {caso.sumar} mes(es)",
                    esperado: caso.esperado,
                    accion: () =>
                    {
                        var fb = new DateTime(caso.anio, caso.mes, caso.dia, 12, 0, 0);
                        var res = PeruTimeHelper.CalcularVencimientoCivil(fb, caso.sumar);
                        return res.ToString("yyyy-MM-dd");
                    });
            }

            // 9. Cuota con fecha límite futura no está vencida
            runner.Evaluar(
                nombreEscenario: "Evaluación de cuota futura (No vencida)",
                datoEntrada: "Fecha límite: TodayPeru + 5 días",
                esperado: false,
                accion: () => PeruTimeHelper.EsCuotaVencida(PeruTimeHelper.TodayPeru.AddDays(5)));

            // 10. Cuota con fecha límite hoy no está vencida durante el día
            runner.Evaluar(
                nombreEscenario: "Evaluación de cuota con fecha de vencimiento hoy (No vencida hasta expirar el día)",
                datoEntrada: "Fecha límite: TodayPeru",
                esperado: false,
                accion: () => PeruTimeHelper.EsCuotaVencida(PeruTimeHelper.TodayPeru));

            // 11. Cuota con fecha límite en el pasado (ayer) está vencida
            runner.Evaluar(
                nombreEscenario: "Evaluación de cuota vencida (Fecha límite fue ayer)",
                datoEntrada: "Fecha límite: TodayPeru - 1 día",
                esperado: true,
                accion: () => PeruTimeHelper.EsCuotaVencida(PeruTimeHelper.TodayPeru.AddDays(-1)));

            // 12. Formateadores estándar de fecha y hora para vistas y comprobantes
            runner.Evaluar(
                nombreEscenario: "Formateadores de presentación (Fecha, Hora, FechaHora e ISO)",
                datoEntrada: "2026-07-28 18:45:30",
                esperado: "Fecha: 28/07/2026 | Hora: 18:45 | FechaHora: 28/07/2026 18:45 | ISO: 2026-07-28",
                accion: () =>
                {
                    var dt = new DateTime(2026, 7, 28, 18, 45, 30);
                    return $"Fecha: {PeruTimeHelper.FormatoFecha(dt)} | Hora: {PeruTimeHelper.FormatoHora(dt)} | FechaHora: {PeruTimeHelper.FormatoFechaHora(dt)} | ISO: {PeruTimeHelper.FormatoIsoFecha(dt)}";
                });

            runner.VerificarTodosAprobados();
        }
    }
}
