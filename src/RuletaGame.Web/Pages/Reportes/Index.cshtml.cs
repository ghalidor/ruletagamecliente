using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;
using System.Text;
using RuletaGame.Application.Abstractions;
using RuletaGame.Domain.Entities;
using RuletaGame.Domain.Enums;

namespace RuletaGame.Web.Pages.Reportes;

public sealed class IndexModel(
    IJugadaRepositorio jugadas,
    IRuletaRepositorio ruletas,
    IConfiguracionRepositorio configuracion) : PageModel {
    public IReadOnlyList<Ruleta> Ruletas { get; private set; } = [];
    public string Moneda { get; private set; } = "S/.";

    public async Task OnGetAsync() {
        Ruletas = await ruletas.ListarAsync();

        var config = await configuracion.ObtenerDiccionarioAsync();
        Moneda = config.GetValueOrDefault(ClaveConfiguracion.MonedaSimbolo, "S/.");
    }

    public async Task<IActionResult> OnPostBuscarAsync(
        string desde, string hasta, int? ruletaId, bool soloRegistradas) {
        if(!TryFecha(desde, out var d) || !TryFecha(hasta, out var h))
            return new JsonResult(new { exito = false, mensaje = "Revisa las fechas." });

        if(d > h)
            return new JsonResult(new { exito = false, mensaje = "La fecha inicial es posterior a la final." });

        if((h - d).TotalDays > 366)
            return new JsonResult(new { exito = false, mensaje = "El rango no puede pasar de un ano." });

        var lista = await jugadas.ReporteAsync(d, h, ruletaId == 0 ? null : ruletaId, soloRegistradas);

        return new JsonResult(new {
            exito = true,
            total = lista.Count,
            registradas = lista.Count(j => j.Registrado),

            // Dos montos, no uno. El tablero mide el consumo del tope, que
            // solo cuenta los premios con AfectaTope = 1. Si el reporte
            // mostrara un unico total, las cifras nunca cuadrarian entre
            // pantallas y pareceria un error.
            monto = lista.Sum(j => j.MontoPremio),
            montoDescuenta = lista.Where(j => j.AfectaTope).Sum(j => j.MontoPremio),

            datos = lista.Select(j => new {
                j.JugadaId,
                fecha = j.FechaJuego.ToString("dd/MM/yyyy HH:mm"),

                // Valor ordenable: la tabla ordenaba "dd/MM/yyyy" como texto,
                // asi que 01/12 quedaba antes que 05/09.
                fechaOrden = j.FechaJuego.ToString("yyyyMMddHHmm"),
                ruleta = j.NombreRuleta,
                pantalla = j.NombrePantalla ?? "—",
                premio = j.DescripcionPremio,
                tipo = j.TipoPremio == TipoTajada.Monto ? "Monto" : "Producto",
                monto = j.MontoPremio,
                afectaTope = j.AfectaTope,
                documento = j.Dni ?? "—",
                tipoDoc = NombreTipoDocumento(j.TipoDocumento),
                cliente = j.NombreCliente ?? "—",
                correo = j.CorreoCliente ?? "—",
                registrado = j.Registrado
            })
        });
    }

    /// <summary>Excel real, con encabezados, formatos, filtros y total.</summary>
    public async Task<IActionResult> OnGetExportarExcelAsync(
        string desde, string hasta, int? ruletaId, bool soloRegistradas) {
        if(!TryFecha(desde, out var d) || !TryFecha(hasta, out var h))
            return BadRequest("Fechas invalidas.");

        var lista = await jugadas.ReporteAsync(d, h, ruletaId == 0 ? null : ruletaId, soloRegistradas);
        var config = await configuracion.ObtenerDiccionarioAsync();
        string moneda = config.GetValueOrDefault(ClaveConfiguracion.MonedaSimbolo, "S/.");

        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Jugadas");

        string[] encabezados =
        [
            "Jugada",
            "Fecha",
            "Ruleta",
            "Pedestal",
            "Premio",
            "Tipo",
            "Monto",
            "Descuenta",
            "Tipo documento",
            "Documento",
            "Cliente",
            "Correo",
            "Registrado"
        ];

        for(int i = 0; i < encabezados.Length; i++)
            hoja.Cell(1, i + 1).Value = encabezados[i];

        var cabecera = hoja.Range(1, 1, 1, encabezados.Length);
        cabecera.Style.Font.Bold = true;
        cabecera.Style.Fill.BackgroundColor = XLColor.FromHtml("#6D4AD8");
        cabecera.Style.Font.FontColor = XLColor.White;
        cabecera.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        hoja.Row(1).Height = 22;

        int fila = 2;
        foreach(var j in lista) {
            hoja.Cell(fila, 1).Value = j.JugadaId;
            hoja.Cell(fila, 2).Value = j.FechaJuego;
            hoja.Cell(fila, 3).Value = j.NombreRuleta ?? "";
            hoja.Cell(fila, 4).Value = j.NombrePantalla ?? "";
            hoja.Cell(fila, 5).Value = j.DescripcionPremio;
            hoja.Cell(fila, 6).Value = j.TipoPremio == TipoTajada.Monto ? "Monto" : "Producto";
            hoja.Cell(fila, 7).Value = j.MontoPremio;
            hoja.Cell(fila, 8).Value = j.AfectaTope ? "Si" : "No";
            hoja.Cell(fila, 9).Value = NombreTipoDocumento(j.TipoDocumento);

            // Como texto: un documento que empiece con cero perderia el cero
            // si Excel lo interpreta como numero.
            hoja.Cell(fila, 10).Value = j.Dni ?? "";
            hoja.Cell(fila, 10).Style.NumberFormat.Format = "@";

            hoja.Cell(fila, 11).Value = j.NombreCliente ?? "";
            hoja.Cell(fila, 12).Value = j.CorreoCliente ?? "";
            hoja.Cell(fila, 13).Value = j.Registrado ? "Si" : "No";

            fila++;
        }

        if(fila > 2) {
            hoja.Range(2, 2, fila - 1, 2).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
            hoja.Range(2, 7, fila - 1, 7).Style.NumberFormat.Format = $"\"{moneda}\"#,##0.00";

            // Totales como formula, no como valor: si filtran en Excel se
            // recalculan solos y se pueden auditar.
            hoja.Cell(fila + 1, 6).Value = "TOTAL ENTREGADO";
            hoja.Cell(fila + 1, 6).Style.Font.Bold = true;
            hoja.Cell(fila + 1, 7).FormulaA1 = $"SUM(G2:G{fila - 1})";
            hoja.Cell(fila + 1, 7).Style.Font.Bold = true;
            hoja.Cell(fila + 1, 7).Style.NumberFormat.Format = $"\"{moneda}\"#,##0.00";

            // Lo que de verdad descuenta del tope. Es la cifra que tiene que
            // coincidir con el tablero del gestor.
            hoja.Cell(fila + 2, 6).Value = "DESCUENTA DEL TOPE";
            hoja.Cell(fila + 2, 6).Style.Font.Bold = true;
            hoja.Cell(fila + 2, 7).FormulaA1 = $"SUMIF(H2:H{fila - 1},\"Si\",G2:G{fila - 1})";
            hoja.Cell(fila + 2, 7).Style.Font.Bold = true;
            hoja.Cell(fila + 2, 7).Style.NumberFormat.Format = $"\"{moneda}\"#,##0.00";
        }

        hoja.RangeUsed()?.SetAutoFilter();
        hoja.SheetView.FreezeRows(1);
        hoja.Columns().AdjustToContents();

        using var flujo = new MemoryStream();
        libro.SaveAs(flujo);

        return File(flujo.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"jugadas-{d:yyyyMMdd}-{h:yyyyMMdd}.xlsx");
    }

    /// <summary>CSV con BOM, para que Excel respete las tildes al abrirlo.</summary>
    public async Task<IActionResult> OnGetExportarAsync(
        string desde, string hasta, int? ruletaId, bool soloRegistradas) {
        if(!TryFecha(desde, out var d) || !TryFecha(hasta, out var h))
            return BadRequest("Fechas invalidas.");

        var lista = await jugadas.ReporteAsync(d, h, ruletaId == 0 ? null : ruletaId, soloRegistradas);

        var csv = new StringBuilder();
        csv.AppendLine("Jugada;Fecha;Ruleta;Pedestal;Premio;Tipo;Monto;Descuenta;Tipo documento;Documento;Cliente;Correo;Registrado");

        foreach(var j in lista) {
            csv.AppendLine(string.Join(';',
                j.JugadaId,
                j.FechaJuego.ToString("dd/MM/yyyy HH:mm"),
                Limpiar(j.NombreRuleta),
                Limpiar(j.NombrePantalla),
                Limpiar(j.DescripcionPremio),
                j.TipoPremio == TipoTajada.Monto ? "Monto" : "Producto",
                j.MontoPremio.ToString("0.00", CultureInfo.InvariantCulture),
                j.AfectaTope ? "Si" : "No",
                NombreTipoDocumento(j.TipoDocumento),
                j.Dni ?? "",
                Limpiar(j.NombreCliente),
                Limpiar(j.CorreoCliente),
                j.Registrado ? "Si" : "No"));
        }

        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

        return File(bytes, "text/csv", $"jugadas-{d:yyyyMMdd}-{h:yyyyMMdd}.csv");
    }

    private static string NombreTipoDocumento(TipoDocumento? tipo) =>
        tipo is null ? "" : ReglasDocumento.Nombre(tipo.Value);

    private static bool TryFecha(string? texto, out DateTime fecha) =>
        DateTime.TryParseExact(texto ?? "", "d/M/yyyy",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);

    /// <summary>El punto y coma partiria la columna en el CSV.</summary>
    private static string Limpiar(string? texto) => (texto ?? "").Replace(';', ',');
}