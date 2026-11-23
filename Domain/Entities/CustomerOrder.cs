using System;

namespace ConceptJewelleryPro.Domain.Entities;

public sealed class CustomerOrder
{
    public string OrderNo { get; set; } = string.Empty;
    public string OrderCategory { get; set; } = "Order"; // "Order" or "Requirement"
    public int Nos { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.Today;
    public DateTime? DeliveryDate { get; set; }
    public DateTime? OrderDeliveryDate { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;
    public decimal Qty { get; set; } = 1;
    public string Size { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public decimal AppWeight { get; set; }
    public decimal MakPerGr { get; set; }
    public decimal Making { get; set; }
    public decimal MetalRate { get; set; }
    public decimal AppValue { get; set; }
    public string RateType { get; set; } = "Fix";
    public decimal AdvancePaid { get; set; }

    public string StoneName { get; set; } = string.Empty;
    public decimal StoneWt { get; set; }
    public decimal StoneValue { get; set; }

    public string KarigarName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string StyleNo { get; set; } = string.Empty;
    public string CCode { get; set; } = string.Empty;
    public string CompanyPerson { get; set; } = string.Empty;

    public string Photo { get; set; } = string.Empty;
    public string? ResolvedPhotoPath { get; set; }

    public string Status { get; set; } = "Pending"; // Pending, In Progress, Completed, Delivered, Cancelled
    public bool Closed { get; set; }

    public string EntryUser { get; set; } = string.Empty;
    public DateTime? EntryDate { get; set; } = DateTime.Today;
    public DateTime? EntryTime { get; set; }
    public string ComputerName { get; set; } = string.Empty;

    // Computed / Display Properties
    public decimal BalanceDue => Math.Max(0m, AppValue - AdvancePaid);
    public bool HasPhoto => !string.IsNullOrWhiteSpace(ResolvedPhotoPath) && System.IO.File.Exists(ResolvedPhotoPath);
}
