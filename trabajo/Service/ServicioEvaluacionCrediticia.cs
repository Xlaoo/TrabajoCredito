using System;
using System.Collections.Generic;

namespace trabajo.Service
{
    public class ResultadoEvaluacionRiesgo
    {
        // Clasificación de Riesgo
        public string NivelRiesgo { get; set; } = "MEDIO"; // "BAJO", "MEDIO", "ALTO", "CRÍTICO"
        public string ClaseColor { get; set; } = "medio";   // "bajo", "medio", "alto", "critico"
        public string ColorHex { get; set; } = "#eab308";
        public string PosicionAguja { get; set; } = "-20deg"; // "-70deg", "-20deg", "35deg", "70deg"
        public string MotivoClasificacion { get; set; } = "";

        // Métricas Financieras
        public decimal IngresoMensual { get; set; }
        public decimal GastosMensuales { get; set; }
        public decimal IngresoNeto { get; set; }
        public decimal CapacidadPago { get; set; }            // IngresoMensual * 0.30
        public decimal DisponiblePrestamo { get; set; }       // CapacidadPago * 10.5
        public decimal MontoSolicitado { get; set; }
        public int PlazoMeses { get; set; }
        public decimal InteresTotal { get; set; }             // MontoSolicitado * 0.10
        public decimal CuotaMensual { get; set; }             // (MontoSolicitado * 1.10) / PlazoMeses
        public decimal CapacidadCuotaDisponible { get; set; } // Math.Max(0, Math.Min(CapacidadPago, IngresoNeto))

        // Viabilidad y Autorización
        public bool EsCuotaViable { get; set; }
        public bool EsAprobableOrdinario { get; set; }
        public List<string> Advertencias { get; set; } = new List<string>();

        // Desglose de Obligaciones
        public int DeudasActivas { get; set; }
        public int DeudasPagadas { get; set; }
        public int DeudasVencidas { get; set; }
        public int DeudasCrediPlusActivas { get; set; }
        public int DeudasCrediPlusPagadas { get; set; }
        public int DeudasCrediPlusVencidas { get; set; }
        public bool DeclaroOtrosCreditos { get; set; }
        public bool TieneInconsistenciaDeclarativa { get; set; }
        public bool TieneHistorialPrevio { get; set; }
        public bool TieneContadoresSinDetalle { get; set; }
    }

    public interface IServicioEvaluacionCrediticia
    {
        ResultadoEvaluacionRiesgo Evaluar(
            decimal ingresoMensual,
            decimal gastosMensuales,
            decimal montoSolicitado,
            int plazoMeses,
            int deudasActivas,
            int deudasPagadas,
            int deudasVencidas,
            bool declaroOtrosCreditos,
            int totalFilasHistorialDetallado = -1,
            int deudasCrediPlusActivas = 0,
            int deudasCrediPlusPagadas = 0,
            int deudasCrediPlusVencidas = 0);
    }

