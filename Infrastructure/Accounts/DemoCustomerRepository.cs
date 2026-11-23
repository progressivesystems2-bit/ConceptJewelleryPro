using ConceptJewelleryPro.Data.Access.Repositories;
using ConceptJewelleryPro.Domain.Entities;

namespace ConceptJewelleryPro.Infrastructure.Accounts;

public sealed class DemoCustomerRepository : ICustomerRepository
{
    private readonly List<Customer> _customers = new();

    public Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        _customers.Add(customer);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        var index = _customers.FindIndex(c => string.Equals(c.Code, customer.Code, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _customers[index] = customer;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        _customers.RemoveAll(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
        return Task.CompletedTask;
    }

    public Task<Customer?> FindByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var customer = _customers.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(customer);
    }

    public Task<Customer?> FindByPhoneOrMobileAsync(string phoneOrMobile, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneOrMobile))
        {
            return Task.FromResult<Customer?>(null);
        }

        var raw = phoneOrMobile.Trim();
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        var core10 = digits.Length >= 10 ? digits.Substring(digits.Length - 10) : digits;

        var match = _customers
            .Select(c => new { Customer = c, Score = ScoreMatch(c, raw, digits, core10) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Customer)
            .FirstOrDefault();

        return Task.FromResult(match);
    }

    private static int ScoreMatch(Customer customer, string raw, string digits, string core10)
    {
        var custPhone = customer.Phone?.Trim() ?? string.Empty;
        var custMobile = customer.Mobile?.Trim() ?? string.Empty;

        if (string.Equals(custMobile, raw, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(custPhone, raw, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        var custMobileDigits = new string(custMobile.Where(char.IsDigit).ToArray());
        var custPhoneDigits = new string(custPhone.Where(char.IsDigit).ToArray());

        if (!string.IsNullOrEmpty(digits) && (custMobileDigits == digits || custPhoneDigits == digits))
        {
            return 90;
        }

        if (!string.IsNullOrEmpty(core10))
        {
            var custMobileCore = custMobileDigits.Length >= 10 ? custMobileDigits.Substring(custMobileDigits.Length - 10) : custMobileDigits;
            var custPhoneCore = custPhoneDigits.Length >= 10 ? custPhoneDigits.Substring(custPhoneDigits.Length - 10) : custPhoneDigits;

            if (custMobileCore == core10 || custPhoneCore == core10)
            {
                return 80;
            }

            if (custMobileDigits.Contains(core10) || custPhoneDigits.Contains(core10))
            {
                return 50;
            }
        }

        return 0;
    }

    public Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Customer> snapshot = _customers.OrderBy(c => c.CompanyName).ToList();
        return Task.FromResult(snapshot);
    }
}
