using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoOrderRepository : IOrderRepository
{
    private readonly List<CustomerOrder> _orders = new();

    public DemoOrderRepository()
    {
        _orders.AddRange(new[]
        {
            new CustomerOrder
            {
                OrderNo = "ORD-0001",
                OrderCategory = "Order",
                Nos = 1,
                OrderDate = DateTime.Today.AddDays(-10),
                DeliveryDate = DateTime.Today.AddDays(5),
                OrderDeliveryDate = DateTime.Today.AddDays(5),
                CustomerName = "Rajesh Verma",
                Phone = "9876543210",
                City = "Bhubaneswar",
                Address = "Plot 104, Saheed Nagar",
                ItemName = "GOLD NECKLACE",
                Purity = "22K",
                Qty = 1,
                AppWeight = 32.500m,
                MetalRate = 7250m,
                MakPerGr = 650m,
                Making = 21125m,
                AppValue = 256750m,
                AdvancePaid = 50000m,
                Description = "Custom bridal choker design with ruby drop embellishments",
                Status = "In Progress",
                Closed = false,
                KarigarName = "Gopal Karigar",
                EntryUser = "admin"
            },
            new CustomerOrder
            {
                OrderNo = "ORD-0002",
                OrderCategory = "Requirement",
                Nos = 2,
                OrderDate = DateTime.Today.AddDays(-5),
                DeliveryDate = DateTime.Today.AddDays(12),
                OrderDeliveryDate = DateTime.Today.AddDays(12),
                CustomerName = "Priya Mohanty",
                Phone = "9437012345",
                City = "Cuttack",
                Address = "Choudhury Bazar",
                ItemName = "DIAMOND RING",
                Purity = "18K",
                Qty = 1,
                AppWeight = 5.200m,
                MetalRate = 5800m,
                MakPerGr = 800m,
                Making = 4160m,
                StoneName = "VVS-EF Diamonds",
                StoneWt = 0.850m,
                StoneValue = 48000m,
                AppValue = 82320m,
                AdvancePaid = 25000m,
                Description = "Solitaire halo engagement ring, size 14",
                Status = "Pending",
                Closed = false,
                KarigarName = "Bijoy Studio",
                EntryUser = "admin"
            },
            new CustomerOrder
            {
                OrderNo = "ORD-0003",
                OrderCategory = "Order",
                Nos = 3,
                OrderDate = DateTime.Today.AddDays(-25),
                DeliveryDate = DateTime.Today.AddDays(-2),
                OrderDeliveryDate = DateTime.Today.AddDays(-2),
                CustomerName = "Suresh Patel",
                Phone = "9123456780",
                City = "Rourkela",
                Address = "Civil Township",
                ItemName = "GOLD BANGLES",
                Purity = "22K",
                Qty = 2,
                AppWeight = 48.000m,
                MetalRate = 7180m,
                MakPerGr = 550m,
                Making = 26400m,
                AppValue = 371040m,
                AdvancePaid = 100000m,
                Description = "Pair of handmade kadas with filigree floral carvings",
                Status = "Completed",
                Closed = false,
                KarigarName = "Ranjan Goldsmith",
                EntryUser = "admin"
            }
        });
    }

    public Task<IReadOnlyList<CustomerOrder>> GetAllAsync(string? savePath = null)
    {
        IReadOnlyList<CustomerOrder> snapshot = _orders
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Nos)
            .ToList();

        foreach (var order in snapshot)
        {
            order.ResolvedPhotoPath = AccessOrderRepository.ResolveOrderPhotoPath(order.Photo, order.OrderNo, savePath);
        }

        return Task.FromResult(snapshot);
    }

    public Task<CustomerOrder?> GetByOrderNoAsync(string orderNo, string? savePath = null)
    {
        var found = _orders.FirstOrDefault(o => string.Equals(o.OrderNo, orderNo, StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            found.ResolvedPhotoPath = AccessOrderRepository.ResolveOrderPhotoPath(found.Photo, found.OrderNo, savePath);
        }
        return Task.FromResult(found);
    }

    public Task<string> GetNextOrderNoAsync()
    {
        int maxNos = _orders.Count > 0 ? _orders.Max(o => o.Nos) : 0;
        return Task.FromResult($"ORD-{(maxNos + 1):D4}");
    }

    public Task<int> GetNextNosAsync()
    {
        int maxNos = _orders.Count > 0 ? _orders.Max(o => o.Nos) : 0;
        return Task.FromResult(maxNos + 1);
    }

    public Task<bool> SaveAsync(CustomerOrder order, bool isNew, string currentUser)
    {
        if (isNew)
        {
            if (order.Nos <= 0)
            {
                order.Nos = _orders.Count > 0 ? _orders.Max(o => o.Nos) + 1 : 1;
            }
            if (string.IsNullOrWhiteSpace(order.OrderNo))
            {
                order.OrderNo = $"ORD-{order.Nos:D4}";
            }
            order.EntryUser = currentUser;
            order.EntryDate = DateTime.Today;
            order.EntryTime = DateTime.Now;
            _orders.Add(order);
        }
        else
        {
            var idx = _orders.FindIndex(o => string.Equals(o.OrderNo, order.OrderNo, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                _orders[idx] = order;
            }
            else
            {
                _orders.Add(order);
            }
        }
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string orderNo, string currentUser)
    {
        var removed = _orders.RemoveAll(o => string.Equals(o.OrderNo, orderNo, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(removed > 0);
    }

    public Task<bool> UpdateStatusAsync(string orderNo, string status, bool closed, string currentUser)
    {
        var found = _orders.FirstOrDefault(o => string.Equals(o.OrderNo, orderNo, StringComparison.OrdinalIgnoreCase));
        if (found != null)
        {
            found.Status = status;
            found.Closed = closed;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
