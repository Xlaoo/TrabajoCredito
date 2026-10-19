using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace TestTrabajoCredito
{
    public class EscenarioRunner
    {
        private readonly ITestOutputHelper _output;
        private readonly string _nombreFuncionalidad;
        private readonly List<string> _errores = new();
        private int _totalEscenarios = 0;
        private int _aprobados = 0;

        public int TotalEscenarios => _totalEscenarios;
        public int EscenariosAprobados => _aprobados;
        public int EscenariosFallidos => _errores.Count;

        public EscenarioRunner(ITestOutputHelper output, string nombreFuncionalidad)
        {
            _output = output;
            _nombreFuncionalidad = nombreFuncionalidad;
            _output.WriteLine("================================================================================");
            _output.WriteLine($" INICIANDO BATERÍA DE ESCENARIOS: {_nombreFuncionalidad.ToUpperInvariant()}");
            _output.WriteLine("================================================================================");
        }

        public void Evaluar(
            string nombreEscenario,
            string datoEntrada,
            object? esperado,
            Func<object?> accion,
            Func<object?, object?, bool>? comparador = null)
        {
            _totalEscenarios++;
            object? real = null;
            string estado = "CORRECTO";
            string detalleFallo = string.Empty;

            try
            {
                real = accion();
                bool sonIguales = comparador != null
                    ? comparador(esperado, real)
                    : Equals(esperado, real);

                if (!sonIguales)
                {
                    estado = "ERROR";
                    detalleFallo = $"Esperado: [{esperado}] | Obtenido: [{real}]";
                    _errores.Add($"Escenario '{nombreEscenario}' ({datoEntrada}): {detalleFallo}");
                }
                else
                {
                    _aprobados++;
                }
            }
            catch (Exception ex)
            {
                estado = "ERROR";
                real = $"Excepción: {ex.GetType().Name} - {ex.Message}";
                detalleFallo = $"Excepción no esperada: {ex.Message}";
                _errores.Add($"Escenario '{nombreEscenario}' ({datoEntrada}): {detalleFallo}");
            }

            _output.WriteLine($"[ESCENARIO {_totalEscenarios:D2}] {nombreEscenario}");
            _output.WriteLine($"  * Dato utilizado    : {datoEntrada}");
            _output.WriteLine($"  * Resultado esperado: {esperado}");
            _output.WriteLine($"  * Resultado real    : {real}");
            _output.WriteLine($"  * Estado            : [{estado}]");
            _output.WriteLine("--------------------------------------------------------------------------------");
        }

        public async Task EvaluarAsync(
            string nombreEscenario,
            string datoEntrada,
            object? esperado,
            Func<Task<object?>> accionAsync,
            Func<object?, object?, bool>? comparador = null)
        {
            _totalEscenarios++;
            object? real = null;
            string estado = "CORRECTO";
            string detalleFallo = string.Empty;

            try
            {
                real = await accionAsync();
                bool sonIguales = comparador != null
                    ? comparador(esperado, real)
                    : Equals(esperado, real);

                if (!sonIguales)
                {
                    estado = "ERROR";
                    detalleFallo = $"Esperado: [{esperado}] | Obtenido: [{real}]";
                    _errores.Add($"Escenario '{nombreEscenario}' ({datoEntrada}): {detalleFallo}");
                }
                else
                {
                    _aprobados++;
                }
            }
            catch (Exception ex)
            {
                estado = "ERROR";
                real = $"Excepción: {ex.GetType().Name} - {ex.Message}";
                detalleFallo = $"Excepción no esperada: {ex.Message}";
                _errores.Add($"Escenario '{nombreEscenario}' ({datoEntrada}): {detalleFallo}");
            }

            _output.WriteLine($"[ESCENARIO {_totalEscenarios:D2}] {nombreEscenario}");
            _output.WriteLine($"  * Dato utilizado    : {datoEntrada}");
            _output.WriteLine($"  * Resultado esperado: {esperado}");
            _output.WriteLine($"  * Resultado real    : {real}");
            _output.WriteLine($"  * Estado            : [{estado}]");
            _output.WriteLine("--------------------------------------------------------------------------------");
        }

        public void VerificarTodosAprobados()
        {
            _output.WriteLine("");
            _output.WriteLine("================================================================================");
            _output.WriteLine($" RESUMEN FINAL: {_nombreFuncionalidad.ToUpperInvariant()}");
            _output.WriteLine($" Total escenarios evaluados : {_totalEscenarios}");
            _output.WriteLine($" Escenarios APROBADOS       : {_aprobados}");
            _output.WriteLine($" Escenarios FALLIDOS        : {_errores.Count}");
            _output.WriteLine("================================================================================");

            if (_errores.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"La funcionalidad '{_nombreFuncionalidad}' presentó {_errores.Count} escenario(s) fallido(s):");
                for (int i = 0; i < _errores.Count; i++)
                {
                    sb.AppendLine($"  {i + 1}. {_errores[i]}");
                }
                throw new XunitException(sb.ToString());
            }
        }
    }
}
