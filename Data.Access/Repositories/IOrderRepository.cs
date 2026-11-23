using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Data.Access.Repositories;

public interface IOrderRepository
{
    Task<IReadOnlyList<CustomerOrder>> GetAllAsync(string? savePath = null);
    Task<CustomerOrder?> GetByOrderNoAsync(string orderNo, string? savePath = null);
    Task<string> GetNextOrderNoAsync();
    Task<int> GetNextNosAsync();
    Task<bool> SaveAsync(CustomerOrder order, bool isNew, string currentUser);
    Task<bool> DeleteAsync(string orderNo, string currentUser);
    Task<bool> UpdateStatusAsync(string orderNo, string status, bool closed, string currentUser);
}