    public class ServicioEvaluacionCrediticia : IServicioEvaluacionCrediticia
    {
        public ResultadoEvaluacionRiesgo Evaluar(
            decimal ingresoMensual,
            decimal gastosMensuales,
            decimal montoSolicitado,
            int plazoMeses,
            int deudasActivas,
            int deudasPagadas,
            int deudasVencidas,
            bool declaroOtrosCreditos,
            int totalFilasHistorialDetallado = -1,
            int deudasCrediPlusActivas = 0,
            int deudasCrediPlusPagadas = 0,
            int deudasCrediPlusVencidas = 0)
        {
            var res = new ResultadoEvaluacionRiesgo();

            // 1. Sanitización de entradas financieras
            res.IngresoMensual = Math.Max(0m, ingresoMensual);
            res.GastosMensuales = Math.Max(0m, gastosMensuales);
            res.MontoSolicitado = Math.Max(0m, montoSolicitado);
            res.PlazoMeses = Math.Max(1, plazoMeses);

            res.DeclaroOtrosCreditos = declaroOtrosCreditos;
            res.DeudasCrediPlusActivas = deudasCrediPlusActivas;
            res.DeudasCrediPlusPagadas = deudasCrediPlusPagadas;
            res.DeudasCrediPlusVencidas = deudasCrediPlusVencidas;

            // Totales consolidados considerando deudas externas e internas
            res.DeudasActivas = deudasActivas + deudasCrediPlusActivas;
            res.DeudasPagadas = deudasPagadas + deudasCrediPlusPagadas;
            res.DeudasVencidas = deudasVencidas + deudasCrediPlusVencidas;

            // 2. Fórmulas Financieras
            res.IngresoNeto = Math.Max(0m, res.IngresoMensual - res.GastosMensuales);
            res.CapacidadPago = Math.Round(res.IngresoMensual * 0.30m, 2);
            res.DisponiblePrestamo = Math.Round(res.CapacidadPago * 10.5m, 2);
            res.InteresTotal = Math.Round(res.MontoSolicitado * 0.10m, 2);
            res.CuotaMensual = Math.Round((res.MontoSolicitado + res.InteresTotal) / res.PlazoMeses, 2);

            // Capacidad de cuota disponible: el mínimo entre el 30% del ingreso y el ingreso neto real
            res.CapacidadCuotaDisponible = Math.Round(Math.Max(0m, Math.Min(res.CapacidadPago, res.IngresoNeto)), 2);
            res.EsCuotaViable = (res.CuotaMensual > 0) && (res.CuotaMensual <= res.CapacidadCuotaDisponible);

            // Validaciones y advertencias financieras
            if (!res.EsCuotaViable)
            {
                res.Advertencias.Add($"Insuficiencia de capacidad: La cuota mensual estimada (S/ {res.CuotaMensual:N2}) supera la capacidad disponible (S/ {res.CapacidadCuotaDisponible:N2}). Requiere reformulación mediante 'En Evaluación'.");
            }

            if (res.MontoSolicitado > res.DisponiblePrestamo)
            {
                res.Advertencias.Add($"Monto excedido: El monto solicitado (S/ {res.MontoSolicitado:N2}) supera el monto disponible estimado (S/ {res.DisponiblePrestamo:N2}).");
            }

            if (!declaroOtrosCreditos && res.DeudasActivas > 0)
            {
                res.TieneInconsistenciaDeclarativa = true;
                res.Advertencias.Add("Inconsistencia detectada: El cliente declaró NO tener créditos activos, pero se identifican obligaciones activas en el sistema.");
            }

            res.TieneHistorialPrevio = (res.DeudasActivas + res.DeudasPagadas + res.DeudasVencidas) > 0;
            if (totalFilasHistorialDetallado == 0 && res.TieneHistorialPrevio)
            {
                res.TieneContadoresSinDetalle = true;
                res.Advertencias.Add("Discrepancia de registros: Existen contadores numéricos de deudas pero no se encontraron filas detalladas en el historial.");
            }

            // 3. Algoritmo de Scoring y Jerarquía de Prioridades
            // REGLA 1: Máxima Prioridad -> Deudas Vencidas
            if (res.DeudasVencidas > 0)
            {
                res.NivelRiesgo = "CRÍTICO";
                res.ClaseColor = "critico";
                res.ColorHex = "#ef4444";
                res.PosicionAguja = "70deg";
                res.MotivoClasificacion = "Se registran deudas en estado VENCIDO en el historial del cliente. Riesgo crediticio no admisible para crédito ordinario.";
                res.EsAprobableOrdinario = false;
            }
            // REGLA 2: Deudas Activas Vigentes
            else if (res.DeudasActivas > 0)
            {
                res.NivelRiesgo = "ALTO";
                res.ClaseColor = "alto";
                res.ColorHex = "#f97316";
                res.PosicionAguja = "35deg";
                res.MotivoClasificacion = "El cliente registra obligaciones de crédito ACTIVAS vigentes.";
                res.EsAprobableOrdinario = false; // Requiere reformulación o revisión de supervisor
            }
            // REGLA 3: Sin deudas activas ni vencidas
            else
            {
                // Subcaso A: Sin historial previo
                if (res.DeudasPagadas == 0)
                {
                    res.NivelRiesgo = "MEDIO";
                    res.ClaseColor = "medio";
                    res.ColorHex = "#eab308";
                    res.PosicionAguja = "-20deg";
                    res.MotivoClasificacion = "Cliente sin historial crediticio registrado (perfil no bancarizado). Requiere evaluación prudencial.";
                    res.EsAprobableOrdinario = res.EsCuotaViable && !res.TieneInconsistenciaDeclarativa;
                }
                // Subcaso B: Con historial crediticio positivo
                else
                {
                    // Si el monto solicitado supera el disponible, se eleva de BAJO a MEDIO
                    if (res.MontoSolicitado > res.DisponiblePrestamo)
                    {
                        res.NivelRiesgo = "MEDIO";
                        res.ClaseColor = "medio";
                        res.ColorHex = "#eab308";
                        res.PosicionAguja = "-20deg";
                        res.MotivoClasificacion = $"Historial crediticio positivo, pero el monto solicitado (S/ {res.MontoSolicitado:N2}) supera el monto disponible estimado (S/ {res.DisponiblePrestamo:N2}).";
                        res.EsAprobableOrdinario = res.EsCuotaViable && !res.TieneInconsistenciaDeclarativa;
                    }
                    else
                    {
                        res.NivelRiesgo = "BAJO";
                        res.ClaseColor = "bajo";
                        res.ColorHex = "#22c55e";
                        res.PosicionAguja = "-70deg";
                        res.MotivoClasificacion = "Historial crediticio favorable, sin deudas morosas ni activas, y monto compatible con la capacidad estimada.";
                        res.EsAprobableOrdinario = res.EsCuotaViable && !res.TieneInconsistenciaDeclarativa;
                    }
                }
            }

            return res;
        }
    }
}
