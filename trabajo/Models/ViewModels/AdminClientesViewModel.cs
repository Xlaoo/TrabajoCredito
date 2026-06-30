namespace trabajo.Models.ViewModels
{
    public class AdminClientesViewModel
    {
        public int TotalClientes { get; set; }
        public int ClientesActivos { get; set; }
        public int NuevosClientesEsteMes { get; set; }
        public decimal MontoTotalOtorgado { get; set; }
        public double PorcentajeActivos { get; set; }

        public string? Buscar { get; set; }
        public string Estado { get; set; } = "Todos";

        public int PaginaActual { get; set; }
        public int TotalPaginas { get; set; }
        public int TotalFiltrado { get; set; }
        public int InicioRegistro { get; set; }
        public int FinRegistro { get; set; }

        public List<ClienteAdminItemViewModel> Clientes { get; set; } = new();
    }

    public class ClienteAdminItemViewModel
    {
        public int Id { get; set; }
        public int NumeroFila { get; set; }

        public string Nombre { get; set; } = string.Empty;
        public string Apellido { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Iniciales { get; set; } = string.Empty;

        public string Dni { get; set; } = string.Empty;
        public string Celular { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string Genero { get; set; } = string.Empty;

        public bool EstadoActivo { get; set; }
        public DateTime FechaRegistro { get; set; }
    }
}