using System.IO.Compression;
using System.Xml.Linq;
using GestorOT.Data;
using Microsoft.EntityFrameworkCore;

namespace GestorOT.Services;

public class IsoXmlExporterService
{
    private readonly ApplicationDbContext _context;

    public IsoXmlExporterService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<byte[]> ExportWorkOrderAsIsoXml(Guid workOrderId)
    {
        var workOrder = await _context.WorkOrders
            .AsNoTracking()
            .Include(w => w.Lot)
                .ThenInclude(l => l!.Field)
            .Include(w => w.Labors)
                .ThenInclude(l => l.Supplies)
                    .ThenInclude(s => s.Supply)
            .FirstOrDefaultAsync(w => w.Id == workOrderId);

        if (workOrder == null)
            throw new InvalidOperationException("WorkOrder not found");

        var taskDataXml = GenerateTaskDataXml(workOrder);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            var entry = archive.CreateEntry("TASKDATA/TASKDATA.XML");
            using var entryStream = entry.Open();
            taskDataXml.Save(entryStream);
        }

        return memoryStream.ToArray();
    }

    private XDocument GenerateTaskDataXml(WorkOrder workOrder)
    {
        var ns = XNamespace.None;

        var taskElement = new XElement("TSK",
            new XAttribute("A", workOrder.Id.ToString("N")[..8].ToUpper()),
            new XAttribute("B", workOrder.Description),
            new XAttribute("G", ((int)MapStatus(workOrder.Status)).ToString())
        );

        if (workOrder.Lot?.Field != null)
        {
            var farmElement = new XElement("FRM",
                new XAttribute("A", workOrder.Lot.Field.Id.ToString("N")[..8].ToUpper()),
                new XAttribute("B", workOrder.Lot.Field.Name)
            );
            taskElement.AddFirst(farmElement);
        }

        if (workOrder.Lot != null)
        {
            var fieldElement = new XElement("PFD",
                new XAttribute("A", workOrder.Lot.Id.ToString("N")[..8].ToUpper()),
                new XAttribute("C", workOrder.Lot.Name),
                new XAttribute("D", workOrder.Lot.Status == "Active" ? "1" : "0")
            );
            taskElement.Add(fieldElement);
        }

        foreach (var labor in workOrder.Labors)
        {
            var operElement = new XElement("OTP",
                new XAttribute("A", labor.Id.ToString("N")[..8].ToUpper()),
                new XAttribute("B", labor.LaborType)
            );

            foreach (var supply in labor.Supplies.OrderBy(s => s.TankMixOrder))
            {
                var pdtElement = new XElement("PDT",
                    new XAttribute("A", supply.SupplyId.ToString("N")[..8].ToUpper()),
                    new XAttribute("B", supply.Supply?.ItemName ?? "Unknown"),
                    new XAttribute("E", supply.PlannedDose.ToString("F2")),
                    new XAttribute("F", supply.DoseUnit),
                    new XAttribute("G", supply.TankMixOrder.ToString())
                );
                operElement.Add(pdtElement);
            }

            taskElement.Add(operElement);
        }

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("ISO11783_TaskData",
                new XAttribute("VersionMajor", "4"),
                new XAttribute("VersionMinor", "0"),
                new XAttribute("ManagementSoftwareManufacturer", "GestorOT"),
                new XAttribute("ManagementSoftwareVersion", "2.0"),
                new XAttribute("DataTransferOrigin", "1"),
                taskElement
            )
        );
    }

    private static IsoTaskStatus MapStatus(string status) => status switch
    {
        "Draft" => IsoTaskStatus.Planned,
        "Scheduled" => IsoTaskStatus.Running,
        "InProgress" => IsoTaskStatus.Running,
        "Completed" or "Done" => IsoTaskStatus.Completed,
        _ => IsoTaskStatus.Planned
    };
}

public enum IsoTaskStatus
{
    Planned = 1,
    Running = 2,
    Paused = 3,
    Completed = 4,
    Template = 5,
    Cancelled = 6
}
